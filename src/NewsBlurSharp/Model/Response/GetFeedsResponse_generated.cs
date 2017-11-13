using System;
using System.Collections.Generic;
using System.Text;

namespace NewsBlurSharp.Model.Response.GetFeedResponseGenerated
{
    public class Rootobject
    {
        public Folder[] folders { get; set; }
        public object[] saved_searches { get; set; }
        public int user_id { get; set; }
        public Social_Profile social_profile { get; set; }
        public User_Profile user_profile { get; set; }
        public Starred_Counts[] starred_counts { get; set; }
        public int starred_count { get; set; }
        public bool is_staff { get; set; }
        public string result { get; set; }
        public bool authenticated { get; set; }
        public Feeds feeds { get; set; }
        public Social_Services social_services { get; set; }
        public object categories { get; set; }
        public Social_Feeds[] social_feeds { get; set; }
    }

    public class Social_Profile
    {
        public string website { get; set; }
        public int[] following_user_ids { get; set; }
        public int following_count { get; set; }
        public int shared_stories_count { get; set; }
        public bool _private { get; set; }
        public string large_photo_url { get; set; }
        public string id { get; set; }
        public string feed_address { get; set; }
        public int user_id { get; set; }
        public string feed_link { get; set; }
        public int[] follower_user_ids { get; set; }
        public string location { get; set; }
        public object[] popular_publishers { get; set; }
        public int follower_count { get; set; }
        public string username { get; set; }
        public string bio { get; set; }
        public int average_stories_per_month { get; set; }
        public string feed_title { get; set; }
        public string photo_service { get; set; }
        public int stories_last_month { get; set; }
        public string photo_url { get; set; }
        public int num_subscribers { get; set; }
        public bool _protected { get; set; }
    }

    public class User_Profile
    {
        public bool hide_getting_started { get; set; }
        public Preferences preferences { get; set; }
        public bool tutorial_finished { get; set; }
        public bool has_setup_feeds { get; set; }
        public bool is_premium { get; set; }
        public string dashboard_date { get; set; }
        public bool has_trained_intelligence { get; set; }
        public bool has_found_friends { get; set; }
    }

    public class Preferences
    {
        public string show_contextmenus { get; set; }
        public string markread_nextfeed { get; set; }
        public bool story_share_blogger { get; set; }
        public string keyboard_verticalarrows { get; set; }
        public bool mark_read_river_confirm { get; set; }
        public string read_story_delay { get; set; }
        public string feed_view_single_story { get; set; }
        public string story_styling { get; set; }
        public bool story_share_kippt { get; set; }
        public string hide_story_changes { get; set; }
        public bool story_share_delicious { get; set; }
        public string truncate_story { get; set; }
        public string default_view { get; set; }
        public bool story_share_evernote { get; set; }
        public bool full_width_story { get; set; }
        public bool story_share_diigo { get; set; }
        public bool hide_public_comments { get; set; }
        public string dateformat { get; set; }
        public string default_read_filter { get; set; }
        public bool lock_green_slider { get; set; }
        public string space_scroll_spacing { get; set; }
        public bool story_share_facebook { get; set; }
        public bool folder_counts { get; set; }
        public bool story_share_twitter { get; set; }
        public bool story_share_readability { get; set; }
        public string doubleclick_feed { get; set; }
        public string story_pane_anchor { get; set; }
        public string intro_page { get; set; }
        public bool title_counts { get; set; }
        public string space_bar_action { get; set; }
        public string show_image_preview { get; set; }
        public string doubleclick_unread { get; set; }
        public string story_button_placement { get; set; }
        public string open_feed_action { get; set; }
        public string hide_read_feeds { get; set; }
        public string ssl { get; set; }
        public string new_window { get; set; }
        public string story_layout { get; set; }
        public string story_titles_pane_size { get; set; }
        public bool animations { get; set; }
        public string lock_mouse_indicator { get; set; }
        public bool story_share_tumblr { get; set; }
        public string story_size { get; set; }
        public bool story_share_googleplus { get; set; }
        public bool mark_read_on_scroll_titles { get; set; }
        public string arrow_scroll_spacing { get; set; }
        public bool story_share_instapaper { get; set; }
        public string show_content_preview { get; set; }
        public string autoopen_folder { get; set; }
        public bool story_share_pinboard { get; set; }
        public bool story_share_readitlater { get; set; }
        public string feed_order { get; set; }
        public bool story_share_pinterest { get; set; }
        public string show_tooltips { get; set; }
        public string default_order { get; set; }
        public bool story_share_buffer { get; set; }
        public string default_folder { get; set; }
        public string keyboard_horizontalarrows { get; set; }
    }

    public class Feeds
    {
        public _1186180 _1186180 { get; set; }
        public _569 _569 { get; set; }
        public _6605581 _6605581 { get; set; }
        public _1665281 _1665281 { get; set; }
        public _776101 _776101 { get; set; }
        public _6188470 _6188470 { get; set; }
        public _3556 _3556 { get; set; }
        public _588075 _588075 { get; set; }
        public _48 _48 { get; set; }
        public _3581 _3581 { get; set; }
        public _45 _45 { get; set; }
        public _183139 _183139 { get; set; }
        public _551953 _551953 { get; set; }
        public _526 _526 { get; set; }
        public _6043968 _6043968 { get; set; }
        public _5261603 _5261603 { get; set; }
        public _1480028 _1480028 { get; set; }
        public _1095 _1095 { get; set; }
        public _5994357 _5994357 { get; set; }
        public _2661956 _2661956 { get; set; }
        public _64313 _64313 { get; set; }
        public _76 _76 { get; set; }
        public _38 _38 { get; set; }
        public _5984253 _5984253 { get; set; }
        public _576138 _576138 { get; set; }
        public _2 _2 { get; set; }
        public _21309 _21309 { get; set; }
        public _161 _161 { get; set; }
        public _5752038 _5752038 { get; set; }
        public _12 _12 { get; set; }
        public _1627 _1627 { get; set; }
        public _1340342 _1340342 { get; set; }
        public _6227976 _6227976 { get; set; }
        public _50 _50 { get; set; }
        public _208613 _208613 { get; set; }
        public _34 _34 { get; set; }
        public _97265 _97265 { get; set; }
    }

    public class _1186180
    {
        public int subs { get; set; }
        public string favicon { get; set; }
        public string favicon_url { get; set; }
        public bool is_push { get; set; }
        public int feed_opens { get; set; }
        public int id { get; set; }
        public bool s3_icon { get; set; }
        public string feed_link { get; set; }
        public int updated_seconds_ago { get; set; }
        public bool favicon_fetching { get; set; }
        public int ng { get; set; }
        public string favicon_border { get; set; }
        public string last_story_date { get; set; }
        public int nt { get; set; }
        public bool not_yet_fetched { get; set; }
        public string updated { get; set; }
        public int average_stories_per_month { get; set; }
        public int ps { get; set; }
        public string feed_address { get; set; }
        public string feed_title { get; set; }
        public string favicon_fade { get; set; }
        public bool is_newsletter { get; set; }
        public int last_story_seconds_ago { get; set; }
        public string favicon_color { get; set; }
        public int stories_last_month { get; set; }
        public bool active { get; set; }
        public bool fetched_once { get; set; }
        public string favicon_text_color { get; set; }
        public bool subscribed { get; set; }
        public int num_subscribers { get; set; }
        public bool s3_page { get; set; }
        public int min_to_decay { get; set; }
        public bool search_indexed { get; set; }
    }

