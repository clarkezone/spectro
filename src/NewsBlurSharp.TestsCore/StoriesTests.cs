using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NewsBlurSharp.Tests
{
    public class StoriesTests
    {
        [Fact]
        public async Task GetFeedStoriesBuildsUnreadRequestAndParsesStableHash()
        {
            var handler = new RecordingHttpMessageHandler(request =>
            {
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.Equal(
                    "https://newsblur.com/reader/feed/7?page=2&order=oldest&read_filter=unread&include_hidden=true",
                    request.RequestUri.ToString());
                return StoryResponse();
            });

            var response = await new NewsBlurClient(handler).GetStoriesAsync(
                7,
                pageIndex: 2,
                invertOrder: true,
                filterReadStories: true,
                includeHiddenStories: true);

            var story = Assert.Single(response.Stories);
            Assert.Equal("7:abc", story.Hash);
            Assert.Equal("Hello", story.Title);
        }

        [Fact]
        public async Task RiverAndStarredStoriesUseSupportedEndpoints()
        {
            var expectedPath = "https://newsblur.com/reader/river_stories?read_filter=unread";
            var handler = new RecordingHttpMessageHandler(request =>
            {
                Assert.Equal(expectedPath, request.RequestUri.ToString());
                return StoryResponse();
            });
            var subject = new NewsBlurClient(handler);

            await subject.GetRiverStoriesAsync(filterReadStories: true);
            expectedPath = "https://newsblur.com/reader/starred_stories?page=3";
            await subject.GetStarredStoriesAsync(pageIndex: 3);
        }

        [Fact]
        public async Task UnreadHashesParseByFeed()
        {
            var handler = new RecordingHttpMessageHandler(request =>
            {
                Assert.Equal(
                    "https://newsblur.com/reader/unread_story_hashes",
                    request.RequestUri.ToString());
                return RecordingHttpMessageHandler.Json(
                    "{\"authenticated\":true,\"unread_feed_story_hashes\":{\"7\":[\"7:abc\",\"7:def\"]}}");
            });

            var response = await new NewsBlurClient(handler).GetUnreadStoryHashesAsync();

            Assert.Equal(new[] { "7:abc", "7:def" }, response.UnreadStoryHashes["7"]);
        }

        [Fact]
        public async Task StoryMutationsUseExactEndpointsAndFormEncoding()
        {
            var expectedPath = "reader/mark_story_hashes_as_read";
            var expectedBody = "story_hash=7%3Aabc&story_hash=8%3Adef";
            RecordingHttpMessageHandler handler = null;
            handler = new RecordingHttpMessageHandler(request =>
            {
                Assert.Equal(
                    "https://newsblur.com/" + expectedPath,
                    request.RequestUri.ToString());
                Assert.Equal(expectedBody, handler.LastRequestBody);
                return RecordingHttpMessageHandler.Json("{}");
            });
            var subject = new NewsBlurClient(handler);

            await subject.MarkStoriesReadAsync(
                new List<string> { "7:abc", "8:def" },
                CancellationToken.None);

            expectedPath = "reader/mark_story_hash_as_unread";
            expectedBody = "story_hash=7%3Aabc";
            await subject.MarkStoryUnreadAsync("7:abc", CancellationToken.None);

            expectedPath = "reader/mark_story_hash_as_starred";
            await subject.StarStoryAsync("7:abc");

            expectedPath = "reader/mark_story_hash_as_unstarred";
            await subject.UnstarStoryAsync("7:abc");
        }

        [Fact]
        public async Task MarkFeedReadPostsFeedId()
        {
            RecordingHttpMessageHandler handler = null;
            handler = new RecordingHttpMessageHandler(request =>
            {
                Assert.Equal(
                    "https://newsblur.com/reader/mark_feed_as_read",
                    request.RequestUri.ToString());
                Assert.Equal("feed_id=7", handler.LastRequestBody);
                return RecordingHttpMessageHandler.Json("{}");
            });

            await new NewsBlurClient(handler).MarkFeedReadAsync(7);
        }

        private static HttpResponseMessage StoryResponse()
        {
            return RecordingHttpMessageHandler.Json(
                "{\"authenticated\":true,\"feed_id\":7,\"stories\":[{\"story_hash\":\"7:abc\",\"id\":\"legacy-id\",\"story_feed_id\":7,\"story_title\":\"Hello\",\"read_status\":0,\"image_urls\":[]}]}");
        }
    }
}
