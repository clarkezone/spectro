namespace Spectro.Domain;

public sealed record RemoteContentBatch(
    IReadOnlyCollection<Story> Stories,
    IReadOnlySet<string> UnreadStoryHashes,
    bool UnreadSetIsComplete,
    IReadOnlySet<string> SavedStoryHashes,
    bool SavedSetIsComplete)
{
    /// <summary>
    /// Explicit read observations when the unread set is incomplete. Positive unread observations take precedence.
    /// </summary>
    public IReadOnlySet<string> ReadStoryHashes { get; init; } = new HashSet<string>();
}