    public class _569
    {
        public int subs { get; set; }
        public string favicon { get; set; }
        public string favicon_url { get; set; }
        public bool is_push { get; set; }
        public int feed_opens { get; set; }
        public int id { get; set; }
        public bool s3_icon { get; set; }
        public string feed_link { get; set; }
        public int updated_seconds_ago { get; set; }
        public bool favicon_fetching { get; set; }
        public int ng { get; set; }
        public string favicon_border { get; set; }
        public string last_story_date { get; set; }
        public int nt { get; set; }
        public bool not_yet_fetched { get; set; }
        public string updated { get; set; }
        public int average_stories_per_month { get; set; }
        public int ps { get; set; }
        public string feed_address { get; set; }
        public string feed_title { get; set; }
        public string favicon_fade { get; set; }
        public bool is_newsletter { get; set; }
        public int last_story_seconds_ago { get; set; }
        public string favicon_color { get; set; }
        public int stories_last_month { get; set; }
        public bool active { get; set; }
        public bool fetched_once { get; set; }
        public string favicon_text_color { get; set; }
        public bool subscribed { get; set; }
        public int num_subscribers { get; set; }
        public bool s3_page { get; set; }
        public int min_to_decay { get; set; }
        public bool search_indexed { get; set; }
    }

    public class _6605581
    {
        public int subs { get; set; }
        public string favicon { get; set; }
        public string favicon_url { get; set; }
        public bool is_push { get; set; }
        public int feed_opens { get; set; }
        public int id { get; set; }
        public bool s3_icon { get; set; }
        public string feed_link { get; set; }
        public int updated_seconds_ago { get; set; }
        public bool favicon_fetching { get; set; }
        public int ng { get; set; }
        public string favicon_border { get; set; }
        public string last_story_date { get; set; }
        public int nt { get; set; }
        public bool not_yet_fetched { get; set; }
        public string updated { get; set; }
        public int average_stories_per_month { get; set; }
        public int ps { get; set; }
        public string feed_address { get; set; }
        public string feed_title { get; set; }
        public string favicon_fade { get; set; }
        public bool is_newsletter { get; set; }
        public int last_story_seconds_ago { get; set; }
        public string favicon_color { get; set; }
        public int stories_last_month { get; set; }
        public bool active { get; set; }
        public bool fetched_once { get; set; }
        public string favicon_text_color { get; set; }
        public bool subscribed { get; set; }
        public int num_subscribers { get; set; }
        public bool s3_page { get; set; }
        public int min_to_decay { get; set; }
        public bool search_indexed { get; set; }
    }

    public class _1665281
    {
        public int subs { get; set; }
        public string favicon { get; set; }
        public string favicon_url { get; set; }
        public bool is_push { get; set; }
        public int feed_opens { get; set; }
        public int id { get; set; }
        public bool s3_icon { get; set; }
        public string feed_link { get; set; }
        public int updated_seconds_ago { get; set; }
        public bool favicon_fetching { get; set; }
        public int ng { get; set; }
        public string favicon_border { get; set; }
        public string last_story_date { get; set; }
        public int nt { get; set; }
        public bool not_yet_fetched { get; set; }
        public string updated { get; set; }
        public int average_stories_per_month { get; set; }
        public int ps { get; set; }
        public string feed_address { get; set; }
        public string feed_title { get; set; }
        public string favicon_fade { get; set; }
        public bool is_newsletter { get; set; }
        public int last_story_seconds_ago { get; set; }
        public string favicon_color { get; set; }
        public int stories_last_month { get; set; }
        public bool active { get; set; }
        public bool fetched_once { get; set; }
        public string favicon_text_color { get; set; }
        public bool subscribed { get; set; }
        public int num_subscribers { get; set; }
        public bool s3_page { get; set; }
        public int min_to_decay { get; set; }
        public bool search_indexed { get; set; }
    }

    public class _776101
    {
        public int subs { get; set; }
        public string favicon { get; set; }
        public string favicon_url { get; set; }
        public bool is_push { get; set; }
        public int feed_opens { get; set; }
        public int id { get; set; }
        public bool s3_icon { get; set; }
        public string feed_link { get; set; }
        public int updated_seconds_ago { get; set; }
        public bool favicon_fetching { get; set; }
        public int ng { get; set; }
        public string favicon_border { get; set; }
        public string last_story_date { get; set; }
        public int nt { get; set; }
        public bool not_yet_fetched { get; set; }
        public string updated { get; set; }
        public int average_stories_per_month { get; set; }
        public int ps { get; set; }
        public string feed_address { get; set; }
        public string feed_title { get; set; }
        public string favicon_fade { get; set; }
        public bool is_newsletter { get; set; }
        public int last_story_seconds_ago { get; set; }
        public string favicon_color { get; set; }
        public int stories_last_month { get; set; }
        public bool active { get; set; }
        public bool fetched_once { get; set; }
        public string favicon_text_color { get; set; }
        public bool subscribed { get; set; }
        public int num_subscribers { get; set; }
        public bool s3_page { get; set; }
        public int min_to_decay { get; set; }
        public bool search_indexed { get; set; }
    }

    public class _6188470
    {
        public int subs { get; set; }
        public string favicon { get; set; }
        public string favicon_url { get; set; }
        public bool is_push { get; set; }
        public int feed_opens { get; set; }
        public int id { get; set; }
        public bool s3_icon { get; set; }
        public string feed_link { get; set; }
        public int updated_seconds_ago { get; set; }
        public bool favicon_fetching { get; set; }
        public int ng { get; set; }
        public string favicon_border { get; set; }
        public string last_story_date { get; set; }
        public int nt { get; set; }
        public bool not_yet_fetched { get; set; }
        public string updated { get; set; }
        public int average_stories_per_month { get; set; }
        public int ps { get; set; }
        public string feed_address { get; set; }
        public string feed_title { get; set; }
        public string favicon_fade { get; set; }
        public bool is_newsletter { get; set; }
        public int last_story_seconds_ago { get; set; }
        public string favicon_color { get; set; }
        public int stories_last_month { get; set; }
        public bool active { get; set; }
        public bool fetched_once { get; set; }
        public string favicon_text_color { get; set; }
        public bool subscribed { get; set; }
        public int num_subscribers { get; set; }
        public bool s3_page { get; set; }
        public int min_to_decay { get; set; }
        public bool search_indexed { get; set; }
    }

