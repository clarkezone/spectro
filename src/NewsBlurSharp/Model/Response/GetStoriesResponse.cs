using System;
using System.Collections.Generic;
using System.Text;

namespace NewsBlurSharp.Model.GetStoriesResponse
{

    public class Rootobject
    {
        public string updated { get; set; }
        public bool authenticated { get; set; }
        public object[][] feed_tags { get; set; }
        public int feed_id { get; set; }
        public int hidden_stories_removed { get; set; }
        public Classifiers classifiers { get; set; }
        public float elapsed_time { get; set; }
        public object user_search { get; set; }
        public Story[] stories { get; set; }
        public string result { get; set; }
        public int user_id { get; set; }
        public object message { get; set; }
        public object[][] feed_authors { get; set; }
        public User_Profiles[] user_profiles { get; set; }
    }

    public class Classifiers
    {
        public Authors authors { get; set; }
        public Feeds feeds { get; set; }
        public Titles titles { get; set; }
        public Tags tags { get; set; }
    }

    public class Authors
    {
    }

    public class Feeds
    {
    }

    public class Titles
    {
    }

    public class Tags
    {
    }

    public class Story
    {
        public object[] friend_shares { get; set; }
        public string story_authors { get; set; }
        public Intelligence intelligence { get; set; }
        public object[] shared_by_friends { get; set; }
        public string story_permalink { get; set; }
        public int reply_count { get; set; }
        public object[] comment_user_ids { get; set; }
        public string story_timestamp { get; set; }
        public int[] share_user_ids { get; set; }
        public string story_hash { get; set; }
        public string id { get; set; }
        public Nullable<int> comment_count { get; set; }
        public int score { get; set; }
        public string guid_hash { get; set; }
        public Nullable<int> share_count { get; set; }
        public object[] friend_comments { get; set; }
        public string story_date { get; set; }
        public Nullable<int> share_count_public { get; set; }
        public object[] friend_user_ids { get; set; }
        public int[] public_user_ids { get; set; }
        public string short_parsed_date { get; set; }
        public string[] story_tags { get; set; }
        public int share_count_friends { get; set; }
        public string[] image_urls { get; set; }
        public int story_feed_id { get; set; }
        public string long_parsed_date { get; set; }
        public object[] public_comments { get; set; }
        public int read_status { get; set; }
        public int[] shared_by_public { get; set; }
        public object[] commented_by_public { get; set; }
        public bool has_modifications { get; set; }
        public string story_title { get; set; }
        public string story_content { get; set; }
        public object[] commented_by_friends { get; set; }
    }

    public class Intelligence
    {
        public int feed { get; set; }
        public int tags { get; set; }
        public int author { get; set; }
        public int title { get; set; }
    }

    public class User_Profiles
    {
        public string username { get; set; }
        public string feed_address { get; set; }
        public int user_id { get; set; }
        public string feed_link { get; set; }
        public int num_subscribers { get; set; }
        public string feed_title { get; set; }
        public bool _private { get; set; }
        public bool _protected { get; set; }
        public string location { get; set; }
        public string large_photo_url { get; set; }
        public string id { get; set; }
        public string photo_url { get; set; }
    }

}
