using System.Threading.Tasks;
using NewsBlurSharp.Model.Response;
using Xunit;

namespace NewsBlurSharp.Tests
{
    public class FeedsTests
    {
        private readonly NewsBlurClient _subject;

        public FeedsTests()
        {
            _subject = new NewsBlurClient();
            _subject.SetCookieSessionId("qv9sseho0dbv4izpfy9jw8zrl4bdjpzh");
        }

        [Fact]
        public async Task GetFeedsPopulatesFeeds()
        {
            NewsFeedResponse feeds = await _subject.GetFeedsAsync();

            Assert.NotNull(feeds);
        }
    }
}

