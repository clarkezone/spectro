using System.Collections.Concurrent;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Channels;
using NewsBlurSharp;
using Spectro.Domain;
using Spectro.Infrastructure;

namespace Spectro.Sync.Tests;

public sealed class RateLimitBackoffTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "Spectro.Sync.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _account = Guid.NewGuid().ToString("N");
    private readonly TestTimeProvider _time = new(
        new DateTimeOffset(2026, 9, 15, 21, 22, 0, TimeSpan.Zero));

    [Theory]
    [InlineData(SyncRemoteFailureKind.RateLimited, 429, null, SyncOutcome.RateLimited, 0)]
    [InlineData(SyncRemoteFailureKind.RateLimited, 429, null, SyncOutcome.RateLimited, 0.5)]
    [InlineData(SyncRemoteFailureKind.RateLimited, 429, null, SyncOutcome.RateLimited, 1)]
    [InlineData(SyncRemoteFailureKind.RateLimited, 429, 120, SyncOutcome.RateLimited, 0.5)]
    [InlineData(SyncRemoteFailureKind.Transient, 503, 120, SyncOutcome.TransientFailure, 0.5)]
    public async Task CooldownPersistsAcrossManualRetryAndRestartAndExpires(
        SyncRemoteFailureKind kind, int status, int? retrySeconds, SyncOutcome outcome, double random)
    {
        var repository = CreateRepository();
        await SeedAsync(repository);
        await repository.SetStoryReadAsync("7:cached", true);
        await repository.SetStorySavedAsync("7:cached", true);
        var mutations = await repository.GetPendingMutationsAsync(10);
        var stories = await repository.GetStoriesAsync(7);
        var deadline = _time.GetUtcNow().AddSeconds(retrySeconds ?? (360 + random * 30));
        var remote = new ControlledRemote
        {
            Failure = Failure(kind, status, retrySeconds.HasValue ? deadline : null)
        };
        var delay = new RecordingDelay();
        var subject = CreateSynchronizer(repository, remote, delay: delay, random: random);

        var first = await subject.SynchronizeAsync(new SyncRequest(_account));

        AssertPause(first, outcome, deadline, status);
        Assert.Single(remote.Calls);
        Assert.Equal(1, first.State.NetworkAttemptCount);
        Assert.Empty(delay.Delays);
        await AssertCheckpointAsync(repository, deadline, kind, status);
        Assert.Null(await repository.GetCheckpointAsync(
            OfflineFirstSynchronizer.SuccessfulSyncCheckpointName));
        Assert.Equal(mutations, await repository.GetPendingMutationsAsync(10));
        Assert.Empty(await repository.GetUploadedMutationsAsync());
        Assert.Equal(stories, await repository.GetStoriesAsync(7));

        var manual = await subject.SynchronizeAsync(new SyncRequest(_account));
        AssertPause(manual, outcome, deadline, status);
        Assert.Equal(0, manual.State.NetworkAttemptCount);
        Assert.Single(remote.Calls);

        // A new repository and remote eliminate any reliance on instance-local state.
        var reopened = CreateRepository();
        var restartedRemote = new ControlledRemote
        {
            SavedHashes = new Dictionary<string, double> { ["7:saved"] = StoryTimestamp }
        };
        var restarted = CreateSynchronizer(reopened, restartedRemote, delay: delay);
        var afterRestart = await restarted.SynchronizeAsync(new SyncRequest(_account));
        AssertPause(afterRestart, outcome, deadline, status);
        Assert.Equal(0, afterRestart.State.NetworkAttemptCount);
        Assert.Empty(restartedRemote.Calls);
        Assert.Equal(mutations, await reopened.GetPendingMutationsAsync(10));
        Assert.Equal(stories, await reopened.GetStoriesAsync(7));

        _time.Advance(deadline - _time.GetUtcNow() - TimeSpan.FromTicks(1));
        AssertPause(await restarted.SynchronizeAsync(new SyncRequest(_account)),
            outcome, deadline, status);
        Assert.Empty(restartedRemote.Calls);

        _time.Advance(TimeSpan.FromTicks(1));
        var expired = await restarted.SynchronizeAsync(new SyncRequest(_account));
        Assert.True(expired.IsSuccess);
        Assert.Null(expired.RetryAt);
        Assert.Null(expired.HttpStatusCode);
        Assert.Contains("upload", restartedRemote.Calls);
        Assert.Contains("catalog", restartedRemote.Calls);
        Assert.Contains("inventory:unread", restartedRemote.Calls);
        Assert.Contains("saved-hashes", restartedRemote.Calls);
        Assert.Contains("bodies:general", restartedRemote.Calls);
        Assert.Contains("bodies:saved", restartedRemote.Calls);
        Assert.Empty(delay.Delays);
        Assert.NotNull(await reopened.GetCheckpointAsync(
            OfflineFirstSynchronizer.SuccessfulSyncCheckpointName));
    }

    [Fact]
    public async Task QueuedManualSyncObservesFirstRunsPersistedCooldown()
    {
        var repository = CreateRepository();
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var deadline = _time.GetUtcNow().AddMinutes(2);
        var remote = new ControlledRemote
        {
            BeforeCatalog = async token =>
            {
                entered.TrySetResult();
                await release.Task.WaitAsync(token);
                throw Failure(SyncRemoteFailureKind.RateLimited, 429, deadline);
            }
        };
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var first = CreateSynchronizer(repository, remote)
            .SynchronizeAsync(new SyncRequest(_account), timeout.Token);
        await entered.Task.WaitAsync(timeout.Token);
        var siblingRemote = new ControlledRemote();
        var queued = CreateSynchronizer(CreateRepository(), siblingRemote)
            .SynchronizeAsync(new SyncRequest(_account), timeout.Token);
        release.SetResult();

        AssertPause(await first, SyncOutcome.RateLimited, deadline, 429);
        AssertPause(await queued, SyncOutcome.RateLimited, deadline, 429);
        Assert.Single(remote.Calls);
        Assert.Empty(siblingRemote.Calls);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PartialBatchesAndLocalMutationsSurviveCooldown(bool failSavedPage)
    {
        var repository = CreateRepository();
        await SeedAsync(repository, saved: true);
        var deadline = _time.GetUtcNow().AddMinutes(3);
        var bodyCalls = 0;
        var remote = new ControlledRemote
        {
            StoriesPerFeed = failSavedPage ? 0 : 201,
            SavedHashes = Enumerable.Range(0, failSavedPage ? 201 : 0)
                .Select(index => $"7:saved-{index}")
                .Append("7:cached")
                .ToDictionary(hash => hash, _ => StoryTimestamp),
            Bodies = async (hashes, saved, token) =>
            {
                Assert.Equal(failSavedPage, saved);
                Assert.Equal(100, hashes.Count);
                if (Interlocked.Increment(ref bodyCalls) == 2)
                    throw Failure(SyncRemoteFailureKind.RateLimited, 429, deadline);
                await repository.SetStoryReadAsync("7:cached", true, token);
                return new RemoteStoryPage(
                    hashes.Select(CreateStory).ToArray(), true);
            }
        };

        var result = await CreateSynchronizer(repository, remote, concurrency: 1)
            .SynchronizeAsync(new SyncRequest(_account));

        AssertPause(result, SyncOutcome.RateLimited, deadline, 429);
        var stories = await repository.GetStoriesAsync(7);
        Assert.Equal(2, bodyCalls);
        Assert.Equal(101, stories.Count);
        var cached = Assert.Single(stories, story => story.Hash == "7:cached");
        Assert.True(cached.IsRead);
        Assert.True(cached.IsSaved);
        Assert.Equal(100, stories.Count(story => story.Hash != "7:cached"));
        Assert.Single(await repository.GetPendingMutationsAsync(10));
        Assert.Empty(await repository.GetUploadedMutationsAsync());
        Assert.Null(await repository.GetCheckpointAsync(
            OfflineFirstSynchronizer.SuccessfulSyncCheckpointName));
        await AssertCheckpointAsync(repository, deadline, SyncRemoteFailureKind.RateLimited, 429);

        var restartedRemote = new ControlledRemote();
        var restartedRepository = CreateRepository();
        AssertPause(await CreateSynchronizer(restartedRepository, restartedRemote)
            .SynchronizeAsync(new SyncRequest(_account)), SyncOutcome.RateLimited, deadline, 429);
        Assert.Empty(restartedRemote.Calls);
        Assert.Equal(stories, await restartedRepository.GetStoriesAsync(7));
        Assert.Single(await restartedRepository.GetPendingMutationsAsync(10));
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task ConcurrentFailuresStopQueuedSiblingsAndPersistGreatestDeadline(bool longestFirst)
    {
        var repository = CreateRepository();
        var requests = Channel.CreateUnbounded<GatedPage>();
        var remote = new ControlledRemote
        {
            FeedCount = 4,
            StoriesPerFeed = 200,
            Bodies = async (hashes, saved, token) =>
            {
                Assert.False(saved);
                Assert.Equal(100, hashes.Count);
                var request = new GatedPage();
                using var registration = token.Register(() => request.Canceled.TrySetResult());
                await requests.Writer.WriteAsync(request, CancellationToken.None);
                // Responses are already in flight; cancellation cannot retract their headers.
                return await request.Response.Task.WaitAsync(TimeSpan.FromSeconds(10));
            }
        };
        var delay = new RecordingDelay();
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var running = CreateSynchronizer(repository, remote, concurrency: 4, delay: delay)
            .SynchronizeAsync(new SyncRequest(_account), timeout.Token);
        var first = await requests.Reader.ReadAsync(timeout.Token);
        var second = await requests.Reader.ReadAsync(timeout.Token);
        var third = await requests.Reader.ReadAsync(timeout.Token);
        var fourth = await requests.Reader.ReadAsync(timeout.Token);
        var shortDeadline = _time.GetUtcNow().AddMinutes(2);
        var longDeadline = _time.GetUtcNow().AddMinutes(9);
        first.Response.SetException(Failure(SyncRemoteFailureKind.RateLimited, 429,
            longestFirst ? longDeadline : shortDeadline));
        await second.Canceled.Task.WaitAsync(timeout.Token);
        second.Response.SetException(Failure(SyncRemoteFailureKind.RateLimited, 429,
            longestFirst ? shortDeadline : longDeadline));
        third.Response.SetException(Failure(SyncRemoteFailureKind.RateLimited, 429, shortDeadline));
        fourth.Response.SetException(Failure(SyncRemoteFailureKind.RateLimited, 429, shortDeadline));

        var result = await running.WaitAsync(timeout.Token);

        Assert.Equal(SyncOutcome.RateLimited, result.Outcome);
        Assert.Equal(4, remote.Calls.Count(call => call == "bodies:general"));
        Assert.DoesNotContain("bodies:saved", remote.Calls);
        Assert.False(requests.Reader.TryRead(out _));
        Assert.Empty(delay.Delays);
        await AssertCheckpointAsync(repository, longDeadline, SyncRemoteFailureKind.RateLimited, 429);
        var restartedRemote = new ControlledRemote();
        AssertPause(await CreateSynchronizer(CreateRepository(), restartedRemote)
            .SynchronizeAsync(new SyncRequest(_account)), SyncOutcome.RateLimited, longDeadline, 429);
        Assert.Empty(restartedRemote.Calls);
        Assert.Equal(longDeadline, result.RetryAt);
    }

    [Theory]
    [InlineData(HttpStatusCode.TooManyRequests, "120", SyncRemoteFailureKind.RateLimited, 120)]
    [InlineData(HttpStatusCode.TooManyRequests, null, SyncRemoteFailureKind.RateLimited, null)]
    [InlineData(HttpStatusCode.TooManyRequests, "invalid", SyncRemoteFailureKind.RateLimited, null)]
    [InlineData(HttpStatusCode.ServiceUnavailable, "120", SyncRemoteFailureKind.Transient, 120)]
    public async Task AdapterPreservesRateLimitStatusAndRetryDeadline(
        HttpStatusCode status, string? retryAfter, SyncRemoteFailureKind kind, int? seconds)
    {
        var remote = new NewsBlurSyncRemoteService(
            new NewsBlurClient(new StatusHandler(status, retryAfter), timeProvider: _time));

        var exception = await Assert.ThrowsAsync<SyncRemoteException>(
            () => remote.GetFeedCatalogAsync(CancellationToken.None));

        Assert.Equal(kind, exception.Kind);
        Assert.Equal((int)status, exception.HttpStatusCode);
        Assert.Equal(seconds.HasValue ? _time.GetUtcNow().AddSeconds(seconds.Value) : (DateTimeOffset?)null,
            exception.RetryAt);
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private SqliteContentRepository CreateRepository() =>
        new(new SqliteConnectionFactory(Path.Combine(_directory, "spectro.db")), _time);

    private OfflineFirstSynchronizer CreateSynchronizer(
        IContentRepository repository, ISyncRemoteService remote, int concurrency = 4,
        RecordingDelay? delay = null, double random = 0.5) =>
        new(repository, remote, new SyncOptions
        {
            MaximumConcurrentRequests = concurrency,
            RecentStoriesPerFeed = 201,
            MinimumRequestInterval = TimeSpan.Zero,
            MinimumSavedRequestInterval = TimeSpan.Zero
        }, _time, new FixedRandom(random), delay: delay ?? new RecordingDelay());

    private static async Task SeedAsync(IContentRepository repository, bool saved = false)
    {
        await repository.InitializeAsync();
        await repository.UpsertFeedAsync(CreateFeed(7));
        await repository.UpsertStoryAsync(CreateStory("7:cached") with { IsSaved = saved });
    }

    private static Feed CreateFeed(int id) =>
        new(id, $"Feed {id}", $"https://example.test/{id}", null, 1, null, true);

    private static readonly double StoryTimestamp =
        new DateTimeOffset(2026, 9, 15, 20, 0, 0, TimeSpan.Zero).ToUnixTimeSeconds();

    private static Story CreateStory(string hash) =>
        new(hash, int.Parse(hash.AsSpan(0, hash.IndexOf(':'))), hash, hash, "Story", "Author", "https://example.test/story",
            "<p>Cached content</p>", "Cached content", null,
            new DateTimeOffset(2026, 9, 15, 20, 0, 0, TimeSpan.Zero), false, false);

    private static SyncRemoteException Failure(
        SyncRemoteFailureKind kind, int status, DateTimeOffset? retryAt) =>
        new("Server requested a pause.", kind) { RetryAt = retryAt, HttpStatusCode = status };

    private static void AssertPause(SyncResult result, SyncOutcome outcome, DateTimeOffset retryAt, int status)
    {
        Assert.False(result.IsSuccess);
        Assert.Equal(outcome, result.Outcome);
        Assert.Equal(retryAt, result.RetryAt);
        Assert.Equal(status, result.HttpStatusCode);
    }

    private static async Task AssertCheckpointAsync(
        IContentRepository repository, DateTimeOffset retryAt, SyncRemoteFailureKind kind, int status)
    {
        var checkpoint = await repository.GetCheckpointAsync(OfflineFirstSynchronizer.BackoffCheckpointName);
        Assert.NotNull(checkpoint);
        using var json = JsonDocument.Parse(checkpoint.Value);
        Assert.Equal(retryAt, json.RootElement.GetProperty("RetryAt").GetDateTimeOffset());
        Assert.Equal((int)kind, json.RootElement.GetProperty("Kind").GetInt32());
        Assert.Equal(status, json.RootElement.GetProperty("HttpStatusCode").GetInt32());
    }

    private sealed class ControlledRemote : ISyncRemoteService
    {
        public ConcurrentQueue<string> Calls { get; } = new();
        public SyncRemoteException? Failure { get; init; }
        public int FeedCount { get; init; } = 1;
        public int StoriesPerFeed { get; init; } = 1;
        public IReadOnlyDictionary<string, double> SavedHashes { get; init; } =
            new Dictionary<string, double>();
        public Func<CancellationToken, Task>? BeforeCatalog { get; init; }
        public Func<IReadOnlyCollection<string>, bool, CancellationToken, Task<RemoteStoryPage>>? Bodies { get; init; }

        public Task UploadMutationAsync(PendingStoryMutation mutation, CancellationToken cancellationToken)
        {
            Record("upload");
            return Task.CompletedTask;
        }

        public async Task<RemoteFeedCatalog> GetFeedCatalogAsync(CancellationToken cancellationToken)
        {
            Record("catalog");
            if (BeforeCatalog is not null) await BeforeCatalog(cancellationToken);
            return new RemoteFeedCatalog(
                Enumerable.Range(7, FeedCount).Select(CreateFeed).ToArray(), [], []);
        }

        public Task<RemoteStoryInventory> GetStoryHashInventoryAsync(
            bool unreadOnly, IReadOnlyCollection<int> feedIds, CancellationToken cancellationToken)
        {
            Record(unreadOnly ? "inventory:unread" : "inventory:all");
            return Task.FromResult(new RemoteStoryInventory(feedIds.ToDictionary(
                feedId => feedId,
                feedId => (IReadOnlyList<RemoteStoryHash>)Enumerable.Range(0, StoriesPerFeed)
                    .Select(index => new RemoteStoryHash($"{feedId}:remote-{index}", feedId, StoryTimestamp))
                    .ToArray())));
        }

        public Task<IReadOnlyDictionary<string, double>> GetSavedStoryHashesAsync(
            CancellationToken cancellationToken)
        {
            Record("saved-hashes");
            return Task.FromResult(SavedHashes);
        }

        public Task<RemoteStoryPage> GetStoriesByHashesAsync(
            IReadOnlyCollection<string> hashes, bool saved, CancellationToken cancellationToken)
        {
            Record(saved ? "bodies:saved" : "bodies:general");
            return Bodies?.Invoke(hashes, saved, cancellationToken)
                ?? Task.FromResult(new RemoteStoryPage(hashes.Select(CreateStory).ToArray(), true));
        }

        public Task<IReadOnlySet<string>> GetUnreadStoryHashesAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Bulk sync must use the unread inventory.");

        public Task<RemoteStoryPage> GetFeedStoriesAsync(int feedId, int page, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Bulk sync must request bodies by hash.");

        public Task<RemoteStoryPage> GetStarredStoriesAsync(int page, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Bulk sync must use saved hashes and body batches.");

        private void Record(string call)
        {
            Calls.Enqueue(call);
            if (Failure is not null) throw Failure;
        }
    }

    private sealed class GatedPage
    {
        public TaskCompletionSource Canceled { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
        public TaskCompletionSource<RemoteStoryPage> Response { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private sealed class RecordingDelay : ISyncDelay
    {
        public ConcurrentQueue<TimeSpan> Delays { get; } = new();

        public Task DelayAsync(TimeSpan delay, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Delays.Enqueue(delay);
            return Task.CompletedTask;
        }
    }

    private sealed class TestTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan duration) => now += duration;
    }

    private sealed class FixedRandom(double value) : ISyncRandom
    {
        public double NextDouble() => value;
    }

    private sealed class StatusHandler(HttpStatusCode status, string? retryAfter) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = new HttpResponseMessage(status) { Content = new StringContent("not-json") };
            if (retryAfter is not null) response.Headers.TryAddWithoutValidation("Retry-After", retryAfter);
            return Task.FromResult(response);
        }
    }
}
