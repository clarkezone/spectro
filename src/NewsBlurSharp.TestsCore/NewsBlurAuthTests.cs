using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace NewsBlurSharp.Tests
{
    public class NewsBlurAuthTests
    {
        [Fact]
        public async Task LoginPostsEncodedCredentialsReturnsSessionAndDoesNotLogSecrets()
        {
            var logger = new RecordingLogger();
            var handler = new RecordingHttpMessageHandler(request =>
            {
                Assert.Equal(HttpMethod.Post, request.Method);
                Assert.Equal("https://newsblur.com/api/login", request.RequestUri.ToString());

                var response = RecordingHttpMessageHandler.Json(
                    "{\"authenticated\":true,\"user_id\":\"42\"}");
                response.Headers.TryAddWithoutValidation(
                    "Set-Cookie",
                    "newsblur_sessionid=test-session; Path=/; Secure; HttpOnly");
                return response;
            });

            var subject = new NewsBlurClient(handler, logger);
            var result = await subject.LoginAsync(
                "reader+mobile",
                "p&ss=word",
                CancellationToken.None);

            Assert.True(result.IsSuccess);
            Assert.Equal(42, result.UserId);
            Assert.Equal("test-session", result.AuthCookieToken);
            Assert.Equal(
                "username=reader%2Bmobile&password=p%26ss%3Dword",
                handler.LastRequestBody);

            var log = string.Join(Environment.NewLine, logger.Messages);
            Assert.DoesNotContain("reader+mobile", log);
            Assert.DoesNotContain("p&ss=word", log);
            Assert.DoesNotContain("test-session", log);
            Assert.DoesNotContain(handler.LastRequestBody, log);
        }

        [Fact]
        public async Task LoginCanReturnRejectedCredentialsWithoutThrowing()
        {
            var handler = new RecordingHttpMessageHandler(
                _ => RecordingHttpMessageHandler.Json(
                    "{\"authenticated\":false,\"user_id\":0,\"result\":\"invalid login\"}"));

            var result = await new NewsBlurClient(handler).LoginAsync("reader", "wrong");

            Assert.False(result.IsSuccess);
        }

        [Fact]
        public async Task LogoutPostsAndSendsConfiguredCookie()
        {
            var handler = new RecordingHttpMessageHandler(request =>
            {
                Assert.Equal("https://newsblur.com/api/logout", request.RequestUri.ToString());
                Assert.Contains(
                    request.Headers.GetValues("Cookie"),
                    value => value == "newsblur_sessionid=test-session");
                return RecordingHttpMessageHandler.Json("{}");
            });
            var subject = new NewsBlurClient(handler);
            subject.SetCookieSessionId("test-session");

            await subject.LogoutAsync();
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task LoginRejectsMissingUsername(string username)
        {
            var subject = new NewsBlurClient(new RecordingHttpMessageHandler(
                _ => throw new InvalidOperationException("No request expected.")));

            await Assert.ThrowsAsync<ArgumentNullException>(
                () => subject.LoginAsync(username, "password"));
        }
    }
}
