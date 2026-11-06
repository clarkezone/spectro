namespace Spectro.Domain;

public sealed record Story(
    string Hash,
    int FeedId,
    string? ServiceId,
    string? GuidHash,
    string Title,
    string? Author,
    string? Permalink,
    string Content,
    string Summary,
    string? ImageUri,
    DateTimeOffset PublishedAt,
    bool IsRead,
    bool IsSaved);
