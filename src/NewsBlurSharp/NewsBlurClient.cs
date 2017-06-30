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
        private readonly IClientHandlerFactory _handlerFactory;
        private readonly ILogger _logger;

        private const string BaseUrl = "https://newsblur.com/";

        private HttpClient _httpClient;
        private HttpClientHandler _handler;
        private CookieContainer _cookieJar;

        public NewsBlurClient(IClientHandlerFactory handlerFactory, ILogger logger)
        {
            _handlerFactory = handlerFactory;
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

            var response = await PostResponse<InternalLoginResponse>("api", "login", postData, cancellation);
            var loginResponse = response.Response;
            return new LoginResponse(loginResponse.Authenticated, response.NewsBlurCookie, loginResponse.UserId);
        }

        private HttpClientHandler GetHandlerFromFactory(IClientHandlerFactory handlerFactory)
        {
            var handler = handlerFactory?.CreateHandler() as HttpClientHandler ?? new HttpClientHandler();

            _cookieJar = new CookieContainer();
            handler.CookieContainer = _cookieJar;
            handler.UseCookies = true;
            handler.UseDefaultCredentials = false;

            return handler;
        }

        private HttpClient CreateHttpClient()
        {
            var client = _handler != null ? new HttpClient(_handler) : new HttpClient();
            client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("NewsBlurSharp", "0.0.0.1"));
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
            var newsBlurCookie = string.Empty;

            foreach (Cookie cookie in cookies)
            {
                if (cookie.Name == "newsblur_sessionid")
                {
                    newsBlurCookie = cookie.Value;
                    break;
                }
            }

            return new BaseResponse<TResponseType>(item, response, newsBlurCookie);
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
