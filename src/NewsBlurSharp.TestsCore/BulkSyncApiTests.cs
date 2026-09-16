using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using NewsBlurSharp.Model;
using NewsBlurSharp.Model.Response;
using Xunit;

namespace NewsBlurSharp.Tests
{
    public class BulkSyncApiTests
    {
        [Theory]
        [InlineData(false, "all")]
        [InlineData(true, "unread")]
        public async Task InventoryUsesExplicitFeedScopeAndPreservesFractionalTimestamps(
            bool unreadOnly,
            string readFilter)
        {
            var handler = new RecordingHttpMessageHandler(request =>
            {
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.Null(request.Content);
                Assert.Equal(
                    $"https://newsblur.com/reader/unread_story_hashes?read_filter={readFilter}&order=newest&include_timestamps=true&feed_id=7&feed_id=8",
                    request.RequestUri.AbsoluteUri);
                return RecordingHttpMessageHandler.Json(
                    "{\"authenticated\":true,\"result\":\"ok\",\"unread_feed_story_hashes\":{\"7\":[[\"7:abc\",1700000000.125],[\"7:def\",1700000001]],\"8\":[]}}");
            });

            var response = await new NewsBlurClient(handler).GetStoryHashInventoryAsync(unreadOnly, new[] { 7, 8 });

            Assert.Equal(2, response.UnreadStoryHashes.Count);
            Assert.Equal(2, response.UnreadStoryHashes["7"].Count);
            Assert.Equal("7:abc", response.UnreadStoryHashes["7"][0].Hash);
            Assert.Equal(1700000000.125, response.UnreadStoryHashes["7"][0].Timestamp);
            Assert.Equal(1700000001, response.UnreadStoryHashes["7"][1].Timestamp);
            Assert.Empty(response.UnreadStoryHashes["8"]);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task InventoryPreservesSeparateFiveHundredStoryFeedWindows(bool unreadOnly)
        {
            static string FeedWindow(int feedId) => string.Join(",",
                Enumerable.Range(0, 500).Select(index => $"[\"{feedId}:{index}\",1700000000.25]"));
            var handler = new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json(
                "{\"authenticated\":true,\"unread_feed_story_hashes\":{\"7\":[" + FeedWindow(7) + "],\"8\":[" + FeedWindow(8) + "]}}"));

            var response = await new NewsBlurClient(handler).GetStoryHashInventoryAsync(unreadOnly, new[] { 7, 8 });

            Assert.Equal(2, response.UnreadStoryHashes.Count);
            Assert.Equal(500, response.UnreadStoryHashes["7"].Count);
            Assert.Equal(500, response.UnreadStoryHashes["8"].Count);
            Assert.Equal("7:499", response.UnreadStoryHashes["7"][499].Hash);
            Assert.Equal("8:499", response.UnreadStoryHashes["8"][499].Hash);
        }

        [Fact]
        public async Task StarredInventoryUsesFlatTupleListAndAcceptsServerStringTimestamps()
        {
            var handler = new RecordingHttpMessageHandler(request =>
            {
                Assert.Equal(HttpMethod.Get, request.Method);
                Assert.Equal("https://newsblur.com/reader/starred_story_hashes?include_timestamps=true",
                    request.RequestUri.AbsoluteUri);
                return RecordingHttpMessageHandler.Json(
                    "{\"authenticated\":true,\"result\":\"ok\",\"starred_story_hashes\":[[\"7:abc\",\"1700000000\"],[\"8:def\",1700000000.875]]}");
            });

            var response = await new NewsBlurClient(handler).GetStarredStoryHashInventoryAsync();

            Assert.Equal(2, response.StarredStoryHashes.Count);
            Assert.Equal("7:abc", response.StarredStoryHashes[0].Hash);
            Assert.Equal(1700000000, response.StarredStoryHashes[0].Timestamp);
            Assert.Equal(1700000000.875, response.StarredStoryHashes[1].Timestamp);
        }

