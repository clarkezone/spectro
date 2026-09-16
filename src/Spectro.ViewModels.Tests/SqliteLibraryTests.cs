using Spectro.Domain;
using Spectro.Infrastructure;
using Spectro.Presentation;
using Spectro.Sync;
using Xunit;

namespace Spectro.ViewModels.Tests;

public sealed class SqliteLibraryTests : IAsyncLifetime
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "Spectro.ViewModels.Tests", Guid.NewGuid().ToString("N"));
    private readonly SqliteConnectionFactory _connections;
    private readonly SqliteContentRepository _repository;
    private readonly ControlledSynchronization _sync = new();
    private readonly AppViewModel _subject;

    public SqliteLibraryTests()
    {
        _connections = new(Path.Combine(_directory, "library.db"));
        _repository = new(_connections);
        _subject = new(_repository, new AuthenticatedSession(), _sync, new SettingsStore());
    }

    public async Task InitializeAsync()
    {
        await _repository.InitializeAsync();
        await _repository.ReplaceFeedCatalogAsync(
            [
                Feed(1, "Alpha Science"),
                Feed(2, "Beta Technology"),
                Feed(3, "Gamma Arts"),
                Feed(4, "Delta Empty")
            ],
            [new("tech", "Technology", 0), new("arts", "Arts", 1)],
            [new("tech", 1, 0), new("tech", 2, 1), new("arts", 3, 0), new("arts", 4, 1)]);
        foreach (var story in SeedStories)
            await _repository.UpsertStoryAsync(story);
        await _subject.InitializeAsync();
    }

    public Task DisposeAsync()
    {
        _connections.ClearPools();
        Directory.Delete(_directory, recursive: true);
        return Task.CompletedTask;
    }

    public static TheoryData<string, StoryFilter, string[]> Collections => new()
    {
        { "all", StoryFilter.All, ["1:a", "1:b", "2:c", "2:d", "3:e", "3:f"] },
        { "unread", StoryFilter.Unread, ["1:a", "2:c", "3:f"] },
        { "saved", StoryFilter.Saved, ["1:b", "2:c", "3:e"] },
        { "read", StoryFilter.Read, ["1:b", "2:d", "3:e"] },
        { "folder:tech", StoryFilter.All, ["1:a", "1:b", "2:c", "2:d"] },
        { "folder:tech", StoryFilter.Unread, ["1:a", "2:c"] },
        { "folder:tech", StoryFilter.Saved, ["1:b", "2:c"] },
        { "folder:tech", StoryFilter.Read, ["1:b", "2:d"] },
        { "folder:arts", StoryFilter.All, ["3:e", "3:f"] },
        { "folder:arts", StoryFilter.Unread, ["3:f"] },
        { "folder:arts", StoryFilter.Saved, ["3:e"] },
        { "folder:arts", StoryFilter.Read, ["3:e"] },
        { "feed:1", StoryFilter.All, ["1:a", "1:b"] },
        { "feed:1", StoryFilter.Unread, ["1:a"] },
        { "feed:1", StoryFilter.Saved, ["1:b"] },
        { "feed:1", StoryFilter.Read, ["1:b"] },
        { "feed:2", StoryFilter.All, ["2:c", "2:d"] },
        { "feed:2", StoryFilter.Unread, ["2:c"] },
        { "feed:2", StoryFilter.Saved, ["2:c"] },
        { "feed:2", StoryFilter.Read, ["2:d"] },
        { "feed:3", StoryFilter.All, ["3:e", "3:f"] },
        { "feed:3", StoryFilter.Unread, ["3:f"] },
        { "feed:3", StoryFilter.Saved, ["3:e"] },
        { "feed:3", StoryFilter.Read, ["3:e"] },
        { "feed:4", StoryFilter.All, [] },
        { "feed:4", StoryFilter.Unread, [] },
        { "feed:4", StoryFilter.Saved, [] },
        { "feed:4", StoryFilter.Read, [] }
    };

    [Theory]
    [MemberData(nameof(Collections))]
    public async Task NavigationAndScopedFiltersReturnExactlyTheMatchingBodylessStories(
        string navigationId, StoryFilter filter, string[] expectedHashes)
    {
        await NavigateAsync(navigationId);
        await _subject.SelectFilterAsync(filter);

        Assert.Equal(navigationId, _subject.SelectedNavigation?.Id);
        Assert.Equal(filter, _subject.ActiveFilter);
        AssertHashes(expectedHashes);
        Assert.All(_subject.Stories, story =>
        {
            Assert.Empty(story.Content);
            Assert.StartsWith("Preview ", story.Summary);
        });
        if (expectedHashes.Length == 0)
            Assert.Equal("Nothing here yet", _subject.StatusTitle);
    }

    [Fact]
    public void InitialLibraryUsesLocalCountsRatherThanRemoteBadges()
    {
        Assert.True(_subject.IsReaderScreen);
        Assert.Equal(StoryFilter.Unread, _subject.ActiveFilter);
        AssertHashes("1:a", "2:c", "3:f");
        Assert.Equal(
            ["all", "unread", "saved", "read", "folder:tech", "feed:1", "feed:2", "folder:arts", "feed:3", "feed:4"],
            _subject.NavigationItems.Select(item => item.Id));
        Assert.Equal(3, Navigation("unread").UnreadCount);
        Assert.Equal(2, Navigation("folder:tech").UnreadCount);
        Assert.Equal(1, Navigation("folder:arts").UnreadCount);
        foreach (var id in new[] { 1, 2, 3 })
            AssertFeedCounts(id, total: 2, unread: 1, saved: 1);
        AssertFeedCounts(4, total: 0, unread: 0, saved: 0);
        Assert.All(_subject.NavigationItems.Where(item => item.Kind == NavigationItemKind.Feed),
            item => Assert.Equal(1, item.Depth));
    }

    public static TheoryData<string, StoryFilter, string, int[]> VisibleFeeds => new()
    {
        { "all", StoryFilter.All, "", [1, 2, 3, 4] },
        { "all", StoryFilter.Unread, "", [1, 2, 3] },
        { "all", StoryFilter.Saved, "", [1, 2, 3] },
        { "all", StoryFilter.Read, "", [1, 2, 3] },
        { "folder:tech", StoryFilter.All, "", [1, 2] },
        { "folder:tech", StoryFilter.Unread, "", [1, 2] },
        { "folder:tech", StoryFilter.Saved, "", [1, 2] },
        { "folder:tech", StoryFilter.Read, "", [1, 2] },
        { "folder:arts", StoryFilter.All, "", [3, 4] },
        { "folder:arts", StoryFilter.Unread, "", [3] },
        { "folder:arts", StoryFilter.Saved, "", [3] },
        { "folder:arts", StoryFilter.Read, "", [3] },
        { "all", StoryFilter.All, "  aLpHa  ", [1] },
        { "folder:tech", StoryFilter.Saved, "  BETA ", [2] },
        { "folder:tech", StoryFilter.All, "Arts", [] },
        { "folder:arts", StoryFilter.All, "Empty", [4] },
        { "folder:arts", StoryFilter.Unread, "Empty", [] },
        { "folder:arts", StoryFilter.Read, "GAMMA", [3] },
        { "all", StoryFilter.Saved, "not a subscription", [] }
    };

    [Theory]
    [MemberData(nameof(VisibleFeeds))]
    public async Task FeedSearchIntersectsFolderAndFilterWithoutChangingStorySelection(
        string navigationId, StoryFilter filter, string search, int[] expectedFeedIds)
    {
        await NavigateAsync(navigationId);
        await _subject.SelectFilterAsync(filter);
        var stories = _subject.Stories.Select(story => story.Hash).ToArray();
        var selected = _subject.SelectedNavigation;

        Assert.Equal(expectedFeedIds, _subject.GetVisibleFeeds(search).Select(item => item.FeedId!.Value));
        Assert.Equal(stories, _subject.Stories.Select(story => story.Hash));
        Assert.Same(selected, _subject.SelectedNavigation);
    }

    [Fact]
    public async Task FeedNavigationKeepsFolderScopeUntilGlobalNavigationClearsIt()
    {
        await NavigateAsync("folder:tech");
        await _subject.SelectFilterAsync(StoryFilter.Saved);
        await NavigateAsync("feed:2");
        AssertHashes("2:c");
        Assert.Equal([1, 2], _subject.GetVisibleFeeds().Select(item => item.FeedId!.Value));

        await NavigateAsync("saved");
        AssertHashes("1:b", "2:c", "3:e");
        Assert.Equal([1, 2, 3], _subject.GetVisibleFeeds().Select(item => item.FeedId!.Value));

        await NavigateAsync("all");
        AssertHashes("1:a", "1:b", "2:c", "2:d", "3:e", "3:f");
        Assert.Equal([1, 2, 3, 4], _subject.GetVisibleFeeds().Select(item => item.FeedId!.Value));
    }

    [Fact]
    public async Task OpeningPreviewWhoseStoredRowWasDeletedReportsMissingDownloadWithoutOpeningEmptyReader()
    {
        var preview = _subject.Stories.Single(story => story.Hash == "1:a");
        Assert.Empty(preview.Content);
        await using (var connection = await _connections.OpenAsync())
        await using (var command = connection.CreateCommand())
        {
            command.CommandText = "DELETE FROM story WHERE story_hash = $hash;";
            command.Parameters.AddWithValue("$hash", preview.Hash);
            Assert.Equal(1, await command.ExecuteNonQueryAsync());
        }
        Assert.Empty(await _repository.QueryStoriesAsync(new(StoryFilter.All, StoryHash: preview.Hash)));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(
            () => _subject.SelectStoryAsync(preview));

        Assert.Equal("This story is no longer downloaded. Sync or choose another story.", exception.Message);
        Assert.Null(_subject.SelectedStory);
        Assert.Empty(_subject.ReaderHtml);
        Assert.Empty(await _repository.GetPendingMutationsAsync(10));
    }

    [Fact]
    public async Task OpeningAndMutatingBodylessPreviewPreservesFullReaderAndStoredBody()
    {
        var preview = _subject.Stories.Single(story => story.Hash == "1:a");
        var original = SeedStories.Single(story => story.Hash == preview.Hash);
        Assert.Empty(preview.Content);
        var revision = _subject.LibraryRevision;

        await _subject.SelectStoryAsync(preview);

        Assert.True(_subject.LibraryRevision > revision);
        AssertHashes("2:c", "3:f");
        Assert.Equal(original.Content, _subject.SelectedStory?.Content);
        Assert.True(_subject.SelectedStory?.IsRead);
        Assert.Contains("Full body 1:a", _subject.ReaderHtml);
        AssertFeedCounts(1, total: 2, unread: 0, saved: 1);
        Assert.Equal(2, Navigation("unread").UnreadCount);
        Assert.Equal(1, Navigation("folder:tech").UnreadCount);
        Assert.Equal([2, 3], _subject.GetVisibleFeeds().Select(item => item.FeedId!.Value));

        await _subject.ToggleSavedAsync(_subject.SelectedStory!);
        Assert.True(_subject.SelectedStory?.IsSaved);
        AssertFeedCounts(1, total: 2, unread: 0, saved: 2);
        await _subject.ToggleReadAsync(_subject.SelectedStory!);
        Assert.False(_subject.SelectedStory?.IsRead);
        AssertHashes("1:a", "2:c", "3:f");
        AssertFeedCounts(1, total: 2, unread: 1, saved: 2);
        await _subject.ToggleSavedAsync(_subject.Stories.Single(story => story.Hash == "1:a"));
        Assert.False(_subject.SelectedStory?.IsSaved);
        Assert.Equal(original.Content, _subject.SelectedStory?.Content);
        Assert.Contains("Full body 1:a", _subject.ReaderHtml);
        AssertFeedCounts(1, total: 2, unread: 1, saved: 1);

        var stored = Assert.Single(await _repository.QueryStoriesAsync(new(StoryFilter.All, StoryHash: "1:a")));
        Assert.Equal(original.Content, stored.Content);
        Assert.False(stored.IsRead);
        Assert.False(stored.IsSaved);
        var pending = await _repository.GetPendingMutationsAsync(10);
        Assert.Equal(2, pending.Count);
        Assert.All(pending, mutation => Assert.Equal("1:a", mutation.StoryHash));
    }

    [Fact]
    public async Task MarkingLastReadStoryUnreadRemovesOnlyThatFeedFromReadFilter()
    {
        await NavigateAsync("read");
        AssertHashes("1:b", "2:d", "3:e");

        await _subject.ToggleReadAsync(_subject.Stories.Single(story => story.Hash == "3:e"));

        AssertHashes("1:b", "2:d");
        Assert.Equal([1, 2], _subject.GetVisibleFeeds().Select(item => item.FeedId!.Value));
        AssertFeedCounts(3, total: 2, unread: 2, saved: 1);
        Assert.Equal(4, Navigation("unread").UnreadCount);
        Assert.Equal(2, Navigation("folder:arts").UnreadCount);
        await _subject.SelectFilterAsync(StoryFilter.Unread);
        AssertHashes("1:a", "2:c", "3:e", "3:f");
        await _subject.SelectFilterAsync(StoryFilter.Saved);
        AssertHashes("1:b", "2:c", "3:e");
    }

    [Fact]
    public async Task UnsavingSelectedSavedStoryRemovesItFromListButKeepsItsFullReader()
    {
        await NavigateAsync("folder:arts");
        await _subject.SelectFilterAsync(StoryFilter.Saved);
        await _subject.SelectStoryAsync(Assert.Single(_subject.Stories));

        await _subject.ToggleSavedAsync(_subject.SelectedStory!);

        Assert.Empty(_subject.Stories);
        Assert.Empty(_subject.GetVisibleFeeds());
        Assert.Equal("3:e", _subject.SelectedStory?.Hash);
        Assert.False(_subject.SelectedStory?.IsSaved);
        Assert.Contains("Full body 3:e", _subject.ReaderHtml);
        Assert.Equal(SeedStories[4].Content, _subject.SelectedStory?.Content);
        AssertFeedCounts(3, total: 2, unread: 1, saved: 0);
        Assert.Equal(SeedStories[4].Content,
            Assert.Single(await _repository.QueryStoriesAsync(new(StoryFilter.All, StoryHash: "3:e"))).Content);
    }

    [Fact]
    public async Task InitialSyncExposesCachedLibraryBeforeTheNetworkFinishes()
    {
        var run = _sync.BlockNext();
        var subject = new AppViewModel(_repository, new AuthenticatedSession(), _sync, new SettingsStore());
        var initializing = subject.InitializeAsync();
        try
        {
            await run.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.False(initializing.IsCompleted);
            Assert.True(subject.IsReaderScreen);
            Assert.True(subject.IsSyncing);
            Assert.True(subject.CanInteract);
            Assert.False(subject.IsBusy);
            Assert.False(subject.CanSynchronize);
            Assert.Equal(["1:a", "2:c", "3:f"], subject.Stories.Select(story => story.Hash));
            await subject.SelectNavigationAsync(subject.NavigationItems.Single(item => item.Id == "saved"));
            Assert.Equal(["1:b", "2:c", "3:e"], subject.Stories.Select(story => story.Hash));
            await subject.SelectStoryAsync(subject.Stories.Single(story => story.Hash == "1:b"));
            Assert.Contains("Full body 1:b", subject.ReaderHtml);
            Assert.False(initializing.IsCompleted);
        }
        finally
        {
            run.Complete(SyncOutcome.Succeeded);
            await initializing.WaitAsync(TimeSpan.FromSeconds(10));
        }

        Assert.False(subject.IsSyncing);
        Assert.True(subject.CanSynchronize);
        Assert.Equal("Up to date", subject.StatusTitle);
        Assert.Equal("1:b", subject.SelectedStory?.Hash);
        Assert.Contains("Full body 1:b", subject.ReaderHtml);
    }

    [Fact]
    public async Task BlockedSyncAllowsBrowsingAndRefreshesCommittedPagesBeforeCompletion()
    {
        var run = _sync.BlockNext();
        var syncing = _subject.SynchronizeAsync();
        try
        {
            await run.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            Assert.True(_subject.IsSyncing);
            Assert.False(_subject.IsBusy);
            Assert.True(_subject.CanInteract);
            Assert.False(_subject.CanSynchronize);
            Assert.Same(syncing, _subject.SynchronizeAsync());
            Assert.Equal(2, _sync.CallCount);

            await NavigateAsync("folder:arts");
            await _subject.SelectFilterAsync(StoryFilter.All);
            await NavigateAsync("feed:4");
            Assert.Empty(_subject.Stories);
            var revision = _subject.LibraryRevision;
            await _repository.UpsertStoryAsync(Story("4:early", 4, 10, isRead: false, isSaved: true));
            var refreshed = WaitForLibraryAsync(() => _subject.Stories.Any(story => story.Hash == "4:early"));

            run.Report(new("42", SyncStage.FetchStories, 0, 4, 7, 1)
            {
                LocalRevision = 1,
                CompletedFeedCount = 1,
                CurrentFeedTitle = "Delta Empty",
                CurrentPage = 2,
                RetryAttempt = 2
            });
            await refreshed;

            Assert.False(syncing.IsCompleted);
            Assert.True(_subject.IsSyncing);
            Assert.True(_subject.LibraryRevision > revision);
            AssertHashes("4:early");
            Assert.Equal("feed:4", _subject.SelectedNavigation?.Id);
            Assert.Equal(StoryFilter.All, _subject.ActiveFilter);
            Assert.Equal("1/4 feeds, 7 stories - Delta Empty, page 2 - retry 2", _subject.SyncProgressText);
            AssertFeedCounts(4, total: 1, unread: 1, saved: 1);
            Assert.Equal(4, Navigation("unread").UnreadCount);
            Assert.Equal(2, Navigation("folder:arts").UnreadCount);
            await _subject.SelectStoryAsync(Assert.Single(_subject.Stories));
            Assert.Contains("Full body 4:early", _subject.ReaderHtml);
            Assert.True(_subject.IsSyncing);
            Assert.True(_subject.CanInteract);
        }
        finally
        {
            run.Complete(SyncOutcome.Succeeded);
            await syncing.WaitAsync(TimeSpan.FromSeconds(10));
        }

        Assert.False(_subject.IsSyncing);
        Assert.True(_subject.CanSynchronize);
        Assert.Equal("Changes waiting to sync", _subject.StatusTitle);
        Assert.Contains("saved locally", _subject.StatusMessage);
    }

    [Fact]
    public async Task CancelingBlockedSyncRetainsDownloadedAndLocallyChangedStories()
    {
        var run = _sync.BlockNext();
        var syncing = _subject.SynchronizeAsync();
        try
        {
            await run.Started.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await _repository.UpsertStoryAsync(Story("4:partial", 4, 10, false, false));
            await _subject.ToggleSavedAsync(_subject.Stories.Single(story => story.Hash == "1:a"));
            _subject.CancelSync();
            await syncing.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.True(run.Token.IsCancellationRequested);
            Assert.False(_subject.IsSyncing);
            Assert.True(_subject.CanSynchronize);
            Assert.True(_subject.IsReaderScreen);
            Assert.Equal("Sync canceled", _subject.StatusTitle);
            Assert.Contains("local changes are safe", _subject.StatusMessage);
            AssertHashes("4:partial", "1:a", "2:c", "3:f");
            Assert.True(_subject.Stories.Single(story => story.Hash == "1:a").IsSaved);
            AssertFeedCounts(4, total: 1, unread: 1, saved: 0);
        }
        finally
        {
            run.Complete(SyncOutcome.Canceled);
            await syncing.WaitAsync(TimeSpan.FromSeconds(10));
        }
    }

    [Theory]
    [InlineData(SyncOutcome.Offline, "Offline", "Showing downloaded stories.")]
    [InlineData(SyncOutcome.TransientFailure, "Sync delayed", "Remote failure")]
    [InlineData(SyncOutcome.AuthenticationRequired, "Session expired", "sign in again")]
    [InlineData(SyncOutcome.MalformedRemoteData, "Sync error", "could not read")]
    [InlineData(SyncOutcome.PermanentFailure, "Sync error", "Remote failure")]
    [InlineData(SyncOutcome.Canceled, "Sync canceled", "local changes are safe")]
    public async Task SyncFailureKeepsLocalLibraryBrowsableAndShowsActionableFeedback(
        SyncOutcome outcome, string title, string message)
    {
        var run = _sync.BlockNext();
        var syncing = _subject.SynchronizeAsync();
        run.Complete(outcome);
        await syncing.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal(title, _subject.StatusTitle);
        Assert.Contains(message, _subject.StatusMessage);
        Assert.True(_subject.IsStale);
        Assert.Equal(outcome == SyncOutcome.Offline, _subject.IsOffline);
        Assert.False(_subject.IsSyncing);
        Assert.True(_subject.CanSynchronize);
        Assert.True(_subject.IsReaderScreen);
        await NavigateAsync("all");
        AssertHashes("1:a", "1:b", "2:c", "2:d", "3:e", "3:f");
        Assert.Equal(title, _subject.StatusTitle);
    }

    [Fact]
    public async Task SuccessfulRetryClearsOfflineAndStaleFeedback()
    {
        var run = _sync.BlockNext();
        var syncing = _subject.SynchronizeAsync();
        run.Complete(SyncOutcome.Offline);
        await syncing.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(_subject.IsOffline);
        Assert.True(_subject.IsStale);

        await _subject.SynchronizeAsync().WaitAsync(TimeSpan.FromSeconds(10));

        Assert.False(_subject.IsOffline);
        Assert.False(_subject.IsStale);
        Assert.False(_subject.IsSyncing);
        Assert.True(_subject.CanSynchronize);
        Assert.Equal("Up to date", _subject.StatusTitle);
        AssertHashes("1:a", "2:c", "3:f");
    }

    [Fact]
    public async Task UnexpectedSyncExceptionIsReportedWithoutDiscardingLibrary()
    {
        var run = _sync.BlockNext();
        var syncing = _subject.SynchronizeAsync();
        run.Fail(new IOException("Deliberate test failure"));
        await syncing.WaitAsync(TimeSpan.FromSeconds(10));

        Assert.Equal("Sync could not finish", _subject.StatusTitle);
        Assert.Contains("downloaded library is still available", _subject.StatusMessage);
        Assert.True(_subject.IsStale);
        Assert.True(_subject.CanSynchronize);
        Assert.False(_subject.IsSyncing);
        AssertHashes("1:a", "2:c", "3:f");
    }

    private NavigationItem Navigation(string id) => _subject.NavigationItems.Single(item => item.Id == id);

    private Task NavigateAsync(string id) => _subject.SelectNavigationAsync(Navigation(id));

    private void AssertHashes(params string[] hashes) =>
        Assert.Equal(hashes, _subject.Stories.Select(story => story.Hash));

    private void AssertFeedCounts(int id, int total, int unread, int saved)
    {
        var feed = Navigation($"feed:{id}");
        Assert.Equal(total, feed.StoryCount);
        Assert.Equal(unread, feed.UnreadCount);
        Assert.Equal(saved, feed.SavedCount);
    }

    private async Task WaitForLibraryAsync(Func<bool> condition)
    {
        var completion = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        void Changed(object? sender, System.ComponentModel.PropertyChangedEventArgs args)
        {
            if (args.PropertyName == nameof(AppViewModel.LibraryRevision) && condition())
                completion.TrySetResult();
        }

        _subject.PropertyChanged += Changed;
        try
        {
            if (condition()) completion.TrySetResult();
            await completion.Task.WaitAsync(TimeSpan.FromSeconds(10));
        }
        finally { _subject.PropertyChanged -= Changed; }
    }

    private static Feed Feed(int id, string title) =>
        new(id, title, $"https://example.test/feed/{id}", null, 999, null, true);

    private static Story Story(string hash, int feedId, int order, bool isRead, bool isSaved) =>
        new(hash, feedId, null, null, $"Story {hash}", "Author", $"https://example.test/{hash}",
            $"<p>Full body {hash}</p><p>Not included in the list preview.</p>", $"Preview {hash}",
            null, DateTimeOffset.Parse("2026-09-14T12:00:00Z").AddMinutes(order), isRead, isSaved);

    private static readonly Story[] SeedStories =
    [
        Story("1:a", 1, 6, false, false),
        Story("1:b", 1, 5, true, true),
        Story("2:c", 2, 4, false, true),
        Story("2:d", 2, 3, true, false),
        Story("3:e", 3, 2, true, true),
        Story("3:f", 3, 1, false, false)
    ];

    private sealed class AuthenticatedSession : ISessionService
    {
        public Task<SessionResult> RestoreAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new SessionResult(true, "42"));

        public Task<SessionResult> LoginAsync(string username, string password, CancellationToken cancellationToken) =>
            throw new NotSupportedException();

        public Task SignOutAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class SettingsStore : ISettingsStore
    {
        private ReaderSettings _settings = new();
        public ReaderSettings Load() => _settings;
        public void Save(ReaderSettings settings) => _settings = settings;
    }

    private sealed class ControlledSynchronization : ISynchronizationService
    {
        private SyncRun? _next;
        private int _callCount;
        public int CallCount => Volatile.Read(ref _callCount);

        public SyncRun BlockNext()
        {
            var run = new SyncRun();
            Assert.Null(Interlocked.CompareExchange(ref _next, run, null));
            return run;
        }

        public Task<SyncResult> SynchronizeAsync(
            string accountId, IProgress<SyncState>? progress, CancellationToken cancellationToken)
        {
            Assert.Equal("42", accountId);
            Interlocked.Increment(ref _callCount);
            var run = Interlocked.Exchange(ref _next, null);
            return run is null
                ? Task.FromResult(Result(SyncOutcome.Succeeded))
                : run.ExecuteAsync(progress, cancellationToken);
        }
    }

    private sealed class SyncRun
    {
        private readonly TaskCompletionSource<SyncResult> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private IProgress<SyncState>? _progress;
        public TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public CancellationToken Token { get; private set; }

        public Task<SyncResult> ExecuteAsync(IProgress<SyncState>? progress, CancellationToken token)
        {
            _progress = progress;
            Token = token;
            Started.TrySetResult();
            return _completion.Task.WaitAsync(token);
        }

        public void Report(SyncState state) => Assert.IsAssignableFrom<IProgress<SyncState>>(_progress).Report(state);
        public void Complete(SyncOutcome outcome) => _completion.TrySetResult(Result(outcome));
        public void Fail(Exception exception) => _completion.TrySetException(exception);
    }

    private static SyncResult Result(SyncOutcome outcome) =>
        new(outcome, new("42", SyncStage.Completed, 0, 4, 6, 1),
            DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, "Remote failure");
}
