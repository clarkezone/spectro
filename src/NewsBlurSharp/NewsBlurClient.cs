using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Serialization.Metadata;
using System.Threading;
using System.Threading.Tasks;
using NewsBlurSharp.Extensions;
using NewsBlurSharp.Http;
using NewsBlurSharp.Logging;
using NewsBlurSharp.Model;
using NewsBlurSharp.Model.Response;
using NewsBlurSharp.Serialization;

namespace NewsBlurSharp
{
    public class NewsBlurClient : INewsBlurClient
    {
        private const string BaseUrl = "https://newsblur.com/";
        private const string NewsBlurSessionId = "newsblur_sessionid";
        private static readonly TimeSpan DefaultRequestTimeout = TimeSpan.FromSeconds(30);

        private readonly IClientHandlerFactory _handlerFactory;
        private readonly ILogger _logger;
        private readonly TimeSpan _requestTimeout;
        private readonly string _userAgent;
        private readonly TimeProvider _timeProvider;

        private CookieContainer _cookieJar;
        private string _cookieSessionId;
        private HttpClient _httpClient;
        private HttpClientHandler _handler;

        public NewsBlurClient(
            IClientHandlerFactory handlerFactory,
            ILogger logger,
            string userAgent = "NewsBlurSharp",
            TimeSpan? requestTimeout = null,
            TimeProvider timeProvider = null)
        {
            _handlerFactory = handlerFactory;
            _logger = logger ?? new NullLogger();
            _userAgent = userAgent;
            _requestTimeout = ValidateTimeout(requestTimeout);
            _timeProvider = timeProvider ?? TimeProvider.System;
            _handler = GetHandlerFromFactory(handlerFactory);
            _httpClient = CreateHttpClient(_handler);
        }

        public NewsBlurClient(IClientHandlerFactory handlerFactory)
            : this(handlerFactory, new NullLogger())
        {
        }

        public NewsBlurClient()
            : this((IClientHandlerFactory)null)
        {
        }

        public NewsBlurClient(
            HttpMessageHandler messageHandler,
            ILogger logger = null,
            string userAgent = "NewsBlurSharp",
            TimeSpan? requestTimeout = null,
            TimeProvider timeProvider = null)
        {
            _logger = logger ?? new NullLogger();
            _userAgent = userAgent;
            _requestTimeout = ValidateTimeout(requestTimeout);
            _timeProvider = timeProvider ?? TimeProvider.System;
            _cookieJar = new CookieContainer();
            _httpClient = CreateHttpClient(
                messageHandler ?? throw new ArgumentNullException(nameof(messageHandler)));
        }

        public void SetCookieSessionId(string cookieSessionId)
        {
            _cookieSessionId = cookieSessionId;
            _cookieJar ??= new CookieContainer();

            if (string.IsNullOrEmpty(cookieSessionId))
            {
                var existingCookies = _cookieJar.GetCookies(new Uri(BaseUrl));
                foreach (Cookie cookie in existingCookies)
                {
                    if (cookie.Name == NewsBlurSessionId)
                    {
                        cookie.Expired = true;
                    }
                }

                return;
            }

            var cookies = _cookieJar.GetCookies(new Uri(BaseUrl));
            foreach (Cookie cookie in cookies)
            {
                if (cookie.Name == NewsBlurSessionId)
                {
                    cookie.Value = cookieSessionId;
                    return;
                }
            }

            _cookieJar.Add(new Uri(BaseUrl), new Cookie(NewsBlurSessionId, cookieSessionId));
        }

        public async Task<LoginResponse> LoginAsync(
            string username,
            string password,
            CancellationToken cancellation = default)
        {
            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentNullException(nameof(username), "Username cannot be null or empty.");
            }

            var data = new Dictionary<string, string>
            {
                ["username"] = username
            };
            data.AddIfNotNull("password", password);

            var response = await SendAsync(
                HttpMethod.Post,
                "api/login",
                NewsBlurJsonContext.Default.InternalLoginResponse,
                formData: data,
                cancellationToken: cancellation,
                allowUnauthenticatedPayload: true).ConfigureAwait(false);

            return new LoginResponse(
                response.Authenticated,
                _cookieSessionId,
                response.UserId);
        }