    public class _3556
    {
        public int subs { get; set; }
        public string favicon { get; set; }
        public string favicon_url { get; set; }
        public int exception_code { get; set; }
        public bool is_push { get; set; }
        public int feed_opens { get; set; }
        public int id { get; set; }
        public bool has_exception { get; set; }
        public bool s3_icon { get; set; }
        public string feed_link { get; set; }
        public int updated_seconds_ago { get; set; }
        public bool favicon_fetching { get; set; }
        public int ng { get; set; }
        public string favicon_border { get; set; }
        public string last_story_date { get; set; }
        public int nt { get; set; }
        public bool not_yet_fetched { get; set; }
        public string updated { get; set; }
        public int average_stories_per_month { get; set; }
        public int ps { get; set; }
        public string feed_address { get; set; }
        public string feed_title { get; set; }
        public string favicon_fade { get; set; }
        public string exception_type { get; set; }
        public bool is_newsletter { get; set; }
        public int last_story_seconds_ago { get; set; }
        public string favicon_color { get; set; }
        public int stories_last_month { get; set; }
        public bool active { get; set; }
        public bool fetched_once { get; set; }
        public string favicon_text_color { get; set; }
        public bool subscribed { get; set; }
        public int num_subscribers { get; set; }
        public bool s3_page { get; set; }
        public int min_to_decay { get; set; }
        public bool search_indexed { get; set; }
    }

    public class _588075
    {
        public int subs { get; set; }
        public string favicon { get; set; }
        public string favicon_url { get; set; }
        public bool is_push { get; set; }
        public int feed_opens { get; set; }
        public int id { get; set; }
        public bool s3_icon { get; set; }
        public string feed_link { get; set; }
        public int updated_seconds_ago { get; set; }
        public bool favicon_fetching { get; set; }
        public int ng { get; set; }
        public string favicon_border { get; set; }
        public string last_story_date { get; set; }
        public int nt { get; set; }
        public bool not_yet_fetched { get; set; }
        public string updated { get; set; }
        public int average_stories_per_month { get; set; }
        public int ps { get; set; }
        public string feed_address { get; set; }
        public string feed_title { get; set; }
        public string favicon_fade { get; set; }
        public bool is_newsletter { get; set; }
        public int last_story_seconds_ago { get; set; }
        public string favicon_color { get; set; }
        public int stories_last_month { get; set; }
        public bool active { get; set; }
        public bool fetched_once { get; set; }
        public string favicon_text_color { get; set; }
        public bool subscribed { get; set; }
        public int num_subscribers { get; set; }
        public bool s3_page { get; set; }
        public int min_to_decay { get; set; }
        public bool search_indexed { get; set; }
    }

    public class _48
    {
        public int subs { get; set; }
        public string favicon { get; set; }
        public string favicon_url { get; set; }
        public bool is_push { get; set; }
        public int feed_opens { get; set; }
        public int id { get; set; }
        public bool s3_icon { get; set; }
        public string feed_link { get; set; }
        public int updated_seconds_ago { get; set; }
        public bool favicon_fetching { get; set; }
        public int ng { get; set; }
        public string favicon_border { get; set; }
        public string last_story_date { get; set; }
        public int nt { get; set; }
        public bool not_yet_fetched { get; set; }
        public string updated { get; set; }
        public int average_stories_per_month { get; set; }
        public int ps { get; set; }
        public string feed_address { get; set; }
        public string feed_title { get; set; }
        public string favicon_fade { get; set; }
        public bool is_newsletter { get; set; }
        public int last_story_seconds_ago { get; set; }
        public string favicon_color { get; set; }
        public int stories_last_month { get; set; }
        public bool active { get; set; }
        public bool fetched_once { get; set; }
        public string favicon_text_color { get; set; }
        public bool subscribed { get; set; }
        public int num_subscribers { get; set; }
        public bool s3_page { get; set; }
        public int min_to_decay { get; set; }
        public bool search_indexed { get; set; }
    }

    public class _3581
    {
        public int subs { get; set; }
        public string favicon { get; set; }
        public string favicon_url { get; set; }
        public bool is_push { get; set; }
        public int feed_opens { get; set; }
        public int id { get; set; }
        public bool s3_icon { get; set; }
        public string feed_link { get; set; }
        public int updated_seconds_ago { get; set; }
        public bool favicon_fetching { get; set; }
        public int ng { get; set; }
        public string favicon_border { get; set; }
        public string last_story_date { get; set; }
        public int nt { get; set; }
        public bool not_yet_fetched { get; set; }
        public string updated { get; set; }
        public int average_stories_per_month { get; set; }
        public int ps { get; set; }
        public string feed_address { get; set; }
        public string feed_title { get; set; }
        public string favicon_fade { get; set; }
        public bool is_newsletter { get; set; }
        public int last_story_seconds_ago { get; set; }
        public string favicon_color { get; set; }
        public int stories_last_month { get; set; }
        public bool active { get; set; }
        public bool fetched_once { get; set; }
        public string favicon_text_color { get; set; }
        public bool subscribed { get; set; }
        public int num_subscribers { get; set; }
        public bool s3_page { get; set; }
        public int min_to_decay { get; set; }
        public bool search_indexed { get; set; }
    }

    public class _45
    {
        public int subs { get; set; }
        public string favicon { get; set; }
        public string favicon_url { get; set; }
        public bool is_push { get; set; }
        public int feed_opens { get; set; }
        public int id { get; set; }
        public bool s3_icon { get; set; }
        public string feed_link { get; set; }
        public int updated_seconds_ago { get; set; }
        public bool favicon_fetching { get; set; }
        public int ng { get; set; }
        public string favicon_border { get; set; }
        public string last_story_date { get; set; }
        public int nt { get; set; }
        public bool not_yet_fetched { get; set; }
        public string updated { get; set; }
        public int average_stories_per_month { get; set; }
        public int ps { get; set; }
        public string feed_address { get; set; }
        public string feed_title { get; set; }
        public string favicon_fade { get; set; }
        public bool is_newsletter { get; set; }
        public int last_story_seconds_ago { get; set; }
        public string favicon_color { get; set; }
        public int stories_last_month { get; set; }
        public bool active { get; set; }
        public bool fetched_once { get; set; }
        public string favicon_text_color { get; set; }
        public bool subscribed { get; set; }
        public int num_subscribers { get; set; }
        public bool s3_page { get; set; }
        public int min_to_decay { get; set; }
        public bool search_indexed { get; set; }
    }

