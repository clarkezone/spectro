using Moq;
using Spectro.Domain;
using Spectro.Presentation;
using Spectro.Sync;
using Xunit;

namespace Spectro.ViewModels.Tests;

public sealed class AppViewModelTests
{
    private readonly Mock<IContentRepository> _repository = new();
    private readonly Mock<ISessionService> _session = new();
    private readonly Mock<ISynchronizationService> _sync = new();
    private readonly TestSettingsStore _settings = new();

    public AppViewModelTests()
    {
        _repository.Setup(repository => repository.InitializeAsync(It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);
        _repository.Setup(repository => repository.GetFeedsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _repository.Setup(repository => repository.GetLocalUnreadCountsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, int>());
        _repository.Setup(repository => repository.GetLocalFeedCountsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new Dictionary<int, FeedStoryCounts>());
        _repository.Setup(repository => repository.GetFeedFoldersAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
        _repository.Setup(repository => repository.QueryStoriesAsync(
                It.IsAny<ContentQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);
    }

    [Fact]
    public async Task FirstRunShowsLoginAndValidatesWithoutCallingNetwork()
    {
        _session.Setup(service => service.RestoreAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionResult(false));
        var subject = CreateSubject();
        await subject.InitializeAsync();

        await subject.LoginAsync();

        Assert.True(subject.IsLoginScreen);
        Assert.Equal("Check your details", subject.StatusTitle);
        _session.Verify(service => service.LoginAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(SessionFailureKind.Authentication, "Sign-in failed")]
    [InlineData(SessionFailureKind.Offline, "You're offline")]
    [InlineData(SessionFailureKind.Transient, "NewsBlur is temporarily unavailable")]
    public async Task LoginMapsActionableFailureSurfaces(
        SessionFailureKind failure,
        string expectedTitle)
    {
        var subject = CreateSubject();
        subject.Username = "reader";
        subject.Password = "secret";
        _session.Setup(service => service.LoginAsync(
                "reader",
                "secret",
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionResult(false, FailureKind: failure));

        await subject.LoginAsync();

        Assert.Equal(expectedTitle, subject.StatusTitle);
        Assert.Empty(subject.Password);
        Assert.True(subject.IsLoginScreen || subject.Screen == AppScreen.Loading);
    }

    [Fact]
    public async Task RestoredSessionLoadsOnlyRepositoryDataThenRunsInitialSync()
    {
        var story = CreateStory();
        _session.Setup(service => service.RestoreAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionResult(true, "42"));
        _repository.Setup(repository => repository.QueryStoriesAsync(
                It.IsAny<ContentQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([story]);
        _sync.Setup(service => service.SynchronizeAsync(
                "42",
                It.IsAny<IProgress<SyncState>>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(SuccessfulSync());

        var subject = CreateSubject();
        await subject.InitializeAsync();

        Assert.True(subject.IsReaderScreen);
        Assert.Equal(story.Hash, Assert.Single(subject.Stories).Hash);
        _sync.Verify(service => service.SynchronizeAsync(
            "42",
            It.IsAny<IProgress<SyncState>>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task LocalReadAndSavedChangesGoThroughRepository()
    {
        var story = CreateStory();
        _repository.SetupSequence(repository => repository.QueryStoriesAsync(
                It.IsAny<ContentQuery>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([story])
            .ReturnsAsync([story with { IsRead = true }])
            .ReturnsAsync([story with { IsSaved = true }]);
        var subject = CreateSubject();
        subject.NavigationItems.Add(new(
            "all",
            "All stories",
            NavigationItemKind.Filter,
            StoryFilter.All));

        await subject.SelectNavigationAsync(subject.NavigationItems[0]);
        await subject.ToggleReadAsync(story);
        await subject.ToggleSavedAsync(story);

        _repository.Verify(repository => repository.SetStoryReadAsync(
            story.Hash,
            true,
            It.IsAny<CancellationToken>()));
        _repository.Verify(repository => repository.SetStorySavedAsync(
            story.Hash,
            true,
            It.IsAny<CancellationToken>()));
    }

    [Fact]
    public void ReaderHtmlIsSanitizedAndUsesReaderPreferences()
    {
        var story = CreateStory() with
        {
            Content = "<p onclick=\"steal()\" style=\"background:url(https://tracker.test)\">Safe</p><script>alert(1)</script><img src=\"https://tracker.test/pixel\"><svg><image href=\"https://tracker.test/vector\" /></svg><a href=\"javascript:bad()\">link</a>"
        };
        var html = ReaderHtmlBuilder.Build(
            story,
            new ReaderSettings(AppTheme.Dark, 22, 840));

        Assert.DoesNotContain("<script", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("onclick", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("javascript:", html, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("tracker.test", html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("font: 22px", html, StringComparison.Ordinal);
        Assert.Contains("max-width: 840px", html, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpeningUnreadStoryKeepsItsReaderAfterItLeavesTheFilter()
    {
        var story = CreateStory();
        var subject = CreateSubject();
        var unread = new NavigationItem("unread", "Unread", NavigationItemKind.Filter, StoryFilter.Unread);
        subject.NavigationItems.Add(unread);
        SetupStoryDetails(story);
        _repository.SetupSequence(repository => repository.QueryStoriesAsync(
                It.Is<ContentQuery>(query => query.StoryHash == null), It.IsAny<CancellationToken>()))
            .ReturnsAsync([story])
            .ReturnsAsync([]);
        await subject.SelectNavigationAsync(unread);

        await subject.SelectStoryAsync(story);

        Assert.Empty(subject.Stories);
        Assert.Equal(story.Hash, subject.SelectedStory?.Hash);
        Assert.True(subject.SelectedStory?.IsRead);
        Assert.Contains("Body", subject.ReaderHtml);
    }

    [Fact]
    public async Task TogglingSavedOutsideCurrentFilterPreservesTheSelectedStoryAndNewValue()
    {
        var story = CreateStory() with { IsRead = true };
        SetupStoryDetails(story);
        var subject = CreateSubject();
        subject.NavigationItems.Add(new("all", "All", NavigationItemKind.Filter));
        await subject.SelectNavigationAsync(subject.NavigationItems[0]);
        await subject.SelectStoryAsync(story);

        await subject.ToggleSavedAsync(subject.SelectedStory!);
        Assert.True(subject.SelectedStory?.IsSaved);
        await subject.ToggleSavedAsync(subject.SelectedStory!);
        Assert.False(subject.SelectedStory?.IsSaved);
        _repository.Verify(repository => repository.SetStorySavedAsync(story.Hash, false, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task SwitchingCollectionClearsThePreviouslyOpenArticle()
    {
        var subject = CreateSubject();
        var story = CreateStory() with { IsRead = true };
        SetupStoryDetails(story);
        await subject.SelectStoryAsync(story);
        Assert.Equal(story.Hash, subject.SelectedStory?.Hash);
        Assert.Contains("Body", subject.ReaderHtml);

        await subject.SelectNavigationAsync(new("saved", "Saved", NavigationItemKind.Filter, StoryFilter.Saved));

        Assert.Null(subject.SelectedStory);
        Assert.Empty(subject.ReaderHtml);
    }

    [Fact]
    public void StoryPresentationDecodesPreviewAndMeasuresReadingTime()
    {
        Assert.Equal("A & B with spaces", StoryPresentation.PlainText("<p>A &amp; B</p>\n with  spaces"));
        var now = DateTimeOffset.Parse("2026-09-14T12:00:00Z");
        Assert.Equal("24m", StoryPresentation.DateLabel(now.AddMinutes(-24), now));
        Assert.Equal("Now", StoryPresentation.DateLabel(now.AddMinutes(1), now));
        Assert.Equal(1, StoryPresentation.ReadingMinutes("<p>Short article.</p>"));
        Assert.Equal(2, StoryPresentation.ReadingMinutes(string.Join(" ", Enumerable.Repeat("word", 221))));
    }

    [Fact]
    public void ReaderOnlyEmbedsExplicitLocalBitmapDataAndHasOfflineContentPolicy()
    {
        var unsafeHtml = ReaderHtmlBuilder.Build(CreateStory(), new(), "https://tracker.test/pixel", "<Publisher>");
        Assert.DoesNotContain("tracker.test", unsafeHtml);
        Assert.Contains("&lt;Publisher&gt;", unsafeHtml);
        Assert.Contains("default-src 'none'", unsafeHtml);
        Assert.Contains("forced-colors: active", unsafeHtml);
        var html = ReaderHtmlBuilder.Build(CreateStory(), new(), "data:image/png;base64,AABB");
        Assert.Contains("<figure class=\"cover\">", html);
        Assert.Contains("1 min read", html);
        Assert.DoesNotContain("<figure", ReaderHtmlBuilder.Build(CreateStory(), new(), "data:image/svg+xml;base64,AABB"));
    }

    [Fact]
    public void ReaderCssNumbersDoNotChangeWithDecimalCommaLocale()
    {
        var previous = System.Globalization.CultureInfo.CurrentCulture;
        try
        {
            System.Globalization.CultureInfo.CurrentCulture = new("fr-FR");
            var html = ReaderHtmlBuilder.Build(CreateStory(), new ReaderSettings(TextSize: 18.5, ReadingWidth: 700.5));
            Assert.Contains("font: 18.5px", html);
            Assert.Contains("max-width: 700.5px", html);
        }
        finally { System.Globalization.CultureInfo.CurrentCulture = previous; }
    }

    [Fact]
    public void SettingsAreClampedPersistedAndRebuildReader()
    {
        var subject = CreateSubject();

        subject.UpdateSettings(new ReaderSettings(
            AppTheme.Light,
            TextSize: 99,
            ReadingWidth: 10,
            RetentionDays: 1));

        Assert.Equal(30, subject.Settings.TextSize);
        Assert.Equal(520, subject.Settings.ReadingWidth);
        Assert.Equal(7, subject.Settings.RetentionDays);
        Assert.Equal(subject.Settings, _settings.Saved);
    }

    [Fact]
    public async Task SignOutClearsSessionAndAllLocalAccountData()
    {
        var subject = CreateSubject();

        await subject.SignOutAsync();

        _session.Verify(service => service.SignOutAsync(It.IsAny<CancellationToken>()));
        _repository.Verify(repository =>
            repository.ClearAccountDataAsync(It.IsAny<CancellationToken>()));
        Assert.True(subject.IsLoginScreen);
        Assert.Empty(subject.Stories);
    }

    private AppViewModel CreateSubject() =>
        new(_repository.Object, _session.Object, _sync.Object, _settings);

    private void SetupStoryDetails(Story story) =>
        _repository.Setup(repository => repository.QueryStoriesAsync(
                It.Is<ContentQuery>(query =>
                    query.StoryHash == story.Hash && query.IncludeContent && query.Limit == 1),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([story]);

    private static Story CreateStory() =>
        new(
            "1:story",
            1,
            null,
            null,
            "A story",
            "Author",
            "https://example.test/story",
            "<p>Body</p>",
            "Body",
            null,
            DateTimeOffset.Parse("2026-09-14T12:00:00Z"),
            false,
            false);

    private static SyncResult SuccessfulSync() =>
        new(
            SyncOutcome.Succeeded,
            new SyncState("42", SyncStage.Completed, 0, 1, 1, 1),
            DateTimeOffset.Parse("2026-09-14T12:00:00Z"),
            DateTimeOffset.Parse("2026-09-14T12:00:01Z"));

    private sealed class TestSettingsStore : ISettingsStore
    {
        public ReaderSettings Saved { get; private set; } = new();

        public ReaderSettings Load() => Saved;

        public void Save(ReaderSettings settings) => Saved = settings;
    }
}
