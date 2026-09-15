using Moq;
using Spectro.Domain;
using Spectro.Presentation;
using Xunit;

namespace Spectro.ViewModels.Tests;

public sealed class LatestLibraryQueryTests
{
    [Fact]
    public async Task SlowerPreviousNavigationCannotReplaceTheLatestCollection()
    {
        var repository = new Mock<IContentRepository>(MockBehavior.Strict);
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstResult = new TaskCompletionSource<IReadOnlyList<Story>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var oldStory = CreateStory("1:old", 1);
        var latestStory = CreateStory("2:latest", 2);
        repository.Setup(store => store.QueryStoriesAsync(
                It.Is<ContentQuery>(query => query.FeedId == 1 && query.StoryHash == null && !query.IncludeContent),
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                firstStarted.TrySetResult();
                return firstResult.Task;
            });
        repository.Setup(store => store.QueryStoriesAsync(
                It.Is<ContentQuery>(query => query.FeedId == 2 && query.StoryHash == null && !query.IncludeContent),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([latestStory with { Content = string.Empty }]);
        var subject = CreateSubject(repository.Object);
        var first = subject.SelectNavigationAsync(new("feed:1", "First", NavigationItemKind.Feed, FeedId: 1));
        try
        {
            await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await subject.SelectNavigationAsync(new("feed:2", "Latest", NavigationItemKind.Feed, FeedId: 2));
            var revision = subject.LibraryRevision;
            firstResult.SetResult([oldStory with { Content = string.Empty }]);
            await first.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal("feed:2", subject.SelectedNavigation?.Id);
            Assert.Equal("2:latest", Assert.Single(subject.Stories).Hash);
            Assert.Equal("Latest", subject.StatusTitle);
            Assert.Equal(revision, subject.LibraryRevision);
        }
        finally
        {
            firstResult.TrySetResult([]);
            await first.WaitAsync(TimeSpan.FromSeconds(10));
        }
        repository.VerifyAll();
    }

    [Fact]
    public async Task SlowerPreviousFilterCannotReplaceLatestFilterWithinFeedScope()
    {
        var repository = new Mock<IContentRepository>(MockBehavior.Strict);
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstResult = new TaskCompletionSource<IReadOnlyList<Story>>(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.Setup(store => store.QueryStoriesAsync(
                It.Is<ContentQuery>(query => query.FeedId == 1 && query.Filter == StoryFilter.All && !query.IncludeContent),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        repository.Setup(store => store.QueryStoriesAsync(
                It.Is<ContentQuery>(query => query.FeedId == 1 && query.Filter == StoryFilter.Unread && !query.IncludeContent),
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                firstStarted.TrySetResult();
                return firstResult.Task;
            });
        repository.Setup(store => store.QueryStoriesAsync(
                It.Is<ContentQuery>(query => query.FeedId == 1 && query.Filter == StoryFilter.Saved && !query.IncludeContent),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([CreateStory("1:saved", 1) with { IsSaved = true, Content = string.Empty }]);
        var subject = CreateSubject(repository.Object);
        await subject.SelectNavigationAsync(new("feed:1", "First", NavigationItemKind.Feed, FeedId: 1));
        var first = subject.SelectFilterAsync(StoryFilter.Unread);
        try
        {
            await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            await subject.SelectFilterAsync(StoryFilter.Saved);
            firstResult.SetResult([CreateStory("1:unread", 1) with { IsRead = false, Content = string.Empty }]);
            await first.WaitAsync(TimeSpan.FromSeconds(10));

            Assert.Equal(StoryFilter.Saved, subject.ActiveFilter);
            Assert.Equal("feed:1", subject.SelectedNavigation?.Id);
            Assert.Equal("1:saved", Assert.Single(subject.Stories).Hash);
        }
        finally
        {
            firstResult.TrySetResult([]);
            await first.WaitAsync(TimeSpan.FromSeconds(10));
        }
        repository.VerifyAll();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task LateExactHashDetailsCannotReopenAnArticleAfterSelectionChanges(bool switchCollection)
    {
        var repository = new Mock<IContentRepository>(MockBehavior.Strict);
        var firstStarted = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstResult = new TaskCompletionSource<IReadOnlyList<Story>>(TaskCreationOptions.RunContinuationsAsynchronously);
        var firstStory = CreateStory("1:first", 1);
        var latestStory = CreateStory("2:latest", 2);
        repository.Setup(store => store.QueryStoriesAsync(
                It.Is<ContentQuery>(query =>
                    query.StoryHash == firstStory.Hash && query.Limit == 1 && query.IncludeContent && query.Filter == StoryFilter.All),
                It.IsAny<CancellationToken>()))
            .Returns(() =>
            {
                firstStarted.TrySetResult();
                return firstResult.Task;
            });
        if (switchCollection)
        {
            repository.Setup(store => store.QueryStoriesAsync(
                    It.Is<ContentQuery>(query => query.StoryHash == null && !query.IncludeContent && query.Filter == StoryFilter.Saved),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync([]);
        }
        else
        {
            repository.Setup(store => store.QueryStoriesAsync(
                    It.Is<ContentQuery>(query =>
                        query.StoryHash == latestStory.Hash && query.Limit == 1 && query.IncludeContent && query.Filter == StoryFilter.All),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync([latestStory]);
        }
        var subject = CreateSubject(repository.Object);
        var first = subject.SelectStoryAsync(firstStory with { Content = string.Empty });
        try
        {
            await firstStarted.Task.WaitAsync(TimeSpan.FromSeconds(10));
            if (switchCollection)
                await subject.SelectNavigationAsync(new("saved", "Saved", NavigationItemKind.Filter, StoryFilter.Saved));
            else
                await subject.SelectStoryAsync(latestStory with { Content = string.Empty });
            firstResult.SetResult([firstStory]);
            await first.WaitAsync(TimeSpan.FromSeconds(10));

            if (switchCollection)
            {
                Assert.Null(subject.SelectedStory);
                Assert.Empty(subject.ReaderHtml);
            }
            else
            {
                Assert.Equal("2:latest", subject.SelectedStory?.Hash);
                Assert.Equal(latestStory.Content, subject.SelectedStory?.Content);
                Assert.Contains("Body 2:latest", subject.ReaderHtml);
                Assert.DoesNotContain("Body 1:first", subject.ReaderHtml);
            }
        }
        finally
        {
            firstResult.TrySetResult([]);
            await first.WaitAsync(TimeSpan.FromSeconds(10));
        }
        repository.VerifyAll();
    }

    private static AppViewModel CreateSubject(IContentRepository repository)
    {
        var settings = new Mock<ISettingsStore>(MockBehavior.Strict);
        settings.Setup(store => store.Load()).Returns(new ReaderSettings());
        return new(repository, Mock.Of<ISessionService>(), Mock.Of<ISynchronizationService>(), settings.Object);
    }

    private static Story CreateStory(string hash, int feedId) =>
        new(hash, feedId, null, null, hash, null, null, $"<p>Body {hash}</p>",
            "Preview", null, DateTimeOffset.UtcNow, true, false);
}
