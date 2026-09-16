using Spectro.Domain;

namespace Spectro.Sync;

public enum SyncStage
{
    None,
    Initialize,
    UploadPendingMutations,
    RefreshFeedsAndFolders,
    FetchUnreadState,
    FetchStories,
    Reconcile,
    Checkpoint,
    Completed
}

public enum SyncOutcome
{
    Succeeded,
    Offline,
    TransientFailure,
    AuthenticationRequired,
    MalformedRemoteData,
    PermanentFailure,
    Canceled,
    RateLimited
}

public sealed record SyncState(
    string AccountId,
    SyncStage Stage,
    int UploadedMutationCount,
    int FeedCount,
    int StoryCount,
    int NetworkAttemptCount)
{
    public int CompletedFeedCount { get; init; }
    public int DownloadedPageCount { get; init; }
    public int CurrentPage { get; init; }
    public string? CurrentFeedTitle { get; init; }
    public int RetryAttempt { get; init; }
    public int LocalRevision { get; init; }
}

public sealed record SyncResult(
    SyncOutcome Outcome,
    SyncState State,
    DateTimeOffset StartedAt,
    DateTimeOffset FinishedAt,
    string? ErrorMessage = null)
{
    public bool IsSuccess => Outcome == SyncOutcome.Succeeded;
    public DateTimeOffset? RetryAt { get; init; }
    public int? HttpStatusCode { get; init; }
}

public sealed record SyncRequest(string AccountId);

public sealed record SyncOptions
{
    public int MaximumStoryPages { get; init; } = 10;

    public int MaximumNetworkAttempts { get; init; } = 3;

    public int MaximumConcurrentRequests { get; init; } = 4;

    public TimeSpan InitialRetryDelay { get; init; } = TimeSpan.FromSeconds(1);

    public TimeSpan MaximumRetryDelay { get; init; } = TimeSpan.FromSeconds(30);

    public TimeSpan RateLimitRetryDelay { get; init; } = TimeSpan.FromMinutes(6);

    public TimeSpan MinimumRequestInterval { get; init; } = TimeSpan.FromSeconds(1);

    public TimeSpan MinimumSavedRequestInterval { get; init; } = TimeSpan.FromSeconds(8);

    public int RecentStoriesPerFeed { get; init; } = 60;

    public double RetryJitterRatio { get; init; } = 0.2;

    public TimeSpan RetentionAge { get; init; } = TimeSpan.FromDays(90);
}

public interface ISyncRandom
{
    double NextDouble();
}

public interface ISyncDelay
{
    Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken);
}

public interface ISyncStageObserver
{
    Task StageCompletedAsync(SyncState state, CancellationToken cancellationToken);
}

public sealed class SystemSyncRandom : ISyncRandom
{
    public double NextDouble() => Random.Shared.NextDouble();
}

public sealed class SystemSyncDelay(TimeProvider? timeProvider = null) : ISyncDelay
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken) =>
        Task.Delay(delay, _timeProvider, cancellationToken);
}
