using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using NewsBlurSharp.Model;
using Xunit;

namespace NewsBlurSharp.Tests
{
    public class TransportFailureTests
    {
        [Fact]
        public async Task MalformedJsonThrowsTypedFailure()
        {
            var subject = new NewsBlurClient(new RecordingHttpMessageHandler(
                _ => RecordingHttpMessageHandler.Json("{not-json")));

            var exception = await Assert.ThrowsAsync<NewsBlurMalformedResponseException>(
                () => subject.GetFeedsAsync());

            Assert.Equal(NewsBlurFailureKind.MalformedResponse, exception.Kind);
        }

        [Theory]
        [InlineData(HttpStatusCode.Unauthorized)]
        [InlineData(HttpStatusCode.Forbidden)]
        public async Task AuthenticationStatusThrowsTypedFailure(HttpStatusCode statusCode)
        {
            var subject = new NewsBlurClient(new RecordingHttpMessageHandler(
                _ => RecordingHttpMessageHandler.Json("{}", statusCode)));

            var exception = await Assert.ThrowsAsync<NewsBlurAuthenticationException>(
                () => subject.GetFeedsAsync());

            Assert.Equal(statusCode, exception.StatusCode);
        }

        [Theory]
        [InlineData(HttpStatusCode.RequestTimeout)]
        [InlineData(HttpStatusCode.ServiceUnavailable)]
        public async Task RetryableStatusThrowsTransientFailure(HttpStatusCode statusCode)
        {
            var subject = new NewsBlurClient(new RecordingHttpMessageHandler(
                _ => RecordingHttpMessageHandler.Json("{}", statusCode)));

            var exception = await Assert.ThrowsAsync<NewsBlurTransientException>(
                () => subject.GetFeedsAsync());

            Assert.Equal(statusCode, exception.StatusCode);
        }

        [Theory]
        [InlineData(HttpStatusCode.TooManyRequests, "120", 120)]
        [InlineData(HttpStatusCode.TooManyRequests, "Tue, 15 Sep 2026 21:25:00 GMT", 180)]
        [InlineData(HttpStatusCode.TooManyRequests, null, null)]
        [InlineData(HttpStatusCode.TooManyRequests, "not-a-retry-date", null)]
        [InlineData(HttpStatusCode.TooManyRequests, "-1", null)]
        [InlineData(HttpStatusCode.ServiceUnavailable, "120", 120)]
        [InlineData(HttpStatusCode.ServiceUnavailable, "Tue, 15 Sep 2026 21:25:00 GMT", 180)]
        [InlineData(HttpStatusCode.ServiceUnavailable, "not-a-retry-date", null)]
        public async Task RetryAfterPreservesTypedFailureAndDeadline(
            HttpStatusCode statusCode,
            string retryAfter,
            int? retrySeconds)
        {
            var now = new DateTimeOffset(2026, 9, 15, 21, 22, 0, TimeSpan.Zero);
            var subject = new NewsBlurClient(new RecordingHttpMessageHandler(_ =>
            {
                var response = RecordingHttpMessageHandler.Json("not-json", statusCode);
                if (retryAfter != null)
                    response.Headers.TryAddWithoutValidation("Retry-After", retryAfter);
                return response;
            }), timeProvider: new FixedTimeProvider(now));

            var exception = await Assert.ThrowsAnyAsync<NewsBlurException>(
                () => subject.GetFeedsAsync());

            if (statusCode == HttpStatusCode.TooManyRequests)
            {
                Assert.IsType<NewsBlurRateLimitedException>(exception);
                Assert.Equal(NewsBlurFailureKind.RateLimited, exception.Kind);
            }
            else
            {
                Assert.IsType<NewsBlurTransientException>(exception);
                Assert.Equal(NewsBlurFailureKind.Transient, exception.Kind);
            }
            Assert.Equal(statusCode, exception.StatusCode);
            Assert.Equal(retrySeconds.HasValue ? now.AddSeconds(retrySeconds.Value) : (DateTimeOffset?)null,
                exception.RetryAt);
        }

