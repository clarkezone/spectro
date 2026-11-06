using Spectro.Domain;
using Spectro.Infrastructure;

namespace Spectro.Sync.Tests;

public sealed class UploadedReadConfirmationTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "Spectro.ReadConfirmation.Tests", Guid.NewGuid().ToString("N"));

    [Theory]
    [InlineData(true, true)]
    [InlineData(true, false)]
    [InlineData(false, true)]
    [InlineData(false, false)]
    public async Task OlderArticleOutsideInventoriesIsExplicitlyConfirmed(bool saved, bool read)
    {
        var (repository, remote, sync) = await PrepareAsync(saved, read);

        for (var run = 0; run < 2; run++)
            Assert.True((await sync.SynchronizeAsync(new SyncRequest(_directory))).IsSuccess);

        Assert.Equal(read, remote.OldStory.IsRead);
        Assert.Equal(1, remote.UploadCount);
        Assert.Equal(1, remote.ConfirmationRequests);
        Assert.Equal(saved, remote.LastConfirmationWasSaved);
        Assert.Empty(await repository.GetPendingMutationsAsync(100));
        Assert.Empty(await repository.GetUploadedMutationsAsync());
        Assert.Equal(read, (await repository.QueryStoriesAsync(new ContentQuery(StoryFilter.All, StoryHash: "1:old"))).Single().IsRead);
    }

    [Fact]
    public async Task MissingConfirmationRetainsIntentAndReceiptThenRecoversWithoutReuploading()
    {
        var (repository, remote, sync) = await PrepareAsync(saved: true, read: true);
        remote.OmitConfirmation = true;

        var first = await sync.SynchronizeAsync(new SyncRequest(_directory));

        Assert.Equal(SyncOutcome.MalformedRemoteData, first.Outcome);
        Assert.Equal(2, remote.ConfirmationRequests); // Batch followed by a singleton retry.
        Assert.Single(await repository.GetPendingMutationsAsync(100));
        Assert.Single(await repository.GetUploadedMutationsAsync());
        Assert.Null(await repository.GetCheckpointAsync(OfflineFirstSynchronizer.SuccessfulSyncCheckpointName));

        remote.OmitConfirmation = false;
        var retry = await sync.SynchronizeAsync(new SyncRequest(_directory));

        Assert.True(retry.IsSuccess, retry.ErrorMessage);
        Assert.Equal(1, remote.UploadCount);
        Assert.Empty(await repository.GetPendingMutationsAsync(100));
        Assert.Empty(await repository.GetUploadedMutationsAsync());
    }

    [Fact]
    public async Task NewerLocalReadIntentSurvivesConfirmationOfEarlierUpload()
    {
        var (repository, remote, sync) = await PrepareAsync(saved: true, read: true);
        remote.BeforeConfirmation = () => repository.SetStoryReadAsync("1:old", false);

        var first = await sync.SynchronizeAsync(new SyncRequest(_directory));

        Assert.True(first.IsSuccess, first.ErrorMessage);
        Assert.False(Assert.Single(await repository.GetPendingMutationsAsync(100)).Value);
        Assert.Empty(await repository.GetUploadedMutationsAsync());
        Assert.False((await repository.QueryStoriesAsync(new ContentQuery(StoryFilter.All, StoryHash: "1:old"))).Single().IsRead);

        remote.BeforeConfirmation = null;
        Assert.True((await sync.SynchronizeAsync(new SyncRequest(_directory))).IsSuccess);
        Assert.Equal(2, remote.UploadCount);
        Assert.Empty(await repository.GetPendingMutationsAsync(100));
    }

    private async Task<(SqliteContentRepository, Remote, OfflineFirstSynchronizer)> PrepareAsync(bool saved, bool read)
    {
        var repository = new SqliteContentRepository(new SqliteConnectionFactory(Path.Combine(_directory, "spectro.db")));
        await repository.InitializeAsync();
        await repository.UpsertFeedAsync(new Feed(1, "Fixture", "https://fixture.invalid/", null, 0, null, true));
        var remote = new Remote(saved);
        await repository.UpsertStoryAsync(remote.OldStory with { IsRead = !read });
        await repository.UpsertStoryAsync(remote.RecentStory);
        if (saved)
            await repository.SetCheckpointAsync(new SyncCheckpoint("saved-story-versions", "{\"1:old\":1}", DateTimeOffset.UtcNow));
        await repository.SetStoryReadAsync("1:old", read);
        var sync = new OfflineFirstSynchronizer(repository, remote,
            new SyncOptions { MinimumRequestInterval = TimeSpan.Zero, MinimumSavedRequestInterval = TimeSpan.Zero });
        return (repository, remote, sync);
    }

    private static Story Story(string hash, DateTimeOffset date, bool read, bool saved) =>
        new(hash, 1, null, null, "Fixture article", null, null, "Fixture body", "Fixture summary", null, date, read, saved);

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private sealed class Remote(bool saved) : ISyncRemoteService
    {
        public Story OldStory = Story("1:old", DateTimeOffset.UtcNow.AddDays(-60), false, saved);
        public Story RecentStory = Story("1:recent", DateTimeOffset.UtcNow, true, false);
        public int UploadCount;
        public int ConfirmationRequests;
        public bool LastConfirmationWasSaved;
        public bool OmitConfirmation;
        public Func<Task>? BeforeConfirmation;

        public Task UploadMutationAsync(PendingStoryMutation mutation, CancellationToken token)
        {
            Assert.Equal("1:old", mutation.StoryHash);
            Assert.Equal(StoryMutationKind.Read, mutation.Kind);
            OldStory = OldStory with { IsRead = mutation.Value };
            UploadCount++;
            return Task.CompletedTask;
        }

        public Task<RemoteFeedCatalog> GetFeedCatalogAsync(CancellationToken token) =>
            Task.FromResult(new RemoteFeedCatalog([new Feed(1, "Fixture", "https://fixture.invalid/", null, 0, null, true)], [], []));

        public Task<RemoteStoryInventory> GetStoryHashInventoryAsync(bool unreadOnly, IReadOnlyCollection<int> feeds, CancellationToken token) =>
            Task.FromResult(new RemoteStoryInventory(new Dictionary<int, IReadOnlyList<RemoteStoryHash>>
            {
                [1] = unreadOnly ? Enumerable.Range(0, 500).Select(index =>
                    new RemoteStoryHash($"1:unread-{index}", 1, RecentStory.PublishedAt.AddDays(-1).ToUnixTimeSeconds())).ToArray()
                    : [new RemoteStoryHash(RecentStory.Hash, 1, RecentStory.PublishedAt.ToUnixTimeSeconds())]
            }));

        public Task<IReadOnlyDictionary<string, double>> GetSavedStoryHashesAsync(CancellationToken token) =>
            Task.FromResult<IReadOnlyDictionary<string, double>>(saved
                ? new Dictionary<string, double> { ["1:old"] = 1 } : new Dictionary<string, double>());

        public async Task<RemoteStoryPage> GetStoriesByHashesAsync(IReadOnlyCollection<string> hashes, bool isSaved, CancellationToken token)
        {
            Assert.Contains("1:old", hashes);
            ConfirmationRequests++;
            LastConfirmationWasSaved = isSaved;
            if (BeforeConfirmation is not null) await BeforeConfirmation();
            return new RemoteStoryPage(OmitConfirmation ? [] : [OldStory], true);
        }

        public Task<RemoteStoryPage> GetFeedStoriesAsync(int feed, int page, CancellationToken token) =>
            throw new InvalidOperationException("Unexpected fallback");
        public Task<IReadOnlySet<string>> GetUnreadStoryHashesAsync(CancellationToken token) =>
            throw new InvalidOperationException("Unexpected legacy call");
        public Task<RemoteStoryPage> GetStarredStoriesAsync(int page, CancellationToken token) =>
            throw new InvalidOperationException("Unexpected legacy call");
    }
}
