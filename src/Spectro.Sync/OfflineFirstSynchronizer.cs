using System.Collections.Concurrent;
using Spectro.Domain;

namespace Spectro.Sync;

public sealed class OfflineFirstSynchronizer
{
    public const string SuccessfulSyncCheckpointName = "last-successful-sync";

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> AccountLocks =
        new(StringComparer.Ordinal);

    private readonly IContentRepository _repository;
    private readonly ISyncRemoteService _remoteService;
    private readonly SyncOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly ISyncRandom _random;
    private readonly ISyncDelay _delay;
    private readonly ISyncStageObserver? _observer;

    public OfflineFirstSynchronizer(
        IContentRepository repository,
        ISyncRemoteService remoteService,
        SyncOptions? options = null,
        TimeProvider? timeProvider = null,
        ISyncRandom? random = null,
        ISyncDelay? delay = null,
        ISyncStageObserver? observer = null)
    {
        _repository = repository;
        _remoteService = remoteService;
        _options = options ?? new SyncOptions();
        _timeProvider = timeProvider ?? TimeProvider.System;
        _random = random ?? new SystemSyncRandom();
        _delay = delay ?? new SystemSyncDelay(_timeProvider);
        _observer = observer;
        ValidateOptions(_options);
    }

    public Task<SyncResult> SynchronizeAsync(
        SyncRequest request,
        CancellationToken cancellationToken = default) =>
        SynchronizeAsync(request, null, cancellationToken);

    public async Task<SyncResult> SynchronizeAsync(
        SyncRequest request,
        IProgress<SyncState>? progress,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.AccountId);

        var accountLock = AccountLocks.GetOrAdd(
            request.AccountId,
            static _ => new SemaphoreSlim(1, 1));
        var startedAt = _timeProvider.GetUtcNow();
        using var run = new SyncRun(request.AccountId, _options.MaximumConcurrentRequests, progress);

        try
        {
            await accountLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return Canceled(run.State, startedAt);
        }

