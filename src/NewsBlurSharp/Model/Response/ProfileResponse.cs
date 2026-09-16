using System.Text.Json.Serialization;

namespace NewsBlurSharp.Model.Response
{
    public class Services
    {
        [JsonPropertyName("facebook")]
        public Facebook Facebook { get; set; }

        [JsonPropertyName("twitter")]
        public Twitter Twitter { get; set; }

        [JsonPropertyName("gravatar")]
        public Gravatar Gravatar { get; set; }

        [JsonPropertyName("appdotnet")]
        public Appdotnet Appdotnet { get; set; }

        [JsonPropertyName("upload")]
        public Upload Upload { get; set; }
    }

    public class UserProfile
    {
        [JsonPropertyName("website")]
        public object Website { get; set; }

        [JsonPropertyName("following_user_ids")]
        public object[] FollowingUserIds { get; set; }

        [JsonPropertyName("following_count")]
        public int FollowingCount { get; set; }

        [JsonPropertyName("shared_stories_count")]
        public int SharedStoriesCount { get; set; }

        [JsonPropertyName("private")]
        public object Private { get; set; }

        [JsonPropertyName("large_photo_url")]
        public string LargePhotoUrl { get; set; }

        [JsonPropertyName("custom_bgcolor")]
        public string CustomBgcolor { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("feed_address")]
        public string FeedAddress { get; set; }

        [JsonPropertyName("user_id")]
        public int UserId { get; set; }

        [JsonPropertyName("feed_link")]
        public string FeedLink { get; set; }

        [JsonPropertyName("follower_user_ids")]
        public string[] FollowerUserIds { get; set; }

        [JsonPropertyName("location")]
        public string Location { get; set; }

        [JsonPropertyName("popular_publishers")]
        public object PopularPublishers { get; set; }

        [JsonPropertyName("follower_count")]
        public int FollowerCount { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; }

        [JsonPropertyName("bio")]
        public string Bio { get; set; }

        [JsonPropertyName("average_stories_per_month")]
        public int AverageStoriesPerMonth { get; set; }

        [JsonPropertyName("bb_permalink_direct")]
        public string BbPermalinkDirect { get; set; }

        [JsonPropertyName("feed_title")]
        public string FeedTitle { get; set; }

        [JsonPropertyName("photo_service")]
        public object PhotoService { get; set; }

        [JsonPropertyName("stories_last_month")]
        public int StoriesLastMonth { get; set; }

        [JsonPropertyName("photo_url")]
        public string PhotoUrl { get; set; }

        [JsonPropertyName("custom_css")]
        public object CustomCss { get; set; }

        [JsonPropertyName("num_subscribers")]
        public int NumSubscribers { get; set; }

        [JsonPropertyName("protected")]
        public object Protected { get; set; }
    }

    public class ProfileResponse
    {
        [JsonPropertyName("services")]
        public Services Services { get; set; }

        [JsonPropertyName("authenticated")]
        public bool Authenticated { get; set; }

        [JsonPropertyName("user_id")]
        public int UserId { get; set; }

        [JsonPropertyName("result")]
        public string Result { get; set; }

        [JsonPropertyName("user_profile")]
        public UserProfile UserProfile { get; set; }
    }
}
