using System.Net;
using System.Net.Http;
using System.Text;
using NewsBlurSharp;
using Spectro.Domain;

namespace Spectro.Sync.Tests;

public sealed class NewsBlurSyncRemoteServiceTests
{
    [Fact]
    public async Task AdapterAcceptsUnfiledSubscriptionsAndRequestsFlatFolders()
    {
        var handler = new ScriptedHandler(request =>
        {
            Assert.Contains("flat=true", request.RequestUri!.Query);
            return Json(
                """
                {
                  "authenticated": true,
                  "folders": [7, {"Tech": [8]}],
                  "feeds": {
                    "7": {"id": 7, "feed_title": "Unfiled", "feed_address": "https://example.test/a"},
                    "8": {"id": 8, "feed_title": "Filed", "feed_address": "https://example.test/b"}
                  }
                }
                """);
        });
        var subject = new NewsBlurSyncRemoteService(new NewsBlurClient(handler));

        var catalog = await subject.GetFeedCatalogAsync(CancellationToken.None);

        Assert.Equal(2, catalog.Feeds.Count);
        Assert.Equal("Tech", Assert.Single(catalog.Folders).Title);
        Assert.Equal(8, Assert.Single(catalog.FolderFeeds).FeedId);
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
                "/reader/feed/7" => Json(StoryJson(readStatus: 1)),
                "/reader/unread_story_hashes" => Json(
                    """
                    {
                      "authenticated": true,
                      "unread_feed_story_hashes": { "7": ["7:abc"] }
                    }
                    """),
                "/reader/starred_stories" => Json(StoryJson(readStatus: 0)),
                _ => throw new InvalidOperationException(path)
            };
        });
        var subject = new NewsBlurSyncRemoteService(new NewsBlurClient(handler));

        var catalog = await subject.GetFeedCatalogAsync(CancellationToken.None);
        var stories = await subject.GetFeedStoriesAsync(7, 1, CancellationToken.None);
        var unread = await subject.GetUnreadStoryHashesAsync(CancellationToken.None);
        var starred = await subject.GetStarredStoriesAsync(1, CancellationToken.None);

        Assert.Equal("Example", Assert.Single(catalog.Feeds).Title);
        Assert.Equal("Tech", Assert.Single(catalog.Folders).Title);
        Assert.Equal(7, Assert.Single(catalog.FolderFeeds).FeedId);
        Assert.True(Assert.Single(stories.Stories).IsRead);
        Assert.False(stories.IsLastPage);
        Assert.Contains("7:abc", unread);
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
            () => subject.GetUnreadStoryHashesAsync(CancellationToken.None));
        Assert.Equal(SyncRemoteFailureKind.MalformedData, error.Kind);
    }

    [Theory]
    [InlineData(0, true)]
    [InlineData(6, false)]
    public async Task EmptyPageIsTerminalOnlyWhenNoStoriesWereHidden(int hidden, bool terminal)
    {
        var handler = new ScriptedHandler(_ => Json(
            $$"""{"authenticated":true,"stories":[],"hidden_stories_removed":{{hidden}}}"""));
        var subject = new NewsBlurSyncRemoteService(new NewsBlurClient(handler));
        var page = await subject.GetFeedStoriesAsync(7, 1, CancellationToken.None);
        Assert.Equal(terminal, page.IsLastPage);
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
