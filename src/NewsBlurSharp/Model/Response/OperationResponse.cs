using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace NewsBlurSharp.Model.Response
{
    public class OperationResponse
    {
        [JsonPropertyName("authenticated")]
        public bool Authenticated { get; set; }

        [JsonPropertyName("result")]
        public string Result { get; set; }
    }

    public class UnreadStoryHashesResponse : OperationResponse
    {
        [JsonRequired]
        [JsonPropertyName("unread_feed_story_hashes")]
        public Dictionary<string, string[]> UnreadStoryHashes { get; set; }
    }
}
