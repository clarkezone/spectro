using Spectro.Domain;

namespace Spectro.Presentation;

public enum NavigationItemKind
{
    Filter,
    Folder,
    Feed
}

public sealed record NavigationItem(
    string Id,
    string Title,
    NavigationItemKind Kind,
    StoryFilter Filter = StoryFilter.All,
    int? FeedId = null,
    string? FolderId = null,
    int UnreadCount = 0,
    int Depth = 0);
