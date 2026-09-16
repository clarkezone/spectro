using System.Net;
using System.Runtime.InteropServices;
using Moq;
using NewsBlurSharp;
using NewsBlurSharp.Model;
using Spectro.Domain;
using Spectro.Presentation;
using Spectro.WinUI;
using Xunit;

namespace Spectro.ViewModels.Tests;

public sealed class AuthenticationTests
{
    [Fact]
    public async Task FreshClientAndServiceRestoreCookieSavedByLogin()
    {
        var store = new MemoryCookieStore();
        var first = Create(store, request =>
        {
            Assert.Equal("/api/login", request.RequestUri!.AbsolutePath);
            var response = Json("""{"authenticated":true,"user_id":42}""");
            response.Headers.TryAddWithoutValidation(
                "Set-Cookie", "newsblur_sessionid=fixture-session; Path=/; Secure; HttpOnly");
            return response;
        });
        var login = await first.LoginAsync("fixture-reader", "fixture-password", default);
        Assert.True(login.IsAuthenticated);
        Assert.Equal(1, store.Writes);
        Assert.Equal(0, store.Clears);

        // A new transport, client, and service have no in-memory login state.
        var restarted = Create(store, request =>
        {
            Assert.Equal("/social/load_user_profile", request.RequestUri!.AbsolutePath);
            Assert.Contains(request.Headers.GetValues("Cookie"),
                value => value == "newsblur_sessionid=fixture-session");
            return Json("""{"authenticated":true,"user_id":42,"user_profile":{"user_id":42}}""");
        });
        var restored = await restarted.RestoreAsync(default);

        Assert.True(restored.IsAuthenticated);
        Assert.Equal("42", restored.AccountId);
        Assert.Equal(0, store.Clears);
        Assert.Equal(1, store.Writes);
    }

    [Fact]
    public async Task UnauthorizedHttpAuthenticationClearsSavedSession()
    {
        var store = SavedCookie();
        var subject = Create(store, _ => new HttpResponseMessage(HttpStatusCode.Unauthorized));

        var result = await subject.RestoreAsync(default);

        Assert.False(result.IsAuthenticated);
        Assert.Equal(SessionFailureKind.Authentication, result.FailureKind);
        Assert.Equal(1, store.Clears);
        Assert.Null(store.Read());
    }

    [Fact]
    public async Task ExplicitFalseAuthenticationClearsEvenWithProfileUserId()
    {
        var store = SavedCookie();
        var subject = Create(store, _ =>
            Json("""{"authenticated":false,"user_profile":{"user_id":42}}"""));

        var result = await subject.RestoreAsync(default);

        Assert.False(result.IsAuthenticated);
        Assert.Equal(SessionFailureKind.Authentication, result.FailureKind);
        Assert.Equal(1, store.Clears);
    }

    [Theory]
    [InlineData(408, SessionFailureKind.Transient)]
    [InlineData(429, SessionFailureKind.Transient)]
    [InlineData(500, SessionFailureKind.Transient)]
    [InlineData(503, SessionFailureKind.Transient)]
    [InlineData(404, SessionFailureKind.Permanent)]
    [InlineData(403, SessionFailureKind.Permanent)]
    public async Task NonAuthenticationHttpFailureKeepsSessionForNextRestart(
        int status, SessionFailureKind expectedFailure)
    {
        var store = SavedCookie();
        var subject = Create(store, _ => new HttpResponseMessage((HttpStatusCode)status));

        var result = await subject.RestoreAsync(default);

        Assert.True(result.IsAuthenticated);
        Assert.Equal(expectedFailure, result.FailureKind);
        Assert.NotEmpty(result.Message!);
        Assert.Equal(0, store.Clears);
        var restarted = Create(store, _ => Json("""{"authenticated":true,"user_id":42}"""));
        Assert.Equal("42", (await restarted.RestoreAsync(default)).AccountId);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("""{"user_profile":{"user_id":42}}""")]
    [InlineData("null")]
    [InlineData("<html>Temporarily unavailable</html>")]
    public async Task UnexpectedProfileIsNotEvidenceToDeleteSavedSession(string payload)
    {
        var store = SavedCookie();

        var result = await Create(store, _ => Json(payload)).RestoreAsync(default);

        Assert.True(result.IsAuthenticated);
        Assert.Equal(SessionFailureKind.Permanent, result.FailureKind);
        Assert.NotEmpty(result.Message!);
        Assert.Equal(0, store.Clears);
        Assert.True(store.HasCookie);
    }