    public class _183139
    {
        public int subs { get; set; }
        public string favicon { get; set; }
        public string favicon_url { get; set; }
        public bool is_push { get; set; }
        public int feed_opens { get; set; }
        public int id { get; set; }
        public bool s3_icon { get; set; }
        public string feed_link { get; set; }
        public int updated_seconds_ago { get; set; }
        public bool favicon_fetching { get; set; }
        public int ng { get; set; }
        public string favicon_border { get; set; }
        public string last_story_date { get; set; }
        public int nt { get; set; }
        public bool not_yet_fetched { get; set; }
        public string updated { get; set; }
        public int average_stories_per_month { get; set; }
        public int ps { get; set; }
        public string feed_address { get; set; }
        public string feed_title { get; set; }
        public string favicon_fade { get; set; }
        public bool is_newsletter { get; set; }
        public int last_story_seconds_ago { get; set; }
        public string favicon_color { get; set; }
        public int stories_last_month { get; set; }
        public bool active { get; set; }
        public bool fetched_once { get; set; }
        public string favicon_text_color { get; set; }
        public bool subscribed { get; set; }
        public int num_subscribers { get; set; }
        public bool s3_page { get; set; }
        public int min_to_decay { get; set; }
        public bool search_indexed { get; set; }
    }

    public class _551953
    {
        public int subs { get; set; }
        public string favicon { get; set; }
        public string favicon_url { get; set; }
        public int exception_code { get; set; }
        public bool is_push { get; set; }
        public int feed_opens { get; set; }
        public int id { get; set; }
        public bool has_exception { get; set; }
        public bool s3_icon { get; set; }
        public string feed_link { get; set; }
        public int updated_seconds_ago { get; set; }
        public bool favicon_fetching { get; set; }
        public int ng { get; set; }
        public string favicon_border { get; set; }
        public string last_story_date { get; set; }
        public int nt { get; set; }
        public bool not_yet_fetched { get; set; }
        public string updated { get; set; }
        public int average_stories_per_month { get; set; }
        public int ps { get; set; }
        public string feed_address { get; set; }
        public string feed_title { get; set; }
        public string favicon_fade { get; set; }
        public string exception_type { get; set; }
        public bool is_newsletter { get; set; }
        public int last_story_seconds_ago { get; set; }
        public string favicon_color { get; set; }
        public int stories_last_month { get; set; }
        public bool active { get; set; }
        public bool fetched_once { get; set; }
        public string favicon_text_color { get; set; }
        public bool subscribed { get; set; }
        public int num_subscribers { get; set; }
        public bool s3_page { get; set; }
        public int min_to_decay { get; set; }
        public bool search_indexed { get; set; }
    }

    public class _526
    {
        public int subs { get; set; }
        public string favicon { get; set; }
        public string favicon_url { get; set; }
        public bool is_push { get; set; }
        public int feed_opens { get; set; }
        public int id { get; set; }
        public bool s3_icon { get; set; }
        public string feed_link { get; set; }
        public int updated_seconds_ago { get; set; }
        public bool favicon_fetching { get; set; }
        public int ng { get; set; }
        public string favicon_border { get; set; }
        public string last_story_date { get; set; }
        public int nt { get; set; }
        public bool not_yet_fetched { get; set; }
        public string updated { get; set; }
        public int average_stories_per_month { get; set; }
        public int ps { get; set; }
        public string feed_address { get; set; }
        public string feed_title { get; set; }
        public string favicon_fade { get; set; }
        public bool is_newsletter { get; set; }
        public int last_story_seconds_ago { get; set; }
        public string favicon_color { get; set; }
        public int stories_last_month { get; set; }
        public bool active { get; set; }
        public bool fetched_once { get; set; }
        public string favicon_text_color { get; set; }
        public bool subscribed { get; set; }
        public int num_subscribers { get; set; }
        public bool s3_page { get; set; }
        public int min_to_decay { get; set; }
        public bool search_indexed { get; set; }
    }

    public class _6043968
    {
        public int subs { get; set; }
        public string favicon { get; set; }
        public string favicon_url { get; set; }
        public bool is_push { get; set; }
        public int feed_opens { get; set; }
        public int id { get; set; }
        public bool s3_icon { get; set; }
        public string feed_link { get; set; }
        public int updated_seconds_ago { get; set; }
        public bool favicon_fetching { get; set; }
        public int ng { get; set; }
        public string favicon_border { get; set; }
        public string last_story_date { get; set; }
        public int nt { get; set; }
        public bool not_yet_fetched { get; set; }
        public string updated { get; set; }
        public int average_stories_per_month { get; set; }
        public int ps { get; set; }
        public string feed_address { get; set; }
        public string feed_title { get; set; }
        public string favicon_fade { get; set; }
        public bool is_newsletter { get; set; }
        public int last_story_seconds_ago { get; set; }
        public string favicon_color { get; set; }
        public int stories_last_month { get; set; }
        public bool active { get; set; }
        public bool fetched_once { get; set; }
        public string favicon_text_color { get; set; }
        public bool subscribed { get; set; }
        public int num_subscribers { get; set; }
        public bool s3_page { get; set; }
        public int min_to_decay { get; set; }
        public bool search_indexed { get; set; }
    }

    public class _5261603
    {
        public int subs { get; set; }
        public string favicon { get; set; }
        public string favicon_url { get; set; }
        public bool is_push { get; set; }
        public int feed_opens { get; set; }
        public int id { get; set; }
        public bool s3_icon { get; set; }
        public string feed_link { get; set; }
        public int updated_seconds_ago { get; set; }
        public bool favicon_fetching { get; set; }
        public int ng { get; set; }
        public string favicon_border { get; set; }
        public string last_story_date { get; set; }
        public int nt { get; set; }
        public bool not_yet_fetched { get; set; }
        public string updated { get; set; }
        public int average_stories_per_month { get; set; }
        public int ps { get; set; }
        public string feed_address { get; set; }
        public string feed_title { get; set; }
        public string favicon_fade { get; set; }
        public bool is_newsletter { get; set; }
        public int last_story_seconds_ago { get; set; }
        public string favicon_color { get; set; }
        public int stories_last_month { get; set; }
        public bool active { get; set; }
        public bool fetched_once { get; set; }
        public string favicon_text_color { get; set; }
        public bool subscribed { get; set; }
        public int num_subscribers { get; set; }
        public bool s3_page { get; set; }
        public int min_to_decay { get; set; }
        public bool search_indexed { get; set; }
    }

    public class _1480028
    {
        public int subs { get; set; }
        public string favicon { get; set; }
        public string favicon_url { get; set; }
        public bool is_push { get; set; }
        public int feed_opens { get; set; }
        public int id { get; set; }
        public bool s3_icon { get; set; }
        public string feed_link { get; set; }
        public int updated_seconds_ago { get; set; }
        public bool favicon_fetching { get; set; }
        public int ng { get; set; }
        public string favicon_border { get; set; }
        public string last_story_date { get; set; }
        public int nt { get; set; }
        public bool not_yet_fetched { get; set; }
        public string updated { get; set; }
        public int average_stories_per_month { get; set; }
        public int ps { get; set; }
        public string feed_address { get; set; }
        public string feed_title { get; set; }
        public string favicon_fade { get; set; }
        public bool is_newsletter { get; set; }
        public int last_story_seconds_ago { get; set; }
        public string favicon_color { get; set; }
        public int stories_last_month { get; set; }
        public bool active { get; set; }
        public bool fetched_once { get; set; }
        public string favicon_text_color { get; set; }
        public bool subscribed { get; set; }
        public int num_subscribers { get; set; }
        public bool s3_page { get; set; }
        public int min_to_decay { get; set; }
        public bool search_indexed { get; set; }
    }

