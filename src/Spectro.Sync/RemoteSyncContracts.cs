using Spectro.Domain;

namespace Spectro.Sync;

public sealed record RemoteFeedCatalog(
    IReadOnlyCollection<Feed> Feeds,
    IReadOnlyCollection<Folder> Folders,
    IReadOnlyCollection<FolderFeed> FolderFeeds);

public sealed record RemoteStoryPage(
    IReadOnlyCollection<Story> Stories,
    bool IsLastPage);

public enum SyncRemoteFailureKind
{
    Offline,
    Transient,
    Authentication,
    MalformedData,
    Permanent
}

public sealed class SyncRemoteException(
    string message,
    SyncRemoteFailureKind kind,
    Exception? innerException = null) : Exception(message, innerException)
{
    public SyncRemoteFailureKind Kind { get; } = kind;
}

public interface ISyncRemoteService
{
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
