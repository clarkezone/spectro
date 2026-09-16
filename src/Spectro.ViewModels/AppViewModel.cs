using System.Collections.ObjectModel;
using System.Threading.Channels;
using Spectro.Domain;
using Spectro.Sync;

namespace Spectro.Presentation;

public enum AppScreen
{
    Loading,
    Login,
    Reader
}

public sealed class AppViewModel : ObservableObject
{
    private readonly IContentRepository _repository;
    private readonly ISessionService _sessionService;
    private readonly ISynchronizationService _synchronizationService;
    private readonly ISettingsStore _settingsStore;
    private AppScreen _screen = AppScreen.Loading;
    private string _username = string.Empty;
    private string _password = string.Empty;
    private string? _accountId;
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
    private int _libraryRevision;
    private StoryFilter _activeFilter;
    private string? _folderScope;
    private IReadOnlyList<FeedFolder> _folders = [];

    public AppViewModel(
        IContentRepository repository,
        ISessionService sessionService,
        ISynchronizationService synchronizationService,
        ISettingsStore settingsStore)
    {
        _repository = repository;
        _sessionService = sessionService;
        _synchronizationService = synchronizationService;
        _settingsStore = settingsStore;
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
            }
        }
    }

    public bool IsLoadingScreen => Screen == AppScreen.Loading;
    public bool IsLoginScreen => Screen == AppScreen.Login;
    public bool IsReaderScreen => Screen == AppScreen.Reader;

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

    public bool CanInteract => !IsBusy;
    public bool CanSynchronize => !IsBusy && !IsSyncing;
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
        try
        {
            await Task.Run(() => _repository.InitializeAsync(cancellationToken), cancellationToken);
            var session = await _sessionService.RestoreAsync(cancellationToken);
            if (!session.IsAuthenticated)
            {
                Screen = AppScreen.Login;
                SetSessionFailure(session);
                return;
            }

            _accountId = session.AccountId;
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

    public async Task LoginAsync(CancellationToken cancellationToken = default)
    {
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

            _accountId = result.AccountId;
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
        SelectedStory = null;
        ReaderHtml = string.Empty;
    }

    public async Task SelectStoryAsync(
        Story? story,
        CancellationToken cancellationToken = default)
    {
        var version = ++_selectionVersion;
        if (story is not null)
        {
            var details = await Task.Run(() => _repository.QueryStoriesAsync(
                new ContentQuery(StoryFilter.All, Limit: 1, StoryHash: story.Hash), cancellationToken), cancellationToken);
            if (version != _selectionVersion) return;
            story = details.FirstOrDefault()
                ?? throw new InvalidOperationException("This story is no longer downloaded. Sync or choose another story.");
        }
        if (story is { IsRead: false })
        {
            await Task.Run(() => _repository.SetStoryReadAsync(story.Hash, true, cancellationToken), cancellationToken);
            if (version != _selectionVersion) return;
            story = story with { IsRead = true };
            SelectedStory = story;
            await RefreshLocalAsync(cancellationToken);
        }
        if (version != _selectionVersion) return;
        SelectedStory = story;
        ReaderHtml = story is null ? string.Empty : ReaderHtmlBuilder.Build(story, Settings);
    }

    public async Task ToggleReadAsync(
        Story story,
        CancellationToken cancellationToken = default)
    {
        await Task.Run(() => _repository.SetStoryReadAsync(story.Hash, !story.IsRead, cancellationToken), cancellationToken);
        if (SelectedStory?.Hash == story.Hash) SelectedStory = SelectedStory with { IsRead = !story.IsRead };
        await RefreshLocalAsync(cancellationToken);
    }

    public async Task ToggleSavedAsync(
        Story story,
        CancellationToken cancellationToken = default)
    {
        await Task.Run(() => _repository.SetStorySavedAsync(story.Hash, !story.IsSaved, cancellationToken), cancellationToken);
        if (SelectedStory?.Hash == story.Hash) SelectedStory = SelectedStory with { IsSaved = !story.IsSaved };
        await RefreshLocalAsync(cancellationToken);
    }

    public void UpdateSettings(ReaderSettings settings)
    {
        Settings = settings with
        {
            TextSize = Math.Clamp(settings.TextSize, 14, 30),
            ReadingWidth = Math.Clamp(settings.ReadingWidth, 520, 1100),
            RetentionDays = Math.Clamp(settings.RetentionDays, 7, 365)
        };
        _settingsStore.Save(Settings);
        if (SelectedStory is not null)
        {
            ReaderHtml = ReaderHtmlBuilder.Build(SelectedStory, Settings);
        }
    }

    public async Task ResetLocalDataAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        try
        {
            CancelSync();
            if (_activeSync is not null) await _activeSync;
            ++_refreshVersion;
            ++_queryVersion;
            ClearSelection();
            await Task.Run(() => _repository.ClearAccountDataAsync(cancellationToken), cancellationToken);
            await RefreshLocalAsync(cancellationToken);
            StatusTitle = "Local data reset";
            StatusMessage = "Your local library is empty. Sync to download it again.";
        }
        finally { IsBusy = false; }
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        try
        {
            CancelSync();
            if (_activeSync is not null) await _activeSync;
            ++_refreshVersion;
            ++_queryVersion;
            ClearSelection();
            await _sessionService.SignOutAsync(cancellationToken);
            await Task.Run(() => _repository.ClearAccountDataAsync(cancellationToken), cancellationToken);
            _accountId = null;
            NavigationItems.Clear();
            Stories.Clear();
            PublishLibrary();
            SelectedStory = null;
            ReaderHtml = string.Empty;
            Username = string.Empty;
            Password = string.Empty;
            Screen = AppScreen.Login;
            StatusTitle = "Signed out";
            StatusMessage = "Local account data and browsing data were cleared.";
        }
        finally
        {
            IsBusy = false;
        }
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
