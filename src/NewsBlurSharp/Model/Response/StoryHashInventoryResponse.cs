using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NewsBlurSharp.Model.Response
{
    public sealed class StoryHashInventoryResponse : OperationResponse, IJsonOnDeserialized
    {
        /// <summary>The server's feed-scoped inventory, capped at 500 stories per feed.</summary>
        [JsonRequired]
        [JsonPropertyName("unread_feed_story_hashes")]
        public Dictionary<string, List<StoryHashTimestamp>> UnreadStoryHashes { get; set; }

        void IJsonOnDeserialized.OnDeserialized()
        {
            if (UnreadStoryHashes == null)
            {
                throw new JsonException("The story hash inventory must be a feed-scoped object.");
            }

            foreach (var hashes in UnreadStoryHashes.Values)
            {
                if (hashes == null)
                {
                    throw new JsonException("Each feed must contain a story hash list.");
                }
            }
        }
    }
}