        [Theory]
        [InlineData("null")]
        [InlineData("[]")]
        [InlineData("{}")]
        [InlineData("\"7:abc\"")]
        [InlineData("[\"7:abc\"]")]
        [InlineData("[\"7:abc\",1,2]")]
        [InlineData("[7,1]")]
        [InlineData("[null,1]")]
        [InlineData("[\"\",1]")]
        [InlineData("[\" \",1]")]
        [InlineData("[\"7:abc\",null]")]
        [InlineData("[\"7:abc\",true]")]
        [InlineData("[\"7:abc\",{}]")]
        [InlineData("[\"7:abc\",[]]")]
        [InlineData("[\"7:abc\",\"not-a-time\"]")]
        [InlineData("[\"7:abc\",\"NaN\"]")]
        [InlineData("[\"7:abc\",\"Infinity\"]")]
        [InlineData("[\"7:abc\",1e999]")]
        public async Task BothInventoriesRejectMalformedTuples(string tuple)
        {
            var handler = new RecordingHttpMessageHandler(request => RecordingHttpMessageHandler.Json(
                request.RequestUri.AbsolutePath.EndsWith("/starred_story_hashes", StringComparison.Ordinal)
                    ? "{\"authenticated\":true,\"starred_story_hashes\":[" + tuple + "]}"
                    : "{\"authenticated\":true,\"unread_feed_story_hashes\":{\"7\":[" + tuple + "]}}"));
            var client = new NewsBlurClient(handler);

            await Assert.ThrowsAsync<NewsBlurMalformedResponseException>(
                () => client.GetStoryHashInventoryAsync(false, new[] { 7 }));
            await Assert.ThrowsAsync<NewsBlurMalformedResponseException>(
                () => client.GetStarredStoryHashInventoryAsync());
        }

        [Theory]
        [InlineData("{\"authenticated\":true}")]
        [InlineData("{\"authenticated\":true,\"unread_feed_story_hashes\":null}")]
        [InlineData("{\"authenticated\":true,\"unread_feed_story_hashes\":[]}")]
        [InlineData("{\"authenticated\":true,\"unread_feed_story_hashes\":{\"7\":null}}")]
        public async Task InventoryRequiresFeedScopedResponseProperty(string json)
        {
            var client = new NewsBlurClient(new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json(json)));
            await Assert.ThrowsAsync<NewsBlurMalformedResponseException>(
                () => client.GetStoryHashInventoryAsync(false, new[] { 7 }));
        }

