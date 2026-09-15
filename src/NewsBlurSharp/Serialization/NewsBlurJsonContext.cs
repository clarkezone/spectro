using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using NewsBlurSharp.Model;
using NewsBlurSharp.Model.Response;

namespace NewsBlurSharp.Serialization
{
    [JsonSourceGenerationOptions(
        GenerationMode = JsonSourceGenerationMode.Metadata,
        NumberHandling = JsonNumberHandling.AllowReadingFromString,
        PropertyNameCaseInsensitive = true)]
    [JsonSerializable(typeof(InternalLoginResponse))]
    [JsonSerializable(typeof(NewsFeedResponse))]
    [JsonSerializable(typeof(StoriesResponse))]
    [JsonSerializable(typeof(OperationResponse))]
    [JsonSerializable(typeof(UnreadStoryHashesResponse))]
    [JsonSerializable(typeof(ProfileResponse))]
    [JsonSerializable(typeof(FeedInfo))]
    [JsonSerializable(typeof(JsonElement))]
    [JsonSerializable(typeof(List<NewsFeedItem>))]
    internal partial class NewsBlurJsonContext : JsonSerializerContext
    {
    }
}
