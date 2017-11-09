using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using NewsBlurSharp.Extensions;
using NewsBlurSharp.Http;
using NewsBlurSharp.Logging;
using NewsBlurSharp.Model.Response;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using System.Diagnostics;

namespace NewsBlurSharp
{
    public class NewsBlurClient : INewsBlurClient
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
            var canAddCookie = !string.IsNullOrEmpty(cookieSessionId);
            if (_cookieJar == null)
            {
                _cookieJar = new CookieContainer();
            }
            else
            {
                var cookies = _cookieJar.GetCookies(new Uri(BaseUrl));
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


        public class FeedResolver : DefaultContractResolver
        {
            public new static readonly FeedResolver Instance = new FeedResolver();

            //protected override JsonContract CreateContract(Type objectType)
            protected override JsonObjectContract CreateObjectContract(Type objectType)
            {
                if (objectType == typeof(NewsBlurSharp.Model.Response.GetFeedResponseLoggedIn.Feeds))
                {
                    var cont = base.CreateObjectContract(objectType);
                    cont.Converter = new FeedConverter();
                    return cont;
                }

                return base.CreateObjectContract(objectType);
            }
        }

        public class FeedConverter : JsonConverter
        {
            public override bool CanConvert(Type objectType)
            {
                if (objectType == typeof(NewsBlurSharp.Model.Response.GetFeedResponseLoggedIn.Feeds))
                {
                    return true;
                }

                return false;
            }

            public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
            {
                List<NewsBlurSharp.Model.Response.GetFeedResponseLoggedIn.Feed> items = new List<Model.Response.GetFeedResponseLoggedIn.Feed>();
                bool read = false;
                while (read = reader.Read()) {
                    Debug.WriteLine(reader.TokenType);
                    if (reader.TokenType!=JsonToken.PropertyName)
                    {
                        break;
                    }
                    
                    string id = reader.Value as string;

                    read = reader.Read();

                    //try
                    //{
                        var outobj = serializer.Deserialize(reader, typeof(NewsBlurSharp.Model.Response.GetFeedResponseLoggedIn.FeedProperties)) as NewsBlurSharp.Model.Response.GetFeedResponseLoggedIn.FeedProperties;

                        items.Add(new NewsBlurSharp.Model.Response.GetFeedResponseLoggedIn.Feed() { id = int.Parse(id), properties = outobj });
                    //} catch (Exception ex)
                    //{
                    //    Debug.WriteLine(ex.Message);
                    //    reader.Read();
                    //}
                }

                return new NewsBlurSharp.Model.Response.GetFeedResponseLoggedIn.Feeds() { FeedItems = items.ToArray() };
            }

            public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
            {
                
            }
        }

        public async Task<NewsBlurSharp.Model.Response.GetFeedResponseLoggedIn.Rootobject> GetFeedsAsync(bool? includeFavIcons = null, bool? isFlatStructure = null, bool? updateCounts = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            var options = new Dictionary<string, string>();
            options.AddIfNotNull("include_favicons", includeFavIcons);
            options.AddIfNotNull("flat", isFlatStructure);
            options.AddIfNotNull("update_counts", updateCounts);

            JsonSerializerSettings settings = new JsonSerializerSettings { ContractResolver = new FeedResolver() };

            var response = await GetResponse<NewsBlurSharp.Model.Response.GetFeedResponseLoggedIn.Rootobject>("reader", "feeds", options, cancellationToken, settings);

            return response.Response;
        }



        #region Stories

        public async Task<StoriesResponse> GetStoriesAsync(int feedId, int? pageIndex = null, bool invertOrder = false, bool filterReadStories = false, bool includeHiddenStories = false, CancellationToken cancellationToken = default(CancellationToken))
        {
            var options = new Dictionary<string, string>();
            options.AddIfNotNull("page", pageIndex);
            options.AddIfNotNull("order", invertOrder ? "oldest" : null);
            options.AddIfNotNull("read_filter", filterReadStories ? "unread" : null);
            options.AddIfNotNull("include_hidden", includeHiddenStories);

            var response = await GetResponse<StoriesResponse>("reader/feed", feedId.ToString(), options, cancellationToken);

            return response.Response;
        }

        public async Task<object> MarkStoriesReadAsync(List<string> storyHashList)
        {
            var hashString = "";
            var firstHash = true;

            foreach (var hash in storyHashList)
            {
                if (!firstHash)
                    hashString += "&";
                else
                    firstHash = false;

                hashString += "story_hash=" + hash;
            }

            var data = new Dictionary<string, string>();
            data.Add("story_hash", hashString);

            var response = await PostResponse<object>("reader", "mark_story_hashes_as_read", data);

            return response.Response;
        }

        public async Task<object> MarkStoryUnreadAsync(string storyHash)
        {
            var data = new Dictionary<string, string>();
            data.Add("story_hash", storyHash);

            var response = await PostResponse<object>("reader", "mark_story_hash_as_unread", data);

            return response.Response;
        }

        #endregion

        #region Social

        public async Task<object> GetUserPublicProfileAsync(int userID, CancellationToken cancellationToken = default(CancellationToken))
        {
            var data = new Dictionary<string, string>();
            data.Add("user_id", userID.ToString());

            var response = await GetResponse<object>("social", "profile", data, cancellationToken);

            return response.Response;
        }

        public async Task<ProfileResponse> GetUserProfileAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            var response = await GetResponse<ProfileResponse>("social", "load_user_profile", cancellationToken: cancellationToken);

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

            var response = await _httpClient.PostAsync(url, new FormUrlEncodedContent(postData ?? new Dictionary<string, string>()), cancellationToken).ConfigureAwait(false);

            var duration = DateTime.Now - requestTime;

            cancellationToken.ThrowNewsBlurExceptionIfCancelled();

            _logger.Debug("Received {0} status code after {1} ms from {2}: {3}", response.StatusCode, duration.TotalMilliseconds, "POST", url);

            return await HandleBaseResponse<TResponseType>(response).ConfigureAwait(false);
        }

        private async Task<BaseResponse<TResponseType>> GetResponse<TResponseType>(string endPoint, 
            string method, 
            Dictionary<string, string> options = null, 
            CancellationToken cancellationToken = default(CancellationToken),
            JsonSerializerSettings settings = null,
            [CallerMemberName] string callingMethod = ""
            )

        {
            _logger.Debug(callingMethod);
            var url = $"{BaseUrl}{endPoint}/{method}";
            url = url.TrimEnd('/');

            if (options != null)
            {
                var queryString = options.ToQueryString();
                url = $"{url}?{queryString}";
            }

            _logger.Debug("GET: {0}", url);
            var requestTime = DateTime.Now;

            cancellationToken.ThrowNewsBlurExceptionIfCancelled();

            HandlerNeedsRecreating();

            var response = await _httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            var duration = DateTime.Now - requestTime;

            cancellationToken.ThrowNewsBlurExceptionIfCancelled();

            _logger.Debug("Received {0} status code after {1} ms from {2}: {3}", response.StatusCode, duration.TotalMilliseconds, "GET", url);

            return await HandleBaseResponse<TResponseType>(response, settings).ConfigureAwait(false);
        }

        private async Task<BaseResponse<TResponseType>> HandleBaseResponse<TResponseType>(HttpResponseMessage response, JsonSerializerSettings settings = null)
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

            var item = await responseString.DeserialiseAsync<TResponseType>(settings).ConfigureAwait(false);
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
