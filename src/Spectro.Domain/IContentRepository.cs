namespace Spectro.Domain;

public interface IContentRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task UpsertFeedAsync(Feed feed, CancellationToken cancellationToken = default);

    Task UpsertStoryAsync(Story story, CancellationToken cancellationToken = default);

    Task ReplaceFeedCatalogAsync(
        IReadOnlyCollection<Feed> feeds,
        IReadOnlyCollection<Folder> folders,
        IReadOnlyCollection<FolderFeed> folderFeeds,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Feed>> GetFeedsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<int, int>> GetLocalUnreadCountsAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<int, FeedStoryCounts>> GetLocalFeedCountsAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FeedFolder>> GetFeedFoldersAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Story>> GetStoriesAsync(
        int feedId,
        CancellationToken cancellationToken = default);

    /// <summary>Returns all cached story identities and state without loading content or summaries.</summary>
    Task<IReadOnlyList<CachedStory>> GetCachedStoryIndexAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Story>> QueryStoriesAsync(
        ContentQuery query,
        CancellationToken cancellationToken = default);

    Task SetStoryReadAsync(
        string storyHash,
        bool isRead,
        CancellationToken cancellationToken = default);

    Task SetStorySavedAsync(
        string storyHash,
        bool isSaved,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PendingStoryMutation>> GetPendingMutationsAsync(
        int limit,
        CancellationToken cancellationToken = default);

    Task<int> AcknowledgePendingMutationsAsync(
        IReadOnlyCollection<PendingStoryMutation> mutations,
        CancellationToken cancellationToken = default);

    Task RecordUploadedMutationAsync(
        PendingStoryMutation mutation,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UploadedStoryMutation>> GetUploadedMutationsAsync(
        CancellationToken cancellationToken = default);

    Task ReconcileRemoteContentAsync(
        RemoteContentBatch content,
        CancellationToken cancellationToken = default);

    Task CacheRemoteStoriesAsync(
        IReadOnlyCollection<Story> stories,
        CancellationToken cancellationToken = default);

    Task SetCheckpointAsync(
        SyncCheckpoint checkpoint,
        CancellationToken cancellationToken = default);

    Task<SyncCheckpoint?> GetCheckpointAsync(
        string name,
        CancellationToken cancellationToken = default);

    Task<int> DeleteStoriesOlderThanAsync(
        DateTimeOffset cutoff,
        CancellationToken cancellationToken = default);

    Task<DatabaseResetResult> ResetDatabaseAsync(
        CancellationToken cancellationToken = default);

    Task ClearAccountDataAsync(CancellationToken cancellationToken = default);
}
