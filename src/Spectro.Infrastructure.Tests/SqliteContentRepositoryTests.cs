using Microsoft.Data.Sqlite;
using Spectro.Domain;

namespace Spectro.Infrastructure.Tests;

public sealed class SqliteContentRepositoryTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        "Spectro.Tests",
        Guid.NewGuid().ToString("N"));
    private readonly string _databasePath;

    public SqliteContentRepositoryTests()
    {
        _databasePath = Path.Combine(_directory, "spectro.db");
    }

    [Fact]
    public async Task InitializeCreatesVersionedSchemaAndIsIdempotent()
    {
        var repository = CreateRepository();

        await repository.InitializeAsync();
        await repository.InitializeAsync();

        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";

        Assert.Equal(
            SqliteDatabase.CurrentSchemaVersion,
            Convert.ToInt32(await command.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task InitializeMigratesVersionOneDatabaseToUploadedSnapshotSchema()
    {
        var repository = CreateRepository();
        await repository.InitializeAsync();
        await using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText =
                """
                DROP TABLE uploaded_mutation;
                PRAGMA user_version = 1;
                """;
            await command.ExecuteNonQueryAsync();
        }

        await repository.InitializeAsync();

        await using var migrated = new SqliteConnection($"Data Source={_databasePath}");
        await migrated.OpenAsync();
        await using var schema = migrated.CreateCommand();
        schema.CommandText =
            """
            SELECT COUNT(*)
            FROM sqlite_master
            WHERE type = 'table' AND name = 'uploaded_mutation';
            """;
        Assert.Equal(1L, await schema.ExecuteScalarAsync());
    }

    [Fact]
    public async Task UpsertsFeedsAndReturnsOnlyActiveFeedsInTitleOrder()
    {
        var repository = CreateRepository();
        await repository.InitializeAsync();
        await repository.UpsertFeedAsync(CreateFeed(2, "Zulu", true));
        await repository.UpsertFeedAsync(CreateFeed(1, "alpha", true));
        await repository.UpsertFeedAsync(CreateFeed(3, "Hidden", false));

        var feeds = await repository.GetFeedsAsync();

        Assert.Collection(
            feeds,
            feed => Assert.Equal(1, feed.Id),
            feed => Assert.Equal(2, feed.Id));
    }

    [Fact]
    public async Task LocalReadIntentSurvivesRemoteStoryUpsert()
    {
        var repository = CreateRepository();
        await repository.InitializeAsync();
        await repository.UpsertFeedAsync(CreateFeed(1, "Feed", true));
        await repository.UpsertStoryAsync(CreateStory("1:abc", isRead: false));

        await repository.SetStoryReadAsync("1:abc", true);
        await repository.UpsertStoryAsync(CreateStory("1:abc", isRead: false));

        var story = Assert.Single(await repository.GetStoriesAsync(1));
        Assert.True(story.IsRead);

        await using var connection = new SqliteConnection($"Data Source={_databasePath}");
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT value
            FROM pending_mutation
            WHERE story_hash = '1:abc' AND kind = 'read';
            """;
        Assert.Equal(1L, await command.ExecuteScalarAsync());
    }

    [Fact]
    public async Task ReadAndSavedMutationsAreTypedOrderedAndDurableAcrossRepositoryInstances()
    {
        var timeProvider = new TestTimeProvider(
            new DateTimeOffset(2026, 2, 1, 1, 2, 3, TimeSpan.Zero));
        var repository = CreateRepository(timeProvider);
        await repository.InitializeAsync();
        await repository.UpsertFeedAsync(CreateFeed(1, "Feed", true));
        await repository.UpsertStoryAsync(CreateStory("1:abc"));

        await repository.SetStorySavedAsync("1:abc", true);
        timeProvider.Advance(TimeSpan.FromSeconds(1));
        await repository.SetStoryReadAsync("1:abc", true);

        var reopenedRepository = CreateRepository(timeProvider);
        var story = Assert.Single(await reopenedRepository.GetStoriesAsync(1));
        var mutations = await reopenedRepository.GetPendingMutationsAsync(10);

        Assert.True(story.IsRead);
        Assert.True(story.IsSaved);
        Assert.Collection(
            mutations,
            mutation =>
            {
                Assert.Equal("1:abc", mutation.StoryHash);
                Assert.Equal(StoryMutationKind.Saved, mutation.Kind);
                Assert.True(mutation.Value);
                Assert.Equal(
                    new DateTimeOffset(2026, 2, 1, 1, 2, 3, TimeSpan.Zero),
                    mutation.CreatedAt);
            },
            mutation =>
            {
                Assert.Equal("1:abc", mutation.StoryHash);
                Assert.Equal(StoryMutationKind.Read, mutation.Kind);
                Assert.True(mutation.Value);
                Assert.Equal(
                    new DateTimeOffset(2026, 2, 1, 1, 2, 4, TimeSpan.Zero),
                    mutation.CreatedAt);
            });
        Assert.Single(await reopenedRepository.GetPendingMutationsAsync(1));

        await reopenedRepository.UpsertStoryAsync(
            CreateStory("1:abc", isRead: false, isSaved: false));
        story = Assert.Single(await reopenedRepository.GetStoriesAsync(1));
        Assert.True(story.IsRead);
        Assert.True(story.IsSaved);
    }

    [Fact]
    public async Task AcknowledgementDeletesOnlyTheExactMutationSnapshot()
    {
        var timeProvider = new TestTimeProvider(
            new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));
        var repository = CreateRepository(timeProvider);
        await repository.InitializeAsync();
        await repository.UpsertFeedAsync(CreateFeed(1, "Feed", true));
        await repository.UpsertStoryAsync(CreateStory("1:abc"));

        await repository.SetStoryReadAsync("1:abc", true);
        var staleMutation = Assert.Single(await repository.GetPendingMutationsAsync(10));

        timeProvider.Advance(TimeSpan.FromSeconds(1));
        await repository.SetStoryReadAsync("1:abc", false);

        Assert.Equal(
            0,
            await repository.AcknowledgePendingMutationsAsync([staleMutation]));
        var currentMutation = Assert.Single(await repository.GetPendingMutationsAsync(10));
        Assert.Equal(staleMutation.Id, currentMutation.Id);
        Assert.False(currentMutation.Value);
        Assert.Equal(
            1,
            await repository.AcknowledgePendingMutationsAsync([currentMutation]));
        Assert.Empty(await repository.GetPendingMutationsAsync(10));
    }

    [Fact]
    public async Task MissingStoryStateChangesThrowWithoutCreatingMutations()
    {
        var repository = CreateRepository();
        await repository.InitializeAsync();

        var readException = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => repository.SetStoryReadAsync("missing", true));
        var savedException = await Assert.ThrowsAsync<KeyNotFoundException>(
            () => repository.SetStorySavedAsync("missing", true));

        Assert.Contains("missing", readException.Message, StringComparison.Ordinal);
        Assert.Contains("missing", savedException.Message, StringComparison.Ordinal);
        Assert.Empty(await repository.GetPendingMutationsAsync(10));
    }

    [Fact]
    public async Task RetentionDeletesOnlyOldReadUnsavedStoriesWithoutPendingMutations()
    {
        var repository = CreateRepository();
        await repository.InitializeAsync();
        await repository.UpsertFeedAsync(CreateFeed(1, "Feed", true));
        var old = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var recent = new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero);

        await repository.UpsertStoryAsync(
            CreateStory("1:deletable", isRead: true, publishedAt: old));
        await repository.UpsertStoryAsync(
            CreateStory("1:unread", isRead: false, publishedAt: old));
        await repository.UpsertStoryAsync(
            CreateStory("1:saved", isRead: true, isSaved: true, publishedAt: old));
        await repository.UpsertStoryAsync(
            CreateStory("1:pending", isRead: true, publishedAt: old));
        await repository.UpsertStoryAsync(
            CreateStory("1:recent", isRead: true, publishedAt: recent));
        await repository.SetStoryReadAsync("1:pending", true);

        var deleted = await repository.DeleteStoriesOlderThanAsync(
            new DateTimeOffset(2026, 1, 15, 0, 0, 0, TimeSpan.Zero));
        var remaining = await repository.GetStoriesAsync(1);

        Assert.Equal(1, deleted);
        Assert.Equal(
            ["1:recent", "1:pending", "1:saved", "1:unread"],
            remaining.Select(story => story.Hash));
    }

    [Fact]
    public async Task CorruptDatabaseIsPreservedUntilExplicitReset()
    {
        Directory.CreateDirectory(_directory);
        var corruptBytes = "not a sqlite database"u8.ToArray();
        await File.WriteAllBytesAsync(_databasePath, corruptBytes);
        var timeProvider = new TestTimeProvider(
            new DateTimeOffset(2026, 2, 3, 4, 5, 6, 789, TimeSpan.Zero));
        var repository = CreateRepository(timeProvider);

        var exception = await Assert.ThrowsAsync<SqliteDatabaseInitializationException>(
            () => repository.InitializeAsync());

        Assert.Equal(Path.GetFullPath(_databasePath), exception.DatabasePath);
        Assert.Equal(corruptBytes, await File.ReadAllBytesAsync(_databasePath));
        Assert.Empty(Directory.GetFiles(_directory, "*.failed-*"));

        var reset = await repository.ResetDatabaseAsync();

        var preservedPath = Assert.IsType<string>(reset.PreservedDatabasePath);
        Assert.Equal(
            $"{Path.GetFullPath(_databasePath)}.failed-20260203040506789",
            preservedPath);
        Assert.Equal(corruptBytes, await File.ReadAllBytesAsync(preservedPath));
        await repository.InitializeAsync();
        Assert.True(File.Exists(_databasePath));
    }

    [Fact]
    public async Task UnsupportedSchemaIsPreservedAndRequiresExplicitReset()
    {
        Directory.CreateDirectory(_directory);
        await using (var connection = new SqliteConnection($"Data Source={_databasePath}"))
        {
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "PRAGMA user_version = 99;";
            await command.ExecuteNonQueryAsync();
        }

        var repository = CreateRepository(new TestTimeProvider(
            new DateTimeOffset(2026, 3, 4, 5, 6, 7, TimeSpan.Zero)));

        var exception = await Assert.ThrowsAsync<SqliteDatabaseInitializationException>(
            () => repository.InitializeAsync());

        Assert.IsType<InvalidOperationException>(exception.InnerException);
        Assert.True(File.Exists(_databasePath));

        var reset = await repository.ResetDatabaseAsync();

        Assert.NotNull(reset.PreservedDatabasePath);
        Assert.True(File.Exists(reset.PreservedDatabasePath));
        await using var freshConnection = new SqliteConnection($"Data Source={_databasePath}");
        await freshConnection.OpenAsync();
        await using var versionCommand = freshConnection.CreateCommand();
        versionCommand.CommandText = "PRAGMA user_version;";
        Assert.Equal(
            SqliteDatabase.CurrentSchemaVersion,
            Convert.ToInt32(await versionCommand.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task StoriesAreOrderedNewestFirst()
    {
        var repository = CreateRepository();
        await repository.InitializeAsync();
        await repository.UpsertFeedAsync(CreateFeed(1, "Feed", true));
        await repository.UpsertStoryAsync(
            CreateStory("1:older", publishedAt: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        await repository.UpsertStoryAsync(
            CreateStory("1:newer", publishedAt: new DateTimeOffset(2026, 1, 2, 0, 0, 0, TimeSpan.Zero)));

        var stories = await repository.GetStoriesAsync(1);

        Assert.Equal(["1:newer", "1:older"], stories.Select(story => story.Hash));
    }

    [Fact]
    public async Task QueryStoriesSupportsTopLevelFiltersAndFolderScope()
    {
        var repository = CreateRepository();
        await repository.InitializeAsync();
        await repository.ReplaceFeedCatalogAsync(
            [CreateFeed(1, "One", true), CreateFeed(2, "Two", true)],
            [new Folder("tech", "Tech", 0)],
            [new FolderFeed("tech", 1, 0)]);
        await repository.UpsertStoryAsync(CreateStory("1:unread"));
        await repository.UpsertStoryAsync(CreateStory("1:saved", isRead: true, isSaved: true));
        await repository.UpsertStoryAsync(CreateStory("1:read", isRead: true));
        await repository.UpsertStoryAsync(CreateStory("2:other", feedId: 2));

        var folders = await repository.GetFeedFoldersAsync();
        var unread = await repository.QueryStoriesAsync(new ContentQuery(StoryFilter.Unread));
        var read = await repository.QueryStoriesAsync(new ContentQuery(StoryFilter.Read));
        var savedInFolder = await repository.QueryStoriesAsync(
            new ContentQuery(StoryFilter.Saved, FolderId: "tech"));

        Assert.Equal("tech", Assert.Single(folders).Folder.Id);
        Assert.Equal(1, Assert.Single(folders).Feeds[0].Id);
        Assert.Equal(["1:unread", "2:other"], unread.Select(story => story.Hash));
        Assert.Equal(["1:read", "1:saved"], read.Select(story => story.Hash));
        Assert.Equal("1:saved", Assert.Single(savedInFolder).Hash);
        Assert.Equal(1, (await repository.GetLocalUnreadCountsAsync())[1]);
        await repository.SetStoryReadAsync("1:unread", true);
        Assert.False((await repository.GetLocalUnreadCountsAsync()).ContainsKey(1));
    }

    [Fact]
    public async Task ClearAccountDataRemovesAllLocalUiData()
    {
        var repository = CreateRepository();
        await repository.InitializeAsync();
        await repository.UpsertFeedAsync(CreateFeed(1, "Feed", true));
        await repository.UpsertStoryAsync(CreateStory("1:story"));
        await repository.SetStorySavedAsync("1:story", true);

        await repository.ClearAccountDataAsync();

        Assert.Empty(await repository.GetFeedsAsync());
        Assert.Empty(await repository.QueryStoriesAsync(new ContentQuery(StoryFilter.All)));
        Assert.Empty(await repository.GetPendingMutationsAsync(10));
    }

    [Fact]
    public async Task FeedCatalogReplacementRollsBackAsOneTransaction()
    {
        var repository = CreateRepository();
        await repository.InitializeAsync();
        await repository.UpsertFeedAsync(CreateFeed(1, "Existing", true));

        await Assert.ThrowsAsync<SqliteException>(
            () => repository.ReplaceFeedCatalogAsync(
                [CreateFeed(2, "Replacement", true)],
                [new Folder("folder", "Folder", 0)],
                [new FolderFeed("missing-folder", 2, 0)]));

        var feed = Assert.Single(await repository.GetFeedsAsync());
        Assert.Equal(1, feed.Id);
        Assert.Equal("Existing", feed.Title);
    }

    [Fact]
    public async Task UploadedMutationProtectsRetentionUntilReconciliation()
    {
        var repository = CreateRepository();
        await repository.InitializeAsync();
        await repository.UpsertFeedAsync(CreateFeed(1, "Feed", true));
        await repository.UpsertStoryAsync(CreateStory(
            "1:abc",
            isRead: true,
            publishedAt: new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero)));
        await repository.SetStoryReadAsync("1:abc", true);
        var mutation = Assert.Single(await repository.GetPendingMutationsAsync(10));
        await repository.RecordUploadedMutationAsync(mutation);
        Assert.Equal(1, await repository.AcknowledgePendingMutationsAsync([mutation]));

        var deleted = await repository.DeleteStoriesOlderThanAsync(
            new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.Equal(0, deleted);
        Assert.Single(await repository.GetStoriesAsync(1));
        Assert.Single(await repository.GetUploadedMutationsAsync());
    }

    [Theory]
    [InlineData("summary")]
    [InlineData("title")]
    [InlineData("empty")]
    public async Task BodylessPreviewsAreBoundedWhileExactHashDetailReturnsFullContent(string summaryKind)
    {
        var repository = CreateRepository();
        await repository.InitializeAsync();
        await repository.UpsertFeedAsync(CreateFeed(1, "Feed", true));
        var body = new string('b', 9000);
        var summary = summaryKind switch
        {
            "summary" => new string('s', 6000),
            "title" => "Story",
            _ => ""
        };
        var target = CreateStory("1:target_%'") with { Content = body, Summary = summary };
        await repository.UpsertStoryAsync(target);
        await repository.UpsertStoryAsync(target with { Hash = "1:target_%'-suffix" });
        for (var index = 0; index < 3; index++)
            await repository.UpsertStoryAsync(target with
            {
                Hash = $"1:newer-{index}",
                PublishedAt = target.PublishedAt.AddDays(index + 1)
            });

        var previews = await repository.QueryStoriesAsync(
            new ContentQuery(StoryFilter.All, Limit: 2, IncludeContent: false));

        Assert.Equal(["1:newer-2", "1:newer-1"], previews.Select(story => story.Hash));
        var expectedPreview = (summaryKind == "summary" ? summary : body)[..4096];
        Assert.All(previews, story =>
        {
            Assert.Equal("", story.Content);
            Assert.Equal(4096, story.Summary.Length);
            Assert.Equal(expectedPreview, story.Summary);
            Assert.Equal(target with
            {
                Hash = story.Hash,
                PublishedAt = story.PublishedAt,
                Content = "",
                Summary = expectedPreview
            }, story);
        });
        var detail = Assert.Single(await repository.QueryStoriesAsync(
            new ContentQuery(StoryFilter.All, Limit: 1, StoryHash: target.Hash)));
        Assert.Equal(target, detail);
        Assert.Empty(await repository.QueryStoriesAsync(
            new ContentQuery(StoryFilter.All, StoryHash: "1:target")));
        Assert.Empty(await repository.QueryStoriesAsync(
            new ContentQuery(StoryFilter.All, StoryHash: "1:TARGET_%'")));
        Assert.Empty(await repository.QueryStoriesAsync(
            new ContentQuery(StoryFilter.All, FeedId: 2, StoryHash: target.Hash)));
        Assert.Empty(await repository.QueryStoriesAsync(
            new ContentQuery(StoryFilter.Read, StoryHash: target.Hash)));
    }

    [Fact]
    public async Task CombinedFeedCountsIncludeAllReadSavedCombinationsAndReflectLocalChanges()
    {
        var repository = CreateRepository();
        await repository.InitializeAsync();
        await repository.ReplaceFeedCatalogAsync(
            [CreateFeed(1, "One", true), CreateFeed(2, "Two", true), CreateFeed(3, "Empty", true)],
            [], []);
        Assert.Empty(await repository.GetLocalFeedCountsAsync());
        await repository.CacheRemoteStoriesAsync([
            CreateStory("1:unread"),
            CreateStory("1:unread-saved", isSaved: true),
            CreateStory("1:read", isRead: true),
            CreateStory("1:read-saved", isRead: true, isSaved: true),
            CreateStory("2:read", isRead: true, feedId: 2)
        ]);

        var counts = await repository.GetLocalFeedCountsAsync();
        Assert.Equal(2, counts.Count);
        Assert.Equal(new FeedStoryCounts(4, 2, 2), counts[1]);
        Assert.Equal(new FeedStoryCounts(1, 0, 0), counts[2]);
        Assert.False(counts.ContainsKey(3));
        Assert.Equal((await repository.GetLocalUnreadCountsAsync())[1], counts[1].Unread);

        await repository.SetStoryReadAsync("1:unread-saved", true);
        await repository.SetStorySavedAsync("1:unread-saved", false);
        counts = await CreateRepository().GetLocalFeedCountsAsync();
        Assert.Equal(new FeedStoryCounts(4, 1, 1), counts[1]);
        Assert.Equal(new FeedStoryCounts(1, 0, 0), counts[2]);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(false, true)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task PartialCachePreservesReadAndKnownSavedStateUntilAuthoritativeReconciliation(bool isRead, bool isSaved)
    {
        var repository = CreateRepository();
        await repository.InitializeAsync();
        await repository.UpsertFeedAsync(CreateFeed(1, "Feed", true));
        var original = CreateStory("1:existing", isRead, isSaved);
        await repository.UpsertStoryAsync(original);
        var incoming = original with
        {
            Title = "Updated",
            Content = "<p>Updated remote body</p>",
            IsRead = !isRead,
            IsSaved = !isSaved
        };
        var newStory = incoming with { Hash = "1:new" };

        await repository.CacheRemoteStoriesAsync([incoming, newStory]);
        await repository.CacheRemoteStoriesAsync([incoming, newStory]);
        await repository.CacheRemoteStoriesAsync([incoming with { IsSaved = false }]);

        var reopened = CreateRepository();
        var stories = await reopened.GetStoriesAsync(1);
        Assert.Equal(2, stories.Count);
        Assert.Equal(incoming with { IsRead = isRead, IsSaved = isSaved || incoming.IsSaved },
            stories.Single(story => story.Hash == original.Hash));
        Assert.Equal(newStory, stories.Single(story => story.Hash == newStory.Hash));
        Assert.Empty(await reopened.GetPendingMutationsAsync(10));
        Assert.Empty(await reopened.GetUploadedMutationsAsync());

        await reopened.ReconcileRemoteContentAsync(
            new RemoteContentBatch([], new HashSet<string>(["1:existing", "1:new"]), true,
                new HashSet<string>(), true));
        Assert.All(await reopened.GetStoriesAsync(1), story =>
        {
            Assert.False(story.IsRead);
            Assert.False(story.IsSaved);
            Assert.Equal(incoming.Content, story.Content);
        });
    }

    [Theory]
    [InlineData("pending")]
    [InlineData("uploaded")]
    [InlineData("newer-local")]
    public async Task PartialCacheDoesNotAcknowledgePendingOrUploadedMutationSnapshots(string boundary)
    {
        var time = new TestTimeProvider(new DateTimeOffset(2026, 2, 1, 0, 0, 0, TimeSpan.Zero));
        var repository = CreateRepository(time);
        await repository.InitializeAsync();
        await repository.UpsertFeedAsync(CreateFeed(1, "Feed", true));
        await repository.UpsertStoryAsync(CreateStory("1:abc"));
        await repository.SetStoryReadAsync("1:abc", true);
        await repository.SetStorySavedAsync("1:abc", true);
        var snapshots = await repository.GetPendingMutationsAsync(10);
        if (boundary != "pending")
        {
            foreach (var mutation in snapshots)
                await repository.RecordUploadedMutationAsync(mutation);
            if (boundary == "uploaded")
                Assert.Equal(2, await repository.AcknowledgePendingMutationsAsync(snapshots));
            else
            {
                time.Advance(TimeSpan.FromSeconds(1));
                await repository.SetStoryReadAsync("1:abc", false);
                await repository.SetStorySavedAsync("1:abc", false);
            }
        }
        var pending = await repository.GetPendingMutationsAsync(10);
        var uploaded = await repository.GetUploadedMutationsAsync();
        var before = Assert.Single(await repository.GetStoriesAsync(1));

        // Even a page matching uploaded intent cannot acknowledge an authoritative boundary.
        var incoming = CreateStory("1:abc", isRead: true, isSaved: true) with { Content = "Fresh body" };
        await repository.CacheRemoteStoriesAsync([incoming]);
        await repository.CacheRemoteStoriesAsync([incoming with { IsRead = false, IsSaved = false }]);

        var reopened = CreateRepository(time);
        Assert.Equal(before with { Content = incoming.Content }, Assert.Single(await reopened.GetStoriesAsync(1)));
        Assert.Equal(pending, await reopened.GetPendingMutationsAsync(10));
        Assert.Equal(uploaded, await reopened.GetUploadedMutationsAsync());
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();

        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }

    private SqliteContentRepository CreateRepository(TimeProvider? timeProvider = null) =>
        new(new SqliteConnectionFactory(_databasePath), timeProvider);

    private static Feed CreateFeed(int id, string title, bool isActive) =>
        new(
            id,
            title,
            $"https://example.test/{id}",
            null,
            0,
            null,
            isActive);

    private static Story CreateStory(
        string hash,
        bool isRead = false,
        bool isSaved = false,
        DateTimeOffset? publishedAt = null,
        int feedId = 1) =>
        new(
            hash,
            feedId,
            null,
            null,
            "Story",
            null,
            "https://example.test/story",
            "<p>Story</p>",
            "Story",
            null,
            publishedAt ?? new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            isRead,
            isSaved);

    private sealed class TestTimeProvider(DateTimeOffset utcNow) : TimeProvider
    {
        private DateTimeOffset _utcNow = utcNow;

        public override DateTimeOffset GetUtcNow() => _utcNow;

        public void Advance(TimeSpan duration) => _utcNow += duration;
    }
}