        [Theory]
        [InlineData(HttpStatusCode.TooManyRequests, NewsBlurFailureKind.RateLimited)]
        [InlineData(HttpStatusCode.ServiceUnavailable, NewsBlurFailureKind.Transient)]
        [InlineData(HttpStatusCode.Unauthorized, NewsBlurFailureKind.Authentication)]
        [InlineData(HttpStatusCode.Forbidden, NewsBlurFailureKind.Authentication)]
        [InlineData(HttpStatusCode.BadRequest, NewsBlurFailureKind.Http)]
        public async Task ErrorStatusIsHandledWithoutBufferingBody(
            HttpStatusCode statusCode, NewsBlurFailureKind expectedKind)
        {
            var content = new UnreadableContent();
            var subject = new NewsBlurClient(new RecordingHttpMessageHandler(
                _ => new HttpResponseMessage(statusCode) { Content = content }));

            var exception = await Assert.ThrowsAnyAsync<NewsBlurException>(
                () => subject.GetFeedsAsync());

            Assert.Equal(expectedKind, exception.Kind);
            Assert.Equal(statusCode, exception.StatusCode);
            Assert.False(content.ReadAttempted);
            Assert.True(content.WasDisposed);
        }

        [Theory]
        [InlineData(HttpStatusCode.TooManyRequests)]
        [InlineData(HttpStatusCode.ServiceUnavailable)]
        public async Task ThrottleDiagnosticsExcludeCredentialsCookiesAndResponseBody(HttpStatusCode statusCode)
        {
            const string username = "private-login-sentinel";
            const string password = "private-password-sentinel";
            const string cookie = "private-cookie-sentinel";
            const string responseBody = "private-response-sentinel";
            const string retryHeader = "private-header-sentinel";
            var logger = new RecordingLogger();
            var handler = new RecordingHttpMessageHandler(_ =>
            {
                var response = RecordingHttpMessageHandler.Json(responseBody, statusCode);
                response.Headers.TryAddWithoutValidation("Retry-After", retryHeader);
                response.Headers.TryAddWithoutValidation("Set-Cookie", "newsblur_sessionid=" + cookie);
                return response;
            });
            var subject = new NewsBlurClient(handler, logger);
            subject.SetCookieSessionId(cookie);

            var exception = await Assert.ThrowsAnyAsync<NewsBlurException>(
                () => subject.LoginAsync(username, password));

            Assert.Contains(username, handler.LastRequestBody);
            Assert.Contains(password, handler.LastRequestBody);
            Assert.Contains(cookie, string.Join(" ", handler.LastRequest.Headers.GetValues("Cookie")));
            Assert.NotEmpty(logger.Messages);
            var diagnostics = string.Join("\n", logger.Messages) + exception;
            foreach (var secret in new[] { username, password, cookie, responseBody, retryHeader })
                Assert.DoesNotContain(secret, diagnostics);
            Assert.Contains(((int)statusCode).ToString(), diagnostics);
        }

        [Fact]
        public async Task CallerCancellationPropagatesOperationCanceledException()
        {
            var handler = new RecordingHttpMessageHandler(async (_, token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                throw new InvalidOperationException();
            });
            var subject = new NewsBlurClient(
                handler,
                requestTimeout: TimeSpan.FromSeconds(5));
            using var cancellation = new CancellationTokenSource();
            cancellation.CancelAfter(TimeSpan.FromMilliseconds(20));

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => subject.GetFeedsAsync(cancellationToken: cancellation.Token));
        }

        [Fact]
        public async Task RequestTimeoutThrowsTypedFailure()
        {
            var handler = new RecordingHttpMessageHandler(async (_, token) =>
            {
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                throw new InvalidOperationException();
            });
            var subject = new NewsBlurClient(
                handler,
                requestTimeout: TimeSpan.FromMilliseconds(20));

            await Assert.ThrowsAsync<NewsBlurTimeoutException>(
                () => subject.GetFeedsAsync());
        }

        [Fact]
        public async Task NameResolutionFailureThrowsOfflineFailure()
        {
            var handler = new RecordingHttpMessageHandler(
                (_, _) => throw new HttpRequestException(
                    HttpRequestError.NameResolutionError,
                    "offline"));
            var subject = new NewsBlurClient(handler);

            var exception = await Assert.ThrowsAsync<NewsBlurOfflineException>(
                () => subject.GetFeedsAsync());

            Assert.Equal(NewsBlurFailureKind.Offline, exception.Kind);
        }

        private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
        {
            public override DateTimeOffset GetUtcNow() => now;
        }

        private sealed class UnreadableContent : HttpContent
        {
            public bool ReadAttempted { get; private set; }
            public bool WasDisposed { get; private set; }

            protected override Task SerializeToStreamAsync(Stream stream, TransportContext context)
            {
                ReadAttempted = true;
                throw new InvalidOperationException("An error response body must not be read.");
            }

            protected override bool TryComputeLength(out long length)
            {
                length = 0;
                return false;
            }

            protected override void Dispose(bool disposing)
            {
                WasDisposed = true;
                base.Dispose(disposing);
            }
        }
    }
}
