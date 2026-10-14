using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NewsBlurSharp.Model.Response
{
    public sealed class StarredStoryHashInventoryResponse : OperationResponse, IJsonOnDeserialized
    {
        [JsonRequired]
        [JsonPropertyName("starred_story_hashes")]
        public List<StoryHashTimestamp> StarredStoryHashes { get; set; }

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (StarredStoryHashes == null)
            {
                throw new JsonException("The starred story hash inventory must be a list.");
            }
        }
    }
}