    public class _1095
    {
        public int subs { get; set; }
        public string favicon { get; set; }
        public string favicon_url { get; set; }
        public bool is_push { get; set; }
        public int feed_opens { get; set; }
        public int id { get; set; }
        public bool s3_icon { get; set; }
        public string feed_link { get; set; }
        public int updated_seconds_ago { get; set; }
        public bool favicon_fetching { get; set; }
        public int ng { get; set; }
        public string favicon_border { get; set; }
        public string last_story_date { get; set; }
        public int nt { get; set; }
        public bool not_yet_fetched { get; set; }
        public string updated { get; set; }
        public int average_stories_per_month { get; set; }
        public int ps { get; set; }
        public string feed_address { get; set; }
        public string feed_title { get; set; }
        public string favicon_fade { get; set; }
        public bool is_newsletter { get; set; }
        public int last_story_seconds_ago { get; set; }
        public string favicon_color { get; set; }
        public int stories_last_month { get; set; }
        public bool active { get; set; }
        public bool fetched_once { get; set; }
        public string favicon_text_color { get; set; }
        public bool subscribed { get; set; }
        public int num_subscribers { get; set; }
        public bool s3_page { get; set; }
        public int min_to_decay { get; set; }
        public bool search_indexed { get; set; }
    }

    public class _5994357
    {
        public int subs { get; set; }
        public string favicon { get; set; }
        public string favicon_url { get; set; }
        public bool is_push { get; set; }
        public int feed_opens { get; set; }
        public int id { get; set; }
        public bool s3_icon { get; set; }
        public string feed_link { get; set; }
        public int updated_seconds_ago { get; set; }
        public bool favicon_fetching { get; set; }
        public int ng { get; set; }
        public string favicon_border { get; set; }
        public string last_story_date { get; set; }
        public int nt { get; set; }
        public bool not_yet_fetched { get; set; }
        public string updated { get; set; }
        public int average_stories_per_month { get; set; }
        public int ps { get; set; }
        public string feed_address { get; set; }
        public string feed_title { get; set; }
        public string favicon_fade { get; set; }
        public bool is_newsletter { get; set; }
        public int last_story_seconds_ago { get; set; }
        public string favicon_color { get; set; }
        public int stories_last_month { get; set; }
        public bool active { get; set; }
        public bool fetched_once { get; set; }
        public string favicon_text_color { get; set; }
        public bool subscribed { get; set; }
        public int num_subscribers { get; set; }
        public bool s3_page { get; set; }
        public int min_to_decay { get; set; }
        public bool search_indexed { get; set; }
    }

    public class _2661956
    {
        public int subs { get; set; }
        public string favicon { get; set; }
        public string favicon_url { get; set; }
        public bool is_push { get; set; }
        public int feed_opens { get; set; }
        public int id { get; set; }
        public bool s3_icon { get; set; }
        public string feed_link { get; set; }
        public int updated_seconds_ago { get; set; }
        public bool favicon_fetching { get; set; }
        public int ng { get; set; }
        public string favicon_border { get; set; }
        public string last_story_date { get; set; }
        public int nt { get; set; }
        public bool not_yet_fetched { get; set; }
        public string updated { get; set; }
        public int average_stories_per_month { get; set; }
        public int ps { get; set; }
        public string feed_address { get; set; }
        public string feed_title { get; set; }
        public string favicon_fade { get; set; }
        public bool is_newsletter { get; set; }
        public int last_story_seconds_ago { get; set; }
        public string favicon_color { get; set; }
        public int stories_last_month { get; set; }
        public bool active { get; set; }
        public bool fetched_once { get; set; }
        public string favicon_text_color { get; set; }
        public bool subscribed { get; set; }
        public int num_subscribers { get; set; }
        public bool s3_page { get; set; }
        public int min_to_decay { get; set; }
        public bool search_indexed { get; set; }
    }

    public class _64313
    {
        public int subs { get; set; }
        public string favicon { get; set; }
        public string favicon_url { get; set; }
        public bool is_push { get; set; }
        public int feed_opens { get; set; }
        public int id { get; set; }
        public bool s3_icon { get; set; }
        public string feed_link { get; set; }
        public int updated_seconds_ago { get; set; }
        public bool favicon_fetching { get; set; }
        public int ng { get; set; }
        public string favicon_border { get; set; }
        public string last_story_date { get; set; }
        public int nt { get; set; }
        public bool not_yet_fetched { get; set; }
        public string updated { get; set; }
        public int average_stories_per_month { get; set; }
        public int ps { get; set; }
        public string feed_address { get; set; }
        public string feed_title { get; set; }
        public string favicon_fade { get; set; }
        public bool is_newsletter { get; set; }
        public int last_story_seconds_ago { get; set; }
        public string favicon_color { get; set; }
        public int stories_last_month { get; set; }
        public bool active { get; set; }
        public bool fetched_once { get; set; }
        public string favicon_text_color { get; set; }
        public bool subscribed { get; set; }
        public int num_subscribers { get; set; }
        public bool s3_page { get; set; }
        public int min_to_decay { get; set; }
        public bool search_indexed { get; set; }
    }

    public class _76
    {
        public int subs { get; set; }
        public string favicon { get; set; }
        public string favicon_url { get; set; }
        public bool is_push { get; set; }
        public int feed_opens { get; set; }
        public int id { get; set; }
        public bool s3_icon { get; set; }
        public string feed_link { get; set; }
        public int updated_seconds_ago { get; set; }
        public bool favicon_fetching { get; set; }
        public int ng { get; set; }
        public string favicon_border { get; set; }
        public string last_story_date { get; set; }
        public int nt { get; set; }
        public bool not_yet_fetched { get; set; }
        public string updated { get; set; }
        public int average_stories_per_month { get; set; }
        public int ps { get; set; }
        public string feed_address { get; set; }
        public string feed_title { get; set; }
        public string favicon_fade { get; set; }
        public bool is_newsletter { get; set; }
        public int last_story_seconds_ago { get; set; }
        public string favicon_color { get; set; }
        public int stories_last_month { get; set; }
        public bool active { get; set; }
        public bool fetched_once { get; set; }
        public string favicon_text_color { get; set; }
        public bool subscribed { get; set; }
        public int num_subscribers { get; set; }
        public bool s3_page { get; set; }
        public int min_to_decay { get; set; }
        public bool search_indexed { get; set; }
    }

