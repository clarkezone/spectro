using System;
using System.Threading;
using System.Threading.Tasks;
using NewsBlurSharp.Model.Response;
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
        public async Task GetUserPublicProfile()
        {
            LoginResponse response = await _subject.LoginAsync("spectrotest", "1ICwn*3^o4g5", CancellationToken.None);
            var profile = await _subject.GetUserPublicProfileAsync(response.UserId);

            Assert.NotNull(profile);
        }

        [Fact]
        public async Task GetUserProfile()
        {
            LoginResponse response = await _subject.LoginAsync("spectrotest", "1ICwn*3^o4g5", CancellationToken.None);
            _subject.SetCookieSessionId(response.AuthCookieToken);
            var profile = await _subject.GetUserProfileAsync();

            Assert.NotNull(profile);
        }
    }
}
