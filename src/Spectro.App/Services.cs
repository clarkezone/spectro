using Microsoft.Web.WebView2.Core;
using NewsBlurSharp;
using NewsBlurSharp.Model;
using Spectro.Domain;
using Spectro.Presentation;
using Spectro.Sync;
using Windows.Security.Credentials;

namespace Spectro.WinUI;

internal sealed class SynchronizationService(
    IContentRepository repository,
    ISyncRemoteService remoteService,
    ISettingsStore settingsStore) : ISynchronizationService
{
    public Task<SyncResult> SynchronizeAsync(
        string accountId,
        CancellationToken cancellationToken)
    {
        var synchronizer = new OfflineFirstSynchronizer(
            repository,
            remoteService,
            new SyncOptions
            {
                RetentionAge = TimeSpan.FromDays(settingsStore.Load().RetentionDays)
            });
        return synchronizer.SynchronizeAsync(new SyncRequest(accountId), cancellationToken);
    }
}

internal sealed class PasswordVaultSessionStore
{
    private const string Resource = "Spectro.NewsBlur.Session";
    private const string CredentialName = "session-cookie";
    private readonly PasswordVault _vault = new();

    public string? Read()
    {
        try
        {
            var credential = _vault.Retrieve(Resource, CredentialName);
            credential.RetrievePassword();
            return string.IsNullOrWhiteSpace(credential.Password) ? null : credential.Password;
        }
        catch (Exception exception) when (
            exception is System.Runtime.InteropServices.COMException
            or KeyNotFoundException)
        {
            return null;
        }
    }

    public void Write(string sessionCookie)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionCookie);
        Clear();
        _vault.Add(new PasswordCredential(Resource, CredentialName, sessionCookie));
    }

    public void Clear()
    {
        try
        {
            _vault.Remove(_vault.Retrieve(Resource, CredentialName));
        }
        catch (Exception exception) when (
            exception is System.Runtime.InteropServices.COMException
            or KeyNotFoundException)
        {
        }
    }
}