    public class _38
    {
        public int subs { get; set; }
        public string favicon { get; set; }
        public string favicon_url { get; set; }
        public bool is_push { get; set; }
        public int feed_opens { get; set; }
        public int id { get; set; }
        public bool s3_icon { get; set; }
        public string feed_link { get; set; }
        public int updated_seconds_ago { get; set; }
        public bool favicon_fetching { get; set; }
        public int ng { get; set; }
        public string favicon_border { get; set; }
        public string last_story_date { get; set; }
        public int nt { get; set; }
        public bool not_yet_fetched { get; set; }
        public string updated { get; set; }
        public int average_stories_per_month { get; set; }
        public int ps { get; set; }
        public string feed_address { get; set; }
        public string feed_title { get; set; }
        public string favicon_fade { get; set; }
        public bool is_newsletter { get; set; }
        public int last_story_seconds_ago { get; set; }
        public string favicon_color { get; set; }
        public int stories_last_month { get; set; }
        public bool active { get; set; }
        public bool fetched_once { get; set; }
        public string favicon_text_color { get; set; }
        public bool subscribed { get; set; }
        public int num_subscribers { get; set; }
        public bool s3_page { get; set; }
        public int min_to_decay { get; set; }
        public bool search_indexed { get; set; }
    }

    public class _5984253
    {
        public int subs { get; set; }
        public string favicon { get; set; }
        public string favicon_url { get; set; }
        public bool is_push { get; set; }
        public int feed_opens { get; set; }
        public int id { get; set; }
        public bool s3_icon { get; set; }
        public string feed_link { get; set; }
        public int updated_seconds_ago { get; set; }
        public bool favicon_fetching { get; set; }
        public int ng { get; set; }
        public string favicon_border { get; set; }
        public string last_story_date { get; set; }
        public int nt { get; set; }
        public bool not_yet_fetched { get; set; }
        public string updated { get; set; }
        public int average_stories_per_month { get; set; }
        public int ps { get; set; }
        public string feed_address { get; set; }
        public string feed_title { get; set; }
        public string favicon_fade { get; set; }
        public bool is_newsletter { get; set; }
        public int last_story_seconds_ago { get; set; }
        public string favicon_color { get; set; }
        public int stories_last_month { get; set; }
        public bool active { get; set; }
        public bool fetched_once { get; set; }
        public string favicon_text_color { get; set; }
        public bool subscribed { get; set; }
        public int num_subscribers { get; set; }
        public bool s3_page { get; set; }
        public int min_to_decay { get; set; }
        public bool search_indexed { get; set; }
    }

    public class _576138
    {
        public int subs { get; set; }
        public string favicon { get; set; }
        public string favicon_url { get; set; }
        public bool is_push { get; set; }
        public int feed_opens { get; set; }
        public int id { get; set; }
        public bool s3_icon { get; set; }
        public string feed_link { get; set; }
        public int updated_seconds_ago { get; set; }
        public bool favicon_fetching { get; set; }
        public int ng { get; set; }
        public string favicon_border { get; set; }
        public string last_story_date { get; set; }
        public int nt { get; set; }
        public bool not_yet_fetched { get; set; }
        public string updated { get; set; }
        public int average_stories_per_month { get; set; }
        public int ps { get; set; }
        public string feed_address { get; set; }
        public string feed_title { get; set; }
        public string favicon_fade { get; set; }
        public bool is_newsletter { get; set; }
        public int last_story_seconds_ago { get; set; }
        public string favicon_color { get; set; }
        public int stories_last_month { get; set; }
        public bool active { get; set; }
        public bool fetched_once { get; set; }
        public string favicon_text_color { get; set; }
        public bool subscribed { get; set; }
        public int num_subscribers { get; set; }
        public bool s3_page { get; set; }
        public int min_to_decay { get; set; }
        public bool search_indexed { get; set; }
    }

    public class _2
    {
        public int subs { get; set; }
        public string favicon { get; set; }
        public string favicon_url { get; set; }
        public bool is_push { get; set; }
        public int feed_opens { get; set; }
        public int id { get; set; }
        public bool s3_icon { get; set; }
        public string feed_link { get; set; }
        public int updated_seconds_ago { get; set; }
        public bool favicon_fetching { get; set; }
        public int ng { get; set; }
        public string favicon_border { get; set; }
        public string last_story_date { get; set; }
        public int nt { get; set; }
        public bool not_yet_fetched { get; set; }
        public string updated { get; set; }
        public int average_stories_per_month { get; set; }
        public int ps { get; set; }
        public string feed_address { get; set; }
        public string feed_title { get; set; }
        public string favicon_fade { get; set; }
        public bool is_newsletter { get; set; }
        public int last_story_seconds_ago { get; set; }
        public string favicon_color { get; set; }
        public int stories_last_month { get; set; }
        public bool active { get; set; }
        public bool fetched_once { get; set; }
        public string favicon_text_color { get; set; }
        public bool subscribed { get; set; }
        public int num_subscribers { get; set; }
        public bool s3_page { get; set; }
        public int min_to_decay { get; set; }
        public bool search_indexed { get; set; }
    }

    public class _21309
    {
        public int subs { get; set; }
        public string favicon { get; set; }
        public string favicon_url { get; set; }
        public bool is_push { get; set; }
        public int feed_opens { get; set; }
        public int id { get; set; }
        public bool s3_icon { get; set; }
        public string feed_link { get; set; }
        public int updated_seconds_ago { get; set; }
        public bool favicon_fetching { get; set; }
        public int ng { get; set; }
        public string favicon_border { get; set; }
        public string last_story_date { get; set; }
        public int nt { get; set; }
        public bool not_yet_fetched { get; set; }
        public string updated { get; set; }
        public int average_stories_per_month { get; set; }
        public int ps { get; set; }
        public string feed_address { get; set; }
        public string feed_title { get; set; }
        public string favicon_fade { get; set; }
        public bool is_newsletter { get; set; }
        public int last_story_seconds_ago { get; set; }
        public string favicon_color { get; set; }
        public int stories_last_month { get; set; }
        public bool active { get; set; }
        public bool fetched_once { get; set; }
        public string favicon_text_color { get; set; }
        public bool subscribed { get; set; }
        public int num_subscribers { get; set; }
        public bool s3_page { get; set; }
        public int min_to_decay { get; set; }
        public bool search_indexed { get; set; }
    }

    public class _161
    {
        public int subs { get; set; }
        public string favicon { get; set; }
        public string favicon_url { get; set; }
        public int exception_code { get; set; }
        public bool is_push { get; set; }
        public int feed_opens { get; set; }
        public int id { get; set; }
        public bool has_exception { get; set; }
        public bool s3_icon { get; set; }
        public string feed_link { get; set; }
        public int updated_seconds_ago { get; set; }
        public bool favicon_fetching { get; set; }
        public int ng { get; set; }
        public string favicon_border { get; set; }
        public string last_story_date { get; set; }
        public int nt { get; set; }
        public bool not_yet_fetched { get; set; }
        public string updated { get; set; }
        public int average_stories_per_month { get; set; }
        public int ps { get; set; }
        public string feed_address { get; set; }
        public string feed_title { get; set; }
        public string favicon_fade { get; set; }
        public string exception_type { get; set; }
        public bool is_newsletter { get; set; }
        public int last_story_seconds_ago { get; set; }
        public string favicon_color { get; set; }
        public int stories_last_month { get; set; }
        public bool active { get; set; }
        public bool fetched_once { get; set; }
        public string favicon_text_color { get; set; }
        public bool subscribed { get; set; }
        public int num_subscribers { get; set; }
        public bool s3_page { get; set; }
        public int min_to_decay { get; set; }
        public bool search_indexed { get; set; }
    }

