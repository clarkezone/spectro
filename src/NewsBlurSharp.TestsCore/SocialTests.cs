using System;
using System.Threading.Tasks;
using Xunit;

namespace NewsBlurSharp.Tests
{
    public class SocialTests
    {
        private readonly NewsBlurClient _subject;

        public SocialTests()
        {
            _subject = new NewsBlurClient();
        }

        [Fact]
        public async Task GetUserProfile()
        {
            var profile = await _subject.GetUserProfileAsync(455319);

            Assert.NotNull(profile);
        }
    }
}
