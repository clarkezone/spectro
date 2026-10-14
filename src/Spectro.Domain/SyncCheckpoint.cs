namespace Spectro.Domain;

public sealed record SyncCheckpoint(
    string Name,
    string Value,
    DateTimeOffset UpdatedAt);
