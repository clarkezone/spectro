using System.Collections.Concurrent;
using System.Globalization;
using System.Text.Json;
using Spectro.Domain;

namespace Spectro.Sync;

public sealed partial class OfflineFirstSynchronizer
{
    private const string SavedVersionsCheckpoint = "saved-story-versions";
    private const string NextRequestCheckpoint = "next-sync-request";
    private const string NextSavedRequestCheckpoint = "next-saved-request";

    private async Task<DateTimeOffset> FetchInventoryContentAsync(
        RemoteFeedCatalog catalog, SyncRun run, CancellationToken cancellationToken)
    {
        var cached = (await _repository.GetCachedStoryIndexAsync(cancellationToken).ConfigureAwait(false))
            .ToDictionary(static story => story.Hash, StringComparer.Ordinal);
        var feedIds = catalog.Feeds.Where(static feed => feed.IsActive).Select(static feed => feed.Id).ToArray();
        var all = feedIds.Length == 0
            ? new RemoteStoryInventory(new Dictionary<int, IReadOnlyList<RemoteStoryHash>>())
            : await ExecuteRemoteAsync(
                token => _remoteService.GetStoryHashInventoryAsync(false, feedIds, token),
                run, cancellationToken).ConfigureAwait(false);
        var unreadFeeds = new Dictionary<int, IReadOnlyList<RemoteStoryHash>>();
        if (feedIds.Length > 0)
        {
            var unread = await ExecuteRemoteAsync(
                token => _remoteService.GetStoryHashInventoryAsync(true, feedIds, token),
                run, cancellationToken).ConfigureAwait(false);
            foreach (var pair in unread.Feeds) unreadFeeds.Add(pair.Key, pair.Value);

            // NewsBlur omits zero-count/hidden-only feeds when other feeds have visible unreads.
            var missing = feedIds.Except(unreadFeeds.Keys).ToArray();
            if (missing.Length > 0)
            {
                var remaining = await ExecuteRemoteAsync(
                    token => _remoteService.GetStoryHashInventoryAsync(true, missing, token),
                    run, cancellationToken).ConfigureAwait(false);
                foreach (var pair in remaining.Feeds) unreadFeeds[pair.Key] = pair.Value;
            }
            foreach (var feedId in feedIds.Except(unreadFeeds.Keys))
            {
                var remaining = await ExecuteRemoteAsync(
                    token => _remoteService.GetStoryHashInventoryAsync(true, [feedId], token),
                    run, cancellationToken).ConfigureAwait(false);
                if (!remaining.Feeds.TryGetValue(feedId, out var hashes))
                    throw InventoryFailure("NewsBlur omitted the requested feed's unread inventory.");
                unreadFeeds.Add(feedId, hashes);
            }
        }

        var saved = await ExecuteRemoteAsync(
            token => _remoteService.GetSavedStoryHashesAsync(token), run, cancellationToken).ConfigureAwait(false);
        var savedHashes = saved.Keys.ToHashSet(StringComparer.Ordinal);
        var unreadHashes = unreadFeeds.Values.SelectMany(static entries => entries)
            .Select(static entry => entry.Hash).ToHashSet(StringComparer.Ordinal);
        var readHashes = new HashSet<string>(StringComparer.Ordinal);
        var candidates = new Dictionary<int, RemoteStoryHash[]>();
        foreach (var feedId in feedIds)
        {
            if (!all.Feeds.TryGetValue(feedId, out var entries))
                throw InventoryFailure("NewsBlur omitted the requested feed's recent story inventory.");
            candidates.Add(feedId, entries.OrderByDescending(static entry => entry.Timestamp).ToArray());
            var unread = unreadFeeds[feedId];
            var oldestUnread = unread.Count == 0 ? double.PositiveInfinity : unread.Min(static entry => entry.Timestamp);
            foreach (var entry in entries)
            {
                if (!unreadHashes.Contains(entry.Hash)
                    && (unread.Count < 500 || entry.Timestamp > oldestUnread))
                    readHashes.Add(entry.Hash);
            }
        }

        run.SetStage(SyncStage.FetchStories);
        var available = new ConcurrentDictionary<string, byte>(
            cached.Keys.Select(static hash => new KeyValuePair<string, byte>(hash, 0)), StringComparer.Ordinal);
        var unavailable = new HashSet<string>(StringComparer.Ordinal);
        var selected = new Dictionary<int, RemoteStoryHash[]>();
        Dictionary<string, double>? savedVersions = null;
        while (true)
        {
            selected = candidates.ToDictionary(
                static pair => pair.Key,
                pair => pair.Value.Where(entry => !unavailable.Contains(entry.Hash))
                    .Take(_options.RecentStoriesPerFeed).ToArray());
            var desired = selected.Values.SelectMany(static entries => entries).ToArray();
            var ambiguousFeeds = desired.Where(entry =>
                !unreadHashes.Contains(entry.Hash) && !readHashes.Contains(entry.Hash))
                .Select(static entry => entry.FeedId).Distinct().ToArray();
            foreach (var feedId in ambiguousFeeds)
            {
                var unresolved = selected[feedId].Where(entry =>
                    !unreadHashes.Contains(entry.Hash) && !readHashes.Contains(entry.Hash))
                    .Select(static entry => entry.Hash).ToHashSet(StringComparer.Ordinal);
                for (var page = 1; page <= _options.MaximumStoryPages && unresolved.Count > 0; page++)
                {
                    var result = await ExecuteRemoteAsync(
                        token => _remoteService.GetFeedStoriesAsync(feedId, page, token),
                        run, cancellationToken).ConfigureAwait(false);
                    foreach (var story in result.Stories.Where(story => unresolved.Remove(story.Hash)))
                    {
                        if (story.IsRead) readHashes.Add(story.Hash);
                        else unreadHashes.Add(story.Hash);
                    }
                    if (result.IsLastPage) break;
                }
                if (unresolved.Count > 0)
                    throw InventoryFailure("NewsBlur's capped unread inventory could not establish recent article state.");
            }

            await ReconcileInventoryAsync().ConfigureAwait(false);
            var missing = desired.Where(entry => !available.ContainsKey(entry.Hash))
                .Select(static entry => entry.Hash).Distinct(StringComparer.Ordinal).ToArray();
            if (missing.Length == 0) break;
            await DownloadBatchesAsync(missing, isSaved: false).ConfigureAwait(false);
            foreach (var hash in missing.Where(hash => !available.ContainsKey(hash))) unavailable.Add(hash);
        }

        var versionsCheckpoint = await _repository.GetCheckpointAsync(
            SavedVersionsCheckpoint, cancellationToken).ConfigureAwait(false);
        savedVersions = versionsCheckpoint is null
            ? new Dictionary<string, double>(StringComparer.Ordinal)
            : JsonSerializer.Deserialize(versionsCheckpoint.Value, SyncJsonContext.Default.DictionaryStringDouble)
                ?? throw new InvalidDataException("The saved article version checkpoint is invalid.");
        var missingSaved = saved.Where(pair => !available.ContainsKey(pair.Key)
                || !savedVersions.TryGetValue(pair.Key, out var version) || version != pair.Value)
            .Select(static pair => pair.Key).ToArray();
        await DownloadBatchesAsync(missingSaved, isSaved: true).ConfigureAwait(false);
        if (missingSaved.Any(hash => !run.DownloadedSavedHashes.Contains(hash)))
            throw InventoryFailure("NewsBlur omitted a requested saved article; its previous local copy was retained.");
        await ReconcileInventoryAsync().ConfigureAwait(false);
        await _repository.SetCheckpointAsync(new SyncCheckpoint(
            SavedVersionsCheckpoint,
            JsonSerializer.Serialize(new Dictionary<string, double>(saved), SyncJsonContext.Default.DictionaryStringDouble),
            _timeProvider.GetUtcNow()), cancellationToken).ConfigureAwait(false);
        run.Update(state => state with { CompletedFeedCount = feedIds.Length });

        var retentionCutoff = _timeProvider.GetUtcNow() - _options.RetentionAge;
        foreach (var entry in selected.Values.SelectMany(static entries => entries))
        {
            var timestamp = DateTimeOffset.UnixEpoch.AddSeconds(entry.Timestamp);
            if (timestamp < retentionCutoff) retentionCutoff = timestamp;
        }
        return retentionCutoff;

        async Task ReconcileInventoryAsync()
        {
            await _repository.ReconcileRemoteContentAsync(
                new RemoteContentBatch([], unreadHashes, false, savedHashes, true) { ReadStoryHashes = readHashes },
                cancellationToken).ConfigureAwait(false);
            run.ContentChanged();
        }

        async Task DownloadBatchesAsync(string[] hashes, bool isSaved)
        {
            await Parallel.ForEachAsync(hashes.Chunk(100), new ParallelOptions
            {
                MaxDegreeOfParallelism = _options.MaximumConcurrentRequests,
                CancellationToken = cancellationToken
            }, async (batch, token) =>
            {
                var returned = await DownloadAsync(batch, isSaved, token).ConfigureAwait(false);
                // Clustering can hide requested siblings. A singleton cannot hide behind another requested hash.
                foreach (var hash in batch.Where(hash => !returned.Contains(hash)))
                    await DownloadAsync([hash], isSaved, token).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        async Task<HashSet<string>> DownloadAsync(string[] hashes, bool isSaved, CancellationToken token)
        {
            var result = await ExecuteRemoteAsync(
                ct => _remoteService.GetStoriesByHashesAsync(hashes, isSaved, ct),
                run, token, isSaved ? "Saved stories" : "Recent articles", savedRequest: isSaved).ConfigureAwait(false);
            var requested = hashes.ToHashSet(StringComparer.Ordinal);
            var stories = result.Stories.Where(story => requested.Contains(story.Hash)).Select(story => story with
            {
                IsRead = unreadHashes.Contains(story.Hash) ? false : readHashes.Contains(story.Hash)
                    || (isSaved ? story.IsRead : cached.TryGetValue(story.Hash, out var previous) && previous.IsRead),
                IsSaved = savedHashes.Contains(story.Hash)
            }).ToArray();
            await run.PersistenceGate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                if (isSaved)
                {
                    await _repository.ReconcileRemoteContentAsync(
                        new RemoteContentBatch(
                            stories,
                            stories.Where(static story => !story.IsRead).Select(static story => story.Hash)
                                .ToHashSet(StringComparer.Ordinal),
                            false, savedHashes, false)
                        {
                            ReadStoryHashes = stories.Where(static story => story.IsRead)
                                .Select(static story => story.Hash).ToHashSet(StringComparer.Ordinal)
                        }, token).ConfigureAwait(false);
                }
                else
                {
                    await _repository.CacheRemoteStoriesAsync(stories, token).ConfigureAwait(false);
                }
                foreach (var story in stories)
                {
                    available.TryAdd(story.Hash, 0);
                    run.DownloadedHashes.Add(story.Hash);
                    if (isSaved) run.DownloadedSavedHashes.Add(story.Hash);
                }
                if (isSaved)
                {
                    var versions = savedVersions
                        ?? throw new InvalidOperationException("Saved article versions were not loaded.");
                    foreach (var story in stories) versions[story.Hash] = saved[story.Hash];
                    await _repository.SetCheckpointAsync(new SyncCheckpoint(
                        SavedVersionsCheckpoint,
                        JsonSerializer.Serialize(versions, SyncJsonContext.Default.DictionaryStringDouble),
                        _timeProvider.GetUtcNow()), token).ConfigureAwait(false);
                }
                run.Update(state => state with
                {
                    StoryCount = run.DownloadedHashes.Count,
                    CompletedFeedCount = selected.Count(pair =>
                        pair.Value.All(entry => available.ContainsKey(entry.Hash))),
                    DownloadedPageCount = state.DownloadedPageCount + 1,
                    LocalRevision = state.LocalRevision + (stories.Length > 0 ? 1 : 0)
                });
            }
            finally { run.PersistenceGate.Release(); }
            return stories.Select(static story => story.Hash).ToHashSet(StringComparer.Ordinal);
        }
    }

    private async Task PaceRequestAsync(SyncRun run, bool saved, CancellationToken cancellationToken)
    {
        await run.PacingGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (run.Backoff is { } pause) throw BackoffException(pause);
            var now = _timeProvider.GetUtcNow();
            var next = await ReadDeadlineAsync(NextRequestCheckpoint).ConfigureAwait(false);
            if (saved)
            {
                var nextSaved = await ReadDeadlineAsync(NextSavedRequestCheckpoint).ConfigureAwait(false);
                if (nextSaved > next) next = nextSaved;
            }
            if (next > now) await _delay.DelayAsync(next - now, cancellationToken).ConfigureAwait(false);
            if (run.Backoff is { } delayedPause) throw BackoffException(delayedPause);
            now = _timeProvider.GetUtcNow();
            await SaveDeadlineAsync(NextRequestCheckpoint, now + _options.MinimumRequestInterval).ConfigureAwait(false);
            if (saved)
                await SaveDeadlineAsync(NextSavedRequestCheckpoint, now + _options.MinimumSavedRequestInterval).ConfigureAwait(false);
        }
        finally { run.PacingGate.Release(); }

        async Task<DateTimeOffset> ReadDeadlineAsync(string name)
        {
            var checkpoint = await _repository.GetCheckpointAsync(name, cancellationToken).ConfigureAwait(false);
            return checkpoint is null ? DateTimeOffset.MinValue
                : DateTimeOffset.Parse(checkpoint.Value, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind);
        }

        Task SaveDeadlineAsync(string name, DateTimeOffset deadline) =>
            _repository.SetCheckpointAsync(new SyncCheckpoint(name, deadline.ToString("O"), _timeProvider.GetUtcNow()),
                cancellationToken);
    }

    private static SyncRemoteException InventoryFailure(string message) =>
        new(message, SyncRemoteFailureKind.MalformedData);
}