        public async Task LogoutAsync(CancellationToken cancellationToken = default)
        {
            await SendAsync(
                HttpMethod.Post,
                "api/logout",
                NewsBlurJsonContext.Default.OperationResponse,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public Task<NewsFeedResponse> GetFeedsAsync(
            bool? includeFavIcons = null,
            bool? isFlatStructure = null,
            bool? updateCounts = null,
            CancellationToken cancellationToken = default)
        {
            var options = new Dictionary<string, string>();
            options.AddIfNotNull("include_favicons", includeFavIcons, ToLowerInvariant);
            options.AddIfNotNull("flat", isFlatStructure, ToLowerInvariant);
            options.AddIfNotNull("update_counts", updateCounts, ToLowerInvariant);

            return SendAsync(
                HttpMethod.Get,
                "reader/feeds",
                NewsBlurJsonContext.Default.NewsFeedResponse,
                query: options,
                cancellationToken: cancellationToken);
        }

        public Task<StoriesResponse> GetStoriesAsync(
            int feedId,
            int? pageIndex = null,
            bool invertOrder = false,
            bool filterReadStories = false,
            bool includeHiddenStories = false,
            CancellationToken cancellationToken = default)
        {
            if (feedId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(feedId));
            }

            var options = CreateStoryOptions(
                pageIndex,
                invertOrder,
                filterReadStories);
            if (includeHiddenStories)
            {
                options["include_hidden"] = "true";
            }

            return SendAsync(
                HttpMethod.Get,
                $"reader/feed/{feedId.ToString(CultureInfo.InvariantCulture)}",
                NewsBlurJsonContext.Default.StoriesResponse,
                query: options,
                cancellationToken: cancellationToken);
        }

        public Task<StoriesResponse> GetRiverStoriesAsync(
            int? pageIndex = null,
            bool invertOrder = false,
            bool filterReadStories = false,
            CancellationToken cancellationToken = default)
        {
            return SendAsync(
                HttpMethod.Get,
                "reader/river_stories",
                NewsBlurJsonContext.Default.StoriesResponse,
                query: CreateStoryOptions(pageIndex, invertOrder, filterReadStories),
                cancellationToken: cancellationToken);
        }

        public Task<StoriesResponse> GetStarredStoriesAsync(
            int? pageIndex = null,
            CancellationToken cancellationToken = default)
        {
            var options = new Dictionary<string, string>();
            options.AddIfNotNull("page", pageIndex);

            return SendAsync(
                HttpMethod.Get,
                "reader/starred_stories",
                NewsBlurJsonContext.Default.StoriesResponse,
                query: options,
                cancellationToken: cancellationToken);
        }

        public Task<UnreadStoryHashesResponse> GetUnreadStoryHashesAsync(
            CancellationToken cancellationToken = default)
        {
            return SendAsync(
                HttpMethod.Get,
                "reader/unread_story_hashes",
                NewsBlurJsonContext.Default.UnreadStoryHashesResponse,
                cancellationToken: cancellationToken);
        }

        public Task<StoryHashInventoryResponse> GetStoryHashInventoryAsync(
            bool unreadOnly,
            IReadOnlyCollection<int> feedIds,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(feedIds);
            if (feedIds.Count == 0)
            {
                throw new ArgumentException("At least one feed ID must be provided.", nameof(feedIds));
            }

            var options = new List<KeyValuePair<string, string>>
            {
                new("read_filter", unreadOnly ? "unread" : "all"),
                new("order", "newest"),
                new("include_timestamps", "true")
            };
            foreach (var feedId in feedIds)
            {
                if (feedId <= 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(feedIds), "Feed IDs must be positive.");
                }

                options.Add(new("feed_id", feedId.ToString(CultureInfo.InvariantCulture)));
            }

            return SendAsync(
                HttpMethod.Get,
                "reader/unread_story_hashes",
                NewsBlurJsonContext.Default.StoryHashInventoryResponse,
                query: options,
                cancellationToken: cancellationToken,
                requiredResponseProperty: "unread_feed_story_hashes");
        }

        public Task<StarredStoryHashInventoryResponse> GetStarredStoryHashInventoryAsync(
            CancellationToken cancellationToken = default)
        {
            return SendAsync(
                HttpMethod.Get,
                "reader/starred_story_hashes",
                NewsBlurJsonContext.Default.StarredStoryHashInventoryResponse,
                query: new Dictionary<string, string> { ["include_timestamps"] = "true" },
                cancellationToken: cancellationToken,
                requiredResponseProperty: "starred_story_hashes");
        }

