using System.Collections.Generic;
using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using NewsBlurSharp.Serialization;

namespace NewsBlurSharp.Model.Response
{
    public class SocialProfile
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

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("feed_address")]
        public string FeedAddress { get; set; }

        [JsonPropertyName("user_id")]
        public int UserId { get; set; }

        [JsonPropertyName("feed_link")]
        public string FeedLink { get; set; }

        [JsonPropertyName("follower_user_ids")]
        public object[] FollowerUserIds { get; set; }

        [JsonPropertyName("location")]
        public object Location { get; set; }

        [JsonPropertyName("popular_publishers")]
        public object PopularPublishers { get; set; }

        [JsonPropertyName("follower_count")]
        public int FollowerCount { get; set; }

        [JsonPropertyName("username")]
        public string Username { get; set; }

        [JsonPropertyName("bio")]
        public object Bio { get; set; }

        [JsonPropertyName("average_stories_per_month")]
        public int AverageStoriesPerMonth { get; set; }

        [JsonPropertyName("feed_title")]
        public string FeedTitle { get; set; }

        [JsonPropertyName("photo_service")]
        public object PhotoService { get; set; }

        [JsonPropertyName("stories_last_month")]
        public int StoriesLastMonth { get; set; }

        [JsonPropertyName("photo_url")]
        public string PhotoUrl { get; set; }

        [JsonPropertyName("num_subscribers")]
        public int NumSubscribers { get; set; }

        [JsonPropertyName("protected")]
        public object Protected { get; set; }
    }

    public class Preferences
    {
        [JsonPropertyName("read_story_delay")]
        public string ReadStoryDelay { get; set; }

        [JsonPropertyName("feed_view_single_story")]
        public string FeedViewSingleStory { get; set; }

        [JsonPropertyName("story_styling")]
        public string StoryStyling { get; set; }

        [JsonPropertyName("story_share_kippt")]
        public bool StoryShareKippt { get; set; }

        [JsonPropertyName("truncate_story")]
        public string TruncateStory { get; set; }

        [JsonPropertyName("story_share_delicious")]
        public bool StoryShareDelicious { get; set; }

        [JsonPropertyName("hide_story_changes")]
        public string HideStoryChanges { get; set; }

        [JsonPropertyName("default_view")]
        public string DefaultView { get; set; }

        [JsonPropertyName("story_share_evernote")]
        public bool StoryShareEvernote { get; set; }

        [JsonPropertyName("story_share_diigo")]
        public bool StoryShareDiigo { get; set; }

        [JsonPropertyName("hide_public_comments")]
        public bool HidePublicComments { get; set; }

        [JsonPropertyName("default_read_filter")]
        public string DefaultReadFilter { get; set; }

        [JsonPropertyName("story_share_facebook")]
        public bool StoryShareFacebook { get; set; }

        [JsonPropertyName("folder_counts")]
        public bool FolderCounts { get; set; }

        [JsonPropertyName("story_share_twitter")]
        public bool StoryShareTwitter { get; set; }

        [JsonPropertyName("story_share_readability")]
        public bool StoryShareReadability { get; set; }

        [JsonPropertyName("story_pane_anchor")]
        public string StoryPaneAnchor { get; set; }

        [JsonPropertyName("intro_page")]
        public string IntroPage { get; set; }

        [JsonPropertyName("open_feed_action")]
        public string OpenFeedAction { get; set; }

        [JsonPropertyName("ssl")]
        public string Ssl { get; set; }

        [JsonPropertyName("new_window")]
        public string NewWindow { get; set; }

        [JsonPropertyName("story_layout")]
        public string StoryLayout { get; set; }

        [JsonPropertyName("animations")]
        public bool Animations { get; set; }

        [JsonPropertyName("story_size")]
        public string StorySize { get; set; }

        [JsonPropertyName("story_share_googleplus")]
        public bool StoryShareGoogleplus { get; set; }

        [JsonPropertyName("story_share_instapaper")]
        public bool StoryShareInstapaper { get; set; }

        [JsonPropertyName("default_order")]
        public string DefaultOrder { get; set; }

        [JsonPropertyName("story_share_readitlater")]
        public bool StoryShareReaditlater { get; set; }

        [JsonPropertyName("feed_order")]
        public string FeedOrder { get; set; }

        [JsonPropertyName("title_counts")]
        public bool TitleCounts { get; set; }

        [JsonPropertyName("show_tooltips")]
        public string ShowTooltips { get; set; }

        [JsonPropertyName("story_share_tumblr")]
        public bool StoryShareTumblr { get; set; }

        [JsonPropertyName("story_share_pinboard")]
        public bool StorySharePinboard { get; set; }
    }

    public class StarredCount
    {
        [JsonPropertyName("count")]
        public int Count { get; set; }

        [JsonPropertyName("feed_address")]
        public string FeedAddress { get; set; }

        [JsonPropertyName("tag")]
        public object Tag { get; set; }

        [JsonPropertyName("feed_id")]
        public object FeedId { get; set; }
    }

    [DebuggerDisplay("Feed name: {FeedTitle}")]
    public class NewsFeedItem
    {
        [JsonPropertyName("subs")]
        public int Subs { get; set; }

        [JsonPropertyName("favicon_url")]
        public string FaviconUrl { get; set; }

        [JsonPropertyName("is_push")]
        public bool IsPush { get; set; }

        [JsonPropertyName("feed_opens")]
        public int FeedOpens { get; set; }

        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("s3_icon")]
        public bool S3Icon { get; set; }

        [JsonPropertyName("feed_link")]
        public string FeedLink { get; set; }

        [JsonPropertyName("updated_seconds_ago")]
        public int UpdatedSecondsAgo { get; set; }

        [JsonPropertyName("favicon_fetching")]
        public bool FaviconFetching { get; set; }

        [JsonPropertyName("ng")]
        public int Ng { get; set; }

        [JsonPropertyName("favicon_border")]
        public string FaviconBorder { get; set; }

        [JsonPropertyName("last_story_date")]
        public string LastStoryDate { get; set; }

        [JsonPropertyName("nt")]
        public int Nt { get; set; }

        [JsonPropertyName("not_yet_fetched")]
        public bool NotYetFetched { get; set; }

        [JsonPropertyName("updated")]
        public string Updated { get; set; }

        [JsonPropertyName("average_stories_per_month")]
        public int AverageStoriesPerMonth { get; set; }

        [JsonPropertyName("ps")]
        public int Ps { get; set; }

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

        [JsonPropertyName("active")]
        public bool Active { get; set; }

        [JsonPropertyName("fetched_once")]
        public bool FetchedOnce { get; set; }

        [JsonPropertyName("favicon_text_color")]
        public string FaviconTextColor { get; set; }

        [JsonPropertyName("subscribed")]
        public bool Subscribed { get; set; }

        [JsonPropertyName("num_subscribers")]
        public int NumSubscribers { get; set; }

        [JsonPropertyName("s3_page")]
        public bool S3Page { get; set; }

        [JsonPropertyName("min_to_decay")]
        public int MinToDecay { get; set; }

        [JsonPropertyName("search_indexed")]
        public bool? SearchIndexed { get; set; }
    }
    
    public class Facebook
    {
        [JsonPropertyName("syncing")]
        public bool Syncing { get; set; }

        [JsonPropertyName("facebook_picture_url")]
        public object FacebookPictureUrl { get; set; }

        [JsonPropertyName("facebook_uid")]
        public object FacebookUid { get; set; }
    }

    public class Twitter
    {
        [JsonPropertyName("twitter_username")]
        public object TwitterUsername { get; set; }

        [JsonPropertyName("syncing")]
        public bool Syncing { get; set; }

        [JsonPropertyName("twitter_picture_url")]
        public object TwitterPictureUrl { get; set; }

        [JsonPropertyName("twitter_uid")]
        public object TwitterUid { get; set; }
    }

    public class Gravatar
    {
        [JsonPropertyName("gravatar_picture_url")]
        public string GravatarPictureUrl { get; set; }
    }

    public class Appdotnet
    {
        [JsonPropertyName("syncing")]
        public bool Syncing { get; set; }

        [JsonPropertyName("appdotnet_uid")]
        public object AppdotnetUid { get; set; }

        [JsonPropertyName("appdotnet_picture_url")]
        public object AppdotnetPictureUrl { get; set; }
    }

    public class Upload
    {
        [JsonPropertyName("upload_picture_url")]
        public object UploadPictureUrl { get; set; }
    }

    public class SocialServices
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

    public class NewsFeedResponse
    {
        [JsonPropertyName("folders")]
        public JsonElement[] Folders { get; set; }

        [JsonPropertyName("saved_searches")]
        public object[] SavedSearches { get; set; }

        [JsonPropertyName("user_id")]
        public int UserId { get; set; }

        [JsonPropertyName("social_profile")]
        public SocialProfile SocialProfile { get; set; }

        [JsonPropertyName("user_profile")]
        public UserProfile UserProfile { get; set; }

        [JsonPropertyName("starred_counts")]
        public StarredCount[] StarredCounts { get; set; }

        [JsonPropertyName("starred_count")]
        public int StarredCount { get; set; }

        [JsonPropertyName("is_staff")]
        public bool IsStaff { get; set; }

        [JsonPropertyName("result")]
        public string Result { get; set; }

        [JsonPropertyName("authenticated")]
        public bool Authenticated { get; set; }

        [JsonPropertyName("feeds")]
        [JsonConverter(typeof(NewsFeedItemsConverter))]
        public List<NewsFeedItem> Feeds { get; set; }

        [JsonPropertyName("social_services")]
        public SocialServices SocialServices { get; set; }

        [JsonPropertyName("categories")]
        public object Categories { get; set; }

        [JsonPropertyName("social_feeds")]
        public object[] SocialFeeds { get; set; }
    }
}
