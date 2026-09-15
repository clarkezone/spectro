using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using NewsBlurSharp.Model.Response;

namespace NewsBlurSharp.Serialization
{
    internal sealed class NewsFeedItemsConverter : JsonConverter<List<NewsFeedItem>>
    {
        public override List<NewsFeedItem> Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null || reader.TokenType == JsonTokenType.String)
            {
                return null;
            }

            var feeds = new List<NewsFeedItem>();
            if (reader.TokenType == JsonTokenType.StartArray)
            {
                while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                {
                    feeds.Add(JsonSerializer.Deserialize(
                        ref reader,
                        NewsBlurJsonContext.Default.NewsFeedItem));
                }

                return feeds;
            }

            if (reader.TokenType != JsonTokenType.StartObject)
            {
                throw new JsonException("Expected the feeds value to be an object or array.");
            }

            while (reader.Read() && reader.TokenType != JsonTokenType.EndObject)
            {
                if (reader.TokenType != JsonTokenType.PropertyName || !reader.Read())
                {
                    throw new JsonException("The feeds object is malformed.");
                }

                feeds.Add(JsonSerializer.Deserialize(
                    ref reader,
                    NewsBlurJsonContext.Default.NewsFeedItem));
            }

            return feeds;
        }

        public override void Write(
            Utf8JsonWriter writer,
            List<NewsFeedItem> value,
            JsonSerializerOptions options)
        {
            JsonSerializer.Serialize(
                writer,
                value,
                NewsBlurJsonContext.Default.ListNewsFeedItem);
        }
    }
}
