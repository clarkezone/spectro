using System.Collections.Generic;
using System.Diagnostics;
using NewsBlurSharp.Converters;
using Newtonsoft.Json;

namespace NewsBlurSharp.Model.Response
{
    public class SocialProfile
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

        [JsonProperty("feed_title")]
        public string FeedTitle { get; set; }

        [JsonProperty("photo_service")]
        public object PhotoService { get; set; }

        [JsonProperty("stories_last_month")]
        public int StoriesLastMonth { get; set; }

        [JsonProperty("photo_url")]
        public string PhotoUrl { get; set; }

        [JsonProperty("num_subscribers")]
        public int NumSubscribers { get; set; }

        [JsonProperty("protected")]
        public object Protected { get; set; }
    }

    public class Preferences
    {
        [JsonProperty("read_story_delay")]
        public string ReadStoryDelay { get; set; }

        [JsonProperty("feed_view_single_story")]
        public string FeedViewSingleStory { get; set; }

        [JsonProperty("story_styling")]
        public string StoryStyling { get; set; }

        [JsonProperty("story_share_kippt")]
        public bool StoryShareKippt { get; set; }

        [JsonProperty("truncate_story")]
        public string TruncateStory { get; set; }

        [JsonProperty("story_share_delicious")]
        public bool StoryShareDelicious { get; set; }

        [JsonProperty("hide_story_changes")]
        public string HideStoryChanges { get; set; }

        [JsonProperty("default_view")]
        public string DefaultView { get; set; }

        [JsonProperty("story_share_evernote")]
        public bool StoryShareEvernote { get; set; }

        [JsonProperty("story_share_diigo")]
        public bool StoryShareDiigo { get; set; }

        [JsonProperty("hide_public_comments")]
        public bool HidePublicComments { get; set; }

        [JsonProperty("default_read_filter")]
        public string DefaultReadFilter { get; set; }

        [JsonProperty("story_share_facebook")]
        public bool StoryShareFacebook { get; set; }

        [JsonProperty("folder_counts")]
        public bool FolderCounts { get; set; }

        [JsonProperty("story_share_twitter")]
        public bool StoryShareTwitter { get; set; }

        [JsonProperty("story_share_readability")]
        public bool StoryShareReadability { get; set; }

        [JsonProperty("story_pane_anchor")]
        public string StoryPaneAnchor { get; set; }

        [JsonProperty("intro_page")]
        public string IntroPage { get; set; }

        [JsonProperty("open_feed_action")]
        public string OpenFeedAction { get; set; }

        [JsonProperty("ssl")]
        public string Ssl { get; set; }

        [JsonProperty("new_window")]
        public string NewWindow { get; set; }

        [JsonProperty("story_layout")]
        public string StoryLayout { get; set; }

        [JsonProperty("animations")]
        public bool Animations { get; set; }

        [JsonProperty("story_size")]
        public string StorySize { get; set; }

        [JsonProperty("story_share_googleplus")]
        public bool StoryShareGoogleplus { get; set; }

        [JsonProperty("story_share_instapaper")]
        public bool StoryShareInstapaper { get; set; }

        [JsonProperty("default_order")]
        public string DefaultOrder { get; set; }

        [JsonProperty("story_share_readitlater")]
        public bool StoryShareReaditlater { get; set; }

        [JsonProperty("feed_order")]
        public string FeedOrder { get; set; }

        [JsonProperty("title_counts")]
        public bool TitleCounts { get; set; }

        [JsonProperty("show_tooltips")]
        public string ShowTooltips { get; set; }

        [JsonProperty("story_share_tumblr")]
        public bool StoryShareTumblr { get; set; }

        [JsonProperty("story_share_pinboard")]
        public bool StorySharePinboard { get; set; }
    }

    public class StarredCount
    {
        [JsonProperty("count")]
        public int Count { get; set; }

        [JsonProperty("feed_address")]
        public string FeedAddress { get; set; }

        [JsonProperty("tag")]
        public object Tag { get; set; }

        [JsonProperty("feed_id")]
        public object FeedId { get; set; }
    }

    [DebuggerDisplay("Feed name: {FeedTitle}")]
    public class NewsFeedItem
    {
        [JsonProperty("subs")]
        public int Subs { get; set; }

        [JsonProperty("favicon_url")]
        public string FaviconUrl { get; set; }

        [JsonProperty("is_push")]
        public bool IsPush { get; set; }

        [JsonProperty("feed_opens")]
        public int FeedOpens { get; set; }

        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("s3_icon")]
        public bool S3Icon { get; set; }

        [JsonProperty("feed_link")]
        public string FeedLink { get; set; }

        [JsonProperty("updated_seconds_ago")]
        public int UpdatedSecondsAgo { get; set; }

        [JsonProperty("favicon_fetching")]
        public bool FaviconFetching { get; set; }

        [JsonProperty("ng")]
        public int Ng { get; set; }

        [JsonProperty("favicon_border")]
        public string FaviconBorder { get; set; }

        [JsonProperty("last_story_date")]
        public string LastStoryDate { get; set; }

        [JsonProperty("nt")]
        public int Nt { get; set; }

        [JsonProperty("not_yet_fetched")]
        public bool NotYetFetched { get; set; }

        [JsonProperty("updated")]
        public string Updated { get; set; }

        [JsonProperty("average_stories_per_month")]
        public int AverageStoriesPerMonth { get; set; }

        [JsonProperty("ps")]
        public int Ps { get; set; }

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

        [JsonProperty("active")]
        public bool Active { get; set; }

        [JsonProperty("fetched_once")]
        public bool FetchedOnce { get; set; }

        [JsonProperty("favicon_text_color")]
        public string FaviconTextColor { get; set; }

        [JsonProperty("subscribed")]
        public bool Subscribed { get; set; }

        [JsonProperty("num_subscribers")]
        public int NumSubscribers { get; set; }

        [JsonProperty("s3_page")]
        public bool S3Page { get; set; }

        [JsonProperty("min_to_decay")]
        public int MinToDecay { get; set; }

        [JsonProperty("search_indexed")]
        public bool? SearchIndexed { get; set; }
    }
    
    public class Facebook
    {
        [JsonProperty("syncing")]
        public bool Syncing { get; set; }

        [JsonProperty("facebook_picture_url")]
        public object FacebookPictureUrl { get; set; }

        [JsonProperty("facebook_uid")]
        public object FacebookUid { get; set; }
    }

    public class Twitter
    {
        [JsonProperty("twitter_username")]
        public object TwitterUsername { get; set; }

        [JsonProperty("syncing")]
        public bool Syncing { get; set; }

        [JsonProperty("twitter_picture_url")]
        public object TwitterPictureUrl { get; set; }

        [JsonProperty("twitter_uid")]
        public object TwitterUid { get; set; }
    }

    public class Gravatar
    {
        [JsonProperty("gravatar_picture_url")]
        public string GravatarPictureUrl { get; set; }
    }

    public class Appdotnet
    {
        [JsonProperty("syncing")]
        public bool Syncing { get; set; }

        [JsonProperty("appdotnet_uid")]
        public object AppdotnetUid { get; set; }

        [JsonProperty("appdotnet_picture_url")]
        public object AppdotnetPictureUrl { get; set; }
    }

    public class Upload
    {
        [JsonProperty("upload_picture_url")]
        public object UploadPictureUrl { get; set; }
    }

    public class SocialServices
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

    public class NewsFeedResponse
    {
        //[JsonProperty("folders")]
        //public Folder[] Folders { get; set; }

        [JsonProperty("saved_searches")]
        public object[] SavedSearches { get; set; }

        [JsonProperty("user_id")]
        public int UserId { get; set; }

        [JsonProperty("social_profile")]
        public SocialProfile SocialProfile { get; set; }

        [JsonProperty("user_profile")]
        public UserProfile UserProfile { get; set; }

        [JsonProperty("starred_counts")]
        public StarredCount[] StarredCounts { get; set; }

        [JsonProperty("starred_count")]
        public int StarredCount { get; set; }

        [JsonProperty("is_staff")]
        public bool IsStaff { get; set; }

        [JsonProperty("result")]
        public string Result { get; set; }

        [JsonProperty("authenticated")]
        public bool Authenticated { get; set; }

        [JsonProperty("feeds")]
        [JsonConverter(typeof(ObjectToArrayConverter<NewsFeedItem>))]
        public List<NewsFeedItem> Feeds { get; set; }

        [JsonProperty("social_services")]
        public SocialServices SocialServices { get; set; }

        [JsonProperty("categories")]
        public object Categories { get; set; }

        [JsonProperty("social_feeds")]
        public object[] SocialFeeds { get; set; }
    }
}