        public Task<StoriesResponse> GetStoriesByHashesAsync(
            IReadOnlyCollection<string> hashes,
            bool starred,
            CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(hashes);
            if (hashes.Count is < 1 or > 100)
            {
                throw new ArgumentOutOfRangeException(nameof(hashes), "Provide between 1 and 100 story hashes.");
            }

            var options = new List<KeyValuePair<string, string>>();
            foreach (var hash in hashes)
            {
                ValidateStoryHash(hash, nameof(hashes));
                options.Add(new("h", hash));
            }

            // The saved-copy endpoint reads request.GET only, unlike the river endpoint.
            if (starred)
            {
                return SendAsync(
                    HttpMethod.Get,
                    "reader/starred_stories",
                    NewsBlurJsonContext.Default.StoriesResponse,
                    query: options,
                    cancellationToken: cancellationToken,
                    requiredResponseProperty: "stories");
            }

            options.Add(new("include_hidden", "true"));
            return SendAsync(
                HttpMethod.Post,
                "reader/river_stories",
                NewsBlurJsonContext.Default.StoriesResponse,
                formData: options,
                cancellationToken: cancellationToken,
                requiredResponseProperty: "stories");
        }

        public Task<OperationResponse> MarkStoriesReadAsync(List<string> storyHashList)
        {
            return MarkStoriesReadAsync(storyHashList, CancellationToken.None);
        }

        public async Task<OperationResponse> MarkStoriesReadAsync(
            List<string> storyHashList,
            CancellationToken cancellationToken)
        {
            if (storyHashList == null)
            {
                throw new ArgumentNullException(nameof(storyHashList));
            }

            var data = new List<KeyValuePair<string, string>>(storyHashList.Count);
            foreach (var hash in storyHashList)
            {
                ValidateStoryHash(hash, nameof(storyHashList));
                data.Add(new KeyValuePair<string, string>("story_hash", hash));
            }

            return await SendOperationAsync(
                "reader/mark_story_hashes_as_read",
                data,
                cancellationToken).ConfigureAwait(false);
        }

        public Task<OperationResponse> MarkStoryUnreadAsync(string storyHash)
        {
            return MarkStoryUnreadAsync(storyHash, CancellationToken.None);
        }

        public Task<OperationResponse> MarkStoryUnreadAsync(
            string storyHash,
            CancellationToken cancellationToken)
        {
            return SendStoryMutationAsync(
                "reader/mark_story_hash_as_unread",
                storyHash,
                cancellationToken);
        }

        public Task<OperationResponse> StarStoryAsync(
            string storyHash,
            CancellationToken cancellationToken = default)
        {
            return SendStoryMutationAsync(
                "reader/mark_story_hash_as_starred",
                storyHash,
                cancellationToken);
        }

        public Task<OperationResponse> UnstarStoryAsync(
            string storyHash,
            CancellationToken cancellationToken = default)
        {
            return SendStoryMutationAsync(
                "reader/mark_story_hash_as_unstarred",
                storyHash,
                cancellationToken);
        }

        public async Task<OperationResponse> MarkFeedReadAsync(
            int feedId,
            CancellationToken cancellationToken = default)
        {
            if (feedId <= 0)
            {
                throw new ArgumentOutOfRangeException(nameof(feedId));
            }

            var data = new Dictionary<string, string>
            {
                ["feed_id"] = feedId.ToString(CultureInfo.InvariantCulture)
            };

            return await SendOperationAsync(
                "reader/mark_feed_as_read",
                data,
                cancellationToken).ConfigureAwait(false);
        }

        public Task<ProfileResponse> GetUserProfileAsync(
            CancellationToken cancellationToken = default)
        {
            return SendAsync(
                HttpMethod.Get,
                "social/load_user_profile",
                NewsBlurJsonContext.Default.ProfileResponse,
                cancellationToken: cancellationToken);
        }

        private static Dictionary<string, string> CreateStoryOptions(
            int? pageIndex,
            bool invertOrder,
            bool filterReadStories)
        {
            var options = new Dictionary<string, string>();
            options.AddIfNotNull("page", pageIndex);
            options.AddIfNotNull("order", invertOrder ? "oldest" : null);
            options.AddIfNotNull("read_filter", filterReadStories ? "unread" : null);
            return options;
        }

        private static string ToLowerInvariant(bool value)
        {
            return value ? "true" : "false";
        }

