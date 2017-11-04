using Newtonsoft.Json;

namespace NewsBlurSharp.Model.Response
{
    public class Services
    {
        [JsonProperty("facebook")]
        public Facebook Facebook { get; set; }

        [JsonProperty("twitter")]
        public Twitter Twitter { get; set; }

        [JsonProperty("gravatar")]
        public Gravatar Gravatar { get; set; }

        [JsonProperty("appdotnet")]
        public Appdotnet Appdotnet { get; set; }

        [JsonProperty("upload")]
        public Upload Upload { get; set; }
    }

    public class UserProfile
    {
        [JsonProperty("website")]
        public object Website { get; set; }

        [JsonProperty("following_user_ids")]
        public object[] FollowingUserIds { get; set; }

        [JsonProperty("following_count")]
        public int FollowingCount { get; set; }

        [JsonProperty("shared_stories_count")]
        public int SharedStoriesCount { get; set; }

        [JsonProperty("private")]
        public object Private { get; set; }

        [JsonProperty("large_photo_url")]
        public string LargePhotoUrl { get; set; }

        [JsonProperty("custom_bgcolor")]
        public object CustomBgcolor { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("feed_address")]
        public string FeedAddress { get; set; }

        [JsonProperty("user_id")]
        public int UserId { get; set; }

        [JsonProperty("feed_link")]
        public string FeedLink { get; set; }

        [JsonProperty("follower_user_ids")]
        public object[] FollowerUserIds { get; set; }

        [JsonProperty("location")]
        public object Location { get; set; }

        [JsonProperty("popular_publishers")]
        public object PopularPublishers { get; set; }

        [JsonProperty("follower_count")]
        public int FollowerCount { get; set; }

        [JsonProperty("username")]
        public string Username { get; set; }

        [JsonProperty("bio")]
        public object Bio { get; set; }

        [JsonProperty("average_stories_per_month")]
        public int AverageStoriesPerMonth { get; set; }

        [JsonProperty("bb_permalink_direct")]
        public object BbPermalinkDirect { get; set; }

        [JsonProperty("feed_title")]
        public string FeedTitle { get; set; }

        [JsonProperty("photo_service")]
        public object PhotoService { get; set; }

        [JsonProperty("stories_last_month")]
        public int StoriesLastMonth { get; set; }

        [JsonProperty("photo_url")]
        public string PhotoUrl { get; set; }

        [JsonProperty("custom_css")]
        public object CustomCss { get; set; }

        [JsonProperty("num_subscribers")]
        public int NumSubscribers { get; set; }

        [JsonProperty("protected")]
        public object Protected { get; set; }
    }

    public class ProfileResponse
    {
        [JsonProperty("services")]
        public Services Services { get; set; }

        [JsonProperty("authenticated")]
        public bool Authenticated { get; set; }

        [JsonProperty("user_id")]
        public int UserId { get; set; }

        [JsonProperty("result")]
        public string Result { get; set; }

        [JsonProperty("user_profile")]
        public UserProfile UserProfile { get; set; }
    }
}
