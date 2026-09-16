using Microsoft.Web.WebView2.Core;
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
        IProgress<SyncState>? progress,
        CancellationToken cancellationToken)
    {
        var synchronizer = new OfflineFirstSynchronizer(
            repository,
            remoteService,
            new SyncOptions
            {
                RetentionAge = TimeSpan.FromDays(settingsStore.Load().RetentionDays)
            });
        return synchronizer.SynchronizeAsync(new SyncRequest(accountId), progress, cancellationToken);
    }
}

internal sealed class PasswordVaultSessionStore : ISessionCookieStore
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
        catch (Exception exception) when (IsNotFound(exception))
        {
            return null;
        }
    }

    public void Write(string sessionCookie)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sessionCookie);
        // PasswordVault replaces an existing resource/user entry; keep it if Add fails.
        _vault.Add(new PasswordCredential(Resource, CredentialName, sessionCookie));
    }

    public void Clear()
    {
        try
        {
            _vault.Remove(_vault.Retrieve(Resource, CredentialName));
        }
        catch (Exception exception) when (IsNotFound(exception))
        {
        }
    }

    private static bool IsNotFound(Exception exception) =>
        exception.HResult == unchecked((int)0x80070490); // HRESULT_FROM_WIN32(ERROR_NOT_FOUND)
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

internal sealed class WebViewDataCleaner : ISessionDataCleaner
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
        IProgress<SyncState>? progress,
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
