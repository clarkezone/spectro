namespace NewsBlurSharp.Model.Response.ProfileResponse
{

    public class Rootobject
    {
        public Services services { get; set; }
        public bool authenticated { get; set; }
        public int user_id { get; set; }
        public string result { get; set; }
        public User_Profile user_profile { get; set; }
    }

    public class Services
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
        public string facebook_picture_url { get; set; }
        public string facebook_uid { get; set; }
    }

    public class Twitter
    {
        public string twitter_username { get; set; }
        public bool syncing { get; set; }
        public string twitter_picture_url { get; set; }
        public string twitter_uid { get; set; }
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

    public class User_Profile
    {
        public string website { get; set; }
        public int[] following_user_ids { get; set; }
        public int following_count { get; set; }
        public int shared_stories_count { get; set; }
        public bool _private { get; set; }
        public string large_photo_url { get; set; }
        public object custom_bgcolor { get; set; }
        public string id { get; set; }
        public string feed_address { get; set; }
        public int user_id { get; set; }
        public string feed_link { get; set; }
        public int[] follower_user_ids { get; set; }
        public string location { get; set; }
        public object popular_publishers { get; set; }
        public int follower_count { get; set; }
        public string username { get; set; }
        public string bio { get; set; }
        public int average_stories_per_month { get; set; }
        public object bb_permalink_direct { get; set; }
        public string feed_title { get; set; }
        public string photo_service { get; set; }
        public int stories_last_month { get; set; }
        public string photo_url { get; set; }
        public object custom_css { get; set; }
        public int num_subscribers { get; set; }
        public bool _protected { get; set; }
    }

}