internal sealed class NewsBlurSessionService(
    INewsBlurClient client,
    PasswordVaultSessionStore sessionStore,
    WebViewDataCleaner webViewDataCleaner) : ISessionService
{
    public async Task<SessionResult> RestoreAsync(CancellationToken cancellationToken)
    {
        var cookie = sessionStore.Read();
        if (string.IsNullOrWhiteSpace(cookie))
        {
            return new SessionResult(false);
        }

        client.SetCookieSessionId(cookie);
        try
        {
            var profile = await client.GetUserProfileAsync(cancellationToken);
            if (!profile.Authenticated)
            {
                sessionStore.Clear();
                return new SessionResult(
                    false,
                    FailureKind: SessionFailureKind.Authentication,
                    Message: "Your saved NewsBlur session expired. Sign in again.");
            }

            var accountId = profile.UserId > 0
                ? profile.UserId.ToString(System.Globalization.CultureInfo.InvariantCulture)
                : profile.UserProfile?.UserId.ToString(
                    System.Globalization.CultureInfo.InvariantCulture);
            return new SessionResult(true, accountId ?? "newsblur");
        }
        catch (Exception exception)
        {
            var failure = MapFailure(exception);
            if (failure == SessionFailureKind.Authentication)
            {
                sessionStore.Clear();
            }

            if (failure is SessionFailureKind.Offline or SessionFailureKind.Transient)
            {
                return new SessionResult(
                    true,
                    "newsblur",
                    failure,
                    RestoreMessage(failure));
            }

            return new SessionResult(
                false,
                FailureKind: failure,
                Message: RestoreMessage(failure));
        }
    }

    public async Task<SessionResult> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(password))
        {
            return new SessionResult(
                false,
                FailureKind: SessionFailureKind.Validation,
                Message: "Enter both your NewsBlur username and password.");
        }

        try
        {
            var response = await client.LoginAsync(username, password, cancellationToken);
            if (!response.IsSuccess || string.IsNullOrWhiteSpace(response.AuthCookieToken))
            {
                return new SessionResult(
                    false,
                    FailureKind: SessionFailureKind.Authentication,
                    Message: "NewsBlur did not accept those credentials.");
            }

            sessionStore.Write(response.AuthCookieToken);
            return new SessionResult(
                true,
                response.UserId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        catch (Exception exception)
        {
            var failure = MapFailure(exception);
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

    private static SessionFailureKind MapFailure(Exception exception) =>
        exception switch
        {
            NewsBlurAuthenticationException => SessionFailureKind.Authentication,
            NewsBlurOfflineException => SessionFailureKind.Offline,
            NewsBlurTransientException or NewsBlurTimeoutException => SessionFailureKind.Transient,
            ArgumentException => SessionFailureKind.Validation,
            _ => SessionFailureKind.Permanent
        };

    private static string RestoreMessage(SessionFailureKind failure) =>
        failure switch
        {
            SessionFailureKind.Offline => "You're offline. Connect to restore your NewsBlur session.",
            SessionFailureKind.Transient => "NewsBlur is temporarily unavailable. Try again shortly.",
            SessionFailureKind.Authentication => "Your saved session expired. Sign in again.",
            _ => "Spectro could not restore your session. Sign in again."
        };
}

internal sealed class WindowsSettingsStore : ISettingsStore
{
    private readonly Windows.Storage.ApplicationDataContainer _settings =
        Windows.Storage.ApplicationData.Current.LocalSettings;

    public WindowsSettingsStore(bool demo = false)
    {
        if (demo)
            _settings = _settings.CreateContainer("demo", Windows.Storage.ApplicationDataCreateDisposition.Always);
        RemoveLegacyPlaintextAuthentication();
    }

    public ReaderSettings Load() =>
        new(
            ParseEnum(ReadString("theme"), AppTheme.System),
            ReadDouble("textSize", 18),
            ReadDouble("readingWidth", 720),
            ParseEnum(ReadString("defaultFilter"), StoryFilter.Unread),
            ReadInt("retentionDays", 90),
            _settings.Values["downloadImages"] is not false);

    public void Save(ReaderSettings settings)
    {
        _settings.Values["theme"] = settings.Theme.ToString();
        _settings.Values["textSize"] = settings.TextSize;
        _settings.Values["readingWidth"] = settings.ReadingWidth;
        _settings.Values["defaultFilter"] = settings.DefaultFilter.ToString();
        _settings.Values["retentionDays"] = settings.RetentionDays;
        _settings.Values["downloadImages"] = settings.DownloadImages;
    }

    private string? ReadString(string key) => _settings.Values[key] as string;

    private double ReadDouble(string key, double fallback) =>
        _settings.Values[key] is double value ? value : fallback;

    private int ReadInt(string key, int fallback) =>
        _settings.Values[key] is int value ? value : fallback;

    private static T ParseEnum<T>(string? value, T fallback) where T : struct, Enum =>
        Enum.TryParse<T>(value, ignoreCase: true, out var parsed) ? parsed : fallback;

    private static void RemoveLegacyPlaintextAuthentication()
    {
        var applicationData = Windows.Storage.ApplicationData.Current;
        applicationData.LocalSettings.Values.Remove("AuthenticationDetailsKey");
        applicationData.LocalSettings.Values.Remove("UserProfileKey");
        applicationData.RoamingSettings.Values.Remove("AuthenticationDetailsKey");
        applicationData.RoamingSettings.Values.Remove("UserProfileKey");
    }
}

internal sealed class WebViewDataCleaner
{
    private CoreWebView2Profile? _profile;
    private string? _readerDirectory;

    public void Register(CoreWebView2 webView, string readerDirectory)
    {
        _profile = webView.Profile;
        _readerDirectory = readerDirectory;
    }

    public async Task ClearAsync()
    {
        if (_profile is not null)
        {
            await _profile.ClearBrowsingDataAsync();
        }
        if (_readerDirectory is not null && Directory.Exists(_readerDirectory))
            foreach (var document in Directory.EnumerateFiles(_readerDirectory, "*.html"))
                File.Delete(document);
    }
}

#if DEBUG
internal sealed class DemoModeService(IContentRepository repository)
    : ISessionService, ISynchronizationService
{
    private bool _seeded;

    public async Task<SessionResult> RestoreAsync(CancellationToken cancellationToken)
    {
        await SeedAsync(cancellationToken);
        return new SessionResult(true, "demo");
    }

    public Task<SessionResult> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken) =>
        Task.FromResult(new SessionResult(
            false,
            FailureKind: SessionFailureKind.Permanent,
            Message: "Demo mode does not accept credentials."));

    public Task SignOutAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    public async Task<SyncResult> SynchronizeAsync(
        string accountId,
        CancellationToken cancellationToken)
    {
        await SeedAsync(cancellationToken);
        var now = DateTimeOffset.UtcNow;
        return new SyncResult(
            SyncOutcome.Succeeded,
            new SyncState("demo", SyncStage.Completed, 0, 15, 12, 0),
            now,
            now);
    }

    private async Task SeedAsync(CancellationToken cancellationToken)
    {
        if (_seeded && (await repository.GetFeedsAsync(cancellationToken)).Count > 0)
        {
            return;
        }

        await repository.InitializeAsync(cancellationToken);
        await repository.ClearAccountDataAsync(cancellationToken);
        await DemoLibrary.SeedAsync(repository, cancellationToken);
        _seeded = true;
    }
}
#endif
