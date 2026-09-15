using System.Net.Http;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace NewsBlurSharp.Tests
{
    public class FeedsTests
    {
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