        private static TimeSpan ValidateTimeout(TimeSpan? requestTimeout)
        {
            var timeout = requestTimeout ?? DefaultRequestTimeout;
            if (timeout <= TimeSpan.Zero && timeout != Timeout.InfiniteTimeSpan)
            {
                throw new ArgumentOutOfRangeException(nameof(requestTimeout));
            }

            return timeout;
        }

        private static void ValidateStoryHash(string storyHash, string parameterName)
        {
            if (string.IsNullOrWhiteSpace(storyHash))
            {
                throw new ArgumentException("A story hash must be provided.", parameterName);
            }
        }

        private async Task<OperationResponse> SendStoryMutationAsync(
            string path,
            string storyHash,
            CancellationToken cancellationToken)
        {
            ValidateStoryHash(storyHash, nameof(storyHash));
            var data = new Dictionary<string, string>
            {
                ["story_hash"] = storyHash
            };

            return await SendOperationAsync(path, data, cancellationToken).ConfigureAwait(false);
        }

        private async Task<OperationResponse> SendOperationAsync(
            string path,
            IEnumerable<KeyValuePair<string, string>> data,
            CancellationToken cancellationToken)
        {
            return await SendAsync(
                HttpMethod.Post,
                path,
                NewsBlurJsonContext.Default.OperationResponse,
                formData: data,
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        private async Task<T> SendAsync<T>(
            HttpMethod method,
            string path,
            JsonTypeInfo<T> responseType,
            IEnumerable<KeyValuePair<string, string>> query = null,
            IEnumerable<KeyValuePair<string, string>> formData = null,
            CancellationToken cancellationToken = default,
            bool allowUnauthenticatedPayload = false,
            string requiredResponseProperty = null)
        {
            HandlerNeedsRecreating();

            var requestUri = CreateRequestUri(path, query);
            using var request = new HttpRequestMessage(method, requestUri);
            if (formData != null)
            {
                request.Content = new FormUrlEncodedContent(formData);
            }

            if (_handler == null && !string.IsNullOrEmpty(_cookieSessionId))
            {
                request.Headers.TryAddWithoutValidation(
                    "Cookie",
                    $"{NewsBlurSessionId}={_cookieSessionId}");
            }

            _logger.Debug("{0} {1}", method.Method, requestUri.AbsolutePath);
            var stopwatch = Stopwatch.StartNew();

            using var timeoutSource =
                CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            if (_requestTimeout != Timeout.InfiniteTimeSpan)
            {
                timeoutSource.CancelAfter(_requestTimeout);
            }

            try
            {
                using var response = await _httpClient.SendAsync(
                    request,
                    HttpCompletionOption.ResponseHeadersRead,
                    timeoutSource.Token).ConfigureAwait(false);
                stopwatch.Stop();
                _logger.Debug(
                    "Received {0} after {1} ms for {2} {3}",
                    (int)response.StatusCode,
                    stopwatch.ElapsedMilliseconds,
                    method.Method,
                    requestUri.AbsolutePath);

                ThrowForStatus(response, method, requestUri);
                CaptureSessionCookie(response, requestUri);
                var responseBody = await response.Content.ReadAsStringAsync(
                    timeoutSource.Token).ConfigureAwait(false);

                try
                {
                    using var document = JsonDocument.Parse(responseBody);
                    if (!allowUnauthenticatedPayload
                        && document.RootElement.ValueKind == JsonValueKind.Object
                        && document.RootElement.TryGetProperty("authenticated", out var authenticated)
                        && authenticated.ValueKind == JsonValueKind.False)
                    {
                        throw new NewsBlurAuthenticationException(
                            $"NewsBlur rejected {method.Method} {requestUri.AbsolutePath}.");
                    }

                    if (requiredResponseProperty != null
                        && (document.RootElement.ValueKind != JsonValueKind.Object
                            || !document.RootElement.TryGetProperty("authenticated", out var authentication)
                            || authentication.ValueKind != JsonValueKind.True
                            || !document.RootElement.TryGetProperty(requiredResponseProperty, out var requiredValue)
                            || requiredValue.ValueKind == JsonValueKind.Null))
                    {
                        throw new JsonException(
                            $"The response requires authenticated: true and a nonnull {requiredResponseProperty} property.");
                    }

                    var result = JsonSerializer.Deserialize(responseBody, responseType);
                    if (result == null)
                    {
                        throw new JsonException("The response contained JSON null.");
                    }

                    return result;
                }
                catch (JsonException exception)
                {
                    throw new NewsBlurMalformedResponseException(
                        $"NewsBlur returned malformed JSON for {method.Method} {requestUri.AbsolutePath}.",
                        exception);
                }
            }
            catch (OperationCanceledException exception)
                when (!cancellationToken.IsCancellationRequested
                    && timeoutSource.IsCancellationRequested)
            {
                throw new NewsBlurTimeoutException(
                    $"NewsBlur did not respond within {_requestTimeout.TotalSeconds:g} seconds.",
                    exception);
            }
            catch (HttpRequestException exception)
            {
                if (exception.HttpRequestError == HttpRequestError.NameResolutionError)
                {
                    throw new NewsBlurOfflineException(
                        $"The NewsBlur host could not be reached for {requestUri.AbsolutePath}.",
                        exception);
                }

                throw new NewsBlurTransientException(
                    $"The NewsBlur request to {requestUri.AbsolutePath} failed.",
                    exception.StatusCode,
                    exception);
            }
        }

        private static Uri CreateRequestUri(
            string path,
            IEnumerable<KeyValuePair<string, string>> query)
        {
            var url = BaseUrl + path.TrimStart('/');
            if (query != null)
            {
                var parameters = new List<string>();
                foreach (var pair in query)
                {
                    parameters.Add(
                        Uri.EscapeDataString(pair.Key) + "=" + Uri.EscapeDataString(pair.Value));
                }
                if (parameters.Count > 0)
                {
                    url += "?" + string.Join("&", parameters);
                }
            }

            return new Uri(url);
        }

        private void ThrowForStatus(
            HttpResponseMessage response,
            HttpMethod method,
            Uri requestUri)
        {
            var statusCode = response.StatusCode;
            if ((int)statusCode is >= 200 and <= 299)
            {
                return;
            }

            var message =
                $"NewsBlur returned HTTP {(int)statusCode} for {method.Method} {requestUri.AbsolutePath}.";
            if (statusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                throw new NewsBlurAuthenticationException(message, statusCode);
            }

            DateTimeOffset? retryAt = response.Headers.RetryAfter?.Date;
            if (response.Headers.RetryAfter?.Delta is { } delay)
            {
                retryAt = _timeProvider.GetUtcNow() + delay;
            }
            if (statusCode == HttpStatusCode.TooManyRequests)
            {
                throw new NewsBlurRateLimitedException(message, retryAt);
            }
            if (statusCode == HttpStatusCode.RequestTimeout || (int)statusCode >= 500)
            {
                throw new NewsBlurTransientException(message, statusCode, retryAt: retryAt);
            }

            throw new NewsBlurException(message, NewsBlurFailureKind.Http, statusCode);
        }

        private void CaptureSessionCookie(HttpResponseMessage response, Uri requestUri)
        {
            if (_handler != null)
            {
                foreach (Cookie cookie in _cookieJar.GetCookies(requestUri))
                {
                    if (cookie.Name == NewsBlurSessionId)
                    {
                        _cookieSessionId = cookie.Value;
                        return;
                    }
                }
            }

            if (!response.Headers.TryGetValues("Set-Cookie", out var headers))
            {
                return;
            }

            foreach (var header in headers)
            {
                var prefix = NewsBlurSessionId + "=";
                var start = header.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
                if (start < 0)
                {
                    continue;
                }

                start += prefix.Length;
                var end = header.IndexOf(';', start);
                _cookieSessionId = end < 0
                    ? header.Substring(start)
                    : header.Substring(start, end - start);
                return;
            }
        }

        private HttpClientHandler GetHandlerFromFactory(IClientHandlerFactory handlerFactory)
        {
            var handler =
                handlerFactory?.CreateHandler() as HttpClientHandler
                ?? new HttpClientHandler();

            _cookieJar ??= new CookieContainer();
            handler.CookieContainer = _cookieJar;
            handler.UseCookies = true;
            handler.UseDefaultCredentials = false;
            return handler;
        }

        private HttpClient CreateHttpClient(HttpMessageHandler handler)
        {
            var client = new HttpClient(handler)
            {
                Timeout = Timeout.InfiniteTimeSpan
            };
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", _userAgent);
            return client;
        }

        private void HandlerNeedsRecreating()
        {
            if (_handlerFactory == null)
            {
                return;
            }

            if (_handler is not IClientHandler clientHandler || !clientHandler.IsDisposed)
            {
                return;
            }

            _handler = GetHandlerFromFactory(_handlerFactory);
            _httpClient = CreateHttpClient(_handler);
        }
    }
}
