using Newtonsoft.Json;

namespace NewsBlurSharp.Model
{
    public class FeedInfo
    {
        [JsonProperty("subs")]
        public int Subs { get; set; }

        [JsonProperty("favicon")]
        public string Favicon { get; set; }

        [JsonProperty("favicon_url")]
        public string FaviconUrl { get; set; }

        [JsonProperty("result")]
        public string Result { get; set; }

        [JsonProperty("is_push")]
        public bool IsPush { get; set; }

        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("s3_icon")]
        public bool S3Icon { get; set; }

        [JsonProperty("authenticated")]
        public bool Authenticated { get; set; }

        [JsonProperty("feed_link")]
        public string FeedLink { get; set; }

        [JsonProperty("updated_seconds_ago")]
        public int UpdatedSecondsAgo { get; set; }

        [JsonProperty("favicon_fetching")]
        public bool FaviconFetching { get; set; }

        [JsonProperty("min_to_decay")]
        public int MinToDecay { get; set; }

        [JsonProperty("last_story_date")]
        public string LastStoryDate { get; set; }

        [JsonProperty("not_yet_fetched")]
        public bool NotYetFetched { get; set; }

        [JsonProperty("updated")]
        public string Updated { get; set; }

        [JsonProperty("average_stories_per_month")]
        public int AverageStoriesPerMonth { get; set; }

        [JsonProperty("feed_address")]
        public string FeedAddress { get; set; }

        [JsonProperty("feed_title")]
        public string FeedTitle { get; set; }

        [JsonProperty("favicon_fade")]
        public string FaviconFade { get; set; }

        [JsonProperty("is_newsletter")]
        public bool IsNewsletter { get; set; }

        [JsonProperty("last_story_seconds_ago")]
        public int LastStorySecondsAgo { get; set; }

        [JsonProperty("favicon_color")]
        public string FaviconColor { get; set; }

        [JsonProperty("stories_last_month")]
        public int StoriesLastMonth { get; set; }

        [JsonProperty("user_id")]
        public int UserId { get; set; }

        [JsonProperty("fetched_once")]
        public bool FetchedOnce { get; set; }

        [JsonProperty("favicon_text_color")]
        public string FaviconTextColor { get; set; }

        [JsonProperty("num_subscribers")]
        public int NumSubscribers { get; set; }

        [JsonProperty("s3_page")]
        public bool S3Page { get; set; }

        [JsonProperty("favicon_border")]
        public string FaviconBorder { get; set; }

        [JsonProperty("search_indexed")]
        public bool SearchIndexed { get; set; }
    }

}