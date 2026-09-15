using System.Collections.Generic;
using System.Text.Json.Serialization;

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
        [JsonPropertyName("authors")]
        public Authors Authors { get; set; }

        //[JsonPropertyName("feeds")]
        //public Feeds Feeds { get; set; }

        [JsonPropertyName("titles")]
        public Titles Titles { get; set; }

        [JsonPropertyName("tags")]
        public Tags Tags { get; set; }
    }

    public class Intelligence
    {
        [JsonPropertyName("feed")]
        public int Feed { get; set; }

        [JsonPropertyName("tags")]
        public int Tags { get; set; }

        [JsonPropertyName("author")]
        public int Author { get; set; }

        [JsonPropertyName("title")]
        public int Title { get; set; }
    }

    public class Story
    {
        [JsonPropertyName("friend_shares")]
        public object[] FriendShares { get; set; }

        [JsonPropertyName("story_authors")]
        public string Authors { get; set; }

        [JsonPropertyName("intelligence")]
        public Intelligence Intelligence { get; set; }

        [JsonPropertyName("story_permalink")]
        public string Permalink { get; set; }

        [JsonPropertyName("reply_count")]
        public int ReplyCount { get; set; }

        [JsonPropertyName("comment_user_ids")]
        public object[] CommentUserIds { get; set; }

        [JsonPropertyName("story_timestamp")]
        public string Timestamp { get; set; }

        [JsonPropertyName("share_user_ids")]
        public int[] ShareUserIds { get; set; }

        [JsonPropertyName("story_hash")]
        public string Hash { get; set; }

        [JsonPropertyName("id")]
        public string Id { get; set; }

        [JsonPropertyName("comment_count")]
        public int? CommentCount { get; set; }

        [JsonPropertyName("score")]
        public int Score { get; set; }

        [JsonPropertyName("story_tags")]
        public string[] Tags { get; set; }

        [JsonPropertyName("share_count")]
        public int? ShareCount { get; set; }

        [JsonPropertyName("friend_comments")]
        public object[] FriendComments { get; set; }

        [JsonPropertyName("story_date")]
        public string Date { get; set; }

        [JsonPropertyName("short_parsed_date")]
        public string ShortParsedDate { get; set; }

        [JsonPropertyName("guid_hash")]
        public string GuidHash { get; set; }

        [JsonPropertyName("image_urls")]
        public string[] ImageUrls { get; set; }

        [JsonPropertyName("story_feed_id")]
        public int FeedId { get; set; }

        [JsonPropertyName("long_parsed_date")]
        public string LongParsedDate { get; set; }

        [JsonPropertyName("public_comments")]
        public object[] PublicComments { get; set; }

        [JsonPropertyName("read_status")]
        public int ReadStatus { get; set; }

        [JsonPropertyName("has_modifications")]
        public bool HasModifications { get; set; }

        [JsonPropertyName("story_title")]
        public string Title { get; set; }

        [JsonPropertyName("story_content")]
        public string Content { get; set; }

        [JsonPropertyName("shared_by_friends")]
        public object[] SharedByFriends { get; set; }

        [JsonPropertyName("share_count_public")]
        public int? ShareCountPublic { get; set; }

        [JsonPropertyName("friend_user_ids")]
        public object[] FriendUserIds { get; set; }

        [JsonPropertyName("public_user_ids")]
        public int[] PublicUserIds { get; set; }

        [JsonPropertyName("share_count_friends")]
        public int? ShareCountFriends { get; set; }

        [JsonPropertyName("shared_by_public")]
        public int[] SharedByPublic { get; set; }

        [JsonPropertyName("commented_by_public")]
        public object[] CommentedByPublic { get; set; }

        [JsonPropertyName("commented_by_friends")]
        public object[] CommentedByFriends { get; set; }
    }

    public class StoriesResponse
    {
        [JsonPropertyName("updated")]
        public string Updated { get; set; }

        [JsonPropertyName("authenticated")]
        public bool Authenticated { get; set; }

        [JsonPropertyName("feed_tags")]
        public object[][] FeedTags { get; set; }

        [JsonPropertyName("feed_id")]
        public int FeedId { get; set; }

        [JsonPropertyName("hidden_stories_removed")]
        public int HiddenStoriesRemoved { get; set; }

        [JsonPropertyName("classifiers")]
        public Classifiers Classifiers { get; set; }

        [JsonPropertyName("elapsed_time")]
        public double ElapsedTime { get; set; }

        [JsonPropertyName("user_search")]
        public object UserSearch { get; set; }

        [JsonPropertyName("stories")]
        public List<Story> Stories { get; set; }

        [JsonPropertyName("result")]
        public string Result { get; set; }

        [JsonPropertyName("user_id")]
        public int UserId { get; set; }

        [JsonPropertyName("message")]
        public object Message { get; set; }

        [JsonPropertyName("feed_authors")]
        public object[][] FeedAuthors { get; set; }

        [JsonPropertyName("user_profiles")]
        public List<UserProfile> UserProfiles { get; set; }
    }

}
