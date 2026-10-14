using System.Collections.ObjectModel;
using System.Threading.Channels;
using Spectro.Domain;
using Spectro.Sync;

namespace Spectro.Presentation;

public enum AppScreen
{
    Loading,
    Login,
    Reader,
    StartupError
}

public sealed class AppViewModel : ObservableObject
{
    public const string LibraryAccountCheckpointName = AccountDataCheckpoints.LibraryAccount;
    public const string SessionFingerprintCheckpointName = AccountDataCheckpoints.SessionFingerprint;
    public const string PendingSignOutCheckpointName = AccountDataCheckpoints.SignOut;
    private readonly IContentRepository _repository;
    private readonly ISessionService _sessionService;
    private readonly ISynchronizationService _synchronizationService;
    private readonly ISettingsStore _settingsStore;
    private readonly Func<Task> _clearAccountArtifacts;
    private string? _libraryBackupPath;
    private AppScreen _screen = AppScreen.Loading;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private string? _accountId;
    private string? _sessionFingerprint;
    private bool _needsSignOutCleanup;
    private string _statusTitle = "Loading";
    private string _statusMessage = "Opening your local library…";
    private bool _isBusy;
    private bool _isOffline;
    private bool _isStale;
    private bool _isSettingsOpen;
    private NavigationItem? _selectedNavigation;
    private Story? _selectedStory;
    private ReaderSettings _settings;
    private string _readerHtml = string.Empty;
    private bool _isSyncing;
    private string _syncProgressText = string.Empty;
    private CancellationTokenSource? _syncCancellation;
    private Task? _activeSync;
    private int _queryVersion;
    private int _refreshVersion;
    private int _selectionVersion;
    private int _readerVersion;
    private int _accountGeneration;
    private readonly SemaphoreSlim _localMutationGate = new(1, 1);
    private int _libraryRevision;
    private StoryFilter _activeFilter;
    private string? _folderScope;
    private IReadOnlyList<FeedFolder> _folders = [];

    public AppViewModel(
        IContentRepository repository,
        ISessionService sessionService,
        ISynchronizationService synchronizationService,
        ISettingsStore settingsStore,
        Func<Task>? clearAccountArtifacts = null)
    {
        _repository = repository;
        _sessionService = sessionService;
        _synchronizationService = synchronizationService;
        _settingsStore = settingsStore;
        _clearAccountArtifacts = clearAccountArtifacts ?? (() => Task.CompletedTask);
        _settings = settingsStore.Load();
    }

    public ObservableCollection<NavigationItem> NavigationItems { get; } = [];

    public ObservableCollection<Story> Stories { get; } = [];

    public AppScreen Screen
    {
        get => _screen;
        private set
        {
            if (SetProperty(ref _screen, value))
            {
                OnPropertyChanged(nameof(IsLoadingScreen));
                OnPropertyChanged(nameof(IsLoginScreen));
                OnPropertyChanged(nameof(IsReaderScreen));
                OnPropertyChanged(nameof(IsStartupErrorScreen));
            }
        }
    }

    public bool IsLoadingScreen => Screen == AppScreen.Loading;
    public bool IsLoginScreen => Screen == AppScreen.Login;
    public bool IsReaderScreen => Screen == AppScreen.Reader;
    public bool IsStartupErrorScreen => Screen == AppScreen.StartupError;
    public bool NeedsSignOutCleanup => _needsSignOutCleanup;
    public string? LibraryBackupPath => _libraryBackupPath;
    public string LibraryBackupMessage => $"Earlier local content and unsynced changes were preserved in a database backup at {_libraryBackupPath}.";

    public string Username
    {
        get => _username;
        set => SetProperty(ref _username, value);
    }

    public string Password
    {
        get => _password;
        set => SetProperty(ref _password, value);
    }

