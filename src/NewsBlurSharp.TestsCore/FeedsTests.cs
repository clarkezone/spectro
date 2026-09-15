using System.Net.Http;
using System.Linq;
using System.Threading.Tasks;
using System.Text.Json;
using Xunit;

namespace NewsBlurSharp.Tests
{
    public class FeedsTests
    {
        [Theory]
        [InlineData("\"active\":false,", false, true)]
        [InlineData("\"active\":true,", true, false)]
        [InlineData("", null, true)]
        [InlineData("", null, false)]
        [InlineData("\"active\":null,", null, true)]
        public async Task FeedContractDistinguishesExplicitInactiveFromMissingActive(
            string activeProperty, bool? expectedActive, bool subscribed)
        {
            var handler = new RecordingHttpMessageHandler(_ =>
                RecordingHttpMessageHandler.Json(
                    $$"""
                    {
                      "authenticated": true,
                      "folders": [],
                      "feeds": {"10310620": {
                        "id": 10310620,
                        "feed_title": "Daily Brief",
                        "feed_address": "https://example.test/daily",
                        {{activeProperty}}
                        "subscribed": {{subscribed.ToString().ToLowerInvariant()}},
                        "future_server_field": {"value": 1}
                      }
                      }
                    }
                    """));

            var response = await new NewsBlurClient(handler).GetFeedsAsync(isFlatStructure: false);

            var feed = Assert.Single(response.Feeds);
            Assert.Equal(expectedActive, feed.Active);
            Assert.Equal(subscribed, feed.Subscribed);
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task ServerFlatOptionOmitsHierarchyWhileNonflatPreservesJsonElements(bool flat)
        {
            var handler = new RecordingHttpMessageHandler(request =>
            {
                Assert.Equal(
                    $"https://newsblur.com/reader/feeds?include_favicons=true&flat={flat.ToString().ToLowerInvariant()}&update_counts=true",
                    request.RequestUri.ToString());
                return RecordingHttpMessageHandler.Json(flat
                    ? """{"authenticated":true,"feeds":{}}"""
                    : """{"authenticated":true,"feeds":{},"folders":[5771943,{"News":[7001666,6227898]},{"Science":[7633417,706322]},{"Empty":[]},{"Parent":[{"Child":[7001666]}]}]}""");
            });

            var response = await new NewsBlurClient(handler).GetFeedsAsync(
                includeFavIcons: true, isFlatStructure: flat, updateCounts: true);

            if (flat)
            {
                Assert.Null(response.Folders);
                return;
            }

            Assert.Equal(5, response.Folders.Length);
            Assert.Equal(JsonValueKind.Number, response.Folders[0].ValueKind);
            Assert.Equal(5771943, response.Folders[0].GetInt32());
            Assert.Equal([7001666, 6227898],
                response.Folders[1].GetProperty("News").EnumerateArray().Select(item => item.GetInt32()));
            Assert.Equal([7633417, 706322],
                response.Folders[2].GetProperty("Science").EnumerateArray().Select(item => item.GetInt32()));
            Assert.Equal(0, response.Folders[3].GetProperty("Empty").GetArrayLength());
            Assert.Equal(7001666, response.Folders[4].GetProperty("Parent")[0].GetProperty("Child")[0].GetInt32());
        }

        [Fact]
        public async Task GetFeedsParsesFeedDictionaryFoldersAndEncodesOptions()
        {
            var handler = new RecordingHttpMessageHandler(request =>
            {
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.Equal(
                    "https://newsblur.com/reader/feeds?include_favicons=true&flat=false&update_counts=true",
                    request.RequestUri.ToString());
                return RecordingHttpMessageHandler.Json(
                    "{\"authenticated\":true,\"folders\":[{\"Tech\":[7]}],\"feeds\":{\"7\":{\"id\":7,\"feed_title\":\"Example\",\"feed_address\":\"https://example.test/feed\",\"active\":true}}}");
            });

            var feeds = await new NewsBlurClient(handler).GetFeedsAsync(
                includeFavIcons: true,
                isFlatStructure: false,
                updateCounts: true);

            var feed = Assert.Single(feeds.Feeds);
            Assert.Equal(7, feed.Id);
            Assert.Equal("Example", feed.FeedTitle);
            Assert.Equal("Tech", Assert.Single(feeds.Folders).EnumerateObject().First().Name);
        }
    }
}
