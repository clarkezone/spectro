using System.Threading.Tasks;
using Xunit;

namespace NewsBlurSharp.Tests
{
    public class StoriesTests
    {
        private readonly NewsBlurClient _subject;

        public StoriesTests()
        {
            _subject = new NewsBlurClient();
            _subject.SetCookieSessionId("qv9sseho0dbv4izpfy9jw8zrl4bdjpzh");
        }

        [Fact]
        public async Task GetStories()
        {
            var stories = await _subject.GetStoriesAsync(19354);

            Assert.NotNull(stories);
        }
    }
}