    [Fact]
    public async Task OfflineRestoreKeepsSavedSession()
    {
        var store = SavedCookie();
        var subject = Create(store, _ => throw new HttpRequestException(
            HttpRequestError.NameResolutionError, "Fixture host unavailable."));

        var result = await subject.RestoreAsync(default);

        Assert.True(result.IsAuthenticated);
        Assert.Equal(SessionFailureKind.Offline, result.FailureKind);
        Assert.Equal(0, store.Clears);
    }

    [Fact]
    public async Task TimedOutRestoreKeepsSavedSession()
    {
        var store = SavedCookie();
        var client = new NewsBlurClient(new WaitingHandler(), requestTimeout: TimeSpan.FromMilliseconds(20));

        var result = await new NewsBlurSessionService(client, store, new NoOpCleaner()).RestoreAsync(default);

        Assert.True(result.IsAuthenticated);
        Assert.Equal(SessionFailureKind.Transient, result.FailureKind);
        Assert.Equal(0, store.Clears);
    }

    [Fact]
    public async Task CallerCancellationDoesNotInvalidateSession()
    {
        var store = SavedCookie();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();
        var client = new NewsBlurClient(new WaitingHandler());
        var subject = new NewsBlurSessionService(client, store, new NoOpCleaner());

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => subject.RestoreAsync(cancellation.Token));

