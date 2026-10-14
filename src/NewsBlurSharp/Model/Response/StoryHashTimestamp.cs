using System;
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NewsBlurSharp.Model.Response
{
    [JsonConverter(typeof(StoryHashTimestampConverter))]
    public sealed class StoryHashTimestamp
    {
        public string Hash { get; set; }

        /// <summary>Unix seconds, preserving the server's fractional timestamp precision.</summary>
        public double Timestamp { get; set; }
    }

    public sealed class StoryHashTimestampConverter : JsonConverter<StoryHashTimestamp>
    {
        public override bool HandleNull => true;

        public override StoryHashTimestamp Read(
            ref Utf8JsonReader reader,
            Type typeToConvert,
            JsonSerializerOptions options)
        {
            if (reader.TokenType != JsonTokenType.StartArray
                || !reader.Read()
                || reader.TokenType != JsonTokenType.String)
            {
                throw new JsonException("A story hash timestamp must be a [hash, timestamp] tuple.");
            }

            var hash = reader.GetString();
            if (string.IsNullOrWhiteSpace(hash) || !reader.Read())
            {
                throw new JsonException("A story hash timestamp requires a nonempty hash and timestamp.");
            }

            double timestamp = 0;
            // The starred inventory returns Unix seconds as strings via strftime("%s").
            var validTimestamp = reader.TokenType switch
            {
                JsonTokenType.Number => reader.TryGetDouble(out timestamp),
                JsonTokenType.String => double.TryParse(
                    reader.GetString(), NumberStyles.Float, CultureInfo.InvariantCulture, out timestamp),
                _ => false
            };
            if (!validTimestamp)
            {
                throw new JsonException("A story hash timestamp must contain numeric Unix seconds.");
            }

            if (!double.IsFinite(timestamp)
                || !reader.Read()
                || reader.TokenType != JsonTokenType.EndArray)
            {
                throw new JsonException("A story hash timestamp requires exactly two elements and finite Unix seconds.");
            }

            return new StoryHashTimestamp { Hash = hash, Timestamp = timestamp };
        }

        public override void Write(
            Utf8JsonWriter writer,
            StoryHashTimestamp value,
            JsonSerializerOptions options)
        {
            if (value == null || string.IsNullOrWhiteSpace(value.Hash) || !double.IsFinite(value.Timestamp))
            {
                throw new JsonException("A story hash timestamp requires a nonempty hash and finite Unix seconds.");
            }

            writer.WriteStartArray();
            writer.WriteStringValue(value.Hash);
            writer.WriteNumberValue(value.Timestamp);
            writer.WriteEndArray();
        }
    }
}
