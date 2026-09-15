using System.Collections.ObjectModel;
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
            }
        }
    }

    public bool CanInteract => !IsBusy;

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
            await _repository.InitializeAsync(cancellationToken);
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
            await SynchronizeAsync(isInitial: true, cancellationToken);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SynchronizeAsync(
        bool isInitial = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_accountId) || IsBusy && !isInitial)
        {
            return;
        }

        IsBusy = true;
        StatusTitle = "Syncing";
        StatusMessage = "Updating feeds and stories…";
        try
        {
            var result = await _synchronizationService.SynchronizeAsync(
                _accountId,
                cancellationToken);
            await RefreshLocalAsync(cancellationToken);
            ApplySyncResult(result);
        }
        finally
        {
            IsBusy = false;
        }
    }

    public async Task SelectNavigationAsync(
        NavigationItem item,
        CancellationToken cancellationToken = default)
    {
        SelectedStory = null;
        ReaderHtml = string.Empty;
        SelectedNavigation = item;
        await LoadStoriesAsync(item, cancellationToken);
    }

    public async Task SelectStoryAsync(
        Story? story,
        CancellationToken cancellationToken = default)
    {
        if (story is { IsRead: false })
        {
            await _repository.SetStoryReadAsync(story.Hash, true, cancellationToken);
            story = story with { IsRead = true };
            SelectedStory = story;
            await RefreshLocalAsync(cancellationToken);
        }
        SelectedStory = story;
        ReaderHtml = story is null ? string.Empty : ReaderHtmlBuilder.Build(story, Settings);
    }

    public async Task ToggleReadAsync(
        Story story,
        CancellationToken cancellationToken = default)
    {
        await _repository.SetStoryReadAsync(story.Hash, !story.IsRead, cancellationToken);
        if (SelectedStory?.Hash == story.Hash) SelectedStory = story with { IsRead = !story.IsRead };
        await RefreshLocalAsync(cancellationToken);
    }

    public async Task ToggleSavedAsync(
        Story story,
        CancellationToken cancellationToken = default)
    {
        await _repository.SetStorySavedAsync(story.Hash, !story.IsSaved, cancellationToken);
        if (SelectedStory?.Hash == story.Hash) SelectedStory = story with { IsSaved = !story.IsSaved };
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
        await _repository.ClearAccountDataAsync(cancellationToken);
        SelectedStory = null;
        ReaderHtml = string.Empty;
        await RefreshLocalAsync(cancellationToken);
        StatusTitle = "Local data reset";
        StatusMessage = "Your local library is empty. Sync to download it again.";
    }

    public async Task SignOutAsync(CancellationToken cancellationToken = default)
    {
        IsBusy = true;
        try
        {
            await _sessionService.SignOutAsync(cancellationToken);
            await _repository.ClearAccountDataAsync(cancellationToken);
            _accountId = null;
            NavigationItems.Clear();
            Stories.Clear();
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
        var unreadCounts = await _repository.GetLocalUnreadCountsAsync(cancellationToken);
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
            UnreadCount: unreadCounts.Values.Sum()));
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

        var folders = await _repository.GetFeedFoldersAsync(cancellationToken);
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
                UnreadCount: group.Feeds.Sum(feed => unreadCounts.GetValueOrDefault(feed.Id))));
            foreach (var feed in group.Feeds)
            {
                NavigationItems.Add(new(
                    $"feed:{feed.Id}",
                    feed.Title,
                    NavigationItemKind.Feed,
                    FeedId: feed.Id,
                    UnreadCount: unreadCounts.GetValueOrDefault(feed.Id),
                    Depth: 1));
            }
        }

        foreach (var feed in (await _repository.GetFeedsAsync(cancellationToken))
                     .Where(feed => !folderFeedIds.Contains(feed.Id)))
        {
            NavigationItems.Add(new(
                $"feed:{feed.Id}",
                feed.Title,
                NavigationItemKind.Feed,
                FeedId: feed.Id,
                UnreadCount: unreadCounts.GetValueOrDefault(feed.Id)));
        }

        var selected = NavigationItems.FirstOrDefault(item =>
            item.Id == SelectedNavigation?.Id) ?? DefaultNavigation();
        await LoadStoriesAsync(selected, cancellationToken);
    }

    private async Task LoadStoriesAsync(
        NavigationItem navigation,
        CancellationToken cancellationToken)
    {
        SelectedNavigation = navigation;
        var stories = await _repository.QueryStoriesAsync(
            new ContentQuery(
                navigation.Filter,
                navigation.FeedId,
                navigation.FolderId),
            cancellationToken);
        Stories.Clear();
        foreach (var story in stories)
        {
            Stories.Add(story);
        }

        StatusTitle = Stories.Count == 0 ? "Nothing here yet" : navigation.Title;
        StatusMessage = Stories.Count == 0
            ? "Sync to download stories, or choose another filter."
            : $"{Stories.Count} local stor{(Stories.Count == 1 ? "y" : "ies")}";
        if (SelectedStory is not null)
        {
            // Reading a story removes it from Unread, but must not close the article.
            SelectedStory = Stories.FirstOrDefault(item => item.Hash == SelectedStory.Hash) ?? SelectedStory;
            ReaderHtml = SelectedStory is null
                ? string.Empty
                : ReaderHtmlBuilder.Build(SelectedStory, Settings);
        }
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
            SyncOutcome.Succeeded => ("Up to date", $"Synced {result.State.StoryCount} stories."),
            SyncOutcome.Offline => ("Offline", "Showing downloaded stories. Changes will sync later."),
            SyncOutcome.AuthenticationRequired => ("Session expired", "Sign out, then sign in again."),
            SyncOutcome.TransientFailure => ("Sync delayed", "NewsBlur is temporarily unavailable. Your local library is safe."),
            SyncOutcome.MalformedRemoteData => ("Sync error", "NewsBlur returned data Spectro could not read."),
            SyncOutcome.PermanentFailure => ("Sync error", result.ErrorMessage ?? "The sync could not be completed."),
            _ => ("Sync canceled", "Your local library was not changed.")
        };
        OnPropertyChanged(nameof(StatusTitle));
        OnPropertyChanged(nameof(StatusMessage));
    }
}
