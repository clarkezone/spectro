using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.Web.WebView2.Core;
using Spectro.Domain;
using Spectro.Presentation;

namespace Spectro.WinUI;

public sealed partial class MainPage : Page
{
    private bool _readerInitialized;
    private bool _updatingSettings;
    private bool _bindingCollections;
    private bool _compactReaderOpen;
    private readonly string _readerDirectory = Path.Combine(
        Windows.Storage.ApplicationData.Current.TemporaryFolder.Path, "ArticleReader");
    private string? _readerAddress;
    private double? _restoreScroll;
    private ulong _readerNavigationId;
    private bool _focusReaderAfterNavigation;
    private Control? _settingsReturnFocus;

    public AppViewModel ViewModel { get; } = App.Services.ViewModel;

    public MainPage()
    {
        InitializeComponent();
        Root.AddHandler(
            UIElement.KeyDownEvent,
            new KeyEventHandler(Root_KeyDown),
            handledEventsToo: true);
        ViewModel.PropertyChanged += ViewModel_PropertyChanged;
        ActualThemeChanged += (_, _) => NavigateReader();
    }

    private void Root_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key != Windows.System.VirtualKey.D)
        {
            return;
        }

        var controlState = Microsoft.UI.Input.InputKeyboardSource
            .GetKeyStateForCurrentThread(Windows.System.VirtualKey.Control);
        if (!controlState.HasFlag(Windows.UI.Core.CoreVirtualKeyStates.Down))
        {
            return;
        }

        e.Handled = true;
        OpenSettings();
    }

    private async void MainPage_Loaded(object sender, RoutedEventArgs e)
    {
        UpdateScreen();
        await ViewModel.InitializeAsync();
        BindLocalCollections();
        UpdateScreen();
        SelectCurrentNavigation();
        ApplyResponsiveLayout(ActualWidth);
        await RefreshImagesAsync();
    }

    private async void SignIn_Click(object sender, RoutedEventArgs e)
    {
        LoginProgress.Visibility = Visibility.Visible;
        await ViewModel.LoginAsync();
        PasswordBox.Password = string.Empty;
        BindLocalCollections();
        LoginProgress.Visibility = Visibility.Collapsed;
        UpdateScreen();
        SelectCurrentNavigation();
        await RefreshImagesAsync();
    }

    private async void Sync_Click(object sender, RoutedEventArgs e)
    {
        await ViewModel.SynchronizeAsync();
        BindLocalCollections();
        await RefreshImagesAsync();
    }

    private void Settings_Click(object sender, RoutedEventArgs e) => OpenSettings();

    private void CloseSettings_Click(object sender, RoutedEventArgs e)
    {
        SettingsPanel.Visibility = Visibility.Collapsed;
        ReaderHost.IsEnabled = true;
        _settingsReturnFocus?.Focus(FocusState.Programmatic);
    }

    private void PasswordBox_PasswordChanged(object sender, RoutedEventArgs e) =>
        ViewModel.Password = PasswordBox.Password;

    private async void SignOut_Click(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmDestructiveActionAsync(
                "Sign out of NewsBlur?",
                "Downloaded stories and any changes that have not synced will be removed from this device.",
                "Sign out")) return;
        await EnsureArticleReaderInitializedAsync();
        await ViewModel.SignOutAsync();
        ClearReaderDocument();
        await App.Services.WebViewDataCleaner.ClearAsync();
        await App.Services.ClearImagesAsync();
        SettingsPanel.Visibility = Visibility.Collapsed;
        ReaderHost.IsEnabled = true;
        BindLocalCollections();
        UpdateScreen();
    }

    private async void ResetLocalData_Click(object sender, RoutedEventArgs e)
    {
        if (!await ConfirmDestructiveActionAsync(
                "Reset your downloaded library?",
                "This removes downloaded stories and unsynced changes from this device. Your NewsBlur account is not deleted.",
                "Reset library")) return;
        await ViewModel.ResetLocalDataAsync();
        ClearReaderDocument();
        await App.Services.WebViewDataCleaner.ClearAsync();
        await App.Services.ClearImagesAsync();
        BindLocalCollections();
        SelectCurrentNavigation();
    }

    private async Task<bool> ConfirmDestructiveActionAsync(string title, string content, string action)
    {
        var dialog = new ContentDialog
        {
            XamlRoot = XamlRoot,
            Title = title,
            Content = content,
            PrimaryButtonText = action,
            CloseButtonText = "Cancel",
            DefaultButton = ContentDialogButton.Close,
            RequestedTheme = RequestedTheme
        };
        return await dialog.ShowAsync() == ContentDialogResult.Primary;
    }

    private async void NavigationList_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (!_bindingCollections && NavigationList.SelectedItem is NavigationItemView item
            && item.Model.Id != ViewModel.SelectedNavigation?.Id)
        {
            await ViewModel.SelectNavigationAsync(item.Model);
            _compactReaderOpen = false;
            BindStories();
            SelectCurrentNavigation();
        }
    }

    private async void NavigationPicker_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (!_bindingCollections && NavigationPicker.SelectedItem is NavigationItemView item
            && item.Model.Id != ViewModel.SelectedNavigation?.Id)
        {
            await ViewModel.SelectNavigationAsync(item.Model);
            _compactReaderOpen = false;
            BindStories();
            SelectCurrentNavigation();
        }
    }

    private async void StoryList_SelectionChanged(
        object sender,
        SelectionChangedEventArgs e)
    {
        if (_bindingCollections) return;
        if (StoryList.SelectedItem is StoryItemView item)
        {
            _compactReaderOpen = true;
            _focusReaderAfterNavigation = true;
            await ViewModel.SelectStoryAsync(item.Model);
            // Defer collection replacement until WinUI has finished raising selection events.
            DispatcherQueue.TryEnqueue(BindLocalCollections);
            ShowReader();
        }
    }

    private void StoryList_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is StoryItemView item && item.Model.Hash == ViewModel.SelectedStory?.Hash)
        {
            _compactReaderOpen = true;
            ShowReader();
        }
    }

    private void StorySearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (StoryList is not null && !_bindingCollections) BindStories();
    }

    private void FeedSearch_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (FeedList is not null && !_bindingCollections) BindFeeds();
    }

    private void BindFeeds()
    {
        _bindingCollections = true;
        FeedList.Items.Clear();
        foreach (var item in ViewModel.NavigationItems.Where(item => item.Kind == NavigationItemKind.Feed)
                     .DistinctBy(item => item.FeedId)
                     .Where(item => item.Title.Contains(FeedSearch.Text.Trim(), StringComparison.CurrentCultureIgnoreCase)))
            FeedList.Items.Add(new NavigationItemView(item));
        _bindingCollections = false;
    }

    private async void FeedList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_bindingCollections || FeedList.SelectedItem is not NavigationItemView item) return;
        await ViewModel.SelectNavigationAsync(item.Model);
        _compactReaderOpen = false;
        BindStories();
        SelectCurrentNavigation();
    }

    private async void Filter_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is not string id) return;
        var item = ViewModel.NavigationItems.First(item => item.Id == id);
        await ViewModel.SelectNavigationAsync(item);
        _compactReaderOpen = false;
        BindStories();
        SelectCurrentNavigation();
    }

    private async void SelectedRead_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedStory is not { } story) return;
        await ViewModel.ToggleReadAsync(story);
        BindLocalCollections();
    }

    private async void SelectedSaved_Click(object sender, RoutedEventArgs e)
    {
        if (ViewModel.SelectedStory is not { } story) return;
        await ViewModel.ToggleSavedAsync(story);
        BindLocalCollections();
    }

    private async void OpenOriginal_Click(object sender, RoutedEventArgs e) => await OpenOriginalAsync();

    private async Task OpenOriginalAsync()
    {
        if (Uri.TryCreate(ViewModel.SelectedStory?.Permalink, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https")
            await Windows.System.Launcher.LaunchUriAsync(uri);
    }

    private async void ToggleRead_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is StoryItemView item)
        {
            await ViewModel.ToggleReadAsync(item.Model);
            BindLocalCollections();
        }
    }

    private async void ToggleSaved_Click(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.Tag is StoryItemView item)
        {
            await ViewModel.ToggleSavedAsync(item.Model);
            BindLocalCollections();
        }
    }

    private async void ArticleReader_Loaded(object sender, RoutedEventArgs e)
    {
        await EnsureArticleReaderInitializedAsync();
        NavigateReader();
    }

    private async Task EnsureArticleReaderInitializedAsync()
    {
        if (_readerInitialized)
        {
            return;
        }

        await ArticleReader.EnsureCoreWebView2Async();
        var settings = ArticleReader.CoreWebView2.Settings;
        settings.IsScriptEnabled = false;
        settings.IsWebMessageEnabled = false;
        settings.AreDefaultScriptDialogsEnabled = false;
        settings.AreDevToolsEnabled = false;
        settings.AreHostObjectsAllowed = false;
        settings.IsStatusBarEnabled = false;
        settings.AreBrowserAcceleratorKeysEnabled = false;
        Directory.CreateDirectory(_readerDirectory);
        ArticleReader.CoreWebView2.SetVirtualHostNameToFolderMapping(
            "reader.spectro.invalid", _readerDirectory, CoreWebView2HostResourceAccessKind.Deny);
        ArticleReader.CoreWebView2.NavigationCompleted += ArticleReader_NavigationCompleted;
        ArticleReader.CoreWebView2.NewWindowRequested += ArticleReader_NewWindowRequested;
        App.Services.WebViewDataCleaner.Register(ArticleReader.CoreWebView2, _readerDirectory);
        _readerInitialized = true;
    }

    private async void ArticleReader_NewWindowRequested(
        object? sender,
        CoreWebView2NewWindowRequestedEventArgs args)
    {
        args.Handled = true;
        if (Uri.TryCreate(args.Uri, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https")
        {
            await Windows.System.Launcher.LaunchUriAsync(uri);
        }
    }

    private async void ArticleReader_NavigationStarting(
        WebView2 sender,
        CoreWebView2NavigationStartingEventArgs args)
    {
        if (_readerAddress is not null
            && (args.Uri == _readerAddress || args.Uri?.StartsWith(_readerAddress + "#", StringComparison.Ordinal) == true))
        {
            _readerNavigationId = args.NavigationId;
            return;
        }
        if (args.Uri is null
            || args.Uri.StartsWith("about:", StringComparison.OrdinalIgnoreCase)
            || args.Uri.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        args.Cancel = true;
        if (Uri.TryCreate(args.Uri, UriKind.Absolute, out var uri)
            && uri.Scheme is "http" or "https" && uri.Host != "reader.spectro.invalid")
        {
            await Windows.System.Launcher.LaunchUriAsync(uri);
        }
    }

    private void ViewModel_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (!DispatcherQueue.HasThreadAccess)
        {
            DispatcherQueue.TryEnqueue(() => ViewModel_PropertyChanged(sender, e));
            return;
        }

        if (e.PropertyName is nameof(AppViewModel.Screen) or nameof(AppViewModel.IsBusy))
        {
            UpdateScreen();
        }
        else if (e.PropertyName == nameof(AppViewModel.ReaderHtml))
        {
            NavigateReader();
        }
        else if (e.PropertyName == nameof(AppViewModel.Settings))
        {
            ApplyTheme();
        }
        else if (e.PropertyName is nameof(AppViewModel.StatusMessage) or nameof(AppViewModel.StatusTitle)
                 or nameof(AppViewModel.IsStale) or nameof(AppViewModel.IsOffline))
        {
            UpdateStatus();
        }
        else if (e.PropertyName == nameof(AppViewModel.SelectedStory))
        {
            UpdateArticleActions();
        }
    }

    private void UpdateScreen()
    {
        LoadingPanel.Visibility = ViewModel.IsLoadingScreen ? Visibility.Visible : Visibility.Collapsed;
        LoginPanel.Visibility = ViewModel.IsLoginScreen ? Visibility.Visible : Visibility.Collapsed;
        ReaderPanel.Visibility = ViewModel.IsReaderScreen ? Visibility.Visible : Visibility.Collapsed;
        LoginProgress.Visibility = ViewModel.IsBusy && ViewModel.IsLoginScreen
            ? Visibility.Visible
            : Visibility.Collapsed;
        ApplyTheme();
        UpdateStatus();
    }

    private void ApplyTheme()
    {
        RequestedTheme = ViewModel.Settings.Theme switch
        {
            AppTheme.Light => ElementTheme.Light,
            AppTheme.Dark => ElementTheme.Dark,
            _ => ElementTheme.Default
        };
        if (App.Window is MainWindow window) window.SetTheme(RequestedTheme);
    }

    private void UpdateStatus()
    {
        if (SyncStatusText is null) return;
        SyncStatusText.Text = ViewModel.IsBusy ? "Syncing your library..." :
            ViewModel.IsStale ? ViewModel.StatusTitle : "Available offline";
        ToolTipService.SetToolTip(SyncStatusText, ViewModel.StatusMessage);
        SyncStatusIcon.Glyph = ViewModel.IsBusy ? "\uE72C" : ViewModel.IsStale ? "\uE8CD" : "\uE73E";
        LibraryStatus.Text = App.Services.IsDemo ? "Sample library" : ViewModel.IsBusy ? "Syncing..." : ViewModel.IsOffline ? "Reading offline" : "Connected to NewsBlur";
        StatusBar.IsOpen = ViewModel.IsStale && ViewModel.IsReaderScreen;
        LoginStatus.IsOpen = ViewModel.IsLoginScreen && ViewModel.StatusTitle is not ("Sign in to NewsBlur" or "Loading");
    }

    private void UpdateArticleActions()
    {
        if (ReadButton is null) return;
        var story = ViewModel.SelectedStory;
        ReadButton.IsEnabled = SaveButton.IsEnabled = OpenButton.IsEnabled = story is not null;
        OpenButton.IsEnabled = Uri.TryCreate(story?.Permalink, UriKind.Absolute, out var link)
            && link.Scheme is "http" or "https";
        ArticleSource.Text = story is null ? "THE READING ROOM" : SourceFor(story);
        ReadIcon.Glyph = story?.IsRead == true ? "\uE73E" : "\uE915";
        SaveIcon.Glyph = story?.IsSaved == true ? "\uE735" : "\uE734";
        var readAction = story?.IsRead == true ? "Mark as unread" : "Mark as read";
        var saveAction = story?.IsSaved == true ? "Remove from saved" : "Save story";
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(ReadButton, readAction);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(SaveButton, saveAction);
        ToolTipService.SetToolTip(ReadButton, $"{readAction} (Ctrl+Shift+M)");
        ToolTipService.SetToolTip(SaveButton, $"{saveAction} (Ctrl+Shift+S)");
    }

    private string SourceFor(Story story) =>
        ViewModel.NavigationItems.FirstOrDefault(item => item.FeedId == story.FeedId)?.Title
        ?? story.Author ?? "Your library";

    private void SelectCurrentNavigation()
    {
        _bindingCollections = true;
        try
        {
        if (ViewModel.SelectedNavigation is not null)
        {
            var navigation = NavigationList.Items
                .OfType<NavigationItemView>()
                .FirstOrDefault(item => item.Model.Id == ViewModel.SelectedNavigation.Id);
            NavigationList.SelectedItem = navigation;
            NavigationPicker.SelectedItem = NavigationPicker.Items
                .OfType<NavigationItemView>()
                .FirstOrDefault(item => item.Model.Id == ViewModel.SelectedNavigation.Id);
            FeedList.SelectedItem = FeedList.Items
                .OfType<NavigationItemView>()
                .FirstOrDefault(item => item.Model.Id == ViewModel.SelectedNavigation.Id);
        }
        }
        finally { _bindingCollections = false; }
    }

    private void ShowReader()
    {
        if (ReaderEmpty is null) return;
        ReaderEmpty.Visibility = ViewModel.SelectedStory is null
            ? Visibility.Visible
            : Visibility.Collapsed;
        var compact = ActualWidth < 900;
        var browse = ViewModel.SelectedStory is null;
        FeedSearch.Visibility = browse ? Visibility.Visible : Visibility.Collapsed;
        var denseBrowse = ActualWidth is >= 820 and < 1000 && browse;
        BrandTagline.Visibility = denseBrowse ? Visibility.Collapsed : Visibility.Visible;
        StoryHeader.Margin = denseBrowse ? new Thickness(16, 12, 12, 6) : new Thickness(22, 24, 16, 14);
        LibrarySubtitle.Visibility = denseBrowse ? Visibility.Collapsed : Visibility.Visible;
        StorySearch.Visibility = denseBrowse ? Visibility.Collapsed : Visibility.Visible;
        var showFeedPane = ActualWidth >= 820 && browse;
        FeedPane.Visibility = showFeedPane ? Visibility.Visible : Visibility.Collapsed;
        FeedColumn.Width = new GridLength(showFeedPane ? 270 : 0);
        NavigationColumn.Width = new GridLength(ActualWidth >= 1180 || showFeedPane ? 224 : 0);
        NavigationPane.Visibility = NavigationColumn.Width.Value > 0 ? Visibility.Visible : Visibility.Collapsed;
        NavigationPicker.Visibility = NavigationPane.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
        if (!compact)
        {
            NavigationColumn.Width = new GridLength(ActualWidth >= 1180 || browse ? 224 : 0);
            NavigationPane.Visibility = NavigationColumn.Width.Value > 0 ? Visibility.Visible : Visibility.Collapsed;
            StoryColumn.Width = browse ? new GridLength(1, GridUnitType.Star) : new GridLength(340);
            ReaderColumn.Width = browse ? new GridLength(0) : new GridLength(1, GridUnitType.Star);
        }
        var showArticle = ViewModel.SelectedStory is not null && (!compact || _compactReaderOpen);
        if (compact)
        {
            StoryColumn.Width = new GridLength(showArticle ? 0 : 1, GridUnitType.Star);
            ReaderColumn.Width = new GridLength(showArticle ? 1 : 0, GridUnitType.Star);
        }
        StoryPane.Visibility = compact && showArticle ? Visibility.Collapsed : Visibility.Visible;
        ArticlePane.Visibility = !showArticle ? Visibility.Collapsed : Visibility.Visible;
        ReaderBack.Visibility = showArticle ? Visibility.Visible : Visibility.Collapsed;
        ArticleReader.Visibility = showArticle ? Visibility.Visible : Visibility.Collapsed;
        UpdateArticleActions();
    }

    private void MainPage_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyResponsiveLayout(e.NewSize.Width);
        if (ViewModel.IsReaderScreen && (e.PreviousSize.Width < 1000) != (e.NewSize.Width < 1000))
            BindStories();
    }

    private void ApplyResponsiveLayout(double width)
    {
        if (PaneGrid is null) return;
        SettingsCard.Width = Math.Min(400, Math.Max(0, width));
        if (width >= 1180)
        {
            NavigationColumn.Width = new GridLength(224);
            StoryColumn.Width = new GridLength(340);
            ReaderColumn.Width = new GridLength(1, GridUnitType.Star);
            NavigationPane.Visibility = Visibility.Visible;
            NavigationPicker.Visibility = Visibility.Collapsed;
            ReaderBack.Visibility = Visibility.Collapsed;
        }
        else if (width >= 900)
        {
            NavigationColumn.Width = new GridLength(0);
            StoryColumn.Width = new GridLength(320);
            ReaderColumn.Width = new GridLength(1, GridUnitType.Star);
            NavigationPane.Visibility = Visibility.Collapsed;
            NavigationPicker.Visibility = Visibility.Visible;
            ReaderBack.Visibility = Visibility.Collapsed;
        }
        else
        {
            NavigationColumn.Width = new GridLength(0);
            NavigationPane.Visibility = Visibility.Collapsed;
            NavigationPicker.Visibility = Visibility.Visible;
            StoryColumn.Width = ViewModel.SelectedStory is null
                ? new GridLength(1, GridUnitType.Star)
                : new GridLength(0);
            ReaderColumn.Width = ViewModel.SelectedStory is null
                ? new GridLength(0)
                : new GridLength(1, GridUnitType.Star);
            ReaderBack.Visibility = ViewModel.SelectedStory is null
                ? Visibility.Collapsed
                : Visibility.Visible;
        }

        ShowReader();
    }

    private void BindLocalCollections()
    {
        _bindingCollections = true;
        NavigationPicker.Items.Clear();
        NavigationList.Items.Clear();
        foreach (var item in ViewModel.NavigationItems)
        {
            NavigationPicker.Items.Add(new NavigationItemView(item));
            if (item.Kind != NavigationItemKind.Feed)
                NavigationList.Items.Add(new NavigationItemView(item));
        }
        _bindingCollections = false;
        BindFeeds();
        BindStories();
        SelectCurrentNavigation();
    }

    private void BindStories()
    {
        _bindingCollections = true;
        var selectedHash = ViewModel.SelectedStory?.Hash;
        var search = StorySearch.Text.Trim();
        StoryList.Items.Clear();
        foreach (var story in ViewModel.Stories)
        {
            if (search.Length > 0 && !story.Title.Contains(search, StringComparison.CurrentCultureIgnoreCase)
                && !StoryPresentation.PlainText(story.Summary).Contains(search, StringComparison.CurrentCultureIgnoreCase)
                && !SourceFor(story).Contains(search, StringComparison.CurrentCultureIgnoreCase)) continue;
            var item = new StoryItemView(story, SourceFor(story), ActualWidth is >= 820 and < 1000 && ViewModel.SelectedStory is null);
            if (App.Services.Images?.GetCachedPath(story.ImageUri) is { } cachedPath)
                item.Thumbnail = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(cachedPath));
#if DEBUG
            if (App.Services.IsDemo && story.ImageUri?.StartsWith("ms-appx:///Assets/Demo/", StringComparison.Ordinal) == true)
                item.Thumbnail = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(story.ImageUri));
#endif
            StoryList.Items.Add(item);
        }

        if (selectedHash is not null)
        {
            StoryList.SelectedItem = StoryList.Items
                .OfType<StoryItemView>()
                .FirstOrDefault(item => item.Model.Hash == selectedHash);
        }
        _bindingCollections = false;
        LibraryHeading.Text = ViewModel.SelectedNavigation?.Title ?? "Your library";
        LibrarySubtitle.Text = $"{StoryList.Items.Count} {(StoryList.Items.Count == 1 ? "story" : "stories")}  /  {DateTime.Today:dddd, MMMM d}";
        ListEmpty.Visibility = StoryList.Items.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        ListEmptyTitle.Text = search.Length > 0 ? "No matching stories." : ViewModel.SelectedNavigation?.Filter == StoryFilter.Unread ? "All caught up." : "A fresh page.";
        ListEmptyMessage.Text = search.Length > 0 ? "Try a different title, publication, or phrase." : "Choose another collection or sync for new stories.";
        ShowReader();
    }

    private async Task RefreshImagesAsync()
    {
        if (!ViewModel.IsReaderScreen) return;
        var selectedHash = ViewModel.SelectedStory?.Hash;
        var hadCover = App.Services.Images?.GetCachedPath(ViewModel.SelectedStory?.ImageUri) is not null;
        await App.Services.CacheImagesAsync();
        BindStories();
        if (!hadCover && _readerInitialized && ViewModel.SelectedStory?.Hash == selectedHash
            && App.Services.Images?.GetCachedPath(ViewModel.SelectedStory?.ImageUri) is not null)
        {
            var scrollJson = await ArticleReader.CoreWebView2.ExecuteScriptAsync("window.scrollY");
            if (ViewModel.SelectedStory?.Hash == selectedHash
                && double.TryParse(scrollJson, System.Globalization.NumberStyles.Float,
                    System.Globalization.CultureInfo.InvariantCulture, out var scrollY))
                NavigateReader(scrollY);
        }
        if (App.Services.ImageFailures > 0)
        {
            SyncStatusText.Text = "Some images unavailable";
            ToolTipService.SetToolTip(SyncStatusText, "Your stories are available. Images that could not be downloaded will be retried on the next sync.");
        }
    }

    private async void ReaderBack_Click(object sender, RoutedEventArgs e)
    {
        _compactReaderOpen = false;
        if (ActualWidth >= 900)
        {
            await ViewModel.SelectStoryAsync(null);
            BindStories();
        }
        ShowReader();
        StoryList.Focus(FocusState.Programmatic);
    }

    private void NavigateReader(double? restoreScroll = null)
    {
        if (_readerInitialized && ViewModel.SelectedStory is { } story)
        {
            var readerSettings = ViewModel.Settings with
            {
                Theme = ActualTheme == ElementTheme.Dark ? AppTheme.Dark : AppTheme.Light
            };
            var html = ReaderHtmlBuilder.Build(story, readerSettings, ReadCover(story), SourceFor(story));
            try
            {
                // A local virtual host avoids NavigateToString's 2 MiB limit without allowing network content.
                File.WriteAllText(Path.Combine(_readerDirectory, "article.html"), html);
                _readerAddress = $"https://reader.spectro.invalid/article.html?v={Guid.NewGuid():N}";
                _restoreScroll = restoreScroll;
                ArticleReader.CoreWebView2.Navigate(_readerAddress);
            }
            catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
            {
                System.Diagnostics.Trace.TraceError($"Local reader document failed: {exception.GetType().Name}");
                StatusBar.Title = "Could not open this article";
                StatusBar.Message = "The local reading document could not be written. Check available disk space and try again.";
                StatusBar.IsOpen = true;
            }
        }

        ShowReader();
    }

    private async void ArticleReader_NavigationCompleted(object? sender, CoreWebView2NavigationCompletedEventArgs args)
    {
        if (!args.IsSuccess || args.NavigationId != _readerNavigationId) return;
        if (_focusReaderAfterNavigation)
        {
            _focusReaderAfterNavigation = false;
            ArticleReader.Focus(FocusState.Programmatic);
        }
        if (_restoreScroll is { } position)
        {
            _restoreScroll = null;
            await ArticleReader.CoreWebView2.ExecuteScriptAsync(
                $"window.scrollTo(0,{position.ToString(System.Globalization.CultureInfo.InvariantCulture)})");
        }
    }

    private void ClearReaderDocument()
    {
        _readerAddress = null;
        _restoreScroll = null;
        if (_readerInitialized) ArticleReader.CoreWebView2.Navigate("about:blank");
    }

    private string? ReadCover(Story story)
    {
        var path = App.Services.Images?.GetCachedPath(story.ImageUri);
#if DEBUG
        if (App.Services.IsDemo && story.ImageUri?.StartsWith("ms-appx:///Assets/Demo/", StringComparison.Ordinal) == true)
            path = Path.Combine(AppContext.BaseDirectory, "Assets", "Demo", Path.GetFileName(new Uri(story.ImageUri).LocalPath));
#endif
        if (path is null || !File.Exists(path)) return null;
        try
        {
            if (new FileInfo(path).Length > 512 * 1024) return null;
            var bytes = File.ReadAllBytes(path);
            var mime = bytes.Length >= 12 ? bytes[0] switch
            {
                0x89 => "png",
                0xFF => "jpeg",
                0x47 => "gif",
                0x52 => "webp",
                _ => null
            } : null;
            return mime is null ? null : $"data:image/{mime};base64,{Convert.ToBase64String(bytes)}";
        }
        catch (IOException exception)
        {
            System.Diagnostics.Debug.WriteLine($"Cached cover unavailable: {exception.GetType().Name}");
            return null;
        }
    }

    private void OpenSettings()
    {
        if (!ViewModel.IsReaderScreen) return;
        if (SettingsPanel.Visibility != Visibility.Visible)
            _settingsReturnFocus = FocusManager.GetFocusedElement(XamlRoot) as Control;
        _updatingSettings = true;
        ThemeBox.SelectedIndex = (int)ViewModel.Settings.Theme;
        TextSizeBox.Value = ViewModel.Settings.TextSize;
        ReadingWidthBox.Value = ViewModel.Settings.ReadingWidth;
        DefaultFilterBox.SelectedIndex = (int)ViewModel.Settings.DefaultFilter;
        RetentionBox.Value = ViewModel.Settings.RetentionDays;
        DownloadImagesSwitch.IsOn = ViewModel.Settings.DownloadImages;
        _updatingSettings = false;
        SettingsPanel.Visibility = Visibility.Visible;
        ReaderHost.IsEnabled = false;
        ThemeBox.Focus(FocusState.Programmatic);
    }

    internal void OpenSettingsFromAccelerator() => OpenSettings();

    internal void ReportShortcutConflict(Windows.System.VirtualKey key)
    {
        SyncStatusText.Text = $"Shortcut {key} is in use by another app";
        ToolTipService.SetToolTip(SyncStatusText, "Use the toolbar action instead. Spectro will retry the shortcut when this window is activated again.");
    }

    internal async Task HandleReaderShortcutAsync(Windows.System.VirtualKey key)
    {
        if (key == Windows.System.VirtualKey.D) { OpenSettings(); return; }
        if (!ViewModel.CanInteract || SettingsPanel.Visibility == Visibility.Visible || !ViewModel.IsReaderScreen) return;
        switch (key)
        {
            case Windows.System.VirtualKey.R:
                await ViewModel.SynchronizeAsync();
                BindLocalCollections();
                await RefreshImagesAsync();
                break;
            case Windows.System.VirtualKey.O:
                await OpenOriginalAsync();
                break;
            case Windows.System.VirtualKey.S when ViewModel.SelectedStory is { } saved:
                await ViewModel.ToggleSavedAsync(saved);
                BindLocalCollections();
                break;
            case Windows.System.VirtualKey.M when ViewModel.SelectedStory is { } read:
                await ViewModel.ToggleReadAsync(read);
                BindLocalCollections();
                break;
        }
    }

    private void Settings_Changed(object sender, SelectionChangedEventArgs e) =>
        SaveSettings();

    private void SettingsNumber_Changed(
        NumberBox sender,
        NumberBoxValueChangedEventArgs args) =>
        SaveSettings();

    private void DownloadImages_Toggled(object sender, RoutedEventArgs e)
    {
        if (ViewModel.IsReaderScreen && ThemeBox is not null) SaveSettings();
    }

    private void SaveSettings()
    {
        if (_updatingSettings
            || ThemeBox.SelectedIndex < 0
            || DefaultFilterBox.SelectedIndex < 0
            || double.IsNaN(TextSizeBox.Value)
            || double.IsNaN(ReadingWidthBox.Value)
            || double.IsNaN(RetentionBox.Value))
        {
            return;
        }

        ViewModel.UpdateSettings(new ReaderSettings(
            (AppTheme)ThemeBox.SelectedIndex,
            TextSizeBox.Value,
            ReadingWidthBox.Value,
            (StoryFilter)DefaultFilterBox.SelectedIndex,
            (int)RetentionBox.Value,
            DownloadImagesSwitch.IsOn));
    }

    private async void SyncAccelerator_Invoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await ViewModel.SynchronizeAsync();
        BindLocalCollections();
        await RefreshImagesAsync();
    }

    private async void SaveAccelerator_Invoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (ViewModel.SelectedStory is not null)
        {
            await ViewModel.ToggleSavedAsync(ViewModel.SelectedStory);
            BindLocalCollections();
        }
    }

    private async void ReadAccelerator_Invoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        if (ViewModel.SelectedStory is not null)
        {
            await ViewModel.ToggleReadAsync(ViewModel.SelectedStory);
            BindLocalCollections();
        }
    }

    private void SettingsAccelerator_Invoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        OpenSettings();
    }

    private async void OpenAccelerator_Invoked(
        KeyboardAccelerator sender,
        KeyboardAcceleratorInvokedEventArgs args)
    {
        args.Handled = true;
        await OpenOriginalAsync();
    }
}
