using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NewsBlurSharp.Extensions;
using NewsBlurSharp.Http;
using NewsBlurSharp.Logging;
using NewsBlurSharp.Model.Response;

namespace NewsBlurSharp
{
    public class NewsBlurClient
    {
        private const string NewsBlurSessionId = "newsblur_sessionid";
        private readonly IClientHandlerFactory _handlerFactory;
        private readonly string _userAgent;
        private readonly ILogger _logger;

        private const string BaseUrl = "https://newsblur.com/";

        private HttpClient _httpClient;
        private HttpClientHandler _handler;
        private CookieContainer _cookieJar;
        private string _cookieSessionId;

        public NewsBlurClient(IClientHandlerFactory handlerFactory, ILogger logger, string userAgent = "NewsBlurSharp")
        {
            _handlerFactory = handlerFactory;
            _userAgent = userAgent;
            _handler = GetHandlerFromFactory(handlerFactory);

            _httpClient = CreateHttpClient();
            _logger = logger ?? new NullLogger();
        }

        public NewsBlurClient(IClientHandlerFactory handlerFactory)
            : this(handlerFactory, new NullLogger())
        {
        }

        public NewsBlurClient()
            : this(null)
        {
        }

        #region Authentication methods
        public void SetCookieSessionId(string cookieSessionId)
        {
            var canAddCookie = false;
            if (_cookieJar == null)
            {
                _cookieJar = new CookieContainer();
                canAddCookie = !string.IsNullOrEmpty(cookieSessionId);
            }
            else
            {
                var cookies = _cookieJar.GetCookies(new Uri(BaseUrl));
                canAddCookie = true;
                foreach (Cookie cookie in cookies)
                {
                    if (cookie.Name == NewsBlurSessionId)
                    {
                        cookie.Value = cookieSessionId;
                        canAddCookie = false;
                        break;
                    }
                }
            }

            if (canAddCookie)
            {
                _cookieJar.Add(new Uri(BaseUrl), new Cookie(NewsBlurSessionId, cookieSessionId));
            }
        }

        public async Task<LoginResponse> LoginAsync(string username, string password, CancellationToken cancellation = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentNullException(nameof(username), "Username cannot be null or empty");
            }

            var postData = new Dictionary<string, string>
            {
                {"username", username}
            };

            postData.AddIfNotNull("password", password);

            var response = await PostResponse<InternalLoginResponse>("api", "login", postData, cancellation).ConfigureAwait(false);
            var loginResponse = response.Response;
            return new LoginResponse(loginResponse.Authenticated, response.NewsBlurCookie, loginResponse.UserId);
        }

