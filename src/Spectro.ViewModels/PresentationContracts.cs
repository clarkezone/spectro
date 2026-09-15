using Spectro.Sync;

namespace Spectro.Presentation;

public enum SessionFailureKind
{
    None,
    Validation,
    Authentication,
    Offline,
    Transient,
    Permanent
}

public sealed record SessionResult(
    bool IsAuthenticated,
    string? AccountId = null,
    SessionFailureKind FailureKind = SessionFailureKind.None,
    string? Message = null);

public interface ISessionService
{
    Task<SessionResult> RestoreAsync(CancellationToken cancellationToken);

    Task<SessionResult> LoginAsync(
        string username,
        string password,
        CancellationToken cancellationToken);

    Task SignOutAsync(CancellationToken cancellationToken);
}

public interface ISynchronizationService
{
    Task<SyncResult> SynchronizeAsync(string accountId, CancellationToken cancellationToken);
}

public interface IExternalLauncher
{
    Task OpenAsync(Uri uri);
}

public enum AppTheme
{
    System,
    Light,
    Dark
}

public sealed record ReaderSettings(
    AppTheme Theme = AppTheme.System,
    double TextSize = 18,
    double ReadingWidth = 720,
    Spectro.Domain.StoryFilter DefaultFilter = Spectro.Domain.StoryFilter.Unread,
    int RetentionDays = 90,
    bool DownloadImages = true);

public interface ISettingsStore
{
    ReaderSettings Load();

    void Save(ReaderSettings settings);
}
