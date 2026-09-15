using System.Globalization;
using System.Text.Json;
using NewsBlurSharp;
using NewsBlurSharp.Model;
using NewsBlurSharp.Model.Response;
using Spectro.Domain;
using RemoteStory = NewsBlurSharp.Model.Response.Story;

namespace Spectro.Sync;

public sealed class NewsBlurSyncRemoteService(
    INewsBlurClient client) : ISyncRemoteService
{
    public async Task UploadMutationAsync(
        PendingStoryMutation mutation,
        CancellationToken cancellationToken)
    {
        await ExecuteAsync(
            async () =>
            {
                switch (mutation.Kind)
                {
                    case StoryMutationKind.Read when mutation.Value:
                        await client.MarkStoriesReadAsync(
                            [mutation.StoryHash],
                            cancellationToken).ConfigureAwait(false);
                        break;
                    case StoryMutationKind.Read:
                        await client.MarkStoryUnreadAsync(
                            mutation.StoryHash,
                            cancellationToken).ConfigureAwait(false);
                        break;
                    case StoryMutationKind.Saved when mutation.Value:
                        await client.StarStoryAsync(
                            mutation.StoryHash,
                            cancellationToken).ConfigureAwait(false);
                        break;
                    case StoryMutationKind.Saved:
                        await client.UnstarStoryAsync(
                            mutation.StoryHash,
                            cancellationToken).ConfigureAwait(false);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(
                            nameof(mutation),
                            mutation.Kind,
                            null);
                }
            }).ConfigureAwait(false);
    }

    public Task<RemoteFeedCatalog> GetFeedCatalogAsync(
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            async () => MapCatalog(
                await client.GetFeedsAsync(
                    includeFavIcons: true,
                    isFlatStructure: false,
                    updateCounts: true,
                    cancellationToken).ConfigureAwait(false)));

    public Task<RemoteStoryPage> GetFeedStoriesAsync(
        int feedId,
        int page,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            async () => MapStories(
                await client.GetStoriesAsync(
                    feedId,
                    page,
                    cancellationToken: cancellationToken).ConfigureAwait(false),
                isSaved: false));

    public Task<IReadOnlySet<string>> GetUnreadStoryHashesAsync(
        CancellationToken cancellationToken) =>
        ExecuteAsync<IReadOnlySet<string>>(
            async () =>
            {
                var response = await client.GetUnreadStoryHashesAsync(cancellationToken)
                    .ConfigureAwait(false);
                if (response.UnreadStoryHashes is null
                    || response.UnreadStoryHashes.Values.Any(static hashes =>
                        hashes is null || hashes.Any(string.IsNullOrWhiteSpace)))
                {
                    throw Malformed("NewsBlur returned an invalid unread story set.");
                }
                return response.UnreadStoryHashes
                    .SelectMany(static pair => pair.Value)
                    .ToHashSet(StringComparer.Ordinal);
            });

    public Task<RemoteStoryPage> GetStarredStoriesAsync(
        int page,
        CancellationToken cancellationToken) =>
        ExecuteAsync(
            async () => MapStories(
                await client.GetStarredStoriesAsync(page, cancellationToken)
                    .ConfigureAwait(false),
                isSaved: true));

    private static RemoteFeedCatalog MapCatalog(NewsFeedResponse response)
    {
        if (!response.Authenticated)
        {
            throw new SyncRemoteException(
                "NewsBlur rejected the feed catalog request.",
                SyncRemoteFailureKind.Authentication);
        }

        if (response.Folders is null)
        {
            throw Malformed("NewsBlur omitted the requested folder hierarchy.");
        }

        var feeds = (response.Feeds ?? [])
            .Select(MapFeed)
            .ToArray();
        var feedIds = feeds.Select(static feed => feed.Id).ToHashSet();
        var folders = new List<Folder>();
        var folderFeeds = new List<FolderFeed>();
        var folderIds = new HashSet<string>(StringComparer.Ordinal);
        var folderOrder = 0;

        foreach (var folderElement in response.Folders)
        {
            MapFolderEntry(folderElement, null);
        }

        return new RemoteFeedCatalog(feeds, folders, folderFeeds);

        void MapFolderEntry(JsonElement element, string? parentPath)
        {
            if (element.ValueKind == JsonValueKind.Number
                && element.TryGetInt32(out var unfiledFeedId)
                && unfiledFeedId > 0 && parentPath is null)
            {
                return;
            }

            if (element.ValueKind != JsonValueKind.Object
                || !element.EnumerateObject().Any())
            {
                throw Malformed("NewsBlur returned a non-object folder entry.");
            }

            foreach (var property in element.EnumerateObject())
            {
                // The local contract is flat, so retain nested names as full paths.
                var folderId = parentPath is null ? property.Name : $"{parentPath} / {property.Name}";
                if (string.IsNullOrWhiteSpace(property.Name) || !folderIds.Add(folderId))
                {
                    throw Malformed($"NewsBlur returned an empty or ambiguous folder name '{folderId}'.");
                }
                folders.Add(new Folder(folderId, folderId, folderOrder++));
                if (property.Value.ValueKind != JsonValueKind.Array)
                {
                    throw Malformed($"Folder '{property.Name}' did not contain a feed array.");
                }

                var feedOrder = 0;
                var members = new HashSet<int>();
                foreach (var feedElement in property.Value.EnumerateArray())
                {
                    if (feedElement.ValueKind == JsonValueKind.Object)
                    {
                        MapFolderEntry(feedElement, folderId);
                        continue;
                    }
                    if (feedElement.ValueKind != JsonValueKind.Number
                        || !feedElement.TryGetInt32(out var feedId) || feedId <= 0)
                    {
                        throw Malformed($"Folder '{property.Name}' contained an invalid feed id.");
                    }

                    if (feedIds.Contains(feedId) && members.Add(feedId))
                    {
                        folderFeeds.Add(new FolderFeed(folderId, feedId, feedOrder++));
                    }
                }
            }
        }
    }

    private static Feed MapFeed(NewsFeedItem feed)
    {
        if (feed.Id <= 0
            || string.IsNullOrWhiteSpace(feed.FeedTitle)
            || string.IsNullOrWhiteSpace(feed.FeedAddress))
        {
            throw Malformed("NewsBlur returned a feed without a valid id, title, or address.");
        }

        return new Feed(
            feed.Id,
            feed.FeedTitle,
            feed.FeedAddress,
            string.IsNullOrWhiteSpace(feed.FaviconUrl) ? null : feed.FaviconUrl,
            Math.Max(0, feed.Ng + feed.Nt + feed.Ps),
            ParseDate(feed.LastStoryDate),
            feed.Active ?? feed.Subscribed);
    }

    private RemoteStoryPage MapStories(StoriesResponse response, bool isSaved)
    {
        if (!response.Authenticated)
        {
            throw new SyncRemoteException(
                "NewsBlur rejected a story request.",
                SyncRemoteFailureKind.Authentication);
        }

        var stories = (response.Stories ?? [])
            .Select(story => MapStory(story, isSaved))
            .ToArray();
        return new RemoteStoryPage(
            stories,
            stories.Length == 0 && response.HiddenStoriesRemoved == 0);
    }

    private static Spectro.Domain.Story MapStory(RemoteStory story, bool isSaved)
    {
        if (string.IsNullOrWhiteSpace(story.Hash)
            || story.FeedId <= 0
            || string.IsNullOrWhiteSpace(story.Title))
        {
            throw Malformed("NewsBlur returned a story without a valid hash, feed, or title.");
        }

        return new Spectro.Domain.Story(
            story.Hash,
            story.FeedId,
            NullIfWhiteSpace(story.Id),
            NullIfWhiteSpace(story.GuidHash),
            story.Title,
            NullIfWhiteSpace(story.Authors),
            NullIfWhiteSpace(story.Permalink),
            story.Content ?? string.Empty,
            story.Title,
            story.ImageUrls?.FirstOrDefault(static value => !string.IsNullOrWhiteSpace(value)),
            ParseStoryDate(story),
            story.ReadStatus != 0,
            isSaved);
    }

    private static DateTimeOffset ParseStoryDate(RemoteStory story) =>
        ParseDate(story.Timestamp)
        ?? ParseDate(story.Date)
        ?? DateTimeOffset.UnixEpoch;

    private static DateTimeOffset? ParseDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (long.TryParse(
            value,
            NumberStyles.Integer,
            CultureInfo.InvariantCulture,
            out var unixSeconds))
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
        }

        if (DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal,
            out var date))
        {
            return date.ToUniversalTime();
        }

        return null;
    }

    private static string? NullIfWhiteSpace(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;

    private static SyncRemoteException Malformed(string message) =>
        new(message, SyncRemoteFailureKind.MalformedData);

    private static async Task ExecuteAsync(Func<Task> operation)
    {
        try
        {
            await operation().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            throw MapException(exception);
        }
    }

    private static async Task<T> ExecuteAsync<T>(Func<Task<T>> operation)
    {
        try
        {
            return await operation().ConfigureAwait(false);
        }
        catch (Exception exception)
        {
            throw MapException(exception);
        }
    }

    private static Exception MapException(Exception exception) =>
        exception switch
        {
            OperationCanceledException => exception,
            SyncRemoteException => exception,
            NewsBlurAuthenticationException => new SyncRemoteException(
                exception.Message,
                SyncRemoteFailureKind.Authentication,
                exception),
            NewsBlurMalformedResponseException => new SyncRemoteException(
                exception.Message,
                SyncRemoteFailureKind.MalformedData,
                exception),
            NewsBlurOfflineException => new SyncRemoteException(
                exception.Message,
                SyncRemoteFailureKind.Offline,
                exception),
            NewsBlurTransientException or NewsBlurTimeoutException => new SyncRemoteException(
                exception.Message,
                SyncRemoteFailureKind.Transient,
                exception),
            NewsBlurException => new SyncRemoteException(
                exception.Message,
                SyncRemoteFailureKind.Permanent,
                exception),
            _ => exception
        };
}
