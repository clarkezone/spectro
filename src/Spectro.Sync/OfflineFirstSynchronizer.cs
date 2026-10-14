using System.Collections.Concurrent;
using System.Text.Json;
using Spectro.Domain;

namespace Spectro.Sync;

public sealed partial class OfflineFirstSynchronizer
{
    public const string SuccessfulSyncCheckpointName = "last-successful-sync";
    public const string BackoffCheckpointName = "sync-backoff";

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
                var backoff = await _repository.GetCheckpointAsync(
                    BackoffCheckpointName, cancellationToken).ConfigureAwait(false);
                if (backoff is not null)
                {
                    var pause = JsonSerializer.Deserialize(backoff.Value, SyncJsonContext.Default.SyncBackoff)
                        ?? throw new InvalidDataException("The saved sync backoff is invalid.");
                    if (pause.RetryAt > _timeProvider.GetUtcNow())
                    {
                        throw BackoffException(pause);
                    }
                }

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
                var retentionCutoff = await FetchInventoryContentAsync(catalog, run, cancellationToken)
                    .ConfigureAwait(false);
                await CompleteStageAsync(run.State, cancellationToken).ConfigureAwait(false);

                run.SetStage(SyncStage.Reconcile);
                await CompleteStageAsync(run.State, cancellationToken).ConfigureAwait(false);

                run.SetStage(SyncStage.Checkpoint);
                await _repository.DeleteStoriesOlderThanAsync(
                    retentionCutoff,
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
                var pause = run.Backoff;
                return new SyncResult(
                    MapOutcome(pause?.Kind ?? exception.Kind),
                    run.State,
                    startedAt,
                    _timeProvider.GetUtcNow(),
                    exception.Message)
                {
                    RetryAt = pause?.RetryAt ?? exception.RetryAt,
                    HttpStatusCode = pause?.HttpStatusCode ?? exception.HttpStatusCode
                };
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

    private async Task<T> ExecuteRemoteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        SyncRun run,
        CancellationToken cancellationToken,
        string? feedTitle = null,
        int page = 0,
        bool savedRequest = false)
    {
        for (var attempt = 1; ; attempt++)
        {
            await run.NetworkSlots.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                try
                {
                    if (run.Backoff is { } pause) throw BackoffException(pause);
                    await PaceRequestAsync(run, savedRequest, cancellationToken).ConfigureAwait(false);
                    run.Update(state => state with
                    {
                        NetworkAttemptCount = state.NetworkAttemptCount + 1,
                        CurrentFeedTitle = feedTitle,
                        CurrentPage = page,
                        RetryAttempt = attempt
                    });
                    return await operation(cancellationToken).ConfigureAwait(false);
                }
                catch (SyncRemoteException exception) when (
                    exception.Kind == SyncRemoteFailureKind.RateLimited
                    || exception.RetryAt > _timeProvider.GetUtcNow())
                {
                    var now = _timeProvider.GetUtcNow();
                    var pause = run.Pause(new SyncBackoff(
                        exception.RetryAt > now ? exception.RetryAt.Value
                            : now + _options.RateLimitRetryDelay
                                + TimeSpan.FromSeconds(_random.NextDouble() * 30),
                        exception.Kind,
                        exception.HttpStatusCode));
                    await run.PersistenceGate.WaitAsync(CancellationToken.None).ConfigureAwait(false);
                    try
                    {
                        pause = run.Backoff ?? throw new InvalidOperationException("Sync backoff was not recorded.");
                        await _repository.SetCheckpointAsync(new SyncCheckpoint(
                            BackoffCheckpointName,
                            JsonSerializer.Serialize(pause, SyncJsonContext.Default.SyncBackoff),
                            now), CancellationToken.None).ConfigureAwait(false);
                    }
                    finally { run.PersistenceGate.Release(); }
                    throw BackoffException(pause);
                }
                finally { run.NetworkSlots.Release(); }
            }
            catch (SyncRemoteException exception)
                when (exception.Kind == SyncRemoteFailureKind.Transient
                    && exception.RetryAt is null
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
            SyncRemoteFailureKind.RateLimited => SyncOutcome.RateLimited,
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
        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(options.RateLimitRetryDelay, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MinimumRequestInterval, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.MinimumSavedRequestInterval, TimeSpan.Zero);
        ArgumentOutOfRangeException.ThrowIfLessThan(options.RecentStoriesPerFeed, 1);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(options.RecentStoriesPerFeed, 500);
    }

    private static SyncRemoteException BackoffException(SyncBackoff pause) =>
        new("NewsBlur requested a pause before further requests.", pause.Kind)
        {
            RetryAt = pause.RetryAt,
            HttpStatusCode = pause.HttpStatusCode
        };

    private sealed class SyncRun : IDisposable
    {
        private readonly object _gate = new();
        private readonly IProgress<SyncState>? _progress;
        private SyncState _state;
        private SyncBackoff? _backoff;

        public SyncRun(string accountId, int concurrency, IProgress<SyncState>? progress)
        {
            _state = new SyncState(accountId, SyncStage.None, 0, 0, 0, 0);
            _progress = progress;
            NetworkSlots = new SemaphoreSlim(concurrency, concurrency);
        }

        public SyncState State { get { lock (_gate) return _state; } }
        public SyncBackoff? Backoff { get { lock (_gate) return _backoff; } }
        public SemaphoreSlim NetworkSlots { get; }
        public SemaphoreSlim PersistenceGate { get; } = new(1, 1);
        public SemaphoreSlim PacingGate { get; } = new(1, 1);
        public HashSet<string> DownloadedHashes { get; } = new(StringComparer.Ordinal);
        public HashSet<string> DownloadedSavedHashes { get; } = new(StringComparer.Ordinal);

        public SyncBackoff Pause(SyncBackoff pause)
        {
            lock (_gate)
            {
                if (_backoff is null || _backoff.RetryAt < pause.RetryAt) _backoff = pause;
                return _backoff;
            }
        }

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
            PacingGate.Dispose();
        }
    }
}
