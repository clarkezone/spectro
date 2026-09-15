namespace Spectro.Domain;

public sealed record UploadedStoryMutation(
    PendingStoryMutation Mutation,
    DateTimeOffset UploadedAt);