        try
        {
            try
            {
                run.SetStage(SyncStage.Initialize);
                await _repository.InitializeAsync(cancellationToken).ConfigureAwait(false);
                await CompleteStageAsync(run.State, cancellationToken).ConfigureAwait(false);

                run.SetStage(SyncStage.UploadPendingMutations);
                await UploadPendingMutationsAsync(run, cancellationToken).ConfigureAwait(false);
                await CompleteStageAsync(run.State, cancellationToken).ConfigureAwait(false);

                run.SetStage(SyncStage.RefreshFeedsAndFolders);
                var catalog = await ExecuteRemoteAsync(
                    token => _remoteService.GetFeedCatalogAsync(token),
                    run,
                    cancellationToken).ConfigureAwait(false);
                run.Update(state => state with
                {
                    FeedCount = catalog.Feeds.Count(static feed => feed.IsActive)
                });
                await _repository.ReplaceFeedCatalogAsync(
                    catalog.Feeds,
                    catalog.Folders,
                    catalog.FolderFeeds,
                    cancellationToken).ConfigureAwait(false);
                run.ContentChanged();
                await CompleteStageAsync(run.State, cancellationToken).ConfigureAwait(false);

                run.SetStage(SyncStage.FetchUnreadState);
                var unread = await ExecuteRemoteAsync(
                    token => _remoteService.GetUnreadStoryHashesAsync(token),
                    run, cancellationToken).ConfigureAwait(false);
                await _repository.ReconcileRemoteContentAsync(
                    new RemoteContentBatch([], unread, true, new HashSet<string>(), false),
                    cancellationToken).ConfigureAwait(false);
                run.ContentChanged();

                run.SetStage(SyncStage.FetchStories);
                var saved = await FetchRemoteContentAsync(catalog, unread, run, cancellationToken)
                    .ConfigureAwait(false);
                await CompleteStageAsync(run.State, cancellationToken).ConfigureAwait(false);

                run.SetStage(SyncStage.Reconcile);
                await _repository.ReconcileRemoteContentAsync(
                    new RemoteContentBatch([], unread, true, saved.Hashes, saved.IsComplete),
                    cancellationToken).ConfigureAwait(false);
                run.ContentChanged();
                await CompleteStageAsync(run.State, cancellationToken).ConfigureAwait(false);

                run.SetStage(SyncStage.Checkpoint);
                await _repository.DeleteStoriesOlderThanAsync(
                    _timeProvider.GetUtcNow() - _options.RetentionAge,
                    cancellationToken).ConfigureAwait(false);
                var checkpointTime = _timeProvider.GetUtcNow();
                await _repository.SetCheckpointAsync(
                    new SyncCheckpoint(
                        SuccessfulSyncCheckpointName,
                        checkpointTime.ToString("O"),
                        checkpointTime),
                    cancellationToken).ConfigureAwait(false);
                await CompleteStageAsync(run.State, cancellationToken).ConfigureAwait(false);

                run.SetStage(SyncStage.Completed);
                return new SyncResult(
                    SyncOutcome.Succeeded,
                    run.State,
                    startedAt,
                    _timeProvider.GetUtcNow());
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Canceled(run.State, startedAt);
            }
            catch (SyncRemoteException exception)
            {
                return new SyncResult(
                    MapOutcome(exception.Kind),
                    run.State,
                    startedAt,
                    _timeProvider.GetUtcNow(),
                    exception.Message);
            }
        }
        finally
        {
            accountLock.Release();
        }
    }

    private async Task UploadPendingMutationsAsync(
        SyncRun run,
        CancellationToken cancellationToken)
    {
        var uploadedSnapshots = await _repository.GetUploadedMutationsAsync(cancellationToken)
            .ConfigureAwait(false);
        var acknowledged = uploadedSnapshots
            .Select(static item => item.Mutation)
            .ToHashSet();
        var pending = await _repository.GetPendingMutationsAsync(
            int.MaxValue,
            cancellationToken).ConfigureAwait(false);

        foreach (var mutation in pending)
        {
            if (acknowledged.Contains(mutation))
            {
                continue;
            }

            await ExecuteRemoteAsync(
                token => _remoteService.UploadMutationAsync(mutation, token),
                run,
                cancellationToken).ConfigureAwait(false);
            run.Update(state => state with
            {
                UploadedMutationCount = state.UploadedMutationCount + 1
            });
            await _repository.RecordUploadedMutationAsync(mutation, cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private async Task<(IReadOnlySet<string> Hashes, bool IsComplete)> FetchRemoteContentAsync(
        RemoteFeedCatalog catalog,
        IReadOnlySet<string> unread,
        SyncRun run,
        CancellationToken cancellationToken)
    {
        using var downloads = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        var savedHashes = new HashSet<string>(StringComparer.Ordinal);
        var savedComplete = false;
        var tasks = new List<Task>
        {
            DownloadAsync(async () =>
            {
                for (var page = 1; page <= _options.MaximumStoryPages; page++)
                {
                    var result = await ExecuteRemoteAsync(
                        token => _remoteService.GetStarredStoriesAsync(page, token),
                        run, downloads.Token, "Saved stories", page).ConfigureAwait(false);
                    foreach (var story in result.Stories) savedHashes.Add(story.Hash);
                    await CachePageAsync(result.Stories, unread, isSaved: true, run, downloads.Token)
                        .ConfigureAwait(false);
                    if (result.IsLastPage)
                    {
                        savedComplete = true;
                        break;
                    }
                }
            })
        };

        foreach (var feed in catalog.Feeds.Where(static feed => feed.IsActive))
        {
            tasks.Add(DownloadAsync(async () =>
            {
                for (var page = 1; page <= _options.MaximumStoryPages; page++)
                {
                    var result = await ExecuteRemoteAsync(
                        token => _remoteService.GetFeedStoriesAsync(feed.Id, page, token),
                        run, downloads.Token, feed.Title, page).ConfigureAwait(false);
                    await CachePageAsync(result.Stories, unread, isSaved: false, run, downloads.Token)
                        .ConfigureAwait(false);
                    if (result.IsLastPage) break;
                }
                run.Update(state => state with { CompletedFeedCount = state.CompletedFeedCount + 1 });
            }));
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
        return (savedHashes, savedComplete);

        async Task DownloadAsync(Func<Task> operation)
        {
            try { await operation().ConfigureAwait(false); }
            catch
            {
                await downloads.CancelAsync().ConfigureAwait(false);
                throw;
            }
        }
    }

    private async Task CachePageAsync(
        IReadOnlyCollection<Story> stories,
        IReadOnlySet<string> unread,
        bool isSaved,
        SyncRun run,
        CancellationToken cancellationToken)
    {
        await run.PersistenceGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var page = stories.Select(story => story with
            {
                IsRead = !unread.Contains(story.Hash),
                IsSaved = isSaved || story.IsSaved
            }).ToArray();
            await _repository.CacheRemoteStoriesAsync(page, cancellationToken).ConfigureAwait(false);
            foreach (var story in page) run.DownloadedHashes.Add(story.Hash);
            run.Update(state => state with
            {
                StoryCount = run.DownloadedHashes.Count,
                DownloadedPageCount = state.DownloadedPageCount + 1,
                LocalRevision = state.LocalRevision + (page.Length > 0 ? 1 : 0)
            });
        }
        finally { run.PersistenceGate.Release(); }
    }

    private async Task<T> ExecuteRemoteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        SyncRun run,
        CancellationToken cancellationToken,
        string? feedTitle = null,
        int page = 0)
    {
        for (var attempt = 1; ; attempt++)
        {
            await run.NetworkSlots.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                try
                {
                    run.Update(state => state with
                    {
                        NetworkAttemptCount = state.NetworkAttemptCount + 1,
                        CurrentFeedTitle = feedTitle,
                        CurrentPage = page,
                        RetryAttempt = attempt
                    });
                    return await operation(cancellationToken).ConfigureAwait(false);
                }
                finally { run.NetworkSlots.Release(); }
            }
            catch (SyncRemoteException exception)
                when (exception.Kind == SyncRemoteFailureKind.Transient
                    && attempt < _options.MaximumNetworkAttempts)
            {
                await _delay.DelayAsync(
                    CalculateRetryDelay(attempt),
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task ExecuteRemoteAsync(
        Func<CancellationToken, Task> operation,
        SyncRun run,
        CancellationToken cancellationToken)
    {
        await ExecuteRemoteAsync<object?>(
            async token =>
            {
                await operation(token).ConfigureAwait(false);
                return null;
            },
            run,
            cancellationToken).ConfigureAwait(false);
    }

    private TimeSpan CalculateRetryDelay(int failedAttempt)
    {
        var exponent = Math.Min(failedAttempt - 1, 30);
        var exponentialMilliseconds =
            _options.InitialRetryDelay.TotalMilliseconds * Math.Pow(2, exponent);
        var jitter = 1 + ((_random.NextDouble() * 2) - 1) * _options.RetryJitterRatio;
        var jitteredMilliseconds = Math.Max(0, exponentialMilliseconds * jitter);
        return TimeSpan.FromMilliseconds(Math.Min(
            jitteredMilliseconds,
            _options.MaximumRetryDelay.TotalMilliseconds));
    }

    private async Task<SyncState> CompleteStageAsync(
        SyncState state,
        CancellationToken cancellationToken)
    {
        if (_observer is not null)
        {
            await _observer.StageCompletedAsync(state, cancellationToken)
                .ConfigureAwait(false);
        }

        return state;
    }

    private SyncResult Canceled(SyncState state, DateTimeOffset startedAt) =>
        new(
            SyncOutcome.Canceled,
            state,
            startedAt,
            _timeProvider.GetUtcNow(),
            "Synchronization was canceled.");

    private static SyncOutcome MapOutcome(SyncRemoteFailureKind kind) =>
        kind switch
        {
            SyncRemoteFailureKind.Offline => SyncOutcome.Offline,
            SyncRemoteFailureKind.Transient => SyncOutcome.TransientFailure,
            SyncRemoteFailureKind.Authentication => SyncOutcome.AuthenticationRequired,
            SyncRemoteFailureKind.MalformedData => SyncOutcome.MalformedRemoteData,
            SyncRemoteFailureKind.Permanent => SyncOutcome.PermanentFailure,
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };

    private static void ValidateOptions(SyncOptions options)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaximumStoryPages, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaximumNetworkAttempts, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MaximumConcurrentRequests, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.MaximumConcurrentRequests, 8);
        if (options.InitialRetryDelay < TimeSpan.Zero
            || options.MaximumRetryDelay < options.InitialRetryDelay)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }

        if (options.RetryJitterRatio is < 0 or > 1)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }

        if (options.RetentionAge < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options));
        }
    }

    private sealed class SyncRun : IDisposable
    {
        private readonly object _gate = new();
        private readonly IProgress<SyncState>? _progress;
        private SyncState _state;

        public SyncRun(string accountId, int concurrency, IProgress<SyncState>? progress)
        {
            _state = new SyncState(accountId, SyncStage.None, 0, 0, 0, 0);
            _progress = progress;
            NetworkSlots = new SemaphoreSlim(concurrency, concurrency);
        }

        public SyncState State { get { lock (_gate) return _state; } }
        public SemaphoreSlim NetworkSlots { get; }
        public SemaphoreSlim PersistenceGate { get; } = new(1, 1);
        public HashSet<string> DownloadedHashes { get; } = new(StringComparer.Ordinal);

        public void Update(Func<SyncState, SyncState> update)
        {
            lock (_gate)
            {
                _state = update(_state);
                _progress?.Report(_state);
            }
        }

        public void SetStage(SyncStage stage) => Update(state => state with
        {
            Stage = stage, CurrentFeedTitle = null, CurrentPage = 0, RetryAttempt = 0
        });

        public void ContentChanged() => Update(state => state with { LocalRevision = state.LocalRevision + 1 });

        public void Dispose()
        {
            NetworkSlots.Dispose();
            PersistenceGate.Dispose();
        }
    }
}
