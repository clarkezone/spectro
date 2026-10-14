namespace Spectro.Domain;

public sealed record CachedStory(
    string Hash,
    int FeedId,
    DateTimeOffset PublishedAt,
    bool IsRead,
    bool IsSaved);
