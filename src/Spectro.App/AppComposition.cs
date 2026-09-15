using NewsBlurSharp;
using Spectro.Infrastructure;
using Spectro.Presentation;
using Spectro.Sync;
using Spectro.Domain;

namespace Spectro.WinUI;

internal sealed class AppComposition
{
    private AppComposition(
        AppViewModel viewModel,
        WebViewDataCleaner webViewDataCleaner)
    {
        ViewModel = viewModel;
        WebViewDataCleaner = webViewDataCleaner;
    }

    public AppViewModel ViewModel { get; }

    public WebViewDataCleaner WebViewDataCleaner { get; }

    public bool IsDemo { get; private init; }

    public StoryImageCache? Images { get; private set; }
    private IContentRepository? _repository;
    private string? _imageDirectory;
    private int _imageFailures;
    private CancellationTokenSource _imageCancellation = new();
    private Task _imageWork = Task.CompletedTask;
    public int ImageFailures => _imageFailures;

    public Task CacheImagesAsync()
    {
        if (Images is null || _repository is null || !ViewModel.Settings.DownloadImages) return Task.CompletedTask;
        if (!_imageWork.IsCompleted) return _imageWork;
        var token = _imageCancellation.Token;
        _imageWork = Task.Run(() => CacheImagesCoreAsync(token));
        return _imageWork;
    }

    private async Task CacheImagesCoreAsync(CancellationToken cancellationToken)
    {
        _imageFailures = 0;
        if (Images is null || _repository is null) return;
        try
        {
            await Images.CacheAsync(
                await _repository.QueryStoriesAsync(new ContentQuery(StoryFilter.All, IncludeContent: false), cancellationToken),
                cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            System.Diagnostics.Debug.WriteLine("Thumbnail downloads canceled for local library cleanup.");
        }
    }

    public async Task ClearImagesAsync()
    {
        await _imageCancellation.CancelAsync();
        await _imageWork;
        _imageCancellation.Dispose();
        _imageCancellation = new CancellationTokenSource();
        if (_imageDirectory is null || !Directory.Exists(_imageDirectory)) return;
        foreach (var file in Directory.EnumerateFiles(_imageDirectory, "*.img"))
            File.Delete(file);
    }

    public static AppComposition Create(string[] launchArguments)
    {
        var demoMode = false;
#if DEBUG
        demoMode = launchArguments.Any(static argument =>
                string.Equals(argument, "--demo", StringComparison.OrdinalIgnoreCase))
            || Environment.GetEnvironmentVariable("SPECTRO_DEMO") == "1";
#endif
        var localFolder = Windows.Storage.ApplicationData.Current.LocalFolder.Path;
        var databasePath = Path.Combine(localFolder, demoMode ? "spectro-demo.db" : "spectro.db");
        var repository = new SqliteContentRepository(
            new SqliteConnectionFactory(databasePath));
        var settings = new WindowsSettingsStore(demoMode);
        var webViewDataCleaner = new WebViewDataCleaner();

#if DEBUG
        if (demoMode)
        {
            var demo = new DemoModeService(repository);
            return new AppComposition(
                new AppViewModel(repository, demo, demo, settings),
                webViewDataCleaner) { IsDemo = true };
        }
#endif

        var client = new NewsBlurClient();
        var sessionStore = new PasswordVaultSessionStore();
        var sessionService = new NewsBlurSessionService(
            client,
            sessionStore,
            webViewDataCleaner);
        var composition = new AppComposition(
            new AppViewModel(
                repository,
                sessionService,
                new SynchronizationService(
                    repository,
                    new NewsBlurSyncRemoteService(client),
                    settings),
                settings),
            webViewDataCleaner);
        composition._repository = repository;
        composition._imageDirectory = Path.Combine(localFolder, "StoryImages");
        composition.Images = new StoryImageCache(
            StoryImageCache.CreateHttpClient(),
            composition._imageDirectory,
            exception =>
            {
                Interlocked.Increment(ref composition._imageFailures);
                System.Diagnostics.Debug.WriteLine($"Thumbnail download failed: {exception.GetType().Name}");
            });
        return composition;
    }
}
