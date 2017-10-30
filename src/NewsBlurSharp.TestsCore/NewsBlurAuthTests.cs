using System.Threading;
using System.Threading.Tasks;
using NewsBlurSharp.Model.Response;
using Xunit;

namespace NewsBlurSharp.Tests
{
    public class NewsBlurAuthTests
    {
        private readonly NewsBlurClient _subject;

        public NewsBlurAuthTests()
        {
            _subject = new NewsBlurClient();
        }

        [Fact]
        public async Task SignInWithCorrectCredentials()
        {
            LoginResponse response = await _subject.LoginAsync("spectrotest", "1ICwn*3^o4g5", CancellationToken.None);
            _subject.SetCookieSessionId(response.AuthCookieToken);

            Assert.NotNull(response);
        }

        [Fact]
        public async Task SignOutUser()
        {
            await _subject.LogoutAsync();
        }
    }
}
