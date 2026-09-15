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

    public async Task<SyncResult> SynchronizeAsync(
        SyncRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.AccountId);

        var accountLock = AccountLocks.GetOrAdd(
            request.AccountId,
            static _ => new SemaphoreSlim(1, 1));
        var startedAt = _timeProvider.GetUtcNow();
        var state = new SyncState(request.AccountId, SyncStage.None, 0, 0, 0, 0);

        try
        {
            await accountLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            return Canceled(state, startedAt);
        }

        try
        {
            try
            {
                await _repository.InitializeAsync(cancellationToken).ConfigureAwait(false);
                state = await CompleteStageAsync(
                    state with { Stage = SyncStage.Initialize },
                    cancellationToken).ConfigureAwait(false);

                state = await UploadPendingMutationsAsync(state, cancellationToken)
                    .ConfigureAwait(false);
                state = await CompleteStageAsync(
                    state with { Stage = SyncStage.UploadPendingMutations },
                    cancellationToken).ConfigureAwait(false);

                var catalog = await ExecuteRemoteAsync(
                    token => _remoteService.GetFeedCatalogAsync(token),
                    state,
                    cancellationToken).ConfigureAwait(false);
                state = state with
                {
                    NetworkAttemptCount = catalog.AttemptCount,
                    FeedCount = catalog.Value.Feeds.Count
                };
                await _repository.ReplaceFeedCatalogAsync(
                    catalog.Value.Feeds,
                    catalog.Value.Folders,
                    catalog.Value.FolderFeeds,
                    cancellationToken).ConfigureAwait(false);
                state = await CompleteStageAsync(
                    state with { Stage = SyncStage.RefreshFeedsAndFolders },
                    cancellationToken).ConfigureAwait(false);

                var fetched = await FetchRemoteContentAsync(
                    catalog.Value,
                    state,
                    cancellationToken).ConfigureAwait(false);
                state = fetched.State;
                state = await CompleteStageAsync(
                    state with { Stage = SyncStage.FetchStories },
                    cancellationToken).ConfigureAwait(false);

                await _repository.ReconcileRemoteContentAsync(
                    fetched.Content,
                    cancellationToken).ConfigureAwait(false);
                state = await CompleteStageAsync(
                    state with { Stage = SyncStage.Reconcile },
                    cancellationToken).ConfigureAwait(false);

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
                state = await CompleteStageAsync(
                    state with { Stage = SyncStage.Checkpoint },
                    cancellationToken).ConfigureAwait(false);

                state = state with { Stage = SyncStage.Completed };
                return new SyncResult(
                    SyncOutcome.Succeeded,
                    state,
                    startedAt,
                    _timeProvider.GetUtcNow());
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return Canceled(state, startedAt);
            }
            catch (SyncRemoteException exception)
            {
                return new SyncResult(
                    MapOutcome(exception.Kind),
                    state,
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

    private async Task<SyncState> UploadPendingMutationsAsync(
        SyncState state,
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

            var upload = await ExecuteRemoteAsync(
                token => _remoteService.UploadMutationAsync(mutation, token),
                state,
                cancellationToken).ConfigureAwait(false);
            state = state with
            {
                UploadedMutationCount = state.UploadedMutationCount + 1,
                NetworkAttemptCount = upload.AttemptCount
            };
            await _repository.RecordUploadedMutationAsync(mutation, cancellationToken)
                .ConfigureAwait(false);
        }

        return state;
    }

    private async Task<(RemoteContentBatch Content, SyncState State)> FetchRemoteContentAsync(
        RemoteFeedCatalog catalog,
        SyncState state,
        CancellationToken cancellationToken)
    {
        var stories = new Dictionary<string, Story>(StringComparer.Ordinal);
        foreach (var feed in catalog.Feeds.Where(static feed => feed.IsActive))
        {
            state = await FetchPagesAsync(
                (page, token) => _remoteService.GetFeedStoriesAsync(feed.Id, page, token),
                stories,
                state,
                cancellationToken).ConfigureAwait(false);
        }

        var unread = await ExecuteRemoteAsync(
            token => _remoteService.GetUnreadStoryHashesAsync(token),
            state,
            cancellationToken).ConfigureAwait(false);
        state = state with { NetworkAttemptCount = unread.AttemptCount };

        var savedHashes = new HashSet<string>(StringComparer.Ordinal);
        var starredComplete = false;
        for (var page = 1; page <= _options.MaximumStoryPages; page++)
        {
            var starred = await ExecuteRemoteAsync(
                token => _remoteService.GetStarredStoriesAsync(page, token),
                state,
                cancellationToken).ConfigureAwait(false);
            state = state with { NetworkAttemptCount = starred.AttemptCount };
            foreach (var story in starred.Value.Stories)
            {
                savedHashes.Add(story.Hash);
                stories[story.Hash] = story with { IsSaved = true };
            }

            if (starred.Value.IsLastPage)
            {
                starredComplete = true;
                break;
            }
        }

        state = state with { StoryCount = stories.Count };
        return (
            new RemoteContentBatch(
                stories.Values.ToArray(),
                unread.Value,
                UnreadSetIsComplete: true,
                savedHashes,
                SavedSetIsComplete: starredComplete),
            state);
    }

    private async Task<SyncState> FetchPagesAsync(
        Func<int, CancellationToken, Task<RemoteStoryPage>> fetchPage,
        Dictionary<string, Story> stories,
        SyncState state,
        CancellationToken cancellationToken)
    {
        for (var page = 1; page <= _options.MaximumStoryPages; page++)
        {
            var result = await ExecuteRemoteAsync(
                token => fetchPage(page, token),
                state,
                cancellationToken).ConfigureAwait(false);
            state = state with { NetworkAttemptCount = result.AttemptCount };
            foreach (var story in result.Value.Stories)
            {
                stories[story.Hash] = story;
            }

            if (result.Value.IsLastPage)
            {
                break;
            }
        }

        return state;
    }

    private async Task<RemoteCallResult<T>> ExecuteRemoteAsync<T>(
        Func<CancellationToken, Task<T>> operation,
        SyncState state,
        CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return new RemoteCallResult<T>(
                    await operation(cancellationToken).ConfigureAwait(false),
                    state.NetworkAttemptCount + attempt);
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

    private async Task<RemoteCallResult<object?>> ExecuteRemoteAsync(
        Func<CancellationToken, Task> operation,
        SyncState state,
        CancellationToken cancellationToken)
    {
        return await ExecuteRemoteAsync<object?>(
            async token =>
            {
                await operation(token).ConfigureAwait(false);
                return null;
            },
            state,
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

    private sealed record RemoteCallResult<T>(T Value, int AttemptCount);
}
