namespace Spectro.Domain;

public enum StoryMutationKind
{
    Read,
    Saved
}

public sealed record PendingStoryMutation(
    long Id,
    string StoryHash,
    StoryMutationKind Kind,
    bool Value,
    DateTimeOffset CreatedAt);
