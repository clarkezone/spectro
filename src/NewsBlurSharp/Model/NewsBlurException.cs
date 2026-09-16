using System;
using System.Net;

namespace NewsBlurSharp.Model
{
    public enum NewsBlurFailureKind
    {
        Http,
        Authentication,
        Offline,
        Transient,
        MalformedResponse,
        Timeout,
        RateLimited
    }

    public class NewsBlurException : Exception
    {
        public NewsBlurException()
        {
        }

        public NewsBlurException(
            string message,
            NewsBlurFailureKind kind = NewsBlurFailureKind.Http,
            HttpStatusCode? statusCode = null,
            Exception innerException = null,
            DateTimeOffset? retryAt = null)
            : base(message, innerException)
        {
            Kind = kind;
            StatusCode = statusCode;
            RetryAt = retryAt;
        }

        public NewsBlurFailureKind Kind { get; }

        public HttpStatusCode? StatusCode { get; }

        public DateTimeOffset? RetryAt { get; }
    }

    public sealed class NewsBlurAuthenticationException : NewsBlurException
    {
        public NewsBlurAuthenticationException(string message, HttpStatusCode? statusCode = null)
            : base(message, NewsBlurFailureKind.Authentication, statusCode)
        {
        }
    }

    public sealed class NewsBlurTransientException : NewsBlurException
    {
        public NewsBlurTransientException(
            string message,
            HttpStatusCode? statusCode = null,
            Exception innerException = null,
            DateTimeOffset? retryAt = null)
            : base(message, NewsBlurFailureKind.Transient, statusCode, innerException, retryAt)
        {
        }
    }

    public sealed class NewsBlurRateLimitedException : NewsBlurException
    {
        public NewsBlurRateLimitedException(string message, DateTimeOffset? retryAt = null)
            : base(message, NewsBlurFailureKind.RateLimited, HttpStatusCode.TooManyRequests, retryAt: retryAt)
        {
        }
    }

    public sealed class NewsBlurOfflineException : NewsBlurException
    {
        public NewsBlurOfflineException(string message, Exception innerException = null)
            : base(message, NewsBlurFailureKind.Offline, innerException: innerException)
        {
        }
    }

    public sealed class NewsBlurMalformedResponseException : NewsBlurException
    {
        public NewsBlurMalformedResponseException(string message, Exception innerException)
            : base(message, NewsBlurFailureKind.MalformedResponse, innerException: innerException)
        {
        }
    }

    public sealed class NewsBlurTimeoutException : NewsBlurException
    {
        public NewsBlurTimeoutException(string message, Exception innerException)
            : base(message, NewsBlurFailureKind.Timeout, innerException: innerException)
        {
        }
    }
}