    public string StatusTitle
    {
        get => _statusTitle;
        private set => SetProperty(ref _statusTitle, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        private set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetProperty(ref _isBusy, value))
            {
                OnPropertyChanged(nameof(CanInteract));
                OnPropertyChanged(nameof(CanSynchronize));
            }
        }
    }

    public bool CanInteract => !IsBusy && !NeedsSignOutCleanup;
    public bool CanSynchronize => CanInteract && !IsSyncing;
    public bool IsSyncing
    {
        get => _isSyncing;
        private set
        {
            if (SetProperty(ref _isSyncing, value))
                OnPropertyChanged(nameof(CanSynchronize));
        }
    }
    public string SyncProgressText
    {
        get => _syncProgressText;
        private set => SetProperty(ref _syncProgressText, value);
    }
    public int LibraryRevision => _libraryRevision;
    public StoryFilter ActiveFilter => _activeFilter;

    public bool IsOffline
    {
        get => _isOffline;
        private set => SetProperty(ref _isOffline, value);
    }

    public bool IsStale
    {
        get => _isStale;
        private set => SetProperty(ref _isStale, value);
    }

    public bool IsSettingsOpen
    {
        get => _isSettingsOpen;
        set => SetProperty(ref _isSettingsOpen, value);
    }

    public NavigationItem? SelectedNavigation
    {
        get => _selectedNavigation;
        set => SetProperty(ref _selectedNavigation, value);
    }

    public Story? SelectedStory
    {
        get => _selectedStory;
        private set => SetProperty(ref _selectedStory, value);
    }

    public ReaderSettings Settings
    {
        get => _settings;
        private set => SetProperty(ref _settings, value);
    }

    public string ReaderHtml
    {
        get => _readerHtml;
        private set => SetProperty(ref _readerHtml, value);
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        Screen = AppScreen.Loading;
        try
        {
            await Task.Run(() => _repository.InitializeAsync(cancellationToken), cancellationToken);
            if (await Task.Run(() => _repository.GetCheckpointAsync(PendingSignOutCheckpointName, cancellationToken), cancellationToken) is { Value: not "complete" })
            {
                SetCleanupPending(true);
                Screen = AppScreen.Login;
                SetCleanupFailureStatus();
                return;
            }
            var session = await _sessionService.RestoreAsync(cancellationToken);
            if (!session.IsAuthenticated)
            {
                Screen = AppScreen.Login;
                SetSessionFailure(session);
                return;
            }

            _accountId = await BindLibraryAsync(session, cancellationToken);
            if (_accountId is null) return;
            _sessionFingerprint = session.SessionFingerprint;
            Screen = AppScreen.Reader;
            await RefreshLocalAsync(cancellationToken);
            IsBusy = false;
            await SynchronizeAsync(isInitial: true, cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { throw; }
        catch (Exception exception)
        {
            ClearPresentation();
            Screen = AppScreen.StartupError;
            ReportError("Open library", exception);
            StatusMessage = "Your existing library has been kept. Retry opening it, or reset the local database to preserve a backup and start again.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task LoginAsync(CancellationToken cancellationToken = default)
    {
        if (!CanInteract) return;
        if (string.IsNullOrWhiteSpace(Username) || string.IsNullOrEmpty(Password))
        {
            StatusTitle = "Check your details";
            StatusMessage = "Enter both your NewsBlur username and password.";
            return;
        }

        IsBusy = true;
        try
        {
            var result = await _sessionService.LoginAsync(
                Username.Trim(),
                Password,
                cancellationToken);
            Password = string.Empty;
            if (!result.IsAuthenticated)
            {
                SetSessionFailure(result);
                return;
            }

            _accountId = await BindLibraryAsync(result, cancellationToken);
            if (_accountId is null) return;
            _sessionFingerprint = result.SessionFingerprint;
            Screen = AppScreen.Reader;
            await RefreshLocalAsync(cancellationToken);
            IsBusy = false;
            await SynchronizeAsync(isInitial: true, cancellationToken);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<string?> BindLibraryAsync(SessionResult session, CancellationToken cancellationToken)
    {
        var owner = await Task.Run(() => _repository.GetCheckpointAsync(LibraryAccountCheckpointName, cancellationToken), cancellationToken);
        var credential = await Task.Run(() => _repository.GetCheckpointAsync(SessionFingerprintCheckpointName, cancellationToken), cancellationToken);
        var verified = session.FailureKind == SessionFailureKind.None
            && !string.IsNullOrWhiteSpace(session.AccountId) && session.AccountId != "newsblur";
        if (!verified)
        {
            if (owner is not null && !string.IsNullOrEmpty(session.SessionFingerprint)
                && session.SessionFingerprint == credential?.Value)
                return owner.Value;

            Screen = AppScreen.StartupError;
            IsStale = true;
            StatusTitle = "Connect to verify your account";
            StatusMessage = "Your saved session and library have been kept. Connect to the internet, then retry so Spectro can verify which account owns this library.";
            return null;
        }

        if (owner is not null && owner.Value != session.AccountId
            || owner is null && (await Task.Run(() => _repository.GetCachedStoryIndexAsync(cancellationToken), cancellationToken)).Count > 0)
        {
            // Unclaimed legacy caches cannot safely be assigned to a newly verified account.
            // Preserve their content and unsynced changes instead of discarding or uploading them.
            ++_accountGeneration;
            ClearPresentation();
            var reset = await ResetDatabaseAfterMutationsAsync(cancellationToken);
            SetLibraryBackup(reset.PreservedDatabasePath);
            await _clearAccountArtifacts();
        }
        await Task.Run(() => _repository.SetCheckpointAsync(new SyncCheckpoint(
            LibraryAccountCheckpointName, session.AccountId!, DateTimeOffset.UtcNow), cancellationToken), cancellationToken);
        if (session.SessionFingerprint is not null)
            await Task.Run(() => _repository.SetCheckpointAsync(new SyncCheckpoint(
                SessionFingerprintCheckpointName, session.SessionFingerprint, DateTimeOffset.UtcNow), cancellationToken), cancellationToken);
        return session.AccountId;
    }

    public async Task RecoverDatabaseAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        try
        {
            ++_accountGeneration;
            ClearPresentation();
            var reset = await ResetDatabaseAfterMutationsAsync(cancellationToken);
            SetLibraryBackup(reset.PreservedDatabasePath);
            await _clearAccountArtifacts();
            await InitializeAsync(cancellationToken);
        }
        finally { IsBusy = false; }
    }

    public Task SynchronizeAsync(
        bool isInitial = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_accountId) || !CanSynchronize)
            return _activeSync ?? Task.CompletedTask;
        _activeSync = SynchronizeCoreAsync(_accountId, cancellationToken);
        return _activeSync;
    }

    public void CancelSync() => _syncCancellation?.Cancel();

    private async Task SynchronizeCoreAsync(string accountId, CancellationToken cancellationToken)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _syncCancellation = cancellation;
        IsSyncing = true;
        StatusTitle = "Syncing";
        SyncProgressText = "Connecting to NewsBlur...";
        var channel = Channel.CreateBounded<SyncState>(new BoundedChannelOptions(1)
        {
            FullMode = BoundedChannelFullMode.DropOldest,
            SingleReader = true
        });
        var progress = new SyncProgress(channel.Writer);
        var worker = Task.Run(async () =>
        {
            try { return await _synchronizationService.SynchronizeAsync(accountId, progress, cancellation.Token); }
            finally { channel.Writer.TryComplete(); }
        });
        try
        {
            var revision = 0;
            while (await channel.Reader.WaitToReadAsync())
            {
                SyncState? latest = null;
                while (channel.Reader.TryRead(out var state)) latest = state;
                if (latest is null) continue;
                SyncProgressText = DescribeProgress(latest);
                if (latest.LocalRevision > revision)
                {
                    await RefreshLocalAsync(CancellationToken.None);
                    revision = latest.LocalRevision;
                }
                await Task.Delay(200);
            }
            var result = await worker;
            await RefreshLocalAsync(CancellationToken.None);
            ApplySyncResult(result);
            if (result.IsSuccess && (await Task.Run(() =>
                    _repository.GetPendingMutationsAsync(1, CancellationToken.None))).Count > 0)
            {
                StatusTitle = "Changes waiting to sync";
                StatusMessage = "Your latest reading changes are saved locally. Sync again to upload them.";
            }
        }
        catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
        {
            await RefreshLocalAsync(CancellationToken.None);
            StatusTitle = "Sync canceled";
            StatusMessage = "Downloaded stories and local changes are safe. Sync again to continue.";
        }
        catch (Exception exception)
        {
            cancellation.Cancel();
            try { await worker; }
            catch (Exception workerException)
            {
                System.Diagnostics.Trace.TraceError($"Sync worker failed: {workerException.GetType().Name} (0x{workerException.HResult:X8})");
            }
            ReportError("Sync", exception);
        }
        finally
        {
            _syncCancellation = null;
            IsSyncing = false;
        }
    }

    private sealed class SyncProgress(ChannelWriter<SyncState> writer) : IProgress<SyncState>
    {
        public void Report(SyncState value) => writer.TryWrite(value);
    }

    private static string DescribeProgress(SyncState state)
    {
        var stage = state.Stage switch
        {
            SyncStage.Initialize => "Opening local library",
            SyncStage.UploadPendingMutations => "Uploading reading changes",
            SyncStage.RefreshFeedsAndFolders => "Downloading subscriptions",
            SyncStage.FetchUnreadState => "Updating unread counts",
            SyncStage.FetchStories => $"{state.CompletedFeedCount}/{state.FeedCount} feeds, {state.StoryCount} stories",
            SyncStage.Reconcile => "Reconciling reading changes",
            SyncStage.Checkpoint => "Finishing sync",
            SyncStage.Completed => "Sync complete",
            _ => "Connecting to NewsBlur"
        };
        if (state.Stage == SyncStage.FetchStories && state.CurrentFeedTitle is not null)
            stage += $" - {state.CurrentFeedTitle}, page {state.CurrentPage}";
        return state.RetryAttempt > 1 ? $"{stage} - retry {state.RetryAttempt}" : stage;
    }

    public void ReportError(string action, Exception exception)
    {
        System.Diagnostics.Trace.TraceError($"{action} failed: {exception.GetType().Name} (0x{exception.HResult:X8})");
        IsStale = true;
        StatusTitle = $"{action} could not finish";
        StatusMessage = "Your downloaded library is still available. Try again; if this persists, check your connection and available disk space.";
    }

    public async Task SelectNavigationAsync(
        NavigationItem item,
        CancellationToken cancellationToken = default)
    {
        ClearSelection();
        if (item.Kind == NavigationItemKind.Filter)
        {
            _activeFilter = item.Filter;
            _folderScope = null;
        }
        else if (item.Kind == NavigationItemKind.Folder)
            _folderScope = item.FolderId;
        SelectedNavigation = item;
        OnPropertyChanged(nameof(ActiveFilter));
        await LoadStoriesAsync(item, cancellationToken);
    }

    public async Task SelectFilterAsync(StoryFilter filter, CancellationToken cancellationToken = default)
    {
        ClearSelection();
        _activeFilter = filter;
        OnPropertyChanged(nameof(ActiveFilter));
        var selected = SelectedNavigation;
        if (selected is null || selected.Kind == NavigationItemKind.Filter)
            selected = NavigationItems.First(item => item.Kind == NavigationItemKind.Filter && item.Filter == filter);
        SelectedNavigation = selected;
        await LoadStoriesAsync(selected, cancellationToken);
    }

    public IReadOnlyList<NavigationItem> GetVisibleFeeds(string search = "")
    {
        var scope = _folderScope is null ? null : _folders
            .FirstOrDefault(group => group.Folder.Id == _folderScope)?.Feeds.Select(feed => feed.Id).ToHashSet();
        return NavigationItems.Where(item => item.Kind == NavigationItemKind.Feed)
            .DistinctBy(item => item.FeedId)
            .Where(item => scope is null || scope.Contains(item.FeedId!.Value))
            .Where(item => _activeFilter switch
            {
                StoryFilter.Unread => item.UnreadCount > 0,
                StoryFilter.Saved => item.SavedCount > 0,
                StoryFilter.Read => item.StoryCount > item.UnreadCount,
                _ => true
            })
            .Where(item => item.Title.Contains(search.Trim(), StringComparison.CurrentCultureIgnoreCase)).ToArray();
    }

    private void ClearSelection()
    {
        ++_selectionVersion;
        ++_readerVersion;
        SelectedStory = null;
        ReaderHtml = string.Empty;
    }

    public async Task SelectStoryAsync(
        Story? story,
        CancellationToken cancellationToken = default)
    {
        var generation = _accountGeneration;
        var version = ++_selectionVersion;
        if (story is not null)
        {
            var details = await Task.Run(() => _repository.QueryStoriesAsync(
                new ContentQuery(StoryFilter.All, Limit: 1, StoryHash: story.Hash), cancellationToken), cancellationToken);
            if (version != _selectionVersion || generation != _accountGeneration) return;
            story = details.FirstOrDefault()
                ?? throw new InvalidOperationException("This story is no longer downloaded. Sync or choose another story.");
        }
        if (story is { IsRead: false })
        {
            if (!await MutateStoryAsync(generation,
                () => _repository.SetStoryReadAsync(story.Hash, true, cancellationToken), cancellationToken)) return;
            if (version != _selectionVersion || generation != _accountGeneration) return;
            story = story with { IsRead = true };
            SelectedStory = story;
            await RefreshLocalAsync(cancellationToken);
        }
        if (version != _selectionVersion) return;
        SelectedStory = story;
        await RefreshReaderHtmlAsync(cancellationToken);
    }

    public async Task ToggleReadAsync(
        Story story,
        CancellationToken cancellationToken = default)
    {
        var generation = _accountGeneration;
        if (!await MutateStoryAsync(generation,
            () => _repository.SetStoryReadAsync(story.Hash, !story.IsRead, cancellationToken), cancellationToken)) return;
        if (SelectedStory?.Hash == story.Hash) SelectedStory = SelectedStory with { IsRead = !story.IsRead };
        await RefreshLocalAsync(cancellationToken);
    }

    public async Task ToggleSavedAsync(
        Story story,
        CancellationToken cancellationToken = default)
    {
        var generation = _accountGeneration;
        if (!await MutateStoryAsync(generation,
            () => _repository.SetStorySavedAsync(story.Hash, !story.IsSaved, cancellationToken), cancellationToken)) return;
        if (SelectedStory?.Hash == story.Hash) SelectedStory = SelectedStory with { IsSaved = !story.IsSaved };
        await RefreshLocalAsync(cancellationToken);
    }

    public async Task UpdateSettingsAsync(ReaderSettings settings)
    {
        Settings = settings with
        {
            TextSize = Math.Clamp(settings.TextSize, 14, 30),
            ReadingWidth = Math.Clamp(settings.ReadingWidth, 520, 1100),
            RetentionDays = Math.Clamp(settings.RetentionDays, 7, 365)
        };
        _settingsStore.Save(Settings);
        await RefreshReaderHtmlAsync();
    }

    private async Task<bool> MutateStoryAsync(int generation, Func<Task> mutation, CancellationToken cancellationToken)
    {
        if (IsBusy || NeedsSignOutCleanup) return false;
        await _localMutationGate.WaitAsync(cancellationToken);
        try
        {
            if (generation != _accountGeneration || IsBusy || NeedsSignOutCleanup) return false;
            await Task.Run(mutation, cancellationToken);
            return generation == _accountGeneration;
        }
        finally { _localMutationGate.Release(); }
    }

    private async Task ClearAccountAfterMutationsAsync(CancellationToken cancellationToken, bool clearBackups = false)
    {
        await _localMutationGate.WaitAsync(cancellationToken);
        try
        {
            await CleanupTasks.RunAllAsync(
                () => Task.Run(() => _repository.ClearAccountDataAsync(cancellationToken), cancellationToken),
                () => clearBackups ? Task.Run(() => _repository.ClearPreservedDatabasesAsync(cancellationToken), cancellationToken) : Task.CompletedTask);
        }
        finally { _localMutationGate.Release(); }
    }

    private async Task<DatabaseResetResult> ResetDatabaseAfterMutationsAsync(CancellationToken cancellationToken)
    {
        await _localMutationGate.WaitAsync(cancellationToken);
        try { return await Task.Run(() => _repository.ResetDatabaseAsync(cancellationToken), cancellationToken); }
        finally { _localMutationGate.Release(); }
    }

    private async Task RefreshReaderHtmlAsync(CancellationToken cancellationToken = default)
    {
        var version = ++_readerVersion;
        var selection = _selectionVersion;
        var story = SelectedStory;
        var html = story is null ? string.Empty : await ReaderHtmlBuilder.BuildAsync(story, Settings, cancellationToken: cancellationToken);
        if (version == _readerVersion && selection == _selectionVersion) ReaderHtml = html;
    }

    public async Task ResetLocalDataAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        try
        {
            ++_accountGeneration;
            CancelSync();
            if (_activeSync is not null) await _activeSync;
            ++_refreshVersion;
            ++_queryVersion;
            ClearSelection();
            await CleanupTasks.RunAllAsync(
                () => ClearAccountAfterMutationsAsync(cancellationToken),
                _clearAccountArtifacts);
            if (_accountId is not null)
                await BindLibraryAsync(new SessionResult(true, _accountId) { SessionFingerprint = _sessionFingerprint }, cancellationToken);
            await RefreshLocalAsync(cancellationToken);
            StatusTitle = "Local data reset";
            StatusMessage = "Your local library is empty. Sync to download it again.";
        }
        finally { IsBusy = false; }
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        SetCleanupPending(true);
        ++_accountGeneration;
        ClearSelection();
        try
        {
            await CleanupTasks.RunAllAsync(
                () => Task.Run(() => _repository.SetCheckpointAsync(new SyncCheckpoint(PendingSignOutCheckpointName, "pending", DateTimeOffset.UtcNow))),
                async () => { CancelSync(); if (_activeSync is not null) await _activeSync; },
                () => _sessionService.SignOutAsync(cancellationToken),
                () => ClearAccountAfterMutationsAsync(CancellationToken.None, clearBackups: true),
                _clearAccountArtifacts);
            await Task.Run(() => _repository.SetCheckpointAsync(new SyncCheckpoint(
                PendingSignOutCheckpointName, "complete", DateTimeOffset.UtcNow)));
            SetCleanupPending(false);
            StatusTitle = "Signed out";
            StatusMessage = "Local account data and browsing data were cleared.";
        }
        catch (Exception exception)
        {
            await RecordSignOutCleanupFailureAsync(exception);
            throw;
        }
        finally
        {
            _accountId = null;
            _sessionFingerprint = null;
            ClearPresentation();
            Username = string.Empty;
            Password = string.Empty;
            Screen = AppScreen.Login;
            IsBusy = false;
        }
    }

    public async Task RecordSignOutCleanupFailureAsync(Exception exception)
    {
        System.Diagnostics.Trace.TraceError($"Sign-out cleanup failed: {exception.GetType().Name} (0x{exception.HResult:X8})");
        SetCleanupPending(true);
        SetCleanupFailureStatus();
        try
        {
            await Task.Run(() => _repository.SetCheckpointAsync(new SyncCheckpoint(
                PendingSignOutCheckpointName, "pending", DateTimeOffset.UtcNow)));
        }
        catch (Exception persistenceError)
        {
            System.Diagnostics.Trace.TraceError($"Cleanup marker failed: {persistenceError.GetType().Name} (0x{persistenceError.HResult:X8})");
        }
    }

    private void SetCleanupPending(bool pending)
    {
        _needsSignOutCleanup = pending;
        OnPropertyChanged(nameof(NeedsSignOutCleanup));
        OnPropertyChanged(nameof(CanInteract));
        OnPropertyChanged(nameof(CanSynchronize));
    }

    private void SetCleanupFailureStatus()
    {
        IsStale = true;
        StatusTitle = "Sign-out cleanup needs another try";
        StatusMessage = "The reader is closed. Some account data could not be cleared. Retry cleanup before signing in again.";
    }

    private void ClearPresentation()
    {
        SetLibraryBackup(null);
        ++_refreshVersion;
        ++_queryVersion;
        ClearSelection();
        SelectedNavigation = null;
        _folderScope = null;
        _folders = [];
        NavigationItems.Clear();
        Stories.Clear();
        PublishLibrary();
    }

    private void SetLibraryBackup(string? path)
    {
        if (SetProperty(ref _libraryBackupPath, path, nameof(LibraryBackupPath)))
            OnPropertyChanged(nameof(LibraryBackupMessage));
    }

    private async Task RefreshLocalAsync(CancellationToken cancellationToken)
    {
        var version = ++_refreshVersion;
        var snapshot = await Task.Run(async () =>
        {
            var counts = await _repository.GetLocalFeedCountsAsync(cancellationToken);
            var folders = await _repository.GetFeedFoldersAsync(cancellationToken);
            var feeds = await _repository.GetFeedsAsync(cancellationToken);
            return (counts, folders, feeds);
        }, cancellationToken);
        if (version != _refreshVersion) return;
        var (counts, folders, feeds) = snapshot;
        _folders = folders;
        NavigationItems.Clear();
        NavigationItems.Add(new(
            "all",
            "All stories",
            NavigationItemKind.Filter,
            StoryFilter.All));
        NavigationItems.Add(new(
            "unread",
            "Unread",
            NavigationItemKind.Filter,
            StoryFilter.Unread,
            UnreadCount: counts.Values.Sum(count => count.Unread)));
        NavigationItems.Add(new(
            "saved",
            "Saved",
            NavigationItemKind.Filter,
            StoryFilter.Saved));
        NavigationItems.Add(new(
            "read",
            "Read stories",
            NavigationItemKind.Filter,
            StoryFilter.Read));

        var folderFeedIds = folders.SelectMany(static group => group.Feeds)
            .Select(static feed => feed.Id)
            .ToHashSet();
        foreach (var group in folders)
        {
            NavigationItems.Add(new(
                $"folder:{group.Folder.Id}",
                group.Folder.Title,
                NavigationItemKind.Folder,
                FolderId: group.Folder.Id,
                UnreadCount: group.Feeds.Sum(feed => counts.GetValueOrDefault(feed.Id)?.Unread ?? 0)));
            foreach (var feed in group.Feeds)
            {
                NavigationItems.Add(new(
                    $"feed:{feed.Id}",
                    feed.Title,
                    NavigationItemKind.Feed,
                    FeedId: feed.Id,
                    UnreadCount: counts.GetValueOrDefault(feed.Id)?.Unread ?? 0,
                    Depth: 1,
                    SavedCount: counts.GetValueOrDefault(feed.Id)?.Saved ?? 0,
                    StoryCount: counts.GetValueOrDefault(feed.Id)?.Total ?? 0));
            }
        }

        foreach (var feed in feeds
                     .Where(feed => !folderFeedIds.Contains(feed.Id)))
        {
            NavigationItems.Add(new(
                $"feed:{feed.Id}",
                feed.Title,
                NavigationItemKind.Feed,
                FeedId: feed.Id,
                UnreadCount: counts.GetValueOrDefault(feed.Id)?.Unread ?? 0,
                SavedCount: counts.GetValueOrDefault(feed.Id)?.Saved ?? 0,
                StoryCount: counts.GetValueOrDefault(feed.Id)?.Total ?? 0));
        }

        var selected = NavigationItems.FirstOrDefault(item =>
            item.Id == SelectedNavigation?.Id) ?? DefaultNavigation();
        if (SelectedNavigation is null)
        {
            _activeFilter = selected.Filter;
            OnPropertyChanged(nameof(ActiveFilter));
        }
        await LoadStoriesAsync(selected, cancellationToken);
    }

    private async Task LoadStoriesAsync(
        NavigationItem navigation,
        CancellationToken cancellationToken)
    {
        SelectedNavigation = navigation;
        var version = ++_queryVersion;
        var filter = _activeFilter;
        var stories = await Task.Run(() => _repository.QueryStoriesAsync(
            new ContentQuery(
                filter,
                navigation.FeedId,
                navigation.FolderId,
                IncludeContent: false),
            cancellationToken), cancellationToken);
        if (version != _queryVersion) return;
        Stories.Clear();
        foreach (var story in stories)
        {
            Stories.Add(story);
        }

        if (!IsSyncing && !IsStale)
        {
            StatusTitle = Stories.Count == 0 ? "Nothing here yet" : navigation.Title;
            StatusMessage = Stories.Count == 0
                ? "Sync to download stories, or choose another filter."
                : $"{Stories.Count} local stor{(Stories.Count == 1 ? "y" : "ies")}";
        }
        if (SelectedStory is not null)
        {
            // Reading a story removes it from Unread, but must not close the article.
            if (Stories.FirstOrDefault(item => item.Hash == SelectedStory.Hash) is { } preview)
                SelectedStory = SelectedStory with { IsRead = preview.IsRead, IsSaved = preview.IsSaved };
        }
        PublishLibrary();
    }

    private void PublishLibrary()
    {
        ++_libraryRevision;
        OnPropertyChanged(nameof(LibraryRevision));
    }

    private NavigationItem DefaultNavigation() =>
        NavigationItems.First(item => item.Filter == Settings.DefaultFilter);

    private void SetSessionFailure(SessionResult result)
    {
        StatusTitle = result.FailureKind switch
        {
            SessionFailureKind.Validation => "Check your details",
            SessionFailureKind.Authentication => "Sign-in failed",
            SessionFailureKind.Offline => "You're offline",
            SessionFailureKind.Transient => "NewsBlur is temporarily unavailable",
            _ => "Sign in to NewsBlur"
        };
        StatusMessage = result.Message ?? result.FailureKind switch
        {
            SessionFailureKind.Authentication => "The username or password was not accepted.",
            SessionFailureKind.Offline => "Connect to the internet and try again.",
            SessionFailureKind.Transient => "Wait a moment and try again.",
            _ => "Use your NewsBlur account to continue."
        };
    }

    private void ApplySyncResult(SyncResult result)
    {
        IsOffline = result.Outcome == SyncOutcome.Offline;
        IsStale = !result.IsSuccess;
        (StatusTitle, StatusMessage) = result.Outcome switch
        {
            SyncOutcome.Succeeded => ("Up to date", $"Synced {result.State.StoryCount} stories in {result.State.NetworkAttemptCount} requests."),
            SyncOutcome.Offline => ("Offline", "Showing downloaded stories. Changes will sync later."),
            SyncOutcome.AuthenticationRequired => ("Session expired", "Sign out, then sign in again."),
            SyncOutcome.TransientFailure => ("Sync delayed", SyncFailureMessage(result)),
            SyncOutcome.RateLimited => ("Rate limited by NewsBlur", SyncFailureMessage(result)),
            SyncOutcome.MalformedRemoteData => ("Sync error", $"NewsBlur returned data Spectro could not read. {result.ErrorMessage}"),
            SyncOutcome.PermanentFailure => ("Sync error", result.ErrorMessage ?? "The sync could not be completed."),
            _ => ("Sync canceled", "Downloaded stories and local changes are safe. Sync again to continue.")
        };
        OnPropertyChanged(nameof(StatusTitle));
        OnPropertyChanged(nameof(StatusMessage));
    }

    private static string SyncFailureMessage(SyncResult result)
    {
        var status = result.HttpStatusCode is { } code ? $"HTTP {code}. " : string.Empty;
        if (result.RetryAt is { } retryAt)
            return $"{status}Next sync allowed after {retryAt.ToLocalTime():g}. Your downloaded stories and changes are safe.";
        return $"{status}{result.ErrorMessage ?? "NewsBlur is temporarily unavailable."} Your local library is safe; try syncing again shortly.";
    }
}
