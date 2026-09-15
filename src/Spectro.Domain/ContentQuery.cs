namespace Spectro.Domain;

public enum StoryFilter
{
    All,
    Unread,
    Saved,
    Read
}

public sealed record ContentQuery(
    StoryFilter Filter,
    int? FeedId = null,
    string? FolderId = null,
    int Limit = 500);

public sealed record FeedFolder(
    Folder Folder,
    IReadOnlyList<Feed> Feeds);
