using System.Collections.Generic;
using Newtonsoft.Json;

namespace NewsBlurSharp.Model.Response
{

    public class Authors
    {
    }

    public class Titles
    {
    }

    public class Tags
    {
    }

    public class Classifiers
    {
        [JsonProperty("authors")]
        public Authors Authors { get; set; }

        [JsonProperty("feeds")]
        public Feeds Feeds { get; set; }

        [JsonProperty("titles")]
        public Titles Titles { get; set; }

        [JsonProperty("tags")]
        public Tags Tags { get; set; }
    }

    public class Intelligence
    {
        [JsonProperty("feed")]
        public int Feed { get; set; }

        [JsonProperty("tags")]
        public int Tags { get; set; }

        [JsonProperty("author")]
        public int Author { get; set; }

        [JsonProperty("title")]
        public int Title { get; set; }
    }

    public class Story
    {
        [JsonProperty("friend_shares")]
        public object[] FriendShares { get; set; }

        [JsonProperty("story_authors")]
        public string Authors { get; set; }

        [JsonProperty("intelligence")]
        public Intelligence Intelligence { get; set; }

        [JsonProperty("story_permalink")]
        public string Permalink { get; set; }

        [JsonProperty("reply_count")]
        public int ReplyCount { get; set; }

        [JsonProperty("comment_user_ids")]
        public object[] CommentUserIds { get; set; }

        [JsonProperty("story_timestamp")]
        public string Timestamp { get; set; }

        [JsonProperty("share_user_ids")]
        public int[] ShareUserIds { get; set; }

        [JsonProperty("story_hash")]
        public string Hash { get; set; }

        [JsonProperty("id")]
        public string Id { get; set; }

        [JsonProperty("comment_count")]
        public int? CommentCount { get; set; }

        [JsonProperty("score")]
        public int Score { get; set; }

        [JsonProperty("story_tags")]
        public string[] Tags { get; set; }

        [JsonProperty("share_count")]
        public int? ShareCount { get; set; }

        [JsonProperty("friend_comments")]
        public object[] FriendComments { get; set; }

        [JsonProperty("story_date")]
        public string Date { get; set; }

        [JsonProperty("short_parsed_date")]
        public string ShortParsedDate { get; set; }

        [JsonProperty("guid_hash")]
        public string GuidHash { get; set; }

        [JsonProperty("image_urls")]
        public string[] ImageUrls { get; set; }

        [JsonProperty("story_feed_id")]
        public int FeedId { get; set; }

        [JsonProperty("long_parsed_date")]
        public string LongParsedDate { get; set; }

        [JsonProperty("public_comments")]
        public object[] PublicComments { get; set; }

        [JsonProperty("read_status")]
        public int ReadStatus { get; set; }

        [JsonProperty("has_modifications")]
        public bool HasModifications { get; set; }

        [JsonProperty("story_title")]
        public string Title { get; set; }

        [JsonProperty("story_content")]
        public string Content { get; set; }

        [JsonProperty("shared_by_friends")]
        public object[] SharedByFriends { get; set; }

        [JsonProperty("share_count_public")]
        public int? ShareCountPublic { get; set; }

        [JsonProperty("friend_user_ids")]
        public object[] FriendUserIds { get; set; }

        [JsonProperty("public_user_ids")]
        public int[] PublicUserIds { get; set; }

        [JsonProperty("share_count_friends")]
        public int? ShareCountFriends { get; set; }

        [JsonProperty("shared_by_public")]
        public int[] SharedByPublic { get; set; }

        [JsonProperty("commented_by_public")]
        public object[] CommentedByPublic { get; set; }

        [JsonProperty("commented_by_friends")]
        public object[] CommentedByFriends { get; set; }
    }

    public class StoriesResponse
    {
        [JsonProperty("updated")]
        public string Updated { get; set; }

        [JsonProperty("authenticated")]
        public bool Authenticated { get; set; }

        [JsonProperty("feed_tags")]
        public object[][] FeedTags { get; set; }

        [JsonProperty("feed_id")]
        public int FeedId { get; set; }

        [JsonProperty("hidden_stories_removed")]
        public int HiddenStoriesRemoved { get; set; }

        [JsonProperty("classifiers")]
        public Classifiers Classifiers { get; set; }

        [JsonProperty("elapsed_time")]
        public double ElapsedTime { get; set; }

        [JsonProperty("user_search")]
        public object UserSearch { get; set; }

        [JsonProperty("stories")]
        public List<Story> Stories { get; set; }

        [JsonProperty("result")]
        public string Result { get; set; }

        [JsonProperty("user_id")]
        public int UserId { get; set; }

        [JsonProperty("message")]
        public object Message { get; set; }

        [JsonProperty("feed_authors")]
        public object[][] FeedAuthors { get; set; }

        [JsonProperty("user_profiles")]
        public List<UserProfile> UserProfiles { get; set; }
    }

}
