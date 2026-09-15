namespace Spectro.Domain;

public sealed record Feed(
    int Id,
    string Title,
    string FeedUri,
    string? IconUri,
    int UnreadCount,
    DateTimeOffset? LatestStoryAt,
    bool IsActive);
