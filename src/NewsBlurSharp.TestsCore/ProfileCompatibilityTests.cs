using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace NewsBlurSharp.Tests
{
    public class ProfileCompatibilityTests
    {
        [Fact]
        public async Task GetUserProfileRemainsAvailableForExistingCaller()
        {
            var handler = new RecordingHttpMessageHandler(request =>
            {
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.Equal(
                    "https://newsblur.com/social/load_user_profile",
                    request.RequestUri.ToString());
                return RecordingHttpMessageHandler.Json(
                    "{\"authenticated\":true,\"user_profile\":{\"user_id\":42,\"username\":\"reader\",\"photo_url\":\"https://example.test/avatar.png\"}}");
            });

            var response = await new NewsBlurClient(handler).GetUserProfileAsync();

            Assert.Equal(42, response.UserProfile.UserId);
            Assert.Equal("reader", response.UserProfile.Username);
        }
    }
}
