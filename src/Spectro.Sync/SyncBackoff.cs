using System.Text.Json.Serialization;

namespace Spectro.Sync;

internal sealed record SyncBackoff(
    DateTimeOffset RetryAt,
    SyncRemoteFailureKind Kind,
    int? HttpStatusCode);

[JsonSerializable(typeof(SyncBackoff))]
[JsonSerializable(typeof(Dictionary<string, double>))]
internal partial class SyncJsonContext : JsonSerializerContext
{
}
