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
    public async Task NonterminalEmptyAndRepeatedPagesDoNotTruncateLaterStories(bool emptyFirst)
    {
        var repository = CreateRepository();
        var remote = new ScriptedRemoteService();
        var first = CreateStory(isRead: false, isSaved: false);
        var later = first with { Hash = "7:later" };
        remote.FeedPages.Enqueue(new RemoteStoryPage(emptyFirst ? [] : [first], false));
        remote.FeedPages.Enqueue(new RemoteStoryPage(emptyFirst ? [] : [first], false));
        remote.FeedPages.Enqueue(new RemoteStoryPage([later], false));
        remote.FeedPages.Enqueue(new RemoteStoryPage([], true));

        var result = await CreateSynchronizer(repository, remote)
            .SynchronizeAsync(new SyncRequest("account"));

        Assert.True(result.IsSuccess);
        Assert.Equal(new[] { 1, 2, 3, 4 }, remote.RequestedPages);
        Assert.Contains(await repository.GetStoriesAsync(7), story => story.Hash == "7:later");
    }

    [Fact]
    public async Task NonterminalPagesRemainBoundedByConfiguredLimit()
    {
        var remote = new ScriptedRemoteService();
        for (var i = 0; i < 4; i++) remote.FeedPages.Enqueue(new RemoteStoryPage([], false));

        var result = await CreateSynchronizer(
            CreateRepository(), remote, new SyncOptions { MaximumStoryPages = 3 })
            .SynchronizeAsync(new SyncRequest("account"));

        Assert.True(result.IsSuccess);
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
            options,
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
        public Queue<RemoteStoryPage> FeedPages { get; } = new();
        public List<int> RequestedPages { get; } = [];

        public IReadOnlySet<string> UnreadHashes { get; set; } =
            new HashSet<string>(["7:abc"], StringComparer.Ordinal);

        public int UploadCount { get; private set; }

        public List<bool> UploadedValues { get; } = [];

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
            CancellationToken cancellationToken)
        {
            ThrowIfScripted();
            return Task.FromResult(UnreadHashes);
        }

        public Task<RemoteStoryPage> GetStarredStoriesAsync(
            int page,
            CancellationToken cancellationToken)
        {
            ThrowIfScripted();
            return Task.FromResult(new RemoteStoryPage(
                [CreateStory(isRead: false, isSaved: true)],
                IsLastPage: true));
        }

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
