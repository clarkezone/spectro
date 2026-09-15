using System;
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
        [InlineData(HttpStatusCode.TooManyRequests)]
        [InlineData(HttpStatusCode.ServiceUnavailable)]
        public async Task RetryableStatusThrowsTransientFailure(HttpStatusCode statusCode)
        {
            var subject = new NewsBlurClient(new RecordingHttpMessageHandler(
                _ => RecordingHttpMessageHandler.Json("{}", statusCode)));

            var exception = await Assert.ThrowsAsync<NewsBlurTransientException>(
                () => subject.GetFeedsAsync());

            Assert.Equal(statusCode, exception.StatusCode);
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
    }
}
