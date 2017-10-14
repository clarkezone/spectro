using System;
using System.Collections.Generic;
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

        [Fact]
        public async Task MarkStoryAsUnread()
        {
            String storyHash = "19354:57abe7";

            var response = await _subject.MarkStoryUnreadAsync(storyHash);
            Assert.NotNull(response);
            Console.WriteLine(response.ToString());
        }

        [Fact]
        public async Task MarkStoryAsRead()
        {
            List<String> hashes = new List<string>();
            hashes.Add("19354:57abe7");

            var response = await _subject.MarkStoriesReadAsync(hashes);
            Assert.NotNull(response);
            Console.WriteLine(response.ToString());
        }
    }
}
