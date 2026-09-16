using System.Globalization;
using NewsBlurSharp;
using NewsBlurSharp.Model;
using Spectro.Presentation;

namespace Spectro.WinUI;

internal interface ISessionCookieStore
{
    string? Read();
    void Write(string sessionCookie);
    void Clear();
}

internal interface ISessionDataCleaner
{
    Task ClearAsync();
}

internal sealed class NewsBlurSessionService(
    INewsBlurClient client,
    ISessionCookieStore sessionStore,
    ISessionDataCleaner webViewDataCleaner) : ISessionService
{
    public async Task<SessionResult> RestoreAsync(CancellationToken cancellationToken)
    {
        string? cookie;
        try
        {
            cookie = sessionStore.Read();
        }
        catch (Exception exception)
        {
            System.Diagnostics.Trace.TraceError(
                $"Read saved session failed: {exception.GetType().Name} (0x{exception.HResult:X8})");
            return new SessionResult(
                false,
                FailureKind: SessionFailureKind.Permanent,
                Message: "Windows could not read your saved NewsBlur session. It has not been removed. Close and reopen Spectro to retry.");
        }

        if (string.IsNullOrWhiteSpace(cookie))
            return new SessionResult(false);

        client.SetCookieSessionId(cookie);
        try
        {
            var profile = await client.GetUserProfileAsync(cancellationToken);
            // The client rejects explicit authenticated:false and HTTP authentication failures.
            // A default false from a missing field is an unexpected response, not revocation.
            if (!profile.Authenticated)
                throw new NewsBlurMalformedResponseException(
                    "NewsBlur returned an incomplete session profile.", null);

            var accountId = profile.UserId > 0
                ? profile.UserId
                : profile.UserProfile?.UserId;
            return new SessionResult(
                true, accountId is > 0 ? accountId.Value.ToString(CultureInfo.InvariantCulture) : "newsblur");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var failure = MapFailure(exception);
            System.Diagnostics.Trace.TraceWarning(
                $"Restore session failed: {DescribeFailure(exception)}");
            if (failure == SessionFailureKind.Authentication)
            {
                sessionStore.Clear();
                client.SetCookieSessionId(string.Empty);
                return new SessionResult(false, FailureKind: failure, Message: RestoreMessage(failure));
            }

            // An unverified saved session can still open the local library. Remote calls
            // continue to enforce authentication; only explicit rejection removes the secret.
            return new SessionResult(true, "newsblur", failure, RestoreMessage(failure));
        }
    }

    public async Task<SessionResult> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
            return new SessionResult(
                false,
                FailureKind: SessionFailureKind.Validation,
                Message: "Enter both your NewsBlur username and password.");

        try
        {
            var response = await client.LoginAsync(username, password, cancellationToken);
            if (!response.IsSuccess)
                return new SessionResult(
                    false,
                    FailureKind: SessionFailureKind.Authentication,
                    Message: "NewsBlur did not accept those credentials.");

            if (string.IsNullOrWhiteSpace(response.AuthCookieToken))
                return new SessionResult(
                    false,
                    FailureKind: SessionFailureKind.Permanent,
                    Message: "NewsBlur did not return a session to save. Try signing in again.");

            try
            {
                sessionStore.Write(response.AuthCookieToken);
            }
            catch (Exception exception)
            {
                System.Diagnostics.Trace.TraceError(
                    $"Save session failed: {exception.GetType().Name} (0x{exception.HResult:X8})");
                return new SessionResult(
                    false,
                    FailureKind: SessionFailureKind.Permanent,
                    Message: "Windows could not securely save your NewsBlur session. Try signing in again.");
            }

            return new SessionResult(true, response.UserId.ToString(CultureInfo.InvariantCulture));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception exception)
        {
            var failure = MapFailure(exception);
            System.Diagnostics.Trace.TraceWarning(
                $"Sign in failed: {DescribeFailure(exception)}");
            return new SessionResult(
                false,
                FailureKind: failure,
                Message: failure switch
                {
                    SessionFailureKind.Authentication => "NewsBlur did not accept those credentials.",
                    SessionFailureKind.Offline => "Connect to the internet, then try again.",
                    SessionFailureKind.Transient => "NewsBlur is temporarily unavailable. Try again shortly.",
                    _ => "Spectro could not sign in. Check your connection and try again."
                });
        }
    }

    public async Task SignOutAsync(CancellationToken cancellationToken)
    {
        try
        {
            await client.LogoutAsync(cancellationToken);
        }
        catch (NewsBlurException)
        {
        }
        finally
        {
            client.SetCookieSessionId(string.Empty);
            sessionStore.Clear();
            await webViewDataCleaner.ClearAsync();
        }
    }

    internal static string DescribeFailure(Exception exception)
    {
        var details = new List<string>();
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            var detail = $"{current.GetType().Name} (0x{current.HResult:X8})";
            if (current is HttpRequestException request)
            {
                detail += $", HttpRequestError={request.HttpRequestError}";
                if (request.StatusCode is { } status)
                    detail += $", HTTP={(int)status}";
            }
            else if (current is System.Net.Sockets.SocketException socket)
            {
                detail += $", SocketError={socket.SocketErrorCode}";
            }
            details.Add(detail);
        }
        // Exception messages, URLs, response bodies and Data can contain authentication secrets.
        return string.Join(" -> ", details);
    }

    private static SessionFailureKind MapFailure(Exception exception) =>
        exception switch
        {
            // A forbidden request can be middleware policy, not an expired session.
            NewsBlurAuthenticationException { StatusCode: System.Net.HttpStatusCode.Forbidden }
                => SessionFailureKind.Permanent,
            NewsBlurAuthenticationException => SessionFailureKind.Authentication,
            NewsBlurOfflineException => SessionFailureKind.Offline,
            NewsBlurTransientException or NewsBlurTimeoutException or NewsBlurRateLimitedException
                => SessionFailureKind.Transient,
            ArgumentException => SessionFailureKind.Validation,
            _ => SessionFailureKind.Permanent
        };

    private static string RestoreMessage(SessionFailureKind failure) =>
        failure switch
        {
            SessionFailureKind.Offline => "You're offline. Showing your downloaded library.",
            SessionFailureKind.Transient => "NewsBlur is temporarily unavailable. Your saved session has been kept.",
            SessionFailureKind.Authentication => "Your saved session expired. Sign in again.",
            _ => "Spectro could not verify your session. Your saved session has been kept; try syncing again."
        };
}
