using System.Threading;
using System.Threading.Tasks;
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
            var response = await _subject.LoginAsync("spectrotest", "1ICwn*3^o4g5", CancellationToken.None);

            Assert.NotNull(response);
        }
    }
}
