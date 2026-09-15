namespace Spectro.Domain;

public sealed record RemoteContentBatch(
    IReadOnlyCollection<Story> Stories,
    IReadOnlySet<string> UnreadStoryHashes,
    bool UnreadSetIsComplete,
    IReadOnlySet<string> SavedStoryHashes,
    bool SavedSetIsComplete);
