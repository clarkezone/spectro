using Microsoft.Data.Sqlite;
using Spectro.Domain;

namespace Spectro.Infrastructure.Tests;

public sealed class BulkHashSyncRepositoryTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(), "Spectro.Tests", Guid.NewGuid().ToString("N"));
    private readonly TestTimeProvider _time = new();

    private string DatabasePath => Path.Combine(_directory, "spectro.db");

    [Fact]
    public async Task CachedIndexReturnsEveryStoryAcrossInactiveFeedsWithoutPaging()
    {
        var repository = CreateRepository();
        await repository.InitializeAsync();
        Assert.Empty(await repository.GetCachedStoryIndexAsync());
        var stories = Enumerable.Range(0, 601)
            .Select(index => CreateStory($"{index % 3}:{index:D4}", index % 2 == 0) with
            {
                FeedId = index % 3,
                PublishedAt = _time.GetUtcNow().AddSeconds(index),
                IsSaved = index % 4 == 0,
                Content = "<p>" + new string('b', 9000) + "</p>",
                Summary = new string('s', 6000)
            }).ToArray();
        await repository.CacheRemoteStoriesAsync(stories);
        await repository.SetStoryReadAsync(stories[0].Hash, false);
        await repository.SetStorySavedAsync(stories[1].Hash, true);

        IContentRepository reopened = CreateRepository();
        var index = await reopened.GetCachedStoryIndexAsync();

        Assert.Equal(601, index.Count);
        Assert.Equal(
            stories.Select(story => new CachedStory(
                story.Hash, story.FeedId, story.PublishedAt,
                story.Hash == stories[0].Hash ? false : story.IsRead,
                story.Hash == stories[1].Hash || story.IsSaved)).OrderBy(story => story.Hash),
            index.OrderBy(story => story.Hash));
        Assert.Equal(
            ["FeedId", "Hash", "IsRead", "IsSaved", "PublishedAt"],
            typeof(CachedStory).GetProperties().Select(property => property.Name).Order());
    }

    [Fact]
    public async Task CachedIndexQueryDoesNotReferenceBodySummaryOrOtherStoryColumns()
    {
        var repository = CreateRepository();
        await using (var connection = new SqliteConnection($"Data Source={DatabasePath}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                CREATE TABLE story (
                    story_hash TEXT PRIMARY KEY, feed_id INTEGER, published_utc INTEGER,
                    is_read INTEGER, is_saved INTEGER);
                INSERT INTO story VALUES ('1:metadata-only', 1, 1767225600, 1, 0);
                """;
            await command.ExecuteNonQueryAsync();
        }

        Assert.Equal(
            new CachedStory("1:metadata-only", 1,
                DateTimeOffset.FromUnixTimeSeconds(1767225600), true, false),
            Assert.Single(await repository.GetCachedStoryIndexAsync()));
    }

    [Fact]
    public async Task CachedIndexHonorsCancellation()
    {
        var repository = CreateRepository();
        await repository.InitializeAsync();
        using var cancellation = new CancellationTokenSource();
        cancellation.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => repository.GetCachedStoryIndexAsync(cancellation.Token));
    }

    [Fact]
    public async Task IncompleteSetsApplyOnlyExplicitReadAndPositiveUnreadWithUnreadWinningOverlap()
    {
        var repository = CreateRepository();
        await repository.InitializeAsync();
        await repository.CacheRemoteStoriesAsync([
            CreateStory("1:read", false),
            CreateStory("1:unread", true),
            CreateStory("1:overlap", true),
            CreateStory("1:missing-read", true),
            CreateStory("1:missing-unread", false)
        ]);
        var batch = new RemoteContentBatch(
            [], new HashSet<string>(["1:unread", "1:overlap"]), false,
            new HashSet<string>(), false)
        {
            ReadStoryHashes = new HashSet<string>(["1:read", "1:overlap", "1:not-cached"])
        };

        await repository.ReconcileRemoteContentAsync(batch);
        await repository.ReconcileRemoteContentAsync(
            new RemoteContentBatch([], new HashSet<string>(), false, new HashSet<string>(), false));

        var stories = (await CreateRepository().GetCachedStoryIndexAsync())
            .ToDictionary(story => story.Hash);
        Assert.Equal(5, stories.Count);
        Assert.True(stories["1:read"].IsRead);
        Assert.False(stories["1:unread"].IsRead);
        Assert.False(stories["1:overlap"].IsRead);
        Assert.True(stories["1:missing-read"].IsRead);
        Assert.False(stories["1:missing-unread"].IsRead);
    }

    [Fact]
    public async Task CompleteUnreadSetStillInfersReadForAbsentHashes()
    {
        var repository = CreateRepository();
        await repository.InitializeAsync();
        await repository.CacheRemoteStoriesAsync([
            CreateStory("1:absent", false), CreateStory("1:unread", true)
        ]);

        await repository.ReconcileRemoteContentAsync(new RemoteContentBatch(
            [], new HashSet<string>(["1:unread"]), true, new HashSet<string>(), false)
        {
            ReadStoryHashes = new HashSet<string>(["1:unread"])
        });

        var stories = await CreateRepository().GetCachedStoryIndexAsync();
        Assert.True(stories.Single(story => story.Hash == "1:absent").IsRead);
        Assert.False(stories.Single(story => story.Hash == "1:unread").IsRead);
    }

    public static TheoryData<string, bool, bool?> MutationCases
    {
        get
        {
            var cases = new TheoryData<string, bool, bool?>();
            foreach (var boundary in new[] { "pending", "uploaded", "acknowledged", "newer-opposite", "newer-same" })
            foreach (var uploadedValue in new[] { false, true })
            foreach (var observation in new bool?[] { null, false, true })
                cases.Add(boundary, uploadedValue, observation);
            return cases;
        }
    }

    [Theory]
    [MemberData(nameof(MutationCases))]
    public async Task IncompleteObservationsPreserveMutationSnapshotsUntilExactUploadedValueIsObserved(
        string boundary, bool uploadedValue, bool? observation)
    {
        const string hash = "1:mutation";
        var repository = CreateRepository();
        await repository.InitializeAsync();
        await repository.CacheRemoteStoriesAsync([CreateStory(hash, !uploadedValue)]);
        await repository.SetStoryReadAsync(hash, uploadedValue);
        var original = Assert.Single(await repository.GetPendingMutationsAsync(10));
        if (boundary != "pending")
            await repository.RecordUploadedMutationAsync(original);
        if (boundary == "acknowledged")
            Assert.Equal(1, await repository.AcknowledgePendingMutationsAsync([original]));
        var hasNewerMutation = boundary.StartsWith("newer-", StringComparison.Ordinal);
        var localValue = boundary == "newer-opposite" ? !uploadedValue : uploadedValue;
        if (hasNewerMutation)
        {
            _time.Advance();
            await repository.SetStoryReadAsync(hash, localValue);
        }
        var pendingBefore = await repository.GetPendingMutationsAsync(10);
        var uploadedBefore = await repository.GetUploadedMutationsAsync();

        await repository.ReconcileRemoteContentAsync(Observe(hash, observation) with
        {
            Stories = [CreateStory(hash, !localValue) with { Content = "<p>Fresh remote body</p>" }]
        });

        var reopened = CreateRepository();
        var story = Assert.Single(await reopened.GetStoriesAsync(1));
        Assert.Equal(localValue, story.IsRead);
        Assert.Equal("<p>Fresh remote body</p>", story.Content);
        var observedUpload = boundary != "pending" && observation == uploadedValue;
        Assert.Equal(
            observedUpload && !hasNewerMutation ? [] : pendingBefore,
            await reopened.GetPendingMutationsAsync(10));
        Assert.Equal(
            observedUpload ? [] : uploadedBefore,
            await reopened.GetUploadedMutationsAsync());
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task OverlappingObservationsOnlyAcknowledgeUploadedUnread(bool uploadedValue)
    {
        const string hash = "1:overlap";
        var repository = CreateRepository();
        await repository.InitializeAsync();
        await repository.CacheRemoteStoriesAsync([CreateStory(hash, !uploadedValue)]);
        await repository.SetStoryReadAsync(hash, uploadedValue);
        var mutation = Assert.Single(await repository.GetPendingMutationsAsync(10));
        await repository.RecordUploadedMutationAsync(mutation);

        await repository.ReconcileRemoteContentAsync(Observe(hash, false) with
        {
            ReadStoryHashes = new HashSet<string>([hash])
        });

        Assert.Equal(uploadedValue, Assert.Single(await repository.GetCachedStoryIndexAsync()).IsRead);
        Assert.Equal(uploadedValue ? 1 : 0, (await repository.GetPendingMutationsAsync(10)).Count);
        Assert.Equal(uploadedValue ? 1 : 0, (await repository.GetUploadedMutationsAsync()).Count);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task ConfirmedUploadAllowsSubsequentExplicitRemoteStateChange(bool uploadedValue)
    {
        const string hash = "1:confirmed";
        var repository = CreateRepository();
        await repository.InitializeAsync();
        await repository.CacheRemoteStoriesAsync([CreateStory(hash, !uploadedValue)]);
        await repository.SetStoryReadAsync(hash, uploadedValue);
        await repository.RecordUploadedMutationAsync(
            Assert.Single(await repository.GetPendingMutationsAsync(10)));

        await repository.ReconcileRemoteContentAsync(Observe(hash, uploadedValue));
        Assert.Empty(await repository.GetPendingMutationsAsync(10));
        Assert.Empty(await repository.GetUploadedMutationsAsync());
        await repository.ReconcileRemoteContentAsync(Observe(hash, !uploadedValue));

        Assert.Equal(!uploadedValue, Assert.Single(await CreateRepository().GetCachedStoryIndexAsync()).IsRead);
    }

    private static RemoteContentBatch Observe(string hash, bool? isRead) =>
        new([], isRead == false ? new HashSet<string>([hash]) : new HashSet<string>(),
            false, new HashSet<string>(), false)
        {
            ReadStoryHashes = isRead == true ? new HashSet<string>([hash]) : new HashSet<string>()
        };

    private SqliteContentRepository CreateRepository() =>
        new(new SqliteConnectionFactory(DatabasePath), _time);

    private static Story CreateStory(string hash, bool isRead) =>
        new(hash, 1, null, null, "Story", null, null, "<p>Body</p>", "Summary", null,
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero), isRead, false);

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory))
            Directory.Delete(_directory, recursive: true);
    }

    private sealed class TestTimeProvider : TimeProvider
    {
        private DateTimeOffset _now = new(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => _now;

        public void Advance() => _now = _now.AddSeconds(1);
    }
}