        Assert.Equal(0, store.Clears);
    }

    [Fact]
    public async Task MissingCredentialDoesNotCallNetwork()
    {
        var subject = Create(new MemoryCookieStore(), _ =>
            throw new InvalidOperationException("No request expected."));

        Assert.False((await subject.RestoreAsync(default)).IsAuthenticated);
    }

    [Fact]
    public async Task VaultReadFailureIsNotReportedAsMissingCredential()
    {
        var store = new MemoryCookieStore { FailReads = true };
        var subject = new NewsBlurSessionService(
            new NewsBlurClient(new Handler(_ => throw new InvalidOperationException("No request expected."))),
            store, new NoOpCleaner());

        var result = await subject.RestoreAsync(default);

        Assert.False(result.IsAuthenticated);
        Assert.Equal(SessionFailureKind.Permanent, result.FailureKind);
        Assert.Contains("could not read", result.Message);
        Assert.Equal(0, store.Clears);
    }

    [Fact]
    public async Task FailedSecureSaveIsExplicitAndDoesNotReportSuccessfulLogin()
    {
        var store = SavedCookie();
        store.FailWrites = true;
        var subject = Create(store, _ =>
        {
            var response = Json("""{"authenticated":true,"user_id":42}""");
            response.Headers.TryAddWithoutValidation(
                "Set-Cookie", "newsblur_sessionid=fixture-new-session; Path=/; Secure; HttpOnly");
            return response;
        });

        var result = await subject.LoginAsync("fixture-reader", "fixture-password", default);

        Assert.False(result.IsAuthenticated);
        Assert.Equal(SessionFailureKind.Permanent, result.FailureKind);
        Assert.Contains("securely save", result.Message);
        Assert.Equal(0, store.Clears);
        Assert.True(store.HasCookie);
    }

    [Fact]
    public async Task RejectedLoginDoesNotOverwriteSavedSession()
    {
        var store = SavedCookie();
        var result = await Create(store, _ => Json("""{"authenticated":false,"user_id":0}"""))
            .LoginAsync("fixture-reader", "fixture-password", default);

        Assert.False(result.IsAuthenticated);
        Assert.Equal(SessionFailureKind.Authentication, result.FailureKind);
        Assert.Equal(0, store.Writes);
        Assert.Equal(0, store.Clears);
    }

    [Fact]
    public async Task LoginWithoutSessionCookieIsUnexpectedNotRejectedCredentials()
    {
        var store = new MemoryCookieStore();
        var result = await Create(store, _ => Json("""{"authenticated":true,"user_id":42}"""))
            .LoginAsync("fixture-reader", "fixture-password", default);

        Assert.False(result.IsAuthenticated);
        Assert.Equal(SessionFailureKind.Permanent, result.FailureKind);
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public async Task LoginRateLimitIsTransientAndDoesNotClearSavedSession()
    {
        var store = SavedCookie();
        var result = await Create(store, _ => new HttpResponseMessage(HttpStatusCode.TooManyRequests))
            .LoginAsync("fixture-reader", "fixture-password", default);

        Assert.False(result.IsAuthenticated);
        Assert.Equal(SessionFailureKind.Transient, result.FailureKind);
        Assert.Equal(0, store.Writes);
        Assert.Equal(0, store.Clears);
    }

    [Theory]
    [InlineData(HttpRequestError.NameResolutionError, SessionFailureKind.Offline)]
    [InlineData(HttpRequestError.ConnectionError, SessionFailureKind.Transient)]
    [InlineData(HttpRequestError.SecureConnectionError, SessionFailureKind.Transient)]
    public async Task LoginDistinguishesDnsFailureFromConnectionAndTlsFailures(
        HttpRequestError error, SessionFailureKind expected)
    {
        var store = SavedCookie();
        var result = await Create(store, _ =>
                throw new HttpRequestException(error, "Fixture transport failure."))
            .LoginAsync("fixture-reader", "fixture-password", default);

        Assert.False(result.IsAuthenticated);
        Assert.Equal(expected, result.FailureKind);
        Assert.Equal(0, store.Clears);
        Assert.Equal(0, store.Writes);
    }

    [Fact]
    public void AuthenticationDiagnosticsIncludeErrorCodesButNoExceptionContent()
    {
        const string sensitiveFixture = "fixture-private-message-cookie-password-profile-url";
        var socket = new System.Net.Sockets.SocketException(
            (int)System.Net.Sockets.SocketError.HostNotFound);
        var request = new HttpRequestException(
            HttpRequestError.NameResolutionError, sensitiveFixture, socket);
        request.Data["sensitive"] = sensitiveFixture;
        var exception = new NewsBlurOfflineException(sensitiveFixture, request);

        var details = NewsBlurSessionService.DescribeFailure(exception);

        Assert.Contains(nameof(NewsBlurOfflineException), details);
        Assert.Contains("HttpRequestError=NameResolutionError", details);
        Assert.Contains("SocketError=HostNotFound", details);
        Assert.Contains("0x", details);
        Assert.DoesNotContain(sensitiveFixture, details);
        Assert.DoesNotContain(request.Message, details);
        Assert.DoesNotContain(socket.Message, details);
    }

    [Fact]
    public void AuthenticationDiagnosticsIncludeOnlyNumericHttpStatus()
    {
        var exception = new HttpRequestException(
            HttpRequestError.HttpProtocolError, "fixture response content",
            statusCode: HttpStatusCode.ServiceUnavailable);

        var details = NewsBlurSessionService.DescribeFailure(exception);

        Assert.Contains("HttpRequestError=HttpProtocolError", details);
        Assert.Contains("HTTP=503", details);
        Assert.DoesNotContain(exception.Message, details);
    }

    [Theory]
    [InlineData("", "fixture-password")]
    [InlineData(" ", "fixture-password")]
    [InlineData("fixture-reader", "")]
    public async Task IncompleteInputDoesNotStartAuthentication(string username, string password)
    {
        var subject = Create(new MemoryCookieStore(), _ =>
            throw new InvalidOperationException("No request expected."));

        var result = await subject.LoginAsync(username, password, default);

        Assert.False(result.IsAuthenticated);
        Assert.Equal(SessionFailureKind.Validation, result.FailureKind);
    }

    [Theory]
    [InlineData("", "fixture-password")]
    [InlineData("fixture-reader", "")]
    public async Task IncompleteEnterShowsValidationAndPreservesInput(string username, string password)
    {
        var settings = new Mock<ISettingsStore>();
        settings.Setup(value => value.Load()).Returns(new ReaderSettings());
        var session = new Mock<ISessionService>(MockBehavior.Strict);
        var viewModel = new AppViewModel(
            Mock.Of<IContentRepository>(), session.Object,
            Mock.Of<ISynchronizationService>(), settings.Object)
        {
            Username = username,
            Password = password
        };
        var submission = new LoginSubmission();
        var handled = false;

        await submission.OnKeyDownAsync(true, true, () => handled = true, () =>
            submission.RunAsync(viewModel.CanInteract, () => viewModel.LoginAsync()));

        Assert.True(handled);
        Assert.Equal("Check your details", viewModel.StatusTitle);
        Assert.True(viewModel.Username == username);
        Assert.True(viewModel.Password == password);
        session.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task SharedSubmissionGuardRejectsOverlappingEnterOrClickAndBusyState()
    {
        var submission = new LoginSubmission();
        var pending = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var attempts = 0;
        Task Submit() { attempts++; return pending.Task; }

        await submission.RunAsync(false, Submit);
        Assert.Equal(0, attempts);
        var first = submission.RunAsync(true, Submit);
        await submission.RunAsync(true, Submit);
        Assert.Equal(1, attempts);
        pending.SetResult();
        await first;
        await submission.RunAsync(true, () => { attempts++; return Task.CompletedTask; });
        Assert.Equal(2, attempts);
    }

    [Fact]
    public async Task SubmissionGuardRecoversAfterFailure()
    {
        var submission = new LoginSubmission();
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            submission.RunAsync(true, () => throw new InvalidOperationException("Fixture failure.")));
        var attempted = false;

        await submission.RunAsync(true, () => { attempted = true; return Task.CompletedTask; });

        Assert.True(attempted);
    }

    [Theory]
    [InlineData(false, true, false)]
    [InlineData(true, false, false)]
    [InlineData(true, true, true)]
    public async Task EnterIsHandledBeforeSubmissionOnlyOnLoginScreen(
        bool isEnter, bool isLoginScreen, bool expected)
    {
        var submission = new LoginSubmission();
        var handled = false;
        var attempted = false;

        await submission.OnKeyDownAsync(isEnter, isLoginScreen, () => handled = true, () =>
        {
            Assert.True(handled);
            attempted = true;
            return Task.CompletedTask;
        });

        Assert.Equal(expected, handled);
        Assert.Equal(expected, attempted);
    }

    [Fact]
    public async Task BusyEnterIsConsumedWithoutStartingAnotherLogin()
    {
        var submission = new LoginSubmission();
        var handled = false;
        var attempted = false;

        await submission.OnKeyDownAsync(true, true, () => handled = true, () =>
            submission.RunAsync(false, () => { attempted = true; return Task.CompletedTask; }));

        Assert.True(handled);
        Assert.False(attempted);
    }

    private static NewsBlurSessionService Create(
        ISessionCookieStore store, Func<HttpRequestMessage, HttpResponseMessage> response) =>
        new(new NewsBlurClient(new Handler(response)), store, new NoOpCleaner());

    private static MemoryCookieStore SavedCookie() => new("fixture-session");

    private static HttpResponseMessage Json(string content) =>
        new(HttpStatusCode.OK) { Content = new StringContent(content) };

    private sealed class Handler(Func<HttpRequestMessage, HttpResponseMessage> response) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken) =>
            Task.FromResult(response(request));
    }

    private sealed class WaitingHandler : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            throw new InvalidOperationException("No response expected.");
        }
    }

    private sealed class NoOpCleaner : ISessionDataCleaner
    {
        public Task ClearAsync() => Task.CompletedTask;
    }

    private sealed class MemoryCookieStore(string? initialCookie = null) : ISessionCookieStore
    {
        private string? _cookie = initialCookie;
        public bool HasCookie => !string.IsNullOrEmpty(_cookie);
        public bool FailReads { get; set; }
        public bool FailWrites { get; set; }
        public int Writes { get; private set; }
        public int Clears { get; private set; }
        public string? Read() => FailReads
            ? throw new COMException("Fixture vault failure.", unchecked((int)0x80070005))
            : _cookie;
        public void Write(string sessionCookie)
        {
            if (FailWrites) throw new COMException("Fixture vault failure.");
            Assert.False(string.IsNullOrWhiteSpace(sessionCookie));
            Writes++;
            _cookie = sessionCookie;
        }
        public void Clear() { Clears++; _cookie = null; }
    }
}
