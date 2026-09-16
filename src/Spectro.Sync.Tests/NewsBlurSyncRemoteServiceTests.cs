using System.Net;
using System.Net.Http;
using System.Text;
using NewsBlurSharp;
using Spectro.Domain;
using Spectro.Infrastructure;

namespace Spectro.Sync.Tests;

public sealed class NewsBlurSyncRemoteServiceTests
{
    [Theory]
    [InlineData("\"active\":false,", true, false)]
    [InlineData("\"active\":false,", false, false)]
    [InlineData("\"active\":true,", false, true)]
    [InlineData("", true, true)]
    [InlineData("", false, false)]
    [InlineData("\"active\":null,", true, true)]
    public async Task ExplicitActiveControlsVisibleFeedsMembershipAndDownloads(
        string activeProperty, bool subscribed, bool expectedActive)
    {
        var directory = Path.Combine(Path.GetTempPath(), "Spectro.Sync.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var databasePath = Path.Combine(directory, "spectro.db");
            var repository = new SqliteContentRepository(new SqliteConnectionFactory(databasePath));
            await repository.InitializeAsync();
            await repository.UpsertFeedAsync(
                new Feed(10310620, "Previously visible", "https://example.test/daily", null, 0, null, true));
            var inventoryScopes = new List<int[]>();
            var handler = new ScriptedHandler(request =>
            {
                var path = request.RequestUri!.AbsolutePath;
                if (path == "/reader/feeds")
                {
                    Assert.Contains("flat=false", request.RequestUri.Query);
                    return Json(
                        $$"""
                        {
                          "authenticated": true,
                          "folders": [{"Subscriptions": [7, 10310620]}],
                          "feeds": {
                            "7": {"id":7,"feed_title":"Active","feed_address":"https://example.test/7","active":true},
                            "10310620": {
                              "id": 10310620,
                              "feed_title": "Daily Brief",
                              "feed_address": "https://example.test/daily",
                              {{activeProperty}}
                              "subscribed": {{subscribed.ToString().ToLowerInvariant()}}
                            }
                          }
                        }
                        """);
                }
                if (path == "/reader/unread_story_hashes")
                {
                    var ids = request.RequestUri.Query.TrimStart('?').Split('&')
                        .Where(parameter => parameter.StartsWith("feed_id=", StringComparison.Ordinal))
                        .Select(parameter => int.Parse(parameter["feed_id=".Length..],
                            System.Globalization.CultureInfo.InvariantCulture)).Order().ToArray();
                    inventoryScopes.Add(ids);
                    var entries = string.Join(",", ids.Select(id => $"\"{id}\":[]"));
                    return Json($"{{\"authenticated\":true,\"unread_feed_story_hashes\":{{{entries}}}}}");
                }
                Assert.Equal("/reader/starred_story_hashes", path);
                return Json("""{"authenticated":true,"starred_story_hashes":[]}""");
            });
            var remote = new NewsBlurSyncRemoteService(new NewsBlurClient(handler));

            var result = await new OfflineFirstSynchronizer(repository, remote, UnpacedOptions)
                .SynchronizeAsync(new SyncRequest(Guid.NewGuid().ToString("N")));

            Assert.True(result.IsSuccess);
            int[] expectedIds = expectedActive ? [7, 10310620] : [7];
            Assert.Equal(expectedIds.Length, result.State.FeedCount);
            Assert.Equal(expectedIds.Length, result.State.CompletedFeedCount);
            Assert.Equal(expectedIds, (await repository.GetFeedsAsync()).Select(feed => feed.Id).Order());
            var folder = Assert.Single(await repository.GetFeedFoldersAsync());
            Assert.Equal(expectedIds, folder.Feeds.Select(feed => feed.Id).Order());
            Assert.Equal(2, inventoryScopes.Count);
            Assert.All(inventoryScopes, scope => Assert.Equal(expectedIds, scope));
            Assert.Equal(4, handler.CallCount);
            await using var connection = new Microsoft.Data.Sqlite.SqliteConnection($"Data Source={databasePath}");
            await connection.OpenAsync();
            await using var command = connection.CreateCommand();
            command.CommandText = "SELECT is_active FROM feed WHERE id = 10310620;";
            Assert.Equal(expectedActive ? 1L : 0L, await command.ExecuteScalarAsync());
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task AdapterRequestsHierarchyAndMapsServerFixtureWithUnfiledSubscriptions()
    {
        var handler = new ScriptedHandler(request =>
        {
            Assert.Equal(
                "https://newsblur.com/reader/feeds?include_favicons=true&flat=false&update_counts=true",
                request.RequestUri!.ToString());
            return Json(
                """
                {
                  "authenticated": true,
                  "folders": [
                    5771943,
                    {"Spectro Automation - News - 8d306b70": [7001666, 6227898]},
                    {"Spectro Automation - Science - 8d306b70": [7633417, 706322]}
                  ],
                  "feeds": {
                    "5771943": {"id": 5771943, "feed_title": "Unfiled", "feed_address": "https://example.test/a", "active": true},
                    "7001666": {"id": 7001666, "feed_title": "News one", "feed_address": "https://example.test/b", "active": true},
                    "6227898": {"id": 6227898, "feed_title": "News two", "feed_address": "https://example.test/c", "active": true},
                    "7633417": {"id": 7633417, "feed_title": "Science one", "feed_address": "https://example.test/d", "active": true},
                    "706322": {"id": 706322, "feed_title": "Science two", "feed_address": "https://example.test/e", "active": true}
                  }
                }
                """);
        });
        var subject = new NewsBlurSyncRemoteService(new NewsBlurClient(handler));

        var catalog = await subject.GetFeedCatalogAsync(CancellationToken.None);

        Assert.Equal(5, catalog.Feeds.Count);
        Assert.All(catalog.Feeds, feed => Assert.True(feed.IsActive));
        Assert.Equal([
            new Folder("Spectro Automation - News - 8d306b70", "Spectro Automation - News - 8d306b70", 0),
            new Folder("Spectro Automation - Science - 8d306b70", "Spectro Automation - Science - 8d306b70", 1)
        ], catalog.Folders);
        Assert.Equal([
            new FolderFeed("Spectro Automation - News - 8d306b70", 7001666, 0),
            new FolderFeed("Spectro Automation - News - 8d306b70", 6227898, 1),
            new FolderFeed("Spectro Automation - Science - 8d306b70", 7633417, 0),
            new FolderFeed("Spectro Automation - Science - 8d306b70", 706322, 1)
        ], catalog.FolderFeeds);
    }

    [Fact]
    public async Task AdapterPreservesEmptyAndNestedFoldersWithDistinctPaths()
    {
        var handler = new ScriptedHandler(_ => Json(
            """
            {
              "authenticated": true,
              "folders": [7, {"Tech": [8, {"Child": [9]}, {"Empty": []}]}, {"Child": [7]}],
              "feeds": {
                "7": {"id": 7, "feed_title": "Seven", "feed_address": "https://example.test/7"},
                "8": {"id": 8, "feed_title": "Eight", "feed_address": "https://example.test/8"},
                "9": {"id": 9, "feed_title": "Nine", "feed_address": "https://example.test/9"}
              }
            }
            """));
        var catalog = await new NewsBlurSyncRemoteService(new NewsBlurClient(handler))
            .GetFeedCatalogAsync(CancellationToken.None);

        Assert.Equal([
            new Folder("Tech", "Tech", 0),
            new Folder("Tech / Child", "Tech / Child", 1),
            new Folder("Tech / Empty", "Tech / Empty", 2),
            new Folder("Child", "Child", 3)
        ], catalog.Folders);
        Assert.Equal([
            new FolderFeed("Tech", 8, 0),
            new FolderFeed("Tech / Child", 9, 0),
            new FolderFeed("Child", 7, 0)
        ], catalog.FolderFeeds);
    }

    [Fact]
    public async Task ExplicitEmptyHierarchyIsAValidEmptyCatalog()
    {
        var handler = new ScriptedHandler(_ => Json(
            """{"authenticated":true,"feeds":{},"folders":[]}"""));
        var catalog = await new NewsBlurSyncRemoteService(new NewsBlurClient(handler))
            .GetFeedCatalogAsync(CancellationToken.None);
        Assert.Empty(catalog.Feeds);
        Assert.Empty(catalog.Folders);
        Assert.Empty(catalog.FolderFeeds);
    }

    [Theory]
    [InlineData("")]
    [InlineData(",\"folders\":null")]
    [InlineData(",\"folders\":[{\"Tech\":[null]}]")]
    [InlineData(",\"folders\":[{\"Tech\":[{\"Child\":null}]}]")]
    [InlineData(",\"folders\":[{\"Tech\":[{\"Child\":[]}]},{\"Tech / Child\":[]}]")]
    public async Task MissingOrMalformedHierarchyCannotErasePersistedCatalog(string foldersProperty)
    {
        var directory = Path.Combine(Path.GetTempPath(), "Spectro.Sync.Tests", Guid.NewGuid().ToString("N"));
        try
        {
            var repository = new SqliteContentRepository(
                new SqliteConnectionFactory(Path.Combine(directory, "spectro.db")));
            await repository.InitializeAsync();
            var existing = new Feed(7, "Existing", "https://example.test/7", null, 0, null, true);
            await repository.ReplaceFeedCatalogAsync(
                [existing], [new Folder("Existing folder", "Existing folder", 0)],
                [new FolderFeed("Existing folder", 7, 0)]);
            var handler = new ScriptedHandler(_ => Json(
                $$"""{"authenticated":true,"feeds":{}{{foldersProperty}}}"""));
            var remote = new NewsBlurSyncRemoteService(new NewsBlurClient(handler));

            var result = await new OfflineFirstSynchronizer(repository, remote, UnpacedOptions)
                .SynchronizeAsync(new SyncRequest(Guid.NewGuid().ToString("N")));

            Assert.Equal(SyncOutcome.MalformedRemoteData, result.Outcome);
            Assert.Equal(SyncStage.RefreshFeedsAndFolders, result.State.Stage);
            Assert.Equal(existing, Assert.Single(await repository.GetFeedsAsync()));
            var folder = Assert.Single(await repository.GetFeedFoldersAsync());
            Assert.Equal("Existing folder", folder.Folder.Title);
            Assert.Equal(existing, Assert.Single(folder.Feeds));
            Assert.Null(await repository.GetCheckpointAsync(
                OfflineFirstSynchronizer.SuccessfulSyncCheckpointName));
            Assert.Equal(1, handler.CallCount);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(directory)) Directory.Delete(directory, recursive: true);
        }
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("\"invalid\"")]
    [InlineData("null")]
    public async Task AdapterRejectsInvalidUnfiledEntries(string entry)
    {
        var handler = new ScriptedHandler(_ => Json(
            $$"""{"authenticated":true,"feeds":{},"folders":[{{entry}}]}"""));
        var subject = new NewsBlurSyncRemoteService(new NewsBlurClient(handler));
        var error = await Assert.ThrowsAsync<SyncRemoteException>(
            () => subject.GetFeedCatalogAsync(CancellationToken.None));
        Assert.Equal(SyncRemoteFailureKind.MalformedData, error.Kind);
    }

    [Fact]
    public async Task AdapterMapsNewsBlurResponsesToPlatformNeutralModels()
    {
        var handler = new ScriptedHandler(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            return path switch
            {
                "/reader/feeds" => Json(
                    """
                    {
                      "authenticated": true,
                      "folders": [{ "Tech": [7] }],
                      "feeds": {
                        "7": {
                          "id": 7,
                          "feed_title": "Example",
                          "feed_address": "https://example.test/feed",
                          "active": true,
                          "nt": 2,
                          "last_story_date": "1789416000"
                        }
                      }
                    }
                    """),
                "/reader/river_stories" => Json(StoryJson(readStatus: 1)),
                "/reader/unread_story_hashes" => Json(
                    """
                    {
                      "authenticated": true,
                      "unread_feed_story_hashes": { "7": [["7:abc",1789416000.125]] }
                    }
                    """),
                "/reader/starred_stories" => Json(StoryJson(readStatus: 0)),
                "/reader/starred_story_hashes" => Json(
                    """{"authenticated":true,"starred_story_hashes":[["7:abc",1789416000.875]]}"""),
                _ => throw new InvalidOperationException(path)
            };
        });
        var subject = new NewsBlurSyncRemoteService(new NewsBlurClient(handler));

        var catalog = await subject.GetFeedCatalogAsync(CancellationToken.None);
        var stories = await subject.GetStoriesByHashesAsync(["7:abc"], false, CancellationToken.None);
        var unread = await subject.GetStoryHashInventoryAsync(true, [7], CancellationToken.None);
        var savedHashes = await subject.GetSavedStoryHashesAsync(CancellationToken.None);
        var starred = await subject.GetStoriesByHashesAsync(["7:abc"], true, CancellationToken.None);

        Assert.Equal("Example", Assert.Single(catalog.Feeds).Title);
        Assert.Equal("Tech", Assert.Single(catalog.Folders).Title);
        Assert.Equal(7, Assert.Single(catalog.FolderFeeds).FeedId);
        Assert.True(Assert.Single(stories.Stories).IsRead);
        Assert.False(stories.IsLastPage);
        Assert.Equal(new RemoteStoryHash("7:abc", 7, 1789416000.125), Assert.Single(unread.Feeds[7]));
        Assert.Equal(1789416000.875, savedHashes["7:abc"]);
        Assert.True(Assert.Single(starred.Stories).IsSaved);
        Assert.False(starred.IsLastPage);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"unread_story_hashes\":{}}")]
    [InlineData("{\"unread_feed_story_hashes\":null}")]
    [InlineData("{\"unread_feed_story_hashes\":{\"7\":null}}")]
    [InlineData("{\"unread_feed_story_hashes\":{\"7\":[\"\"]}}")]
    public async Task InvalidUnreadSetCannotBecomeAnAuthoritativeEmptySet(string json)
    {
        var handler = new ScriptedHandler(_ => Json(json));
        var subject = new NewsBlurSyncRemoteService(new NewsBlurClient(handler));
        var error = await Assert.ThrowsAsync<SyncRemoteException>(
            () => subject.GetStoryHashInventoryAsync(true, [7], CancellationToken.None));
        Assert.Equal(SyncRemoteFailureKind.MalformedData, error.Kind);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"starred_story_hashes\":null}")]
    [InlineData("{\"starred_story_hashes\":{\"7\":[]}}")]
    [InlineData("{\"starred_story_hashes\":[[\"7:abc\"]]}")]
    [InlineData("{\"starred_story_hashes\":[[\"7:abc\",\"NaN\"]]}")]
    public async Task InvalidSavedInventoryCannotBecomeAnAuthoritativeEmptySet(string json)
    {
        var subject = new NewsBlurSyncRemoteService(new NewsBlurClient(new ScriptedHandler(_ => Json(json))));

        var error = await Assert.ThrowsAsync<SyncRemoteException>(
            () => subject.GetSavedStoryHashesAsync(CancellationToken.None));

        Assert.Equal(SyncRemoteFailureKind.MalformedData, error.Kind);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task BulkBodyMappingUsesEachStoryFeedIdInsteadOfTopLevelFeedId(bool saved)
    {
        var handler = new ScriptedHandler(request =>
        {
            Assert.Equal(saved ? "/reader/starred_stories" : "/reader/river_stories",
                request.RequestUri!.AbsolutePath);
            return Json(
                """
                {
                  "authenticated":true,
                  "feed_id":99,
                  "stories":[
                    {"story_hash":"7:abc","story_feed_id":7,"story_title":"Seven",
                     "story_content":"seven","story_timestamp":"1789416000","read_status":1},
                    {"story_hash":"8:def","story_feed_id":8,"story_title":"Eight",
                     "story_content":"eight","story_timestamp":"1789416001","read_status":0}
                  ]
                }
                """);
        });
        var subject = new NewsBlurSyncRemoteService(new NewsBlurClient(handler));

        var page = await subject.GetStoriesByHashesAsync(["7:abc", "8:def"], saved, CancellationToken.None);

        Assert.Collection(page.Stories,
            story =>
            {
                Assert.Equal("7:abc", story.Hash);
                Assert.Equal(7, story.FeedId);
                Assert.True(story.IsRead);
                Assert.Equal(saved, story.IsSaved);
            },
            story =>
            {
                Assert.Equal("8:def", story.Hash);
                Assert.Equal(8, story.FeedId);
                Assert.False(story.IsRead);
                Assert.Equal(saved, story.IsSaved);
            });
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(6, false)]
    public async Task EmptyPageIsTerminalOnlyWhenNoStoriesWereHidden(int hidden, bool terminal)
    {
        var handler = new ScriptedHandler(request =>
        {
            Assert.Equal("/reader/feed/7", request.RequestUri!.AbsolutePath);
            Assert.Contains("include_hidden=true", request.RequestUri.Query);
            return Json($$"""{"authenticated":true,"stories":[],"hidden_stories_removed":{{hidden}}}""");
        });
        var subject = new NewsBlurSyncRemoteService(new NewsBlurClient(handler));
        var page = await subject.GetFeedStoriesAsync(7, 1, CancellationToken.None);
        Assert.Equal(terminal, page.IsLastPage);
    }

    [Fact]
    public async Task AmbiguousForbiddenResponseIsPermanentRatherThanInvalidatingAuthentication()
    {
        var handler = new ScriptedHandler(_ => new HttpResponseMessage(HttpStatusCode.Forbidden)
        {
            Content = new StringContent("Forbidden", Encoding.UTF8, "text/plain")
        });
        var subject = new NewsBlurSyncRemoteService(new NewsBlurClient(handler));

        var error = await Assert.ThrowsAsync<SyncRemoteException>(
            () => subject.GetStoryHashInventoryAsync(false, [7], CancellationToken.None));

        Assert.Equal(SyncRemoteFailureKind.Permanent, error.Kind);
        Assert.Equal(403, error.HttpStatusCode);
    }

    [Theory]
    [InlineData(StoryMutationKind.Read, true, "/reader/mark_story_hashes_as_read")]
    [InlineData(StoryMutationKind.Read, false, "/reader/mark_story_hash_as_unread")]
    [InlineData(StoryMutationKind.Saved, true, "/reader/mark_story_hash_as_starred")]
    [InlineData(StoryMutationKind.Saved, false, "/reader/mark_story_hash_as_unstarred")]
    public async Task AdapterUploadsEveryMutationKind(
        StoryMutationKind kind,
        bool value,
        string expectedPath)
    {
        var handler = new ScriptedHandler(request =>
        {
            Assert.Equal(expectedPath, request.RequestUri!.AbsolutePath);
            return Json("{}");
        });
        var subject = new NewsBlurSyncRemoteService(new NewsBlurClient(handler));

        await subject.UploadMutationAsync(
            new PendingStoryMutation(
                1,
                "7:abc",
                kind,
                value,
                DateTimeOffset.UnixEpoch),
            CancellationToken.None);

        Assert.Equal(1, handler.CallCount);
    }

    [Fact]
    public async Task AdapterRejectsMalformedCatalogBeforePersistence()
    {
        var handler = new ScriptedHandler(_ => Json(
            """
            {
              "authenticated": true,
              "folders": [],
              "feeds": {
                "0": { "id": 0, "feed_title": "", "feed_address": "" }
              }
            }
            """));
        var subject = new NewsBlurSyncRemoteService(new NewsBlurClient(handler));

        var exception = await Assert.ThrowsAsync<SyncRemoteException>(
            () => subject.GetFeedCatalogAsync(CancellationToken.None));

        Assert.Equal(SyncRemoteFailureKind.MalformedData, exception.Kind);
    }

    private static string StoryJson(int readStatus) =>
        $$"""
        {
          "authenticated": true,
          "feed_id": 7,
          "stories": [{
            "story_hash": "7:abc",
            "id": "service-id",
            "guid_hash": "guid",
            "story_feed_id": 7,
            "story_title": "Story",
            "story_authors": "Author",
            "story_permalink": "https://example.test/story",
            "story_content": "<p>Story</p>",
            "story_timestamp": "1789416000",
            "read_status": {{readStatus}},
            "image_urls": []
          }]
        }
        """;

    private static HttpResponseMessage Json(string content) =>
        new(HttpStatusCode.OK)
        {
            Content = new StringContent(content, Encoding.UTF8, "application/json")
        };

    private static SyncOptions UnpacedOptions => new()
    {
        MinimumRequestInterval = TimeSpan.Zero,
        MinimumSavedRequestInterval = TimeSpan.Zero
    };

    private sealed class ScriptedHandler(
        Func<HttpRequestMessage, HttpResponseMessage> responder) : HttpMessageHandler
    {
        public int CallCount { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            CallCount++;
            return Task.FromResult(responder(request));
        }
    }
}
