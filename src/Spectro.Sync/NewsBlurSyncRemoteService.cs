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
                    isFlatStructure: true,
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

        var feeds = (response.Feeds ?? [])
            .Select(MapFeed)
            .ToArray();
        var feedIds = feeds.Select(static feed => feed.Id).ToHashSet();
        var folders = new List<Folder>();
        var folderFeeds = new List<FolderFeed>();
        var folderOrder = 0;

        foreach (var folderElement in response.Folders ?? [])
        {
            if (folderElement.ValueKind == JsonValueKind.Number
                && folderElement.TryGetInt32(out var unfiledFeedId)
                && unfiledFeedId > 0)
            {
                continue;
            }

            if (folderElement.ValueKind != JsonValueKind.Object)
            {
                throw Malformed("NewsBlur returned a non-object folder entry.");
            }

            foreach (var property in folderElement.EnumerateObject())
            {
                var folderId = property.Name;
                folders.Add(new Folder(folderId, property.Name, folderOrder++));
                if (property.Value.ValueKind != JsonValueKind.Array)
                {
                    throw Malformed($"Folder '{property.Name}' did not contain a feed array.");
                }

                var feedOrder = 0;
                foreach (var feedElement in property.Value.EnumerateArray())
                {
                    if (!feedElement.TryGetInt32(out var feedId))
                    {
                        throw Malformed($"Folder '{property.Name}' contained an invalid feed id.");
                    }

                    if (feedIds.Contains(feedId))
                    {
                        folderFeeds.Add(new FolderFeed(folderId, feedId, feedOrder++));
                    }
                }
            }
        }

        return new RemoteFeedCatalog(feeds, folders, folderFeeds);
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
            feed.Active || feed.Subscribed);
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
