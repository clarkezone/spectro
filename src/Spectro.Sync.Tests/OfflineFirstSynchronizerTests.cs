using System.Collections.Concurrent;
using System.Threading.Channels;
using Spectro.Domain;
using Spectro.Infrastructure;

namespace Spectro.Sync.Tests;

public sealed class OfflineFirstSynchronizerTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "Spectro.Sync.Tests",
        Guid.NewGuid().ToString("N"));
    private readonly string _databasePath;
    private readonly TestTimeProvider _time = new(
        new DateTimeOffset(2026, 9, 14, 20, 0, 0, TimeSpan.Zero));

    public OfflineFirstSynchronizerTests()
    {
        _databasePath = Path.Combine(_directory, "spectro.db");
    }

    [Fact]
    public async Task SuccessfulSyncPersistsRemoteDataAndIsIdempotent()
    {
        var repository = CreateRepository();
        var remote = new ScriptedRemoteService();
        var subject = CreateSynchronizer(repository, remote);

        var first = await subject.SynchronizeAsync(new SyncRequest("account"));
        var second = await subject.SynchronizeAsync(new SyncRequest("account"));

        Assert.True(first.IsSuccess);
        Assert.True(second.IsSuccess);
        Assert.Equal(SyncStage.Completed, second.State.Stage);
        var feed = Assert.Single(await repository.GetFeedsAsync());
        Assert.Equal("Example", feed.Title);
        var story = Assert.Single(await repository.GetStoriesAsync(feed.Id));
        Assert.Equal("7:abc", story.Hash);
        Assert.False(story.IsRead);
        Assert.True(story.IsSaved);
        await using (var connection = new Microsoft.Data.Sqlite.SqliteConnection(
            $"Data Source={_databasePath}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM folder_feed;";
            Assert.Equal(1L, await command.ExecuteScalarAsync());
        }
        Assert.NotNull(await repository.GetCheckpointAsync(
            OfflineFirstSynchronizer.SuccessfulSyncCheckpointName));
        Assert.Empty(await repository.GetPendingMutationsAsync(10));
        Assert.Empty(await repository.GetUploadedMutationsAsync());
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task CappedUnreadFallbackDoesNotTruncateAfterEmptyOrRepeatedNonterminalPages(bool emptyFirst)
    {
        var repository = CreateRepository();
        var remote = new ScriptedRemoteService { CappedUnread = true, IncludeSaved = false };
        var first = CreateStory(isRead: false, isSaved: false);
        var later = first with { Hash = "7:later" };
        remote.InventoryStories = emptyFirst ? [later] : [first, later];
        remote.FeedPages.Enqueue(new RemoteStoryPage(emptyFirst ? [] : [first], false));
        remote.FeedPages.Enqueue(new RemoteStoryPage(emptyFirst ? [] : [first], false));
        remote.FeedPages.Enqueue(new RemoteStoryPage([later], false));
        remote.FeedPages.Enqueue(new RemoteStoryPage([], true));

        var result = await CreateSynchronizer(repository, remote)
            .SynchronizeAsync(new SyncRequest("account"));

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { 1, 2, 3 }, remote.RequestedPages);
        Assert.Contains(await repository.GetStoriesAsync(7), story => story.Hash == "7:later");
    }

    [Fact]
    public async Task UnresolvedCappedUnreadFallbackFailsAtConfiguredPageLimit()
    {
        var remote = new ScriptedRemoteService { CappedUnread = true, IncludeSaved = false };
        for (var i = 0; i < 4; i++) remote.FeedPages.Enqueue(new RemoteStoryPage([], false));

        var result = await CreateSynchronizer(
            CreateRepository(), remote, new SyncOptions { MaximumStoryPages = 3 })
            .SynchronizeAsync(new SyncRequest("account"));

        Assert.Equal(SyncOutcome.MalformedRemoteData, result.Outcome);
        Assert.Equal(new[] { 1, 2, 3 }, remote.RequestedPages);
    }

    [Theory]
    [InlineData(SyncStage.Initialize)]
    [InlineData(SyncStage.UploadPendingMutations)]
    [InlineData(SyncStage.RefreshFeedsAndFolders)]
    [InlineData(SyncStage.FetchStories)]
    [InlineData(SyncStage.Reconcile)]
    [InlineData(SyncStage.Checkpoint)]
    public async Task RestartAfterEveryCompletedStageConverges(SyncStage interruptedStage)
    {
        var repository = CreateRepository();
        var remote = new ScriptedRemoteService();
        var interrupted = CreateSynchronizer(
            repository,
            remote,
            observer: new InterruptAfterStage(interruptedStage));

        await Assert.ThrowsAsync<SimulatedInterruptionException>(
            () => interrupted.SynchronizeAsync(new SyncRequest("account")));

        var resumed = await CreateSynchronizer(repository, remote)
            .SynchronizeAsync(new SyncRequest("account"));

        Assert.True(resumed.IsSuccess);
        Assert.Single(await repository.GetFeedsAsync());
        Assert.Single(await repository.GetStoriesAsync(7));
        Assert.NotNull(await repository.GetCheckpointAsync(
            OfflineFirstSynchronizer.SuccessfulSyncCheckpointName));
    }

    [Fact]
    public async Task DurableUploadAcknowledgementSurvivesKillBeforeReconcile()
    {
        var repository = CreateRepository();
        await SeedPendingReadMutationAsync(repository, value: true);
        var remote = new ScriptedRemoteService
        {
            FeedStory = CreateStory(isRead: false, isSaved: false),
            UnreadHashes = new HashSet<string>(StringComparer.Ordinal)
        };
        var interrupted = CreateSynchronizer(
            repository,
            remote,
            observer: new InterruptAfterStage(SyncStage.UploadPendingMutations));

        await Assert.ThrowsAsync<SimulatedInterruptionException>(
            () => interrupted.SynchronizeAsync(new SyncRequest("account")));

        Assert.Single(await repository.GetPendingMutationsAsync(10));
        Assert.Single(await repository.GetUploadedMutationsAsync());
        Assert.Equal(1, remote.UploadCount);

        var resumed = await CreateSynchronizer(repository, remote)
            .SynchronizeAsync(new SyncRequest("account"));

        Assert.True(resumed.IsSuccess);
        Assert.Equal(1, remote.UploadCount);
        Assert.True(Assert.Single(await repository.GetStoriesAsync(7)).IsRead);
        Assert.Empty(await repository.GetPendingMutationsAsync(10));
        Assert.Empty(await repository.GetUploadedMutationsAsync());
    }

    [Fact]
    public async Task StaleRemoteStateKeepsUploadedBoundaryUntilObserved()
    {
        var repository = CreateRepository();
        await SeedPendingReadMutationAsync(repository, value: true);
        var remote = new ScriptedRemoteService
        {
            FeedStory = CreateStory(isRead: false, isSaved: false),
            UnreadHashes = new HashSet<string>(["7:abc"], StringComparer.Ordinal)
        };

        var first = await CreateSynchronizer(repository, remote)
            .SynchronizeAsync(new SyncRequest("account"));

        Assert.True(first.IsSuccess);
        Assert.True(Assert.Single(await repository.GetStoriesAsync(7)).IsRead);
        Assert.Single(await repository.GetPendingMutationsAsync(10));
        Assert.Single(await repository.GetUploadedMutationsAsync());
        Assert.Equal(1, remote.UploadCount);

        remote.UnreadHashes = new HashSet<string>(StringComparer.Ordinal);
        var second = await CreateSynchronizer(repository, remote)
            .SynchronizeAsync(new SyncRequest("account"));

        Assert.True(second.IsSuccess);
        Assert.Equal(1, remote.UploadCount);
        Assert.Empty(await repository.GetPendingMutationsAsync(10));
        Assert.Empty(await repository.GetUploadedMutationsAsync());
    }

    [Fact]
    public async Task UploadedSnapshotCannotAcknowledgeOrResurrectNewerLocalIntent()
    {
        var repository = CreateRepository();
        await SeedPendingReadMutationAsync(repository, value: true);
        var remote = new ScriptedRemoteService
        {
            FeedStory = CreateStory(isRead: true, isSaved: false)
        };
        var interrupted = CreateSynchronizer(
            repository,
            remote,
            observer: new InterruptAfterStage(SyncStage.UploadPendingMutations));

        await Assert.ThrowsAsync<SimulatedInterruptionException>(
            () => interrupted.SynchronizeAsync(new SyncRequest("account")));
        _time.Advance(TimeSpan.FromSeconds(1));
        await repository.SetStoryReadAsync("7:abc", false);

        var resumed = await CreateSynchronizer(repository, remote)
            .SynchronizeAsync(new SyncRequest("account"));

        Assert.True(resumed.IsSuccess);
        Assert.Equal([true, false], remote.UploadedValues);
        Assert.False(Assert.Single(await repository.GetStoriesAsync(7)).IsRead);
        Assert.Empty(await repository.GetPendingMutationsAsync(10));
    }

    [Theory]
    [InlineData(SyncRemoteFailureKind.Offline, SyncOutcome.Offline)]
    [InlineData(SyncRemoteFailureKind.Authentication, SyncOutcome.AuthenticationRequired)]
    [InlineData(SyncRemoteFailureKind.MalformedData, SyncOutcome.MalformedRemoteData)]
    public async Task TypedRemoteFailuresDoNotReplaceCachedData(
        SyncRemoteFailureKind failureKind,
        SyncOutcome expectedOutcome)
    {
        var repository = CreateRepository();
        await SeedCachedStoryAsync(repository);
        var remote = new ScriptedRemoteService();
        remote.Failures.Enqueue(new SyncRemoteException("scripted", failureKind));

        var result = await CreateSynchronizer(repository, remote)
            .SynchronizeAsync(new SyncRequest("account"));

        Assert.Equal(expectedOutcome, result.Outcome);
        Assert.Single(await repository.GetStoriesAsync(7));
        Assert.Null(await repository.GetCheckpointAsync(
            OfflineFirstSynchronizer.SuccessfulSyncCheckpointName));
    }

    [Fact]
    public async Task TransientFailuresUseDeterministicBoundedExponentialRetry()
    {
        var repository = CreateRepository();
        var remote = new ScriptedRemoteService();
        remote.Failures.Enqueue(new SyncRemoteException(
            "first",
            SyncRemoteFailureKind.Transient));
        remote.Failures.Enqueue(new SyncRemoteException(
            "second",
            SyncRemoteFailureKind.Transient));
        var delays = new RecordingDelay();
        var subject = CreateSynchronizer(
            repository,
            remote,
            options: new SyncOptions
            {
                MaximumNetworkAttempts = 3,
                MinimumRequestInterval = TimeSpan.Zero,
                MinimumSavedRequestInterval = TimeSpan.Zero,
                InitialRetryDelay = TimeSpan.FromSeconds(2),
                MaximumRetryDelay = TimeSpan.FromSeconds(3),
                RetryJitterRatio = 0,
                RetentionAge = TimeSpan.FromDays(90)
            },
            delay: delays);

        var result = await subject.SynchronizeAsync(new SyncRequest("account"));

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(3)],
            delays.Delays);
    }

    [Fact]
    public async Task ExhaustedTransientFailureReturnsTypedResult()
    {
        var remote = new ScriptedRemoteService();
        remote.Failures.Enqueue(new SyncRemoteException(
            "first",
            SyncRemoteFailureKind.Transient));
        remote.Failures.Enqueue(new SyncRemoteException(
            "second",
            SyncRemoteFailureKind.Transient));

        var result = await CreateSynchronizer(
            CreateRepository(),
            remote,
            options: new SyncOptions
            {
                MaximumNetworkAttempts = 2,
                InitialRetryDelay = TimeSpan.Zero,
                MaximumRetryDelay = TimeSpan.Zero,
                RetryJitterRatio = 0
            },
            delay: new RecordingDelay())
            .SynchronizeAsync(new SyncRequest("account"));

        Assert.Equal(SyncOutcome.TransientFailure, result.Outcome);
    }

    [Fact]
    public async Task RetryJitterRemainsDeterministicAndBounded()
    {
        var remote = new ScriptedRemoteService();
        remote.Failures.Enqueue(new SyncRemoteException(
            "first",
            SyncRemoteFailureKind.Transient));
        remote.Failures.Enqueue(new SyncRemoteException(
            "second",
            SyncRemoteFailureKind.Transient));
        var delays = new RecordingDelay();
        var subject = new OfflineFirstSynchronizer(
            CreateRepository(),
            remote,
            new SyncOptions
            {
                MaximumNetworkAttempts = 3,
                MinimumRequestInterval = TimeSpan.Zero,
                MinimumSavedRequestInterval = TimeSpan.Zero,
                InitialRetryDelay = TimeSpan.FromSeconds(2),
                MaximumRetryDelay = TimeSpan.FromSeconds(3),
                RetryJitterRatio = 0.5
            },
            _time,
            new FixedRandom(1),
            delays);

        var result = await subject.SynchronizeAsync(new SyncRequest("account"));

        Assert.True(result.IsSuccess);
        Assert.Equal(
            [TimeSpan.FromSeconds(3), TimeSpan.FromSeconds(3)],
            delays.Delays);
    }

    [Fact]
    public async Task CancellationReturnsTypedResultAndLeavesNoCheckpoint()
    {
        var repository = CreateRepository();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        var result = await CreateSynchronizer(repository, new ScriptedRemoteService())
            .SynchronizeAsync(new SyncRequest("account"), cancellation.Token);

        Assert.Equal(SyncOutcome.Canceled, result.Outcome);
        await repository.InitializeAsync();
        Assert.Null(await repository.GetCheckpointAsync(
            OfflineFirstSynchronizer.SuccessfulSyncCheckpointName));
    }

    [Fact]
    public async Task SameAccountSynchronizationsAreExclusive()
    {
        var remote = new BlockingCatalogRemoteService();
        var first = CreateSynchronizer(CreateRepository(), remote)
            .SynchronizeAsync(new SyncRequest("same-account"));
        await remote.FirstCatalogEntered.Task.WaitAsync(TimeSpan.FromSeconds(5));

        var second = CreateSynchronizer(CreateRepository(), remote)
            .SynchronizeAsync(new SyncRequest("same-account"));
        await Task.Delay(50);
        Assert.Equal(1, remote.CatalogCallCount);

        remote.ReleaseCatalog.TrySetResult();
        var results = await Task.WhenAll(first, second);

        Assert.All(results, result => Assert.True(result.IsSuccess));
        Assert.Equal(2, remote.CatalogCallCount);
    }

    [Fact]
    public async Task CatalogAndFirstBatchAreDurableAndReportedWhileOtherDownloadsAreBlocked()
    {
        var repository = CreateRepository();
        var remote = new GatedRemoteService(feedCount: 2);
        var progress = new RecordingProgress();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var sync = CreateSynchronizer(repository, remote)
            .SynchronizeAsync(new SyncRequest("account"), progress, cancellation.Token);
        try
        {
            var requests = (await remote.TakeAsync(2)).OrderByDescending(request => request.Hashes.Length).ToArray();
            Assert.False(sync.IsCompleted);
            var reopened = CreateRepository();
            Assert.Equal(2, (await reopened.GetFeedsAsync()).Count);
            Assert.Equal(2, Assert.Single(await reopened.GetFeedFoldersAsync()).Feeds.Count);
            Assert.Contains(progress.States, state =>
                state.Stage == SyncStage.RefreshFeedsAndFolders
                && state.LocalRevision == 1 && state.FeedCount == 2);

            requests[0].CompleteAll();
            await progress.FirstBodyCommitted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.False(sync.IsCompleted);
            Assert.Equal(100, (await reopened.GetCachedStoryIndexAsync()).Count);
            Assert.Null(await reopened.GetCheckpointAsync(
                OfflineFirstSynchronizer.SuccessfulSyncCheckpointName));
            Assert.Contains(progress.States, state =>
                state.Stage == SyncStage.FetchStories && state.DownloadedPageCount == 1
                && state.StoryCount == 100 && state.LocalRevision > 1
                && state.CompletedFeedCount == 1);

            requests[1].CompleteAll();
            var result = await sync.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.True(result.IsSuccess);
            Assert.Equal(120, result.State.StoryCount);
            Assert.Equal(2, result.State.DownloadedPageCount);
            Assert.Equal(2, result.State.CompletedFeedCount);
            Assert.Equal(60, (await reopened.GetStoriesAsync(7)).Count);
            var revisions = progress.States.Select(state => state.LocalRevision).ToArray();
            Assert.Equal(revisions.Order(), revisions);
            Assert.NotNull(await reopened.GetCheckpointAsync(
                OfflineFirstSynchronizer.SuccessfulSyncCheckpointName));
        }
        finally
        {
            await cancellation.CancelAsync();
            await sync.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(4)]
    [InlineData(8)]
    public async Task NetworkRequestsOverlapWithoutExceedingConfiguredBound(int concurrency)
    {
        var remote = new GatedRemoteService(feedCount: 20);
        // Each gated operation is bounded; persisting all 1,200 stories is not a throughput assertion.
        using var cancellation = new CancellationTokenSource();
        var delay = new RecordingDelay();
        // Exercise the default rather than explicitly configuring four slots.
        var options = concurrency == 4 ? new SyncOptions()
            : new SyncOptions { MaximumConcurrentRequests = concurrency };
        var sync = CreateSynchronizer(CreateRepository(), remote, options, delay)
            .SynchronizeAsync(new SyncRequest("account"), cancellation.Token);
        try
        {
            var initial = await remote.TakeAsync(concurrency);
            Assert.Equal(concurrency, remote.ActiveRequests);
            Assert.False(sync.IsCompleted);
            foreach (var request in initial) request.CompleteAll();
            for (var remaining = 12 - concurrency; remaining > 0; remaining--)
                Assert.Single(await remote.TakeAsync(1)).CompleteAll();

            var result = await sync.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(result.IsSuccess);
            Assert.Equal(concurrency, remote.MaximumActiveRequests);
            Assert.Equal(0, remote.ActiveRequests);
            Assert.Equal(16, result.State.NetworkAttemptCount);
            Assert.Equal(12, result.State.DownloadedPageCount);
            Assert.Equal(20, result.State.CompletedFeedCount);
            Assert.Empty(delay.Delays);
        }
        finally
        {
            await cancellation.CancelAsync();
            await sync.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LaterFailureOrCancellationPreservesCommittedBatchesWithoutCheckpoint(bool cancel)
    {
        var remote = new GatedRemoteService(feedCount: 2);
        var progress = new RecordingProgress();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var sync = CreateSynchronizer(CreateRepository(), remote)
            .SynchronizeAsync(new SyncRequest("account"), progress, cancellation.Token);
        try
        {
            var requests = (await remote.TakeAsync(2)).OrderByDescending(request => request.Hashes.Length).ToArray();
            requests[0].CompleteAll();
            await progress.FirstBodyCommitted.Task.WaitAsync(TimeSpan.FromSeconds(5));
            if (cancel) await cancellation.CancelAsync();
            else requests[1].Fail(new SyncRemoteException("later batch failed", SyncRemoteFailureKind.Permanent));

            var result = await sync.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.Equal(cancel ? SyncOutcome.Canceled : SyncOutcome.PermanentFailure, result.Outcome);
            Assert.Equal(SyncStage.FetchStories, result.State.Stage);
            Assert.Equal(100, result.State.StoryCount);
            Assert.Equal(1, result.State.DownloadedPageCount);
            Assert.True(result.State.LocalRevision > 1);
            var reopened = CreateRepository();
            Assert.Equal(2, (await reopened.GetFeedsAsync()).Count);
            Assert.Equal(100, (await reopened.GetCachedStoryIndexAsync()).Count);
            Assert.Null(await reopened.GetCheckpointAsync(
                OfflineFirstSynchronizer.SuccessfulSyncCheckpointName));
            Assert.Equal(0, remote.ActiveRequests);
        }
        finally
        {
            await cancellation.CancelAsync();
            await sync.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public async Task LocalReadAndSaveWhileFetchIsPendingSurviveBatchAndFinalReconciliation()
    {
        var repository = CreateRepository();
        await SeedCachedStoryAsync(repository);
        var remote = new GatedRemoteService(feedCount: 1);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var sync = CreateSynchronizer(repository, remote)
            .SynchronizeAsync(new SyncRequest("account"), cancellation.Token);
        try
        {
            var request = Assert.Single(await remote.TakeAsync(1));
            await repository.SetStoryReadAsync("7:abc", true);
            await repository.SetStorySavedAsync("7:abc", true);
            var pending = await repository.GetPendingMutationsAsync(10);
            request.CompleteAll();
            Assert.True((await sync.WaitAsync(TimeSpan.FromSeconds(5))).IsSuccess);
            var cached = (await CreateRepository().GetStoriesAsync(7)).Single(story => story.Hash == "7:abc");
            Assert.True(cached.IsRead);
            Assert.True(cached.IsSaved);

            var final = (await CreateRepository().GetStoriesAsync(7)).Single(story => story.Hash == "7:abc");
            Assert.True(final.IsRead);
            Assert.True(final.IsSaved);
            Assert.Equal(pending, await repository.GetPendingMutationsAsync(10));
            Assert.Empty(await repository.GetUploadedMutationsAsync());
        }
        finally
        {
            await cancellation.CancelAsync();
            await sync.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public async Task RetryReportsSameBatchAndAttemptWithoutCountingUncommittedBodies()
    {
        var remote = new GatedRemoteService(feedCount: 1);
        var progress = new RecordingProgress();
        var delay = new RecordingDelay();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var sync = CreateSynchronizer(CreateRepository(), remote,
            new SyncOptions { MaximumConcurrentRequests = 1, RetryJitterRatio = 0 }, delay)
            .SynchronizeAsync(new SyncRequest("account"), progress, cancellation.Token);
        try
        {
            var feed = Assert.Single(await remote.TakeAsync(1));
            feed.Fail(new SyncRemoteException("retry batch", SyncRemoteFailureKind.Transient));
            var retry = Assert.Single(await remote.TakeAsync(1));
            Assert.Equal(feed.Hashes, retry.Hashes);
            var state = progress.States.Last();
            Assert.Equal("Recent articles", state.CurrentFeedTitle);
            Assert.Equal(2, state.RetryAttempt);
            Assert.Equal(6, state.NetworkAttemptCount);
            Assert.Equal(0, state.DownloadedPageCount);
            Assert.Equal(0, state.StoryCount);
            Assert.Equal(2, state.LocalRevision);
            Assert.Equal([TimeSpan.FromSeconds(1)], delay.Delays);

            retry.CompleteAll();
            var result = await sync.WaitAsync(TimeSpan.FromSeconds(5));
            Assert.True(result.IsSuccess);
            Assert.Equal(1, result.State.DownloadedPageCount);
            Assert.Equal(60, result.State.StoryCount);
            Assert.Equal(6, result.State.NetworkAttemptCount);
        }
        finally
        {
            await cancellation.CancelAsync();
            await sync.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    [Fact]
    public async Task ThrowingProgressCallbackReleasesNetworkSlotBeforeRetry()
    {
        var progress = new RecordingProgress(state =>
        {
            if (state.NetworkAttemptCount == 1)
                throw new SyncRemoteException("progress failed", SyncRemoteFailureKind.Transient);
        });
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var result = await CreateSynchronizer(CreateRepository(), new ScriptedRemoteService(),
            new SyncOptions { MaximumConcurrentRequests = 1 })
            .SynchronizeAsync(new SyncRequest("account"), progress, cancellation.Token);

        Assert.True(result.IsSuccess);
        Assert.Contains(progress.States, state => state.RetryAttempt == 2);
        Assert.Equal(7, result.State.NetworkAttemptCount);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private SqliteContentRepository CreateRepository() =>
        new(new SqliteConnectionFactory(_databasePath), _time);

    private OfflineFirstSynchronizer CreateSynchronizer(
        IContentRepository repository,
        ISyncRemoteService remote,
        SyncOptions? options = null,
        ISyncDelay? delay = null,
        ISyncStageObserver? observer = null) =>
        new(
            repository,
            remote,
            (options ?? new SyncOptions()) with
            {
                MinimumRequestInterval = TimeSpan.Zero,
                MinimumSavedRequestInterval = TimeSpan.Zero
            },
            _time,
            new FixedRandom(0.5),
            delay ?? new RecordingDelay(),
            observer);

    private async Task SeedPendingReadMutationAsync(
        IContentRepository repository,
        bool value)
    {
        await SeedCachedStoryAsync(repository);
        await repository.SetStoryReadAsync("7:abc", value);
    }

    private static async Task SeedCachedStoryAsync(IContentRepository repository)
    {
        await repository.InitializeAsync();
        await repository.UpsertFeedAsync(CreateFeed());
        await repository.UpsertStoryAsync(CreateStory(isRead: false, isSaved: false));
    }

    private static Feed CreateFeed() =>
        new(7, "Example", "https://example.test/feed", null, 1, null, true);

    private static Story CreateStory(bool isRead, bool isSaved) =>
        new(
            "7:abc",
            7,
            "service-id",
            "guid",
            "Story",
            "Author",
            "https://example.test/story",
            "<p>Story</p>",
            "Story",
            null,
            new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero),
            isRead,
            isSaved);

    private sealed class ScriptedRemoteService : ISyncRemoteService
    {
        public Queue<SyncRemoteException> Failures { get; } = new();

        public Story FeedStory { get; init; } = CreateStory(isRead: true, isSaved: false);
        public Story[]? InventoryStories { get; set; }
        public bool CappedUnread { get; init; }
        public bool IncludeSaved { get; init; } = true;
        public Queue<RemoteStoryPage> FeedPages { get; } = new();
        public List<int> RequestedPages { get; } = [];

        public IReadOnlySet<string> UnreadHashes { get; set; } =
            new HashSet<string>(["7:abc"], StringComparer.Ordinal);

        public int UploadCount { get; private set; }

        public List<bool> UploadedValues { get; } = [];

        public Task<RemoteStoryInventory> GetStoryHashInventoryAsync(
            bool unreadOnly, IReadOnlyCollection<int> feedIds, CancellationToken cancellationToken)
        {
            ThrowIfScripted();
            var stories = InventoryStories ?? [FeedStory];
            IReadOnlyList<RemoteStoryHash> hashes = unreadOnly && CappedUnread
                ? Enumerable.Range(0, 500).Select(index =>
                    new RemoteStoryHash($"7:capped-{index}", 7, FeedStory.PublishedAt.ToUnixTimeSeconds())).ToArray()
                : stories.Where(story => !unreadOnly || UnreadHashes.Contains(story.Hash))
                    .Select(story => new RemoteStoryHash(story.Hash, story.FeedId, story.PublishedAt.ToUnixTimeSeconds()))
                    .ToArray();
            return Task.FromResult(new RemoteStoryInventory(new Dictionary<int, IReadOnlyList<RemoteStoryHash>>
            {
                [7] = hashes
            }));
        }

        public Task<IReadOnlyDictionary<string, double>> GetSavedStoryHashesAsync(CancellationToken cancellationToken)
        {
            ThrowIfScripted();
            return Task.FromResult<IReadOnlyDictionary<string, double>>(IncludeSaved
                ? new Dictionary<string, double> { ["7:abc"] = 1 }
                : new Dictionary<string, double>());
        }

        public Task<RemoteStoryPage> GetStoriesByHashesAsync(
            IReadOnlyCollection<string> hashes, bool saved, CancellationToken cancellationToken)
        {
            ThrowIfScripted();
            return Task.FromResult(new RemoteStoryPage(
                (InventoryStories ?? [FeedStory]).Where(story => hashes.Contains(story.Hash)).ToArray(), true));
        }

        public Task UploadMutationAsync(
            PendingStoryMutation mutation,
            CancellationToken cancellationToken)
        {
            ThrowIfScripted();
            UploadCount++;
            UploadedValues.Add(mutation.Value);
            return Task.CompletedTask;
        }

        public Task<RemoteFeedCatalog> GetFeedCatalogAsync(
            CancellationToken cancellationToken)
        {
            ThrowIfScripted();
            return Task.FromResult(new RemoteFeedCatalog(
                [CreateFeed()],
                [new Folder("Tech", "Tech", 0)],
                [new FolderFeed("Tech", 7, 0)]));
        }

        public Task<RemoteStoryPage> GetFeedStoriesAsync(
            int feedId,
            int page,
            CancellationToken cancellationToken)
        {
            ThrowIfScripted();
            RequestedPages.Add(page);
            if (FeedPages.TryDequeue(out var response)) return Task.FromResult(response);
            return Task.FromResult(new RemoteStoryPage([FeedStory], IsLastPage: true));
        }

        public Task<IReadOnlySet<string>> GetUnreadStoryHashesAsync(
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Legacy global unread requests are not expected.");

        public Task<RemoteStoryPage> GetStarredStoriesAsync(
            int page,
            CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Saved stories must use hash inventory and body requests.");

        private void ThrowIfScripted()
        {
            if (Failures.TryDequeue(out var failure))
            {
                throw failure;
            }
        }
    }

    private sealed class BlockingCatalogRemoteService : ISyncRemoteService
    {
        public Task<RemoteStoryInventory> GetStoryHashInventoryAsync(
            bool unreadOnly, IReadOnlyCollection<int> feedIds, CancellationToken cancellationToken) =>
            Task.FromResult(new RemoteStoryInventory(new Dictionary<int, IReadOnlyList<RemoteStoryHash>>()));

        public Task<IReadOnlyDictionary<string, double>> GetSavedStoryHashesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, double>>(new Dictionary<string, double>());

        public Task<RemoteStoryPage> GetStoriesByHashesAsync(
            IReadOnlyCollection<string> hashes, bool saved, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("An empty catalog has no missing bodies.");

        public TaskCompletionSource FirstCatalogEntered { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource ReleaseCatalog { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public int CatalogCallCount { get; private set; }

        public Task UploadMutationAsync(
            PendingStoryMutation mutation,
            CancellationToken cancellationToken) => Task.CompletedTask;

        public async Task<RemoteFeedCatalog> GetFeedCatalogAsync(
            CancellationToken cancellationToken)
        {
            CatalogCallCount++;
            if (CatalogCallCount == 1)
            {
                FirstCatalogEntered.TrySetResult();
                await ReleaseCatalog.Task.WaitAsync(cancellationToken);
            }

            return new RemoteFeedCatalog([], [], []);
        }

        public Task<RemoteStoryPage> GetFeedStoriesAsync(
            int feedId,
            int page,
            CancellationToken cancellationToken) =>
            Task.FromResult(new RemoteStoryPage([], true));

        public Task<IReadOnlySet<string>> GetUnreadStoryHashesAsync(
            CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlySet<string>>(new HashSet<string>());

        public Task<RemoteStoryPage> GetStarredStoriesAsync(
            int page,
            CancellationToken cancellationToken) =>
            Task.FromResult(new RemoteStoryPage([], true));
    }

    private sealed class RecordingProgress(Action<SyncState>? onReport = null) : IProgress<SyncState>
    {
        public ConcurrentQueue<SyncState> States { get; } = new();
        public TaskCompletionSource FirstBodyCommitted { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Report(SyncState value)
        {
            States.Enqueue(value);
            if (value.DownloadedPageCount > 0) FirstBodyCommitted.TrySetResult();
            onReport?.Invoke(value);
        }
    }

    private sealed class GatedRemoteService(int feedCount) : ISyncRemoteService
    {
        public Task<RemoteStoryInventory> GetStoryHashInventoryAsync(
            bool unreadOnly, IReadOnlyCollection<int> feedIds, CancellationToken cancellationToken) =>
            Task.FromResult(new RemoteStoryInventory(feedIds.ToDictionary(id => id,
                id => (IReadOnlyList<RemoteStoryHash>)Enumerable.Range(0, 60)
                    .Select(index => new RemoteStoryHash($"{id}:{(index == 0 ? "abc" : index)}", id,
                        CreateStory(false, false).PublishedAt.ToUnixTimeSeconds() - index)).ToArray())));

        public Task<IReadOnlyDictionary<string, double>> GetSavedStoryHashesAsync(CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyDictionary<string, double>>(new Dictionary<string, double>());

        public Task<RemoteStoryPage> GetStoriesByHashesAsync(
            IReadOnlyCollection<string> hashes, bool saved, CancellationToken cancellationToken) =>
            FetchAsync(hashes, cancellationToken);

        private readonly Channel<GatedRequest> _requests = Channel.CreateUnbounded<GatedRequest>();
        private readonly object _gate = new();
        private int _active;
        private int _maximumActive;

        public int ActiveRequests { get { lock (_gate) return _active; } }
        public int MaximumActiveRequests { get { lock (_gate) return _maximumActive; } }

        public async Task<GatedRequest[]> TakeAsync(int count)
        {
            var requests = new GatedRequest[count];
            for (var index = 0; index < count; index++)
                requests[index] = await _requests.Reader.ReadAsync().AsTask()
                    .WaitAsync(TimeSpan.FromSeconds(5));
            return requests;
        }

        public Task UploadMutationAsync(PendingStoryMutation mutation, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Mutations must be created after the upload stage.");

        public Task<RemoteFeedCatalog> GetFeedCatalogAsync(CancellationToken cancellationToken) =>
            Task.FromResult(new RemoteFeedCatalog(
                Enumerable.Range(7, feedCount).Select(id => CreateFeed() with { Id = id, Title = $"Feed {id}" }).ToArray(),
                [new Folder("tech", "Tech", 0)],
                Enumerable.Range(7, feedCount).Select(id => new FolderFeed("tech", id, id - 7)).ToArray()));

        public Task<IReadOnlySet<string>> GetUnreadStoryHashesAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Legacy global unread requests are not expected.");

        public Task<RemoteStoryPage> GetFeedStoriesAsync(int feedId, int page, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Normal sync must not download per-feed pages.");

        public Task<RemoteStoryPage> GetStarredStoriesAsync(int page, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Normal sync must not page saved stories.");

        private async Task<RemoteStoryPage> FetchAsync(IReadOnlyCollection<string> hashes, CancellationToken cancellationToken)
        {
            lock (_gate)
            {
                _active++;
                _maximumActive = Math.Max(_maximumActive, _active);
            }
            try
            {
                var request = new GatedRequest(hashes.ToArray());
                await _requests.Writer.WriteAsync(request, cancellationToken);
                return await request.Response.Task.WaitAsync(cancellationToken);
            }
            finally
            {
                lock (_gate) _active--;
            }
        }
    }

    private sealed class GatedRequest(string[] hashes)
    {
        public string[] Hashes { get; } = hashes;
        public TaskCompletionSource<RemoteStoryPage> Response { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public void Complete(IReadOnlyList<Story> stories, bool isLastPage = true) =>
            Response.SetResult(new RemoteStoryPage(stories, isLastPage));

        public void CompleteAll() => Complete(Hashes.Select(hash => CreateStory(false, false) with
        {
            Hash = hash,
            FeedId = int.Parse(hash.Split(':')[0], System.Globalization.CultureInfo.InvariantCulture)
        }).ToArray());

        public void Fail(Exception exception) => Response.SetException(exception);
    }

    private sealed class InterruptAfterStage(SyncStage stage) : ISyncStageObserver
    {
        public Task StageCompletedAsync(
            SyncState state,
            CancellationToken cancellationToken)
        {
            if (state.Stage == stage)
            {
                throw new SimulatedInterruptionException(stage);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class SimulatedInterruptionException(SyncStage stage)
        : Exception($"Interrupted after {stage}.");

    private sealed class RecordingDelay : ISyncDelay
    {
        public List<TimeSpan> Delays { get; } = [];

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Delays.Add(delay);
            return Task.CompletedTask;
        }
    }

    private sealed class FixedRandom(double value) : ISyncRandom
    {
        public double NextDouble() => value;
    }

    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow += duration;
    }
}