    public class _5752038
    {
        public int subs { get; set; }
        public string favicon { get; set; }
        public string favicon_url { get; set; }
        public bool is_push { get; set; }
        public int feed_opens { get; set; }
        public int id { get; set; }
        public bool s3_icon { get; set; }
        public string feed_link { get; set; }
        public int updated_seconds_ago { get; set; }
        public bool favicon_fetching { get; set; }
        public int ng { get; set; }
        public string favicon_border { get; set; }
        public string last_story_date { get; set; }
        public int nt { get; set; }
        public bool not_yet_fetched { get; set; }
        public string updated { get; set; }
        public int average_stories_per_month { get; set; }
        public int ps { get; set; }
        public string feed_address { get; set; }
        public string feed_title { get; set; }
        public string favicon_fade { get; set; }
        public bool is_newsletter { get; set; }
        public int last_story_seconds_ago { get; set; }
        public string favicon_color { get; set; }
        public int stories_last_month { get; set; }
        public bool active { get; set; }
        public bool fetched_once { get; set; }
        public string favicon_text_color { get; set; }
        public bool subscribed { get; set; }
        public int num_subscribers { get; set; }
        public bool s3_page { get; set; }
        public int min_to_decay { get; set; }
        public bool search_indexed { get; set; }
    }

    public class _12
    {
        public int subs { get; set; }
        public string favicon { get; set; }
        public string favicon_url { get; set; }
        public bool is_push { get; set; }
        public int feed_opens { get; set; }
        public int id { get; set; }
        public bool s3_icon { get; set; }
        public string feed_link { get; set; }
        public int updated_seconds_ago { get; set; }
        public bool favicon_fetching { get; set; }
        public int ng { get; set; }
        public string favicon_border { get; set; }
        public string last_story_date { get; set; }
        public int nt { get; set; }
        public bool not_yet_fetched { get; set; }
        public string updated { get; set; }
        public int average_stories_per_month { get; set; }
        public int ps { get; set; }
        public string feed_address { get; set; }
        public string feed_title { get; set; }
        public string favicon_fade { get; set; }
        public bool is_newsletter { get; set; }
        public int last_story_seconds_ago { get; set; }
        public string favicon_color { get; set; }
        public int stories_last_month { get; set; }
        public bool active { get; set; }
        public bool fetched_once { get; set; }
        public string favicon_text_color { get; set; }
        public bool subscribed { get; set; }
        public int num_subscribers { get; set; }
        public bool s3_page { get; set; }
        public int min_to_decay { get; set; }
        public bool search_indexed { get; set; }
    }

    public class _1627
    {
        public int subs { get; set; }
        public string favicon { get; set; }
        public string favicon_url { get; set; }
        public bool is_push { get; set; }
        public int feed_opens { get; set; }
        public int id { get; set; }
        public bool s3_icon { get; set; }
        public string feed_link { get; set; }
        public int updated_seconds_ago { get; set; }
        public bool favicon_fetching { get; set; }
        public int ng { get; set; }
        public string favicon_border { get; set; }
        public string last_story_date { get; set; }
        public int nt { get; set; }
        public bool not_yet_fetched { get; set; }
        public string updated { get; set; }
        public int average_stories_per_month { get; set; }
        public int ps { get; set; }
        public string feed_address { get; set; }
        public string feed_title { get; set; }
        public string favicon_fade { get; set; }
        public bool is_newsletter { get; set; }
        public int last_story_seconds_ago { get; set; }
        public string favicon_color { get; set; }
        public int stories_last_month { get; set; }
        public bool active { get; set; }
        public bool fetched_once { get; set; }
        public string favicon_text_color { get; set; }
        public bool subscribed { get; set; }
        public int num_subscribers { get; set; }
        public bool s3_page { get; set; }
        public int min_to_decay { get; set; }
        public bool search_indexed { get; set; }
    }

    public class _1340342
    {
        public int subs { get; set; }
        public string favicon { get; set; }
        public string favicon_url { get; set; }
        public bool is_push { get; set; }
        public int feed_opens { get; set; }
        public int id { get; set; }
        public bool s3_icon { get; set; }
        public string feed_link { get; set; }
        public int updated_seconds_ago { get; set; }
        public bool favicon_fetching { get; set; }
        public int ng { get; set; }
        public string favicon_border { get; set; }
        public string last_story_date { get; set; }
        public int nt { get; set; }
        public bool not_yet_fetched { get; set; }
        public string updated { get; set; }
        public int average_stories_per_month { get; set; }
        public int ps { get; set; }
        public string feed_address { get; set; }
        public string feed_title { get; set; }
        public string favicon_fade { get; set; }
        public bool is_newsletter { get; set; }
        public int last_story_seconds_ago { get; set; }
        public string favicon_color { get; set; }
        public int stories_last_month { get; set; }
        public bool active { get; set; }
        public bool fetched_once { get; set; }
        public string favicon_text_color { get; set; }
        public bool subscribed { get; set; }
        public int num_subscribers { get; set; }
        public bool s3_page { get; set; }
        public int min_to_decay { get; set; }
        public bool search_indexed { get; set; }
    }

    public class _6227976
    {
        public int subs { get; set; }
        public string favicon { get; set; }
        public string favicon_url { get; set; }
        public bool is_push { get; set; }
        public int feed_opens { get; set; }
        public int id { get; set; }
        public bool s3_icon { get; set; }
        public string feed_link { get; set; }
        public int updated_seconds_ago { get; set; }
        public bool favicon_fetching { get; set; }
        public int ng { get; set; }
        public string favicon_border { get; set; }
        public string last_story_date { get; set; }
        public int nt { get; set; }
        public bool not_yet_fetched { get; set; }
        public string updated { get; set; }
        public int average_stories_per_month { get; set; }
        public int ps { get; set; }
        public string feed_address { get; set; }
        public string feed_title { get; set; }
        public string favicon_fade { get; set; }
        public bool is_newsletter { get; set; }
        public int last_story_seconds_ago { get; set; }
        public string favicon_color { get; set; }
        public int stories_last_month { get; set; }
        public bool active { get; set; }
        public bool fetched_once { get; set; }
        public string favicon_text_color { get; set; }
        public bool subscribed { get; set; }
        public int num_subscribers { get; set; }
        public bool s3_page { get; set; }
        public int min_to_decay { get; set; }
        public bool search_indexed { get; set; }
    }