        public async Task LogoutAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            await PostResponse<object>("api", "logout", cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        public async Task<SignupResponse> SignupAsync(string username, string emailAddress, string password = "", CancellationToken cancellationToken = default(CancellationToken))
        {
            if (string.IsNullOrEmpty(username))
            {
                throw new ArgumentNullException(nameof(username), "username must be provided");
            }

            if (string.IsNullOrEmpty(emailAddress))
            {
                throw new ArgumentNullException(nameof(emailAddress), "email address must be provided");
            }

            var postData = new Dictionary<string, string>
            {
                {"username", username},
                {"email", emailAddress}
            };

            postData.AddIfNotNull("password", password);

            var response = await PostResponse<InternalLoginResponse>("api", "signup", postData, cancellationToken);
            var loginResponse = response.Response;
            return new SignupResponse(loginResponse.Authenticated, response.NewsBlurCookie, loginResponse.UserId);
        }
        #endregion

        public async Task<object> GetFeedsAsync(bool? includeFavIcons = null, bool? isFlatStructure = null, bool? updateCounts = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var options = new Dictionary<string, string>();
            options.AddIfNotNull("include_favicons", includeFavIcons);
            options.AddIfNotNull("flat", isFlatStructure);
            options.AddIfNotNull("update_counts", updateCounts);

            var response = await GetResponse<object>("reader", "feeds", options, cancellationToken);

            return response.Response;
        }

        #region Stories

        public async Task<object> GetStoriesAsync(int feedId, int? pageIndex = null, bool invertOrder = false, bool filterReadStories = false, bool includeHiddenStories = false, CancellationToken cancellationToken = default(CancellationToken))
        {
            var options = new Dictionary<String, String>();
            options.AddIfNotNull("page", pageIndex);
            options.AddIfNotNull("order", (invertOrder) ? "oldest" : null);
            options.AddIfNotNull("read_filter", (filterReadStories) ? "unread" : null);
            options.AddIfNotNull("include_hidden", includeHiddenStories);

            var response = await GetResponse<object>("reader/feed", feedId.ToString(), options, cancellationToken);

            return response.Response;
        }

        public async Task<object> MarkStoriesReadAsync(List<String> storyHashList)
        {
            String hashString = "";
            bool firstHash = false;

            foreach(String hash in storyHashList){
                if (!firstHash)
                    hashString += "&";
                else
                    firstHash = true;

                hashString += "story_hash=" + hash;
            }

            var data = new Dictionary<String, String>();
            data.Add("story_hash", hashString);

            var response = await PostResponse<object>("reader", "mark_story_hashes_as_read", data);

            return response.Response;
        }

        public async Task<object> MarkStoryUnreadAsync(String storyHash)
        {
            var data = new Dictionary<String, String>();
            data.Add("story_hash", storyHash);

            var response = await PostResponse<object>("reader", "mark_story_hash_as_unread", data);

            return response.Response;
        }

        #endregion

        private HttpClientHandler GetHandlerFromFactory(IClientHandlerFactory handlerFactory)
        {
            var handler = handlerFactory?.CreateHandler() as HttpClientHandler ?? new HttpClientHandler();

            SetCookieSessionId(_cookieSessionId);
            handler.CookieContainer = _cookieJar;
            handler.UseCookies = true;
            handler.UseDefaultCredentials = false;

            return handler;
        }

        private HttpClient CreateHttpClient()
        {
            var client = _handler != null ? new HttpClient(_handler) : new HttpClient();
            client.DefaultRequestHeaders.TryAddWithoutValidation("User-Agent", _userAgent);
            return client;
        }

        private void HandlerNeedsRecreating()
        {
            var clientHandler = _handler as IClientHandler;
            if (clientHandler == null || !clientHandler.IsDisposed)
            {
                return;
            }

            _handler = GetHandlerFromFactory(_handlerFactory);
            _httpClient = CreateHttpClient();
        }

        private async Task<BaseResponse<TResponseType>> PostResponse<TResponseType>(string endPoint, string method, Dictionary<string, string> postData = null, CancellationToken cancellationToken = default(CancellationToken), [CallerMemberName] string callingMethod = "")
        {
            _logger.Debug(callingMethod);
            var url = $"{BaseUrl}{endPoint}/{method}";
            url = url.TrimEnd('/');

            var requestTime = DateTime.Now;

            cancellationToken.ThrowNewsBlurExceptionIfCancelled();

            HandlerNeedsRecreating();

            var response = await _httpClient.PostAsync(url, new FormUrlEncodedContent(postData), cancellationToken).ConfigureAwait(false);

            var duration = DateTime.Now - requestTime;

            cancellationToken.ThrowNewsBlurExceptionIfCancelled();

            _logger.Debug("Received {0} status code after {1} ms from {2}: {3}", response.StatusCode, duration.TotalMilliseconds, "POST", url);

            return await HandleBaseResponse<TResponseType>(response).ConfigureAwait(false);
        }

        private async Task<BaseResponse<TResponseType>> GetResponse<TResponseType>(string endPoint, string method, Dictionary<string, string> options = null, CancellationToken cancellationToken = default(CancellationToken), [CallerMemberName] string callingMethod = "")

        {
            _logger.Debug(callingMethod);
            var url = $"{BaseUrl}{endPoint}/{method}";
            url = url.TrimEnd('/');

            var queryString = options.ToQueryString();
            url = $"{url}?{queryString}";

            _logger.Debug("GET: {0}", url);
            var requestTime = DateTime.Now;

            cancellationToken.ThrowNewsBlurExceptionIfCancelled();

            HandlerNeedsRecreating();

            var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            var duration = DateTime.Now - requestTime;

            cancellationToken.ThrowNewsBlurExceptionIfCancelled();

            _logger.Debug("Received {0} status code after {1} ms from {2}: {3}", response.StatusCode, duration.TotalMilliseconds, "GET", url);

            return await HandleBaseResponse<TResponseType>(response).ConfigureAwait(false);
        }

        private async Task<BaseResponse<TResponseType>> HandleBaseResponse<TResponseType>(HttpResponseMessage response)
        {
            var responseString = await response.Content.ReadAsStringAsync().ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                //var jsonSettings = new JsonSerializerSettings { Error = (sender, args) => args.ErrorContext.Handled = true };
                //var error = JsonConvert.DeserializeObject<ErrorResponse>(responseString, jsonSettings);
                //if (error != null)
                //{
                //    throw new ParkRunException(error.ErrorDescription);
                //}

                //var baseError = JsonConvert.DeserializeObject<TResponseType>(responseString, jsonSettings);
                //if (baseError != null)
                //{
                //    throw new ParkRunException(baseError.Error.ErrorMessage, baseError.Error.HttpStatusCode);
                //}
            }

            var item = await responseString.DeserialiseAsync<TResponseType>().ConfigureAwait(false);
            var cookies = _cookieJar.GetCookies(response.RequestMessage.RequestUri);

            foreach (Cookie cookie in cookies)
            {
                if (cookie.Name == NewsBlurSessionId)
                {
                    _cookieSessionId = cookie.Value;
                    break;
                }
            }

            return new BaseResponse<TResponseType>(item, response, _cookieSessionId);
        }

        private class BaseResponse<T>
        {
            internal T Response { get; }
            internal HttpResponseMessage ResponseMessage { get; }
            internal string NewsBlurCookie { get; }

            internal BaseResponse(T response, HttpResponseMessage responseMessage, string newsBlurCookie)
            {
                Response = response;
                ResponseMessage = responseMessage;
                NewsBlurCookie = newsBlurCookie;
            }
        }
    }
}
