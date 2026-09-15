namespace Spectro.Domain;

public sealed record FolderFeed(
    string FolderId,
    int FeedId,
    int SortOrder);