    public class _50
    {
        public int subs { get; set; }
        public string favicon { get; set; }
        public string favicon_url { get; set; }
        public bool is_push { get; set; }
        public int feed_opens { get; set; }
        public int id { get; set; }
        public bool s3_icon { get; set; }
        public string feed_link { get; set; }
        public int updated_seconds_ago { get; set; }
        public bool favicon_fetching { get; set; }
        public int ng { get; set; }
        public string favicon_border { get; set; }
        public string last_story_date { get; set; }
        public int nt { get; set; }
        public bool not_yet_fetched { get; set; }
        public string updated { get; set; }
        public int average_stories_per_month { get; set; }
        public int ps { get; set; }
        public string feed_address { get; set; }
        public string feed_title { get; set; }
        public string favicon_fade { get; set; }
        public bool is_newsletter { get; set; }
        public int last_story_seconds_ago { get; set; }
        public string favicon_color { get; set; }
        public int stories_last_month { get; set; }
        public bool active { get; set; }
        public bool fetched_once { get; set; }
        public string favicon_text_color { get; set; }
        public bool subscribed { get; set; }
        public int num_subscribers { get; set; }
        public bool s3_page { get; set; }
        public int min_to_decay { get; set; }
        public bool search_indexed { get; set; }
    }

    public class _208613
    {
        public int subs { get; set; }
        public string favicon { get; set; }
        public string favicon_url { get; set; }
        public bool is_push { get; set; }
        public int feed_opens { get; set; }
        public int id { get; set; }
        public bool s3_icon { get; set; }
        public string feed_link { get; set; }
        public int updated_seconds_ago { get; set; }
        public bool favicon_fetching { get; set; }
        public int ng { get; set; }
        public string favicon_border { get; set; }
        public string last_story_date { get; set; }
        public int nt { get; set; }
        public bool not_yet_fetched { get; set; }
        public string updated { get; set; }
        public int average_stories_per_month { get; set; }
        public int ps { get; set; }
        public string feed_address { get; set; }
        public string feed_title { get; set; }
        public string favicon_fade { get; set; }
        public bool is_newsletter { get; set; }
        public int last_story_seconds_ago { get; set; }
        public string favicon_color { get; set; }
        public int stories_last_month { get; set; }
        public bool active { get; set; }
        public bool fetched_once { get; set; }
        public string favicon_text_color { get; set; }
        public bool subscribed { get; set; }
        public int num_subscribers { get; set; }
        public bool s3_page { get; set; }
        public int min_to_decay { get; set; }
        public bool search_indexed { get; set; }
    }

    public class _34
    {
        public int subs { get; set; }
        public string favicon { get; set; }
        public string favicon_url { get; set; }
        public bool is_push { get; set; }
        public int feed_opens { get; set; }
        public int id { get; set; }
        public bool s3_icon { get; set; }
        public string feed_link { get; set; }
        public int updated_seconds_ago { get; set; }
        public bool favicon_fetching { get; set; }
        public int ng { get; set; }
        public string favicon_border { get; set; }
        public string last_story_date { get; set; }
        public int nt { get; set; }
        public bool not_yet_fetched { get; set; }
        public string updated { get; set; }
        public int average_stories_per_month { get; set; }
        public int ps { get; set; }
        public string feed_address { get; set; }
        public string feed_title { get; set; }
        public string favicon_fade { get; set; }
        public bool is_newsletter { get; set; }
        public int last_story_seconds_ago { get; set; }
        public string favicon_color { get; set; }
        public int stories_last_month { get; set; }
        public bool active { get; set; }
        public bool fetched_once { get; set; }
        public string favicon_text_color { get; set; }
        public bool subscribed { get; set; }
        public int num_subscribers { get; set; }
        public bool s3_page { get; set; }
        public int min_to_decay { get; set; }
        public bool search_indexed { get; set; }
    }

    public class _97265
    {
        public int subs { get; set; }
        public string favicon { get; set; }
        public string favicon_url { get; set; }
        public bool is_push { get; set; }
        public int feed_opens { get; set; }
        public int id { get; set; }
        public bool s3_icon { get; set; }
        public string feed_link { get; set; }
        public int updated_seconds_ago { get; set; }
        public bool favicon_fetching { get; set; }
        public int ng { get; set; }
        public string favicon_border { get; set; }
        public string last_story_date { get; set; }
        public int nt { get; set; }
        public bool not_yet_fetched { get; set; }
        public string updated { get; set; }
        public int average_stories_per_month { get; set; }
        public int ps { get; set; }
        public string feed_address { get; set; }
        public string feed_title { get; set; }
        public string favicon_fade { get; set; }
        public bool is_newsletter { get; set; }
        public int last_story_seconds_ago { get; set; }
        public string favicon_color { get; set; }
        public int stories_last_month { get; set; }
        public bool active { get; set; }
        public bool fetched_once { get; set; }
        public string favicon_text_color { get; set; }
        public bool subscribed { get; set; }
        public int num_subscribers { get; set; }
        public bool s3_page { get; set; }
        public int min_to_decay { get; set; }
        public bool search_indexed { get; set; }
    }

    public class Social_Services
    {
        public Facebook facebook { get; set; }
        public Twitter twitter { get; set; }
        public Gravatar gravatar { get; set; }
        public Appdotnet appdotnet { get; set; }
        public Upload upload { get; set; }
    }

    public class Facebook
    {
        public bool syncing { get; set; }
        public object facebook_picture_url { get; set; }
        public object facebook_uid { get; set; }
    }

    public class Twitter
    {
        public object twitter_username { get; set; }
        public bool syncing { get; set; }
        public object twitter_picture_url { get; set; }
        public object twitter_uid { get; set; }
    }

    public class Gravatar
    {
        public string gravatar_picture_url { get; set; }
    }

    public class Appdotnet
    {
        public bool syncing { get; set; }
        public object appdotnet_uid { get; set; }
        public object appdotnet_picture_url { get; set; }
    }

    public class Upload
    {
        public object upload_picture_url { get; set; }
    }

    public class Folder
    {
        public int[] Writers { get; set; }
        public object[] Blogs { get; set; }
        public int[] Cooking { get; set; }
        public int[] Tech { get; set; }
    }

    public class Starred_Counts
    {
        public int count { get; set; }
        public string feed_address { get; set; }
        public string tag { get; set; }
        public object feed_id { get; set; }
    }

    public class Social_Feeds
    {
        public string username { get; set; }
        public int ps { get; set; }
        public int user_id { get; set; }
        public int subscription_user_id { get; set; }
        public string feed_link { get; set; }
        public string feed_address { get; set; }
        public int feed_opens { get; set; }
        public int num_subscribers { get; set; }
        public int shared_stories_count { get; set; }
        public bool? _private { get; set; }
        public string feed_title { get; set; }
        public bool? _protected { get; set; }
        public string location { get; set; }
        public string photo_url { get; set; }
        public string large_photo_url { get; set; }
        public bool is_trained { get; set; }
        public int ng { get; set; }
        public int nt { get; set; }
        public string id { get; set; }
        public string page_url { get; set; }
    }


}