        [Theory]
        [InlineData("{\"authenticated\":true}")]
        [InlineData("{\"authenticated\":true,\"starred_story_hashes\":null}")]
        [InlineData("{\"authenticated\":true,\"starred_story_hashes\":{\"7\":[]}}")]
        public async Task StarredInventoryRequiresListResponseProperty(string json)
        {
            var client = new NewsBlurClient(new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json(json)));
            await Assert.ThrowsAsync<NewsBlurMalformedResponseException>(() => client.GetStarredStoryHashInventoryAsync());
        }

        [Fact]
        public async Task EmptyInventoryContainersAreValid()
        {
            var client = new NewsBlurClient(new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json(
                "{\"authenticated\":true,\"unread_feed_story_hashes\":{},\"starred_story_hashes\":[]}")));
            Assert.Empty((await client.GetStoryHashInventoryAsync(false, new[] { 7 })).UnreadStoryHashes);
            Assert.Empty((await client.GetStarredStoryHashInventoryAsync()).StarredStoryHashes);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task BulkBodiesUseRepeatedEncodedHashesAndPreserveServerFeedIds(bool starred)
        {
            RecordingHttpMessageHandler handler = null;
            handler = new RecordingHttpMessageHandler(request =>
            {
                if (starred)
                {
                    Assert.Equal(HttpMethod.Get, request.Method);
                    Assert.Equal("https://newsblur.com/reader/starred_stories?h=7%3Aabc&h=8%3Aa%2Bb%26c",
                        request.RequestUri.AbsoluteUri);
                    Assert.Null(request.Content);
                }
                else
                {
                    Assert.Equal(HttpMethod.Post, request.Method);
                    Assert.Equal("https://newsblur.com/reader/river_stories", request.RequestUri.AbsoluteUri);
                    Assert.Equal("application/x-www-form-urlencoded", request.Content.Headers.ContentType.MediaType);
                    Assert.Equal("h=7%3Aabc&h=8%3Aa%2Bb%26c&include_hidden=true", handler.LastRequestBody);
                }

                return RecordingHttpMessageHandler.Json(
                    "{\"authenticated\":true,\"stories\":[{\"story_hash\":\"7:abc\",\"story_feed_id\":99,\"story_content\":\"retained content\"}]}");
            });

            var result = await new NewsBlurClient(handler).GetStoriesByHashesAsync(new[] { "7:abc", "8:a+b&c" }, starred);
            var story = Assert.Single(result.Stories);
            Assert.Equal("7:abc", story.Hash);
            Assert.Equal(99, story.FeedId);
            Assert.Equal("retained content", story.Content);
        }

        [Theory]
        [InlineData(false, 1)]
        [InlineData(false, 100)]
        [InlineData(true, 1)]
        [InlineData(true, 100)]
        public async Task BulkBodiesAcceptExactBatchBounds(bool starred, int count)
        {
            RecordingHttpMessageHandler handler = null;
            handler = new RecordingHttpMessageHandler(request =>
            {
                var parameters = starred ? request.RequestUri.Query.TrimStart('?') : handler.LastRequestBody;
                Assert.Equal(count, parameters.Split('&').Count(parameter => parameter.StartsWith("h=", StringComparison.Ordinal)));
                return RecordingHttpMessageHandler.Json("{\"authenticated\":true,\"stories\":[]}");
            });
            var hashes = Enumerable.Range(0, count).Select(index => $"7:{index}").ToArray();

            await new NewsBlurClient(handler).GetStoriesByHashesAsync(hashes, starred);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task BulkBodiesRejectInvalidHashesBeforeSending(bool starred)
        {
            var handler = new RecordingHttpMessageHandler(_ => throw new InvalidOperationException("Unexpected HTTP request."));
            var client = new NewsBlurClient(handler);
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetStoriesByHashesAsync(null, starred));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.GetStoriesByHashesAsync(Array.Empty<string>(), starred));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(
                () => client.GetStoriesByHashesAsync(Enumerable.Repeat("7:abc", 101).ToArray(), starred));
            foreach (var invalid in new[] { null, "", " " })
            {
                await Assert.ThrowsAsync<ArgumentException>(
                    () => client.GetStoriesByHashesAsync(new[] { "7:valid", invalid }, starred));
            }
            Assert.Null(handler.LastRequest);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task InventoryRejectsMissingOrInvalidFeedScopeBeforeSending(bool unreadOnly)
        {
            var handler = new RecordingHttpMessageHandler(_ => throw new InvalidOperationException("Unexpected HTTP request."));
            var client = new NewsBlurClient(handler);
            await Assert.ThrowsAsync<ArgumentNullException>(() => client.GetStoryHashInventoryAsync(unreadOnly, null));
            await Assert.ThrowsAsync<ArgumentException>(() => client.GetStoryHashInventoryAsync(unreadOnly, Array.Empty<int>()));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.GetStoryHashInventoryAsync(unreadOnly, new[] { 7, 0 }));
            await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => client.GetStoryHashInventoryAsync(unreadOnly, new[] { -1 }));
            Assert.Null(handler.LastRequest);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public async Task BulkEndpointsPreserveTransportAndPayloadFailures(int endpoint)
        {
            var status = HttpStatusCode.OK;
            var body = "<html>login</html>";
            var client = new NewsBlurClient(new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json(body, status)));
            await Assert.ThrowsAsync<NewsBlurMalformedResponseException>(() => InvokeEndpoint(client, endpoint));

            body = "{\"authenticated\":false}";
            await Assert.ThrowsAsync<NewsBlurAuthenticationException>(() => InvokeEndpoint(client, endpoint));

            body = "<html>failure</html>";
            status = HttpStatusCode.Unauthorized;
            var unauthorized = await Assert.ThrowsAsync<NewsBlurAuthenticationException>(() => InvokeEndpoint(client, endpoint));
            Assert.Equal(status, unauthorized.StatusCode);

            status = HttpStatusCode.ServiceUnavailable;
            var unavailable = await Assert.ThrowsAsync<NewsBlurTransientException>(() => InvokeEndpoint(client, endpoint));
            Assert.Equal(status, unavailable.StatusCode);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public async Task BulkEndpointsRequireAuthenticationAndResponseSchema(int endpoint)
        {
            var property = endpoint switch
            {
                0 => "\"unread_feed_story_hashes\":{}",
                1 => "\"starred_story_hashes\":[]",
                _ => "\"stories\":[]"
            };
            foreach (var json in new[]
            {
                "{" + property + "}",
                "{\"authenticated\":null," + property + "}",
                "{\"authenticated\":0," + property + "}",
                "{\"authenticated\":\"true\"," + property + "}",
                "{\"authenticated\":true}",
                "{\"authenticated\":true," + property.Replace("{}", "null").Replace("[]", "null") + "}"
            })
            {
                var client = new NewsBlurClient(new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json(json)));
                await Assert.ThrowsAsync<NewsBlurMalformedResponseException>(() => InvokeEndpoint(client, endpoint));
            }
        }

        [Fact]
        public async Task InventoryLeavesOmittedRequestedFeedsAbsent()
        {
            var client = new NewsBlurClient(new RecordingHttpMessageHandler(_ => RecordingHttpMessageHandler.Json(
                "{\"authenticated\":true,\"unread_feed_story_hashes\":{\"7\":[[\"7:abc\",1700000000.25]]}}")));

            var response = await client.GetStoryHashInventoryAsync(true, new[] { 7, 8 });

            Assert.Single(response.UnreadStoryHashes);
            Assert.False(response.UnreadStoryHashes.ContainsKey("8"));
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public async Task BulkEndpointsPreserveRateLimitRetryDateBeforeReadingBody(int endpoint)
        {
            var retryAt = new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero);
            var handler = new RecordingHttpMessageHandler(_ =>
            {
                var response = RecordingHttpMessageHandler.Json("<html>slow down</html>", HttpStatusCode.TooManyRequests);
                response.Headers.RetryAfter = new RetryConditionHeaderValue(retryAt);
                return response;
            });

            var exception = await Assert.ThrowsAsync<NewsBlurRateLimitedException>(
                () => InvokeEndpoint(new NewsBlurClient(handler), endpoint));
            Assert.Equal(retryAt, exception.RetryAt);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        public async Task BulkEndpointsForwardCallerCancellation(int endpoint)
        {
            using var cancellation = new CancellationTokenSource();
            var handler = new RecordingHttpMessageHandler(async (_, token) =>
            {
                Assert.True(token.CanBeCanceled);
                cancellation.Cancel();
                await Task.Delay(Timeout.InfiniteTimeSpan, token);
                throw new InvalidOperationException("Cancellation was not forwarded.");
            });

            await Assert.ThrowsAnyAsync<OperationCanceledException>(
                () => InvokeEndpoint(new NewsBlurClient(handler), endpoint, cancellation.Token));
        }

        [Fact]
        public void SourceGeneratedTupleRoundTripPreservesFractionAndIgnoresCurrentCulture()
        {
            var previousCulture = CultureInfo.CurrentCulture;
            try
            {
                CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("fr-FR");
                var tuple = JsonSerializer.Deserialize("[\"7:abc\",\"1700000000.125\"]",
                    BulkSyncTestJsonContext.Default.StoryHashTimestamp);
                Assert.Equal(1700000000.125, tuple.Timestamp);
                Assert.Equal("[\"7:abc\",1700000000.125]",
                    JsonSerializer.Serialize(tuple, BulkSyncTestJsonContext.Default.StoryHashTimestamp));
            }
            finally
            {
                CultureInfo.CurrentCulture = previousCulture;
            }
        }

        private static Task InvokeEndpoint(NewsBlurClient client, int endpoint, CancellationToken cancellationToken = default)
        {
            return endpoint switch
            {
                0 => client.GetStoryHashInventoryAsync(false, new[] { 7 }, cancellationToken),
                1 => client.GetStarredStoryHashInventoryAsync(cancellationToken),
                2 => client.GetStoriesByHashesAsync(new[] { "7:abc" }, false, cancellationToken),
                3 => client.GetStoriesByHashesAsync(new[] { "7:abc" }, true, cancellationToken),
                _ => throw new ArgumentOutOfRangeException(nameof(endpoint))
            };
        }
    }

    [JsonSerializable(typeof(StoryHashTimestamp))]
    internal partial class BulkSyncTestJsonContext : JsonSerializerContext
    {
    }
}
