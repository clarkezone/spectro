using System.Net;
using Moq;
using NewsBlurSharp;
using Spectro.Domain;
using Spectro.Infrastructure;
using Spectro.Presentation;
using Spectro.Sync;
using Spectro.WinUI;
using Xunit;

namespace Spectro.ViewModels.Tests;

public sealed class AccountLifecycleTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "Spectro.AccountLifecycle.Tests", Guid.NewGuid().ToString("N"));
    private string DatabasePath => Path.Combine(_directory, "spectro.db");
    private readonly List<PendingStoryMutation> _uploads = [];

    [Fact]
    public async Task DifferentAccountCannotSeeOrUploadPreviousAccountsContent()
    {
        var repository = await SeedAsync();
        await repository.SetStorySavedAsync("1:private", true);
        var subject = Subject(repository, new SessionResult(false, FailureKind: SessionFailureKind.Authentication), Verified("84", "new-session"));
        await subject.InitializeAsync();
        subject.Username = "fixture-reader";
        subject.Password = "synthetic-password";

        await subject.LoginAsync();

        Assert.True(subject.IsReaderScreen);
        Assert.Empty(subject.Stories);
        Assert.Empty(_uploads);
        Assert.Empty(await repository.GetPendingMutationsAsync(10));
        Assert.Equal("84", (await repository.GetCheckpointAsync(AppViewModel.LibraryAccountCheckpointName))?.Value);
        var backup = Assert.Single(Directory.GetFiles(_directory, "spectro.db.failed-*"));
        var previous = new SqliteContentRepository(new SqliteConnectionFactory(backup));
        Assert.Equal("1:private", Assert.Single(await previous.QueryStoriesAsync(new ContentQuery(StoryFilter.All))).Hash);
        Assert.Single(await previous.GetPendingMutationsAsync(10));
        Assert.Equal(backup, subject.LibraryBackupPath);

        // Explicit sign-out removes preserved account content as well as the current library.
        await subject.SignOutAsync();
        Assert.Empty(Directory.GetFiles(_directory, "spectro.db.failed-*"));
    }

    [Fact]
    public async Task SameAccountReauthenticationPreservesPendingIntentAndUpdatesCredentialBinding()
    {
        var repository = await SeedAsync();
        await repository.SetStorySavedAsync("1:private", true);
        var subject = Subject(repository, new SessionResult(false, FailureKind: SessionFailureKind.Authentication), Verified("42", "replacement-session"));
        await subject.InitializeAsync();
        subject.Username = "fixture-reader";
        subject.Password = "synthetic-password";

        await subject.LoginAsync();

        Assert.Single(subject.Stories);
        Assert.Single(_uploads);
        Assert.Single(await repository.GetPendingMutationsAsync(10));
        Assert.Equal("replacement-session", (await repository.GetCheckpointAsync(AppViewModel.SessionFingerprintCheckpointName))?.Value);
        Assert.Empty(Directory.GetFiles(_directory, "spectro.db.failed-*"));
    }

    [Theory]
    [InlineData("saved-session", true)]
    [InlineData("another-session", false)]
    public async Task OfflineRestoreOnlyExposesLibraryBoundToThatSavedCredential(string fingerprint, bool expectedReader)
    {
        var repository = await SeedAsync();
        var subject = Subject(repository, new SessionResult(true, "newsblur", SessionFailureKind.Offline)
            { SessionFingerprint = fingerprint });

        await subject.InitializeAsync();

        Assert.Equal(expectedReader, subject.IsReaderScreen);
        Assert.Equal(expectedReader ? 1 : 0, subject.Stories.Count);
        Assert.Equal(!expectedReader, subject.IsStartupErrorScreen);
        Assert.Single(await repository.QueryStoriesAsync(new ContentQuery(StoryFilter.All)));
        Assert.Empty(Directory.GetFiles(_directory, "spectro.db.failed-*"));
    }

    [Fact]
    public async Task LegacyCacheWithoutOwnershipIsPreservedInsteadOfAssignedToNewAccount()
    {
        var repository = await SeedAsync(owned: false);
        await repository.SetStorySavedAsync("1:private", true);
        var subject = Subject(repository, Verified("42", "saved-session"));

        await subject.InitializeAsync();

        Assert.Empty(subject.Stories);
        Assert.Empty(_uploads);
        Assert.Single(Directory.GetFiles(_directory, "spectro.db.failed-*"));
    }

    [Fact]
    public async Task WebViewCleanupFailureStillClearsDatabaseAndBlocksLoginUntilRetried()
    {
        var repository = await SeedAsync();
        var cookie = new CookieStore();
        var cleaner = new Cleaner { Fail = true };
        var service = new NewsBlurSessionService(new NewsBlurClient(new FixtureHandler()), cookie, cleaner);
        var subject = Subject(repository, service);
        await subject.InitializeAsync();

        await Assert.ThrowsAsync<AggregateException>(() => subject.SignOutAsync());

        Assert.Null(cookie.Value);
        Assert.True(subject.IsLoginScreen);
        Assert.Empty(subject.Stories);
        Assert.Empty(await repository.QueryStoriesAsync(new ContentQuery(StoryFilter.All)));
        Assert.True(subject.NeedsSignOutCleanup);
        Assert.False(subject.CanInteract);
        Assert.NotNull(await repository.GetCheckpointAsync(AppViewModel.PendingSignOutCheckpointName));
        var restarted = Subject(repository, service);
        await restarted.InitializeAsync();
        Assert.True(restarted.NeedsSignOutCleanup);
        Assert.True(restarted.IsLoginScreen);

        cleaner.Fail = false;
        await subject.SignOutAsync();

        Assert.False(subject.NeedsSignOutCleanup);
        Assert.True(subject.CanInteract);
        Assert.Equal("complete", (await repository.GetCheckpointAsync(AppViewModel.PendingSignOutCheckpointName))?.Value);
    }

    [Fact]
    public async Task CredentialRemovalFailureDoesNotSkipBrowserOrDatabaseCleanup()
    {
        var repository = await SeedAsync();
        var cookie = new CookieStore { FailClear = true };
        var cleaner = new Cleaner();
        var subject = Subject(repository, new NewsBlurSessionService(new NewsBlurClient(new FixtureHandler()), cookie, cleaner));
        await subject.InitializeAsync();

        await Assert.ThrowsAsync<AggregateException>(() => subject.SignOutAsync());

        Assert.Equal(1, cleaner.Clears);
        Assert.Empty(await repository.QueryStoriesAsync(new ContentQuery(StoryFilter.All)));
        Assert.True(subject.IsLoginScreen);
        Assert.False(subject.CanInteract);
    }

    [Fact]
    public async Task CorruptStartupHasVisibleErrorAndExplicitRecoveryPreservesOriginalDatabase()
    {
        Directory.CreateDirectory(_directory);
        var corrupt = new byte[] { 1, 2, 3, 4, 5 };
        await File.WriteAllBytesAsync(DatabasePath, corrupt);
        var repository = new SqliteContentRepository(new SqliteConnectionFactory(DatabasePath));
        var subject = Subject(repository, new SessionResult(false));

        await subject.InitializeAsync();

        Assert.True(subject.IsStartupErrorScreen);
        Assert.False(subject.IsLoadingScreen);
        Assert.False(subject.IsBusy);
        Assert.Equal(corrupt, await File.ReadAllBytesAsync(DatabasePath));

        await subject.RecoverDatabaseAsync();

        Assert.True(subject.IsLoginScreen);
        var backup = Assert.Single(Directory.GetFiles(_directory, "spectro.db.failed-*"));
        Assert.Equal(corrupt, await File.ReadAllBytesAsync(backup));
        await repository.InitializeAsync();
    }

    [Fact]
    public async Task RetryOpeningDatabaseDoesNotResetOrDiscardIt()
    {
        var repository = new Mock<IContentRepository>();
        repository.SetupSequence(x => x.InitializeAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new IOException("Fixture temporary failure")).Returns(Task.CompletedTask);
        var session = new Mock<ISessionService>();
        session.Setup(x => x.RestoreAsync(It.IsAny<CancellationToken>())).ReturnsAsync(new SessionResult(false));
        var subject = new AppViewModel(repository.Object, session.Object, Mock.Of<ISynchronizationService>(), new SettingsStore());
        await subject.InitializeAsync();
        Assert.True(subject.IsStartupErrorScreen);

        await subject.InitializeAsync();

        Assert.True(subject.IsLoginScreen);
        repository.Verify(x => x.ResetDatabaseAsync(It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(x => x.ClearAccountDataAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task SignOutAwaitsArtifactCleanupAndPersistsFailureForRetry()
    {
        var repository = await SeedAsync();
        var session = new Mock<ISessionService>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var subject = new AppViewModel(repository, session.Object, Mock.Of<ISynchronizationService>(), new SettingsStore(), async () =>
        {
            started.TrySetResult();
            await finish.Task;
            throw new IOException("Fixture image cleanup failure");
        });
        var signingOut = subject.SignOutAsync();
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.True(subject.IsBusy);
        Assert.False(subject.CanInteract);
        Assert.False(signingOut.IsCompleted);
        Assert.Empty(await repository.QueryStoriesAsync(new ContentQuery(StoryFilter.All)));

        // A process interruption here must still prevent the saved credential from reopening the account.
        Assert.Equal("pending", (await repository.GetCheckpointAsync(AppViewModel.PendingSignOutCheckpointName))?.Value);

        finish.TrySetResult();
        await Assert.ThrowsAsync<AggregateException>(() => signingOut);

        Assert.True(subject.IsLoginScreen);
        Assert.True(subject.NeedsSignOutCleanup);
        Assert.NotNull(await repository.GetCheckpointAsync(AppViewModel.PendingSignOutCheckpointName));
    }

    [Fact]
    public async Task SignOutDrainsEarlierLocalMutationBeforeClearingAccountData()
    {
        var repository = new Mock<IContentRepository>();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var finish = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        repository.Setup(x => x.SetStorySavedAsync("1:old", true, It.IsAny<CancellationToken>()))
            .Returns(async () => { started.TrySetResult(); await finish.Task; });
        var subject = new AppViewModel(repository.Object, Mock.Of<ISessionService>(), Mock.Of<ISynchronizationService>(), new SettingsStore());
        var story = new Story("1:old", 1, null, null, "Fixture", null, null, "Body", "Preview", null, DateTimeOffset.UtcNow, true, false);
        var mutation = subject.ToggleSavedAsync(story);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10));

        var signingOut = subject.SignOutAsync();

        Assert.False(signingOut.IsCompleted);
        repository.Verify(x => x.ClearAccountDataAsync(It.IsAny<CancellationToken>()), Times.Never);
        finish.TrySetResult();
        await Task.WhenAll(mutation, signingOut).WaitAsync(TimeSpan.FromSeconds(10));
        repository.Verify(x => x.ClearAccountDataAsync(It.IsAny<CancellationToken>()), Times.Once);
        Assert.True(subject.IsLoginScreen);
    }

    private AppViewModel Subject(SqliteContentRepository repository, SessionResult restore, SessionResult? login = null)
    {
        var session = new Mock<ISessionService>();
        session.Setup(x => x.RestoreAsync(It.IsAny<CancellationToken>())).ReturnsAsync(restore);
        session.Setup(x => x.LoginAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(login ?? new SessionResult(false));
        return Subject(repository, session.Object);
    }

    private AppViewModel Subject(SqliteContentRepository repository, ISessionService session)
    {
        var remote = new Mock<ISyncRemoteService>();
        remote.Setup(x => x.UploadMutationAsync(It.IsAny<PendingStoryMutation>(), It.IsAny<CancellationToken>()))
            .Callback<PendingStoryMutation, CancellationToken>((mutation, _) => _uploads.Add(mutation)).Returns(Task.CompletedTask);
        remote.Setup(x => x.GetFeedCatalogAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new SyncRemoteException("Fixture interruption after upload", SyncRemoteFailureKind.Offline));
        return new AppViewModel(repository, session, new SyncAdapter(new OfflineFirstSynchronizer(repository, remote.Object,
            new SyncOptions { MinimumRequestInterval = TimeSpan.Zero, MinimumSavedRequestInterval = TimeSpan.Zero })), new SettingsStore());
    }

    private static SessionResult Verified(string id, string fingerprint) =>
        new(true, id) { SessionFingerprint = fingerprint };

    private async Task<SqliteContentRepository> SeedAsync(bool owned = true)
    {
        var repository = new SqliteContentRepository(new SqliteConnectionFactory(DatabasePath));
        await repository.InitializeAsync();
        await repository.UpsertFeedAsync(new Feed(1, "Fixture", "https://fixture.invalid/", null, 1, null, true));
        await repository.UpsertStoryAsync(new Story("1:private", 1, null, null, "Private fixture story", null, null,
            "Private fixture body", "Private fixture summary", null, DateTimeOffset.UtcNow, false, false));
        if (owned)
        {
            await repository.SetCheckpointAsync(new SyncCheckpoint(AppViewModel.LibraryAccountCheckpointName, "42", DateTimeOffset.UtcNow));
            await repository.SetCheckpointAsync(new SyncCheckpoint(AppViewModel.SessionFingerprintCheckpointName, "saved-session", DateTimeOffset.UtcNow));
        }
        return repository;
    }

    public void Dispose()
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private sealed class SyncAdapter(OfflineFirstSynchronizer synchronizer) : ISynchronizationService
    {
        public Task<SyncResult> SynchronizeAsync(string accountId, IProgress<SyncState>? progress, CancellationToken token) =>
            synchronizer.SynchronizeAsync(new SyncRequest(accountId), progress, token);
    }

    private sealed class SettingsStore : ISettingsStore
    {
        public ReaderSettings Load() => new();
        public void Save(ReaderSettings settings) { }
    }

    private sealed class CookieStore : ISessionCookieStore
    {
        public string? Value = "synthetic-session-cookie";
        public bool FailClear;
        public string? Read() => Value;
        public void Write(string value) => Value = value;
        public void Clear() { if (FailClear) throw new IOException("Fixture vault failure"); Value = null; }
    }

    private sealed class Cleaner : ISessionDataCleaner
    {
        public bool Fail;
        public int Clears;
        public Task ClearAsync()
        {
            Clears++;
            return Fail ? Task.FromException(new IOException("Fixture WebView failure")) : Task.CompletedTask;
        }
    }

    private sealed class FixtureHandler : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token) =>
            Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("{\"authenticated\":true,\"user_id\":42}") });
    }
}
