using Spectro.Domain;

namespace Spectro.Sync;

public sealed record RemoteFeedCatalog(
    IReadOnlyCollection<Feed> Feeds,
    IReadOnlyCollection<Folder> Folders,
    IReadOnlyCollection<FolderFeed> FolderFeeds);

public sealed record RemoteStoryPage(
    IReadOnlyCollection<Story> Stories,
    bool IsLastPage);

public sealed record RemoteStoryHash(string Hash, int FeedId, double Timestamp);

public sealed record RemoteStoryInventory(
    IReadOnlyDictionary<int, IReadOnlyList<RemoteStoryHash>> Feeds);

public enum SyncRemoteFailureKind
{
    Offline,
    Transient,
    Authentication,
    MalformedData,
    Permanent,
    RateLimited
}

public sealed class SyncRemoteException(
    string message,
    SyncRemoteFailureKind kind,
    Exception? innerException = null) : Exception(message, innerException)
{
    public SyncRemoteFailureKind Kind { get; } = kind;
    public DateTimeOffset? RetryAt { get; init; }
    public int? HttpStatusCode { get; init; }
}

public interface ISyncRemoteService
{
    Task<RemoteStoryInventory> GetStoryHashInventoryAsync(
        bool unreadOnly,
        IReadOnlyCollection<int> feedIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<string, double>> GetSavedStoryHashesAsync(
        CancellationToken cancellationToken);

    Task<RemoteStoryPage> GetStoriesByHashesAsync(
        IReadOnlyCollection<string> hashes,
        bool saved,
        CancellationToken cancellationToken);

    Task UploadMutationAsync(
        PendingStoryMutation mutation,
        CancellationToken cancellationToken);

    Task<RemoteFeedCatalog> GetFeedCatalogAsync(CancellationToken cancellationToken);

    Task<RemoteStoryPage> GetFeedStoriesAsync(
        int feedId,
        int page,
        CancellationToken cancellationToken);

    Task<IReadOnlySet<string>> GetUnreadStoryHashesAsync(
        CancellationToken cancellationToken);

    Task<RemoteStoryPage> GetStarredStoriesAsync(
        int page,
        CancellationToken cancellationToken);
}
