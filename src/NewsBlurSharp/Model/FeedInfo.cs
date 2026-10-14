using System.Text.Json.Serialization;

namespace NewsBlurSharp.Model
{
    public class FeedInfo
    {
        [JsonPropertyName("subs")]
        public int Subs { get; set; }

        [JsonPropertyName("favicon")]
        public string Favicon { get; set; }

        [JsonPropertyName("favicon_url")]
        public string FaviconUrl { get; set; }

        [JsonPropertyName("result")]
        public string Result { get; set; }

        [JsonPropertyName("is_push")]
        public bool IsPush { get; set; }

        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("s3_icon")]
        public bool S3Icon { get; set; }

        [JsonPropertyName("authenticated")]
        public bool Authenticated { get; set; }

        [JsonPropertyName("feed_link")]
        public string FeedLink { get; set; }

        [JsonPropertyName("updated_seconds_ago")]
        public int UpdatedSecondsAgo { get; set; }

        [JsonPropertyName("favicon_fetching")]
        public bool FaviconFetching { get; set; }

        [JsonPropertyName("min_to_decay")]
        public int MinToDecay { get; set; }

        [JsonPropertyName("last_story_date")]
        public string LastStoryDate { get; set; }

        [JsonPropertyName("not_yet_fetched")]
        public bool NotYetFetched { get; set; }

        [JsonPropertyName("updated")]
        public string Updated { get; set; }

        [JsonPropertyName("average_stories_per_month")]
        public int AverageStoriesPerMonth { get; set; }

        [JsonPropertyName("feed_address")]
        public string FeedAddress { get; set; }

        [JsonPropertyName("feed_title")]
        public string FeedTitle { get; set; }

        [JsonPropertyName("favicon_fade")]
        public string FaviconFade { get; set; }

        [JsonPropertyName("is_newsletter")]
        public bool IsNewsletter { get; set; }

        [JsonPropertyName("last_story_seconds_ago")]
        public int LastStorySecondsAgo { get; set; }

        [JsonPropertyName("favicon_color")]
        public string FaviconColor { get; set; }

        [JsonPropertyName("stories_last_month")]
        public int StoriesLastMonth { get; set; }

        [JsonPropertyName("user_id")]
        public int UserId { get; set; }

        [JsonPropertyName("fetched_once")]
        public bool FetchedOnce { get; set; }

        [JsonPropertyName("favicon_text_color")]
        public string FaviconTextColor { get; set; }

        [JsonPropertyName("num_subscribers")]
        public int NumSubscribers { get; set; }

        [JsonPropertyName("s3_page")]
        public bool S3Page { get; set; }

        [JsonPropertyName("favicon_border")]
        public string FaviconBorder { get; set; }

        [JsonPropertyName("search_indexed")]
        public bool SearchIndexed { get; set; }
    }

}
