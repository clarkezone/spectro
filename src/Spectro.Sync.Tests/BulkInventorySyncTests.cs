using System.Collections.Concurrent;
using Spectro.Domain;
using Spectro.Infrastructure;

namespace Spectro.Sync.Tests;

public sealed class BulkInventorySyncTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "Spectro.Sync.Tests", Guid.NewGuid().ToString("N"));
    private static readonly DateTimeOffset Now = new(2026, 9, 15, 12, 0, 0, TimeSpan.Zero);
    private readonly FixedTime _time = new();
    private SqliteContentRepository Repository() =>
        new(new SqliteConnectionFactory(Path.Combine(_directory, "bulk.db")), _time);

    private OfflineFirstSynchronizer Synchronizer(BulkRemote remote, SyncOptions? options = null) =>
        new(Repository(), remote, (options ?? new SyncOptions()) with
        {
            MinimumRequestInterval = TimeSpan.Zero,
            MinimumSavedRequestInterval = TimeSpan.Zero
        }, _time);

    [Theory]
    [InlineData(25, 19)]
    [InlineData(100, 64)]
    public async Task InitialSyncUsesSixtyRecentAllStoriesPerFeedWithinRequestBudget(
        int feedCount, int requestBudget)
    {
        var remote = new BulkRemote(feedCount, 80);

        var result = await Synchronizer(remote).SynchronizeAsync(new SyncRequest(_directory));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(requestBudget, result.State.NetworkAttemptCount);
        Assert.Equal(4, remote.MetadataRequests);
        Assert.Equal(feedCount * 60 / 100, remote.BodyRequests.Count);
        Assert.All(remote.BodyRequests, request =>
        {
            Assert.False(request.Saved);
            Assert.InRange(request.Hashes.Length, 1, 100);
        });
        Assert.Empty(remote.FallbackRequests);
        Assert.Equal(new[] { false, true }, remote.InventoryRequests.Select(request => request.UnreadOnly));
        Assert.All(remote.InventoryRequests, request => Assert.Equal(feedCount, request.FeedIds.Length));
        for (var feedId = 1; feedId <= feedCount; feedId++)
        {
            var stories = await Repository().GetStoriesAsync(feedId);
            Assert.Equal(60, stories.Count);
            Assert.Equal(30, stories.Count(story => story.IsRead));
            Assert.Equal(30, stories.Count(story => !story.IsRead));
            Assert.Equal(Enumerable.Range(0, 60).Select(index => $"{feedId}:{index}").Order(),
                stories.Select(story => story.Hash).Order());
            Assert.All(stories, story => Assert.Equal(feedId, story.FeedId));
        }
    }

    [Fact]
    public async Task RestartedUnchangedSyncUsesExactlyFourMetadataRequestsAndNoBodyRequests()
    {
        var remote = new BulkRemote(25, 65);
        Assert.True((await Synchronizer(remote).SynchronizeAsync(new SyncRequest(_directory))).IsSuccess);
        remote.ClearRequests();

        var result = await Synchronizer(remote).SynchronizeAsync(new SyncRequest(_directory));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(4, result.State.NetworkAttemptCount);
        Assert.Equal(4, remote.MetadataRequests);
        Assert.Empty(remote.BodyRequests);
        Assert.Empty(remote.FallbackRequests);
        Assert.Equal(0, result.State.StoryCount);
        Assert.Equal(1500, (await Repository().GetCachedStoryIndexAsync()).Count);
    }

    [Fact]
    public async Task LaterInventoryDownloadsOnlyNewHashesAndKeepsCachedBodies()
    {
        var remote = new BulkRemote(3, 60);
        Assert.True((await Synchronizer(remote).SynchronizeAsync(new SyncRequest(_directory))).IsSuccess);
        var original = (await Repository().GetStoriesAsync(1)).Single(story => story.Hash == "1:0");
        remote.Stories["1:0"] = original with { Content = "must not replace an already cached body" };
        remote.Stories.Add("1:new", MakeStory(1, 0) with { Hash = "1:new", PublishedAt = Now.AddSeconds(1) });
        remote.Stories.Add("3:new", MakeStory(3, 0) with { Hash = "3:new", PublishedAt = Now.AddSeconds(1) });
        remote.ClearRequests();

        var result = await Synchronizer(remote).SynchronizeAsync(new SyncRequest(_directory));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(5, result.State.NetworkAttemptCount);
        Assert.Equal(new[] { "1:new", "3:new" }, Assert.Single(remote.BodyRequests).Hashes.Order());
        Assert.Equal(original.Content,
            (await Repository().GetStoriesAsync(1)).Single(story => story.Hash == "1:0").Content);
        Assert.Equal(182, (await Repository().GetCachedStoryIndexAsync()).Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OmittedUnreadFeedsAreQueriedAsAGroupThenIndividuallyBeforeReadReconciliation(
        bool omitAgain)
    {
        var remote = new BulkRemote(3, 1);
        await SeedAsync(remote, remote.Stories.Values.Select(story => story with { IsRead = false }));
        var unreadCall = 0;
        remote.InventoryResponse = (unread, ids) =>
        {
            if (!unread) return remote.Inventory(false, ids);
            unreadCall++;
            if (unreadCall == 1) return new(new Dictionary<int, IReadOnlyList<RemoteStoryHash>> { [1] = [] });
            if (unreadCall == 2)
            {
                Assert.Equal(new[] { 2, 3 }, ids);
                return new(new Dictionary<int, IReadOnlyList<RemoteStoryHash>> { [2] = [] });
            }
            Assert.Equal(new[] { 3 }, ids);
            return new(omitAgain
                ? new Dictionary<int, IReadOnlyList<RemoteStoryHash>>()
                : new Dictionary<int, IReadOnlyList<RemoteStoryHash>> { [3] = [] });
        };

        var result = await Synchronizer(remote).SynchronizeAsync(new SyncRequest(_directory));

        Assert.Equal(omitAgain ? SyncOutcome.MalformedRemoteData : SyncOutcome.Succeeded, result.Outcome);
        var requests = remote.InventoryRequests.Where(request => request.UnreadOnly).ToArray();
        Assert.Equal(3, requests.Length);
        Assert.Equal(new[] { 1, 2, 3 }, requests[0].FeedIds);
        Assert.Equal(new[] { 2, 3 }, requests[1].FeedIds);
        Assert.Equal(new[] { 3 }, requests[2].FeedIds);
        Assert.Equal(!omitAgain, Assert.Single(await Repository().GetStoriesAsync(3)).IsRead);
        Assert.Empty(remote.BodyRequests);
        if (omitAgain)
            Assert.Null(await Repository().GetCheckpointAsync(OfflineFirstSynchronizer.SuccessfulSyncCheckpointName));
    }

    [Fact]
    public async Task OmittedAllFeedFailsExplicitlyWithoutErasingCacheOrInferringReadState()
    {
        var remote = new BulkRemote(2, 1);
        await SeedAsync(remote, remote.Stories.Values.Select(story => story with { IsRead = false }));
        remote.InventoryResponse = (unread, ids) => unread
            ? remote.Inventory(true, ids)
            : remote.Inventory(false, [1]);

        var result = await Synchronizer(remote).SynchronizeAsync(new SyncRequest(_directory));

        Assert.Equal(SyncOutcome.MalformedRemoteData, result.Outcome);
        Assert.Contains("inventory", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
        Assert.False(Assert.Single(await Repository().GetStoriesAsync(2)).IsRead);
        Assert.Equal(2, (await Repository().GetCachedStoryIndexAsync()).Count);
        Assert.Empty(remote.BodyRequests);
        Assert.Null(await Repository().GetCheckpointAsync(OfflineFirstSynchronizer.SuccessfulSyncCheckpointName));
    }

    [Theory]
    [InlineData(1, false, true)]
    [InlineData(0, false, false)]
    [InlineData(0, true, true)]
    [InlineData(-1, false, false)]
    public async Task CappedUnreadWindowRequiresStrictlyNewerTimestampOrAuthoritativeFallback(
        int secondsFromBoundary, bool fallbackRead, bool expectedRead)
    {
        var remote = new BulkRemote(1, 0);
        var target = MakeStory(1, 0) with { PublishedAt = Now.AddSeconds(secondsFromBoundary), IsRead = false };
        remote.Stories.Add(target.Hash, target);
        remote.InventoryResponse = (unread, ids) => unread
            ? new(new Dictionary<int, IReadOnlyList<RemoteStoryHash>>
            {
                [1] = Enumerable.Range(0, 500)
                    .Select(index => new RemoteStoryHash($"1:unread-{index}", 1, Now.ToUnixTimeSeconds())).ToArray()
            })
            : remote.Inventory(false, ids);
        remote.FallbackResponse = (_, _) => new([target with { IsRead = fallbackRead }], true);

        var result = await Synchronizer(remote).SynchronizeAsync(new SyncRequest(_directory));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.Equal(expectedRead, Assert.Single(await Repository().GetStoriesAsync(1)).IsRead);
        Assert.Equal(secondsFromBoundary > 0 ? 0 : 1, remote.FallbackRequests.Count);
        Assert.Single(remote.BodyRequests);
    }

    [Fact]
    public async Task CachedUnreadOutsideKnownAllWindowIsNeverImplicitlyMarkedRead()
    {
        var remote = new BulkRemote(1, 1);
        var outside = MakeStory(1, 1000) with { IsRead = false };
        await SeedAsync(remote, [outside]);

        var result = await Synchronizer(remote).SynchronizeAsync(new SyncRequest(_directory));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.False((await Repository().GetStoriesAsync(1)).Single(story => story.Hash == outside.Hash).IsRead);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ClusterHiddenHashesAreRetriedAloneAndPermanentlyGoneHashesAdvanceTheWindow(
        bool permanentlyGone)
    {
        var remote = new BulkRemote(1, 65);
        remote.BodyResponse = (hashes, saved, _) => Task.FromResult(new RemoteStoryPage(
            hashes.Where(hash => hash != "1:0" || (!permanentlyGone && hashes.Length == 1))
                .Select(hash => remote.Stories[hash]).ToArray(), true));

        var result = await Synchronizer(remote).SynchronizeAsync(new SyncRequest(_directory));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        var requests = remote.BodyRequests.ToArray();
        Assert.Equal(permanentlyGone ? 3 : 2, requests.Length);
        Assert.Equal(60, requests[0].Hashes.Length);
        Assert.Equal(new[] { "1:0" }, requests[1].Hashes);
        if (permanentlyGone) Assert.Equal(new[] { "1:60" }, requests[2].Hashes);
        var stories = await Repository().GetStoriesAsync(1);
        Assert.Equal(60, stories.Count);
        Assert.Equal(!permanentlyGone, stories.Any(story => story.Hash == "1:0"));
        Assert.Equal(permanentlyGone, stories.Any(story => story.Hash == "1:60"));
    }

    [Fact]
    public async Task SavedRetainedCopyIsFetchedOnceColdAndFractionalVersionChangesRefreshOnlyThatBody()
    {
        var remote = new BulkRemote(1, 0);
        var saved = MakeStory(1, 1) with
        {
            Hash = "1:saved",
            PublishedAt = Now.AddYears(-2),
            IsSaved = true,
            Content = "retained saved copy"
        };
        remote.SavedBodies.Add(saved.Hash, saved);
        remote.SavedVersions.Add(saved.Hash, 1234.125);
        await SeedAsync(remote, [saved with { Content = "cold cached copy" }]);

        Assert.True((await Synchronizer(remote).SynchronizeAsync(new SyncRequest(_directory))).IsSuccess);
        Assert.True(Assert.Single(remote.BodyRequests).Saved);
        Assert.Equal(saved.Content, Assert.Single(await Repository().GetStoriesAsync(1)).Content);
        remote.ClearRequests();

        var unchanged = await Synchronizer(remote).SynchronizeAsync(new SyncRequest(_directory));
        Assert.True(unchanged.IsSuccess, unchanged.ErrorMessage);
        Assert.Equal(4, unchanged.State.NetworkAttemptCount);
        Assert.Empty(remote.BodyRequests);

        remote.SavedVersions[saved.Hash] = 1234.25;
        remote.SavedBodies[saved.Hash] = saved with { Content = "updated retained copy" };
        remote.ClearRequests();
        var updated = await Synchronizer(remote).SynchronizeAsync(new SyncRequest(_directory));
        Assert.True(updated.IsSuccess, updated.ErrorMessage);
        var request = Assert.Single(remote.BodyRequests);
        Assert.True(request.Saved);
        Assert.Equal(new[] { saved.Hash }, request.Hashes);
        var final = Assert.Single(await Repository().GetStoriesAsync(1));
        Assert.True(final.IsSaved);
        Assert.Equal("updated retained copy", final.Content);
    }

    [Fact]
    public async Task ReadSavedBodyOutsideRecentInventoryStaysReadAcrossNoOpRestart()
    {
        var remote = new BulkRemote(1, 0);
        var saved = MakeStory(1, 0) with { IsRead = true, IsSaved = true, PublishedAt = Now.AddYears(-2) };
        remote.SavedBodies.Add(saved.Hash, saved);
        remote.SavedVersions.Add(saved.Hash, 1.125);

        var first = await Synchronizer(remote).SynchronizeAsync(new SyncRequest(_directory));

        Assert.True(first.IsSuccess, first.ErrorMessage);
        Assert.True(Assert.Single(remote.BodyRequests).Saved);
        var cached = Assert.Single(await Repository().GetStoriesAsync(1));
        Assert.True(cached.IsRead);
        Assert.True(cached.IsSaved);
        remote.ClearRequests();

        var second = await Synchronizer(remote).SynchronizeAsync(new SyncRequest(_directory));

        Assert.True(second.IsSuccess, second.ErrorMessage);
        Assert.Equal(4, second.State.NetworkAttemptCount);
        Assert.Empty(remote.BodyRequests);
        Assert.True(Assert.Single(await Repository().GetStoriesAsync(1)).IsRead);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task UpdatedSavedVersionUsesAuthoritativeReadStateUnlessNewPendingActionProtectsIt(
        bool createPendingUnread)
    {
        var remote = new BulkRemote(1, 0);
        var saved = MakeStory(1, 0) with { IsRead = false, IsSaved = true };
        remote.SavedBodies.Add(saved.Hash, saved);
        remote.SavedVersions.Add(saved.Hash, 1.125);
        await SeedAsync(remote, [saved]);
        Assert.True((await Synchronizer(remote).SynchronizeAsync(new SyncRequest(_directory))).IsSuccess);
        Assert.False(Assert.Single(await Repository().GetStoriesAsync(1)).IsRead);

        remote.SavedVersions[saved.Hash] = 1.25;
        remote.BodyResponse = async (hashes, isSaved, _) =>
        {
            Assert.True(isSaved);
            Assert.Equal(new[] { saved.Hash }, hashes);
            if (createPendingUnread)
            {
                await Repository().SetStoryReadAsync(saved.Hash, true);
                await Repository().SetStoryReadAsync(saved.Hash, false);
            }
            return new([saved with { IsRead = true, Content = "updated saved body" }], true);
        };
        remote.ClearRequests();

        var result = await Synchronizer(remote).SynchronizeAsync(new SyncRequest(_directory));

        Assert.True(result.IsSuccess, result.ErrorMessage);
        Assert.True(Assert.Single(remote.BodyRequests).Saved);
        var updated = Assert.Single(await Repository().GetStoriesAsync(1));
        Assert.Equal(!createPendingUnread, updated.IsRead);
        Assert.True(updated.IsSaved);
        Assert.Equal("updated saved body", updated.Content);
        Assert.Equal(1.25, (await SavedVersionCheckpointAsync())[saved.Hash]);
        var pending = await Repository().GetPendingMutationsAsync(10);
        if (createPendingUnread)
        {
            var mutation = Assert.Single(pending);
            Assert.Equal(StoryMutationKind.Read, mutation.Kind);
            Assert.False(mutation.Value);
            Assert.Empty(await Repository().GetUploadedMutationsAsync());
        }
        else
        {
            Assert.Empty(pending);
        }
    }

    [Fact]
    public async Task SavedBatchVersionsSurviveRateLimitAndRestartDownloadsOnlyRemainingHashes()
    {
        var remote = new BulkRemote(1, 0);
        foreach (var index in Enumerable.Range(0, 201))
        {
            var story = MakeStory(1, index) with { IsSaved = true };
            remote.SavedBodies.Add(story.Hash, story);
            remote.SavedVersions.Add(story.Hash, index + 0.125);
        }
        var options = new SyncOptions { MaximumConcurrentRequests = 1 };
        var retryAt = Now.AddMinutes(6);
        var bodyCount = 0;
        remote.BodyResponse = async (hashes, isSaved, _) =>
        {
            Assert.True(isSaved);
            if (++bodyCount == 2)
            {
                Assert.Equal(100, (await Repository().GetCachedStoryIndexAsync()).Count);
                Assert.Equal(100, (await SavedVersionCheckpointAsync()).Count);
                throw new SyncRemoteException("saved endpoint throttled", SyncRemoteFailureKind.RateLimited)
                {
                    HttpStatusCode = 429,
                    RetryAt = retryAt
                };
            }
            return new(hashes.Select(hash => remote.SavedBodies[hash]).ToArray(), true);
        };

        var interrupted = await Synchronizer(remote, options).SynchronizeAsync(new SyncRequest(_directory));

        Assert.Equal(SyncOutcome.RateLimited, interrupted.Outcome);
        Assert.Equal(retryAt, interrupted.RetryAt);
        Assert.Equal(429, interrupted.HttpStatusCode);
        Assert.Equal(100, interrupted.State.StoryCount);
        Assert.Equal(1, interrupted.State.DownloadedPageCount);
        var attempted = remote.BodyRequests.ToArray();
        Assert.Equal(2, attempted.Length);
        Assert.All(attempted, request => Assert.Equal(100, request.Hashes.Length));
        var committed = attempted[0].Hashes.ToHashSet(StringComparer.Ordinal);
        var versions = await SavedVersionCheckpointAsync();
        Assert.Equal(committed.Order(), versions.Keys.Order());
        Assert.All(versions, pair => Assert.Equal(remote.SavedVersions[pair.Key], pair.Value));
        Assert.Null(await Repository().GetCheckpointAsync(OfflineFirstSynchronizer.SuccessfulSyncCheckpointName));
        remote.ClearRequests();

        var paused = await Synchronizer(remote, options).SynchronizeAsync(new SyncRequest(_directory));

        Assert.Equal(SyncOutcome.RateLimited, paused.Outcome);
        Assert.Equal(0, paused.State.NetworkAttemptCount);
        Assert.Equal(0, remote.MetadataRequests);
        Assert.Empty(remote.BodyRequests);
        _time.Advance(TimeSpan.FromMinutes(7));
        remote.BodyResponse = null;

        var resumed = await Synchronizer(remote, options).SynchronizeAsync(new SyncRequest(_directory));

        Assert.True(resumed.IsSuccess, resumed.ErrorMessage);
        Assert.Equal(6, resumed.State.NetworkAttemptCount);
        Assert.Equal(new[] { 100, 1 }, remote.BodyRequests.Select(request => request.Hashes.Length));
        var retried = remote.BodyRequests.SelectMany(request => request.Hashes).ToArray();
        Assert.Equal(101, retried.Length);
        Assert.DoesNotContain(retried, committed.Contains);
        Assert.Equal(remote.SavedVersions.Keys.Except(committed).Order(), retried.Order());
        Assert.Equal(201, (await Repository().GetCachedStoryIndexAsync()).Count);
        Assert.Equal(201, (await SavedVersionCheckpointAsync()).Count);
        Assert.NotNull(await Repository().GetCheckpointAsync(OfflineFirstSynchronizer.SuccessfulSyncCheckpointName));
    }

    [Fact]
    public async Task MissingSavedResponsePreservesCompletedVersionsAndNextSyncRequestsOnlyMissingBody()
    {
        var remote = new BulkRemote(1, 0);
        foreach (var index in Enumerable.Range(0, 3))
        {
            var saved = MakeStory(1, index) with { IsSaved = true };
            remote.SavedBodies.Add(saved.Hash, saved);
            remote.SavedVersions.Add(saved.Hash, index + 0.25);
        }
        remote.BodyResponse = (hashes, _, _) => Task.FromResult(new RemoteStoryPage(
            hashes.Where(hash => hash != "1:1").Select(hash => remote.SavedBodies[hash]).ToArray(), true));

        var interrupted = await Synchronizer(remote).SynchronizeAsync(new SyncRequest(_directory));

        Assert.Equal(SyncOutcome.MalformedRemoteData, interrupted.Outcome);
        Assert.Equal(new[] { "1:0", "1:2" }, (await SavedVersionCheckpointAsync()).Keys.Order());
        Assert.Equal(2, (await Repository().GetCachedStoryIndexAsync()).Count);
        Assert.Equal(new[] { "1:1" }, remote.BodyRequests.Last().Hashes);
        Assert.Null(await Repository().GetCheckpointAsync(OfflineFirstSynchronizer.SuccessfulSyncCheckpointName));
        remote.ClearRequests();
        remote.BodyResponse = null;

        var resumed = await Synchronizer(remote).SynchronizeAsync(new SyncRequest(_directory));

        Assert.True(resumed.IsSuccess, resumed.ErrorMessage);
        var request = Assert.Single(remote.BodyRequests);
        Assert.True(request.Saved);
        Assert.Equal(new[] { "1:1" }, request.Hashes);
        Assert.Equal(3, (await Repository().GetCachedStoryIndexAsync()).Count);
        Assert.Equal(3, (await SavedVersionCheckpointAsync()).Count);
        remote.ClearRequests();

        var unchanged = await Synchronizer(remote).SynchronizeAsync(new SyncRequest(_directory));

        Assert.True(unchanged.IsSuccess, unchanged.ErrorMessage);
        Assert.Equal(4, unchanged.State.NetworkAttemptCount);
        Assert.Empty(remote.BodyRequests);
    }

    [Fact]
    public async Task MissingSavedBodyFailsAndPreservesPreviousBodyAndVersionCheckpoint()
    {
        var remote = new BulkRemote(1, 0);
        var saved = MakeStory(1, 0) with { IsSaved = true, Content = "known saved body" };
        remote.SavedVersions.Add(saved.Hash, 1);
        remote.SavedBodies.Add(saved.Hash, saved);
        Assert.True((await Synchronizer(remote).SynchronizeAsync(new SyncRequest(_directory))).IsSuccess);
        var checkpoint = await Repository().GetCheckpointAsync("saved-story-versions");
        var success = await Repository().GetCheckpointAsync(OfflineFirstSynchronizer.SuccessfulSyncCheckpointName);
        remote.SavedVersions[saved.Hash] = 2;
        remote.SavedBodies.Clear();
        remote.ClearRequests();

        var result = await Synchronizer(remote).SynchronizeAsync(new SyncRequest(_directory));

        Assert.Equal(SyncOutcome.MalformedRemoteData, result.Outcome);
        Assert.Contains("saved", result.ErrorMessage!, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(saved.Content, Assert.Single(await Repository().GetStoriesAsync(1)).Content);
        Assert.All(remote.BodyRequests, request => Assert.True(request.Saved));
        Assert.NotEmpty(remote.BodyRequests);
        Assert.Equal(checkpoint, await Repository().GetCheckpointAsync("saved-story-versions"));
        Assert.Equal(success, await Repository().GetCheckpointAsync(OfflineFirstSynchronizer.SuccessfulSyncCheckpointName));
    }

    [Fact]
    public async Task SavedBodyArrivingAfterNewLocalReadAndUnsaveCannotOverwritePendingIntent()
    {
        var remote = new BulkRemote(1, 0);
        var saved = MakeStory(1, 0) with { IsRead = false, IsSaved = true };
        remote.SavedVersions.Add(saved.Hash, 1);
        remote.SavedBodies.Add(saved.Hash, saved);
        await SeedAsync(remote, [saved]);
        var entered = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        remote.BodyResponse = async (_, isSaved, token) =>
        {
            Assert.True(isSaved);
            entered.TrySetResult();
            await release.Task.WaitAsync(token);
            return new([saved with { Content = "new server content" }], true);
        };
        using var cancellation = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        var sync = Synchronizer(remote).SynchronizeAsync(new SyncRequest(_directory), cancellation.Token);
        try
        {
            await entered.Task.WaitAsync(TimeSpan.FromSeconds(5));
            await Repository().SetStoryReadAsync(saved.Hash, true);
            await Repository().SetStorySavedAsync(saved.Hash, false);
            var pending = await Repository().GetPendingMutationsAsync(10);
            release.TrySetResult();

            var result = await sync.WaitAsync(TimeSpan.FromSeconds(5));

            Assert.True(result.IsSuccess, result.ErrorMessage);
            var story = Assert.Single(await Repository().GetStoriesAsync(1));
            Assert.True(story.IsRead);
            Assert.False(story.IsSaved);
            Assert.Equal("new server content", story.Content);
            Assert.Equal(pending, await Repository().GetPendingMutationsAsync(10));
            Assert.Empty(await Repository().GetUploadedMutationsAsync());
        }
        finally
        {
            await cancellation.CancelAsync();
            await sync.WaitAsync(TimeSpan.FromSeconds(5));
        }
    }

    private async Task SeedAsync(BulkRemote remote, IEnumerable<Story> stories)
    {
        var repository = Repository();
        await repository.InitializeAsync();
        foreach (var feed in remote.Feeds) await repository.UpsertFeedAsync(feed);
        foreach (var story in stories) await repository.UpsertStoryAsync(story);
    }

    private async Task<IReadOnlyDictionary<string, double>> SavedVersionCheckpointAsync()
    {
        var checkpoint = await Repository().GetCheckpointAsync("saved-story-versions");
        Assert.NotNull(checkpoint);
        using var document = System.Text.Json.JsonDocument.Parse(checkpoint.Value);
        return document.RootElement.EnumerateObject()
            .ToDictionary(property => property.Name, property => property.Value.GetDouble(), StringComparer.Ordinal);
    }

    private static Story MakeStory(int feedId, int index) => new(
        $"{feedId}:{index}", feedId, $"service-{feedId}-{index}", null, $"Story {index}", null,
        $"https://example.test/{feedId}/{index}", $"body {feedId}:{index}", $"summary {index}",
        null, Now.AddMinutes(-index), index % 2 == 0, false);

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private sealed class FixedTime : TimeProvider
    {
        private DateTimeOffset _utcNow = Now;
        public override DateTimeOffset GetUtcNow() => _utcNow;
        public void Advance(TimeSpan duration) => _utcNow += duration;
    }

    private sealed record InventoryRequest(bool UnreadOnly, int[] FeedIds);
    private sealed record BodyRequest(string[] Hashes, bool Saved);

    private sealed class BulkRemote : ISyncRemoteService
    {
        public BulkRemote(int feedCount, int storiesPerFeed)
        {
            Feeds = Enumerable.Range(1, feedCount)
                .Select(id => new Feed(id, $"Feed {id}", $"https://example.test/{id}", null, 0, null, true))
                .ToArray();
            Stories = Feeds.SelectMany(feed => Enumerable.Range(0, storiesPerFeed)
                    .Select(index => MakeStory(feed.Id, index)))
                .ToDictionary(story => story.Hash, StringComparer.Ordinal);
        }

        public Feed[] Feeds { get; }
        public Dictionary<string, Story> Stories { get; }
        public Dictionary<string, Story> SavedBodies { get; } = new(StringComparer.Ordinal);
        public Dictionary<string, double> SavedVersions { get; } = new(StringComparer.Ordinal);
        public ConcurrentQueue<InventoryRequest> InventoryRequests { get; } = new();
        public ConcurrentQueue<BodyRequest> BodyRequests { get; } = new();
        public ConcurrentQueue<(int FeedId, int Page)> FallbackRequests { get; } = new();
        public int MetadataRequests { get; private set; }
        public Func<bool, int[], RemoteStoryInventory>? InventoryResponse { get; set; }
        public Func<string[], bool, CancellationToken, Task<RemoteStoryPage>>? BodyResponse { get; set; }
        public Func<int, int, RemoteStoryPage>? FallbackResponse { get; set; }

        public void ClearRequests()
        {
            MetadataRequests = 0;
            InventoryRequests.Clear();
            BodyRequests.Clear();
            FallbackRequests.Clear();
        }

        public RemoteStoryInventory Inventory(bool unread, IReadOnlyCollection<int> ids) =>
            new(ids.ToDictionary(id => id, id => (IReadOnlyList<RemoteStoryHash>)Stories.Values
                .Where(story => story.FeedId == id && (!unread || !story.IsRead))
                .OrderBy(story => story.PublishedAt)
                .Select(story => new RemoteStoryHash(story.Hash, id,
                    (story.PublishedAt - DateTimeOffset.UnixEpoch).TotalSeconds)).ToArray()));

        public Task<RemoteStoryInventory> GetStoryHashInventoryAsync(
            bool unreadOnly, IReadOnlyCollection<int> feedIds, CancellationToken cancellationToken)
        {
            MetadataRequests++;
            var ids = feedIds.ToArray();
            InventoryRequests.Enqueue(new(unreadOnly, ids));
            return Task.FromResult(InventoryResponse?.Invoke(unreadOnly, ids) ?? Inventory(unreadOnly, ids));
        }

        public Task<IReadOnlyDictionary<string, double>> GetSavedStoryHashesAsync(CancellationToken cancellationToken)
        {
            MetadataRequests++;
            return Task.FromResult<IReadOnlyDictionary<string, double>>(new Dictionary<string, double>(SavedVersions));
        }

        public Task<RemoteStoryPage> GetStoriesByHashesAsync(
            IReadOnlyCollection<string> hashes, bool saved, CancellationToken cancellationToken)
        {
            var batch = hashes.ToArray();
            BodyRequests.Enqueue(new(batch, saved));
            if (BodyResponse is not null) return BodyResponse(batch, saved, cancellationToken);
            var bodies = saved ? SavedBodies : Stories;
            return Task.FromResult(new RemoteStoryPage(
                batch.Where(bodies.ContainsKey).Select(hash => bodies[hash]).ToArray(), true));
        }

        public Task<RemoteFeedCatalog> GetFeedCatalogAsync(CancellationToken cancellationToken)
        {
            MetadataRequests++;
            return Task.FromResult(new RemoteFeedCatalog(Feeds, [], []));
        }

        public Task<RemoteStoryPage> GetFeedStoriesAsync(int feedId, int page, CancellationToken cancellationToken)
        {
            FallbackRequests.Enqueue((feedId, page));
            return Task.FromResult(FallbackResponse?.Invoke(feedId, page)
                ?? throw new InvalidOperationException("Per-feed bodies are only allowed for capped unread ambiguity."));
        }

        public Task UploadMutationAsync(PendingStoryMutation mutation, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("No pending mutations should exist before this test's upload stage.");

        public Task<IReadOnlySet<string>> GetUnreadStoryHashesAsync(CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Legacy global unread sets are forbidden.");

        public Task<RemoteStoryPage> GetStarredStoriesAsync(int page, CancellationToken cancellationToken) =>
            throw new InvalidOperationException("Legacy saved pagination is forbidden.");
    }
}
