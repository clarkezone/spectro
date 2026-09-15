using Microsoft.Data.Sqlite;
using Spectro.Domain;

namespace Spectro.Infrastructure;

public sealed class SqliteContentRepository(
    SqliteConnectionFactory connectionFactory,
    TimeProvider? timeProvider = null) : IContentRepository
{
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;
    private readonly SqliteDatabase _database = new(connectionFactory, timeProvider);

    public Task InitializeAsync(CancellationToken cancellationToken = default) =>
        _database.InitializeAsync(cancellationToken);

    public async Task UpsertFeedAsync(
        Feed feed,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO feed (
                id, title, feed_uri, icon_uri, unread_count, latest_story_utc, is_active)
            VALUES (
                $id, $title, $feedUri, $iconUri, $unreadCount, $latestStoryUtc, $isActive)
            ON CONFLICT(id) DO UPDATE SET
                title = excluded.title,
                feed_uri = excluded.feed_uri,
                icon_uri = excluded.icon_uri,
                unread_count = excluded.unread_count,
                latest_story_utc = excluded.latest_story_utc,
                is_active = excluded.is_active;
            """;
        command.Parameters.AddWithValue("$id", feed.Id);
        command.Parameters.AddWithValue("$title", feed.Title);
        command.Parameters.AddWithValue("$feedUri", feed.FeedUri);
        command.Parameters.AddWithValue("$iconUri", (object?)feed.IconUri ?? DBNull.Value);
        command.Parameters.AddWithValue("$unreadCount", feed.UnreadCount);
        command.Parameters.AddWithValue(
            "$latestStoryUtc",
            feed.LatestStoryAt is null
                ? DBNull.Value
                : feed.LatestStoryAt.Value.ToUnixTimeSeconds());
        command.Parameters.AddWithValue("$isActive", feed.IsActive ? 1 : 0);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpsertStoryAsync(
        Story story,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO story (
                story_hash, feed_id, service_id, guid_hash, title, author, permalink,
                content, summary, image_uri, published_utc, is_read, is_saved)
            VALUES (
                $hash, $feedId, $serviceId, $guidHash, $title, $author, $permalink,
                $content, $summary, $imageUri, $publishedUtc, $isRead, $isSaved)
            ON CONFLICT(story_hash) DO UPDATE SET
                feed_id = excluded.feed_id,
                service_id = excluded.service_id,
                guid_hash = excluded.guid_hash,
                title = excluded.title,
                author = excluded.author,
                permalink = excluded.permalink,
                content = excluded.content,
                summary = excluded.summary,
                image_uri = excluded.image_uri,
                published_utc = excluded.published_utc,
                is_read = CASE
                    WHEN EXISTS (
                        SELECT 1 FROM pending_mutation
                        WHERE story_hash = excluded.story_hash AND kind = 'read')
                    THEN story.is_read
                    ELSE excluded.is_read
                END,
                is_saved = CASE
                    WHEN EXISTS (
                        SELECT 1 FROM pending_mutation
                        WHERE story_hash = excluded.story_hash AND kind = 'saved')
                    THEN story.is_saved
                    ELSE excluded.is_saved
                END;
            """;
        AddStoryParameters(command, story);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task ReplaceFeedCatalogAsync(
        IReadOnlyCollection<Feed> feeds,
        IReadOnlyCollection<Folder> folders,
        IReadOnlyCollection<FolderFeed> folderFeeds,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(feeds);
        ArgumentNullException.ThrowIfNull(folders);
        ArgumentNullException.ThrowIfNull(folderFeeds);

        await using var connection = await connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        await ExecuteAsync(
            connection,
            (SqliteTransaction)transaction,
            "UPDATE feed SET is_active = 0;",
            cancellationToken).ConfigureAwait(false);
        await ExecuteAsync(
            connection,
            (SqliteTransaction)transaction,
            "DELETE FROM folder;",
            cancellationToken).ConfigureAwait(false);

        foreach (var feed in feeds)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText =
                """
                INSERT INTO feed (
                    id, title, feed_uri, icon_uri, unread_count, latest_story_utc, is_active)
                VALUES (
                    $id, $title, $feedUri, $iconUri, $unreadCount, $latestStoryUtc, $isActive)
                ON CONFLICT(id) DO UPDATE SET
                    title = excluded.title,
                    feed_uri = excluded.feed_uri,
                    icon_uri = excluded.icon_uri,
                    unread_count = excluded.unread_count,
                    latest_story_utc = excluded.latest_story_utc,
                    is_active = excluded.is_active;
                """;
            AddFeedParameters(command, feed);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var folder in folders)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText =
                """
                INSERT INTO folder (id, title, sort_order)
                VALUES ($id, $title, $sortOrder);
                """;
            command.Parameters.AddWithValue("$id", folder.Id);
            command.Parameters.AddWithValue("$title", folder.Title);
            command.Parameters.AddWithValue("$sortOrder", folder.SortOrder);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        foreach (var folderFeed in folderFeeds)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText =
                """
                INSERT INTO folder_feed (folder_id, feed_id, sort_order)
                VALUES ($folderId, $feedId, $sortOrder);
                """;
            command.Parameters.AddWithValue("$folderId", folderFeed.FolderId);
            command.Parameters.AddWithValue("$feedId", folderFeed.FeedId);
            command.Parameters.AddWithValue("$sortOrder", folderFeed.SortOrder);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<Feed>> GetFeedsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, title, feed_uri, icon_uri, unread_count, latest_story_utc, is_active
            FROM feed
            WHERE is_active = 1
            ORDER BY title COLLATE NOCASE, id;
            """;

        var feeds = new List<Feed>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            feeds.Add(new Feed(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetInt32(4),
                reader.IsDBNull(5)
                    ? null
                    : DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(5)),
                reader.GetInt32(6) == 1));
        }

        return feeds;
    }

    public async Task<IReadOnlyList<FeedFolder>> GetFeedFoldersAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT f.id, f.title, f.sort_order,
                   d.id, d.title, d.feed_uri, d.icon_uri, d.unread_count,
                   d.latest_story_utc, d.is_active
            FROM folder f
            LEFT JOIN folder_feed ff ON ff.folder_id = f.id
            LEFT JOIN feed d ON d.id = ff.feed_id AND d.is_active = 1
            ORDER BY f.sort_order, f.title COLLATE NOCASE, ff.sort_order, d.title COLLATE NOCASE;
            """;

        var folders = new List<(Folder Folder, List<Feed> Feeds)>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            var folderId = reader.GetString(0);
            var entry = folders.LastOrDefault(item =>
                string.Equals(item.Folder.Id, folderId, StringComparison.Ordinal));
            if (entry.Folder is null)
            {
                entry = (new Folder(folderId, reader.GetString(1), reader.GetInt32(2)), []);
                folders.Add(entry);
            }

            if (!reader.IsDBNull(3))
            {
                entry.Feeds.Add(new Feed(
                    reader.GetInt32(3),
                    reader.GetString(4),
                    reader.GetString(5),
                    reader.IsDBNull(6) ? null : reader.GetString(6),
                    reader.GetInt32(7),
                    reader.IsDBNull(8)
                        ? null
                        : DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(8)),
                    reader.GetInt32(9) == 1));
            }
        }

        return folders
            .Select(static item => new FeedFolder(item.Folder, item.Feeds))
            .ToArray();
    }

    public async Task<IReadOnlyList<Story>> GetStoriesAsync(
        int feedId,
        CancellationToken cancellationToken = default)
        => await QueryStoriesAsync(
            new ContentQuery(StoryFilter.All, FeedId: feedId),
            cancellationToken).ConfigureAwait(false);

    public async Task<IReadOnlyList<Story>> QueryStoriesAsync(
        ContentQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        ArgumentOutOfRangeException.ThrowIfLessThan(query.Limit, 1);

        await using var connection = await connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT story_hash, feed_id, service_id, guid_hash, title, author, permalink,
                   content, summary, image_uri, published_utc, is_read, is_saved
            FROM story
            WHERE ($feedId IS NULL OR feed_id = $feedId)
              AND ($folderId IS NULL OR EXISTS (
                    SELECT 1 FROM folder_feed
                    WHERE folder_feed.folder_id = $folderId
                      AND folder_feed.feed_id = story.feed_id))
              AND ($filter = 0
                   OR ($filter = 1 AND is_read = 0)
                   OR ($filter = 2 AND is_saved = 1)
                   OR ($filter = 3 AND is_read = 1))
            ORDER BY published_utc DESC, story_hash
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$feedId", (object?)query.FeedId ?? DBNull.Value);
        command.Parameters.AddWithValue("$folderId", (object?)query.FolderId ?? DBNull.Value);
        command.Parameters.AddWithValue("$filter", (int)query.Filter);
        command.Parameters.AddWithValue("$limit", query.Limit);

        var stories = new List<Story>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            stories.Add(new Story(
                reader.GetString(0),
                reader.GetInt32(1),
                reader.IsDBNull(2) ? null : reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetString(4),
                reader.IsDBNull(5) ? null : reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.GetString(7),
                reader.GetString(8),
                reader.IsDBNull(9) ? null : reader.GetString(9),
                DateTimeOffset.FromUnixTimeSeconds(reader.GetInt64(10)),
                reader.GetInt32(11) == 1,
                reader.GetInt32(12) == 1));
        }

        return stories;
    }

    public async Task<IReadOnlyDictionary<int, int>> GetLocalUnreadCountsAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT feed_id, COUNT(*) FROM story WHERE is_read = 0 GROUP BY feed_id;";
        var counts = new Dictionary<int, int>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            counts.Add(reader.GetInt32(0), reader.GetInt32(1));
        return counts;
    }

    public Task SetStoryReadAsync(
        string storyHash,
        bool isRead,
        CancellationToken cancellationToken = default) =>
        SetStoryStateAsync(storyHash, "read", "is_read", isRead, cancellationToken);

    public Task SetStorySavedAsync(
        string storyHash,
        bool isSaved,
        CancellationToken cancellationToken = default) =>
        SetStoryStateAsync(storyHash, "saved", "is_saved", isSaved, cancellationToken);

    public async Task<IReadOnlyList<PendingStoryMutation>> GetPendingMutationsAsync(
        int limit,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(limit, 1);

        await using var connection = await connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT id, story_hash, kind, value, created_utc
            FROM pending_mutation
            ORDER BY created_utc, id
            LIMIT $limit;
            """;
        command.Parameters.AddWithValue("$limit", limit);

        var mutations = new List<PendingStoryMutation>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            mutations.Add(new PendingStoryMutation(
                reader.GetInt64(0),
                reader.GetString(1),
                ParseMutationKind(reader.GetString(2)),
                reader.GetInt32(3) == 1,
                DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(4))));
        }

        return mutations;
    }

    public async Task<int> AcknowledgePendingMutationsAsync(
        IReadOnlyCollection<PendingStoryMutation> mutations,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutations);
        if (mutations.Count == 0)
        {
            return 0;
        }

        await using var connection = await connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        var acknowledged = 0;

        foreach (var mutation in mutations)
        {
            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText =
                """
                DELETE FROM pending_mutation
                WHERE id = $id
                  AND story_hash = $hash
                  AND kind = $kind
                  AND value = $value
                  AND created_utc = $createdUtc;
                """;
            command.Parameters.AddWithValue("$id", mutation.Id);
            command.Parameters.AddWithValue("$hash", mutation.StoryHash);
            command.Parameters.AddWithValue("$kind", FormatMutationKind(mutation.Kind));
            command.Parameters.AddWithValue("$value", mutation.Value ? 1 : 0);
            command.Parameters.AddWithValue(
                "$createdUtc",
                mutation.CreatedAt.ToUnixTimeMilliseconds());
            acknowledged += await command.ExecuteNonQueryAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        return acknowledged;
    }

    public async Task RecordUploadedMutationAsync(
        PendingStoryMutation mutation,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(mutation);

        await using var connection = await connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO uploaded_mutation (
                mutation_id, story_hash, kind, value, created_utc, uploaded_utc)
            VALUES (
                $id, $hash, $kind, $value, $createdUtc, $uploadedUtc)
            ON CONFLICT(mutation_id) DO UPDATE SET
                story_hash = excluded.story_hash,
                kind = excluded.kind,
                value = excluded.value,
                created_utc = excluded.created_utc,
                uploaded_utc = excluded.uploaded_utc;
            """;
        command.Parameters.AddWithValue("$id", mutation.Id);
        command.Parameters.AddWithValue("$hash", mutation.StoryHash);
        command.Parameters.AddWithValue("$kind", FormatMutationKind(mutation.Kind));
        command.Parameters.AddWithValue("$value", mutation.Value ? 1 : 0);
        command.Parameters.AddWithValue(
            "$createdUtc",
            mutation.CreatedAt.ToUnixTimeMilliseconds());
        command.Parameters.AddWithValue(
            "$uploadedUtc",
            _timeProvider.GetUtcNow().ToUnixTimeMilliseconds());
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<UploadedStoryMutation>> GetUploadedMutationsAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT mutation_id, story_hash, kind, value, created_utc, uploaded_utc
            FROM uploaded_mutation
            ORDER BY uploaded_utc, mutation_id;
            """;

        var mutations = new List<UploadedStoryMutation>();
        await using var reader = await command.ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            mutations.Add(new UploadedStoryMutation(
                new PendingStoryMutation(
                    reader.GetInt64(0),
                    reader.GetString(1),
                    ParseMutationKind(reader.GetString(2)),
                    reader.GetInt32(3) == 1,
                    DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(4))),
                DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(5))));
        }

        return mutations;
    }

    public async Task ReconcileRemoteContentAsync(
        RemoteContentBatch content,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(content);

        await using var connection = await connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (var story in content.Stories)
        {
            await EnsureFeedExistsAsync(
                connection,
                (SqliteTransaction)transaction,
                story.FeedId,
                cancellationToken).ConfigureAwait(false);

            await using var command = connection.CreateCommand();
            command.Transaction = (SqliteTransaction)transaction;
            command.CommandText = StoryUpsertSql;
            AddStoryParameters(command, story);
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        if (content.UnreadSetIsComplete)
        {
            await ApplyRemoteStateSetAsync(
                connection,
                (SqliteTransaction)transaction,
                "is_read",
                "read",
                content.UnreadStoryHashes,
                valueWhenPresent: false,
                valueWhenMissing: true,
                cancellationToken).ConfigureAwait(false);
        }

        if (content.SavedSetIsComplete)
        {
            await ApplyRemoteStateSetAsync(
                connection,
                (SqliteTransaction)transaction,
                "is_saved",
                "saved",
                content.SavedStoryHashes,
                valueWhenPresent: true,
                valueWhenMissing: false,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await ApplyRemoteStateSetAsync(
                connection,
                (SqliteTransaction)transaction,
                "is_saved",
                "saved",
                content.SavedStoryHashes,
                valueWhenPresent: true,
                valueWhenMissing: null,
                cancellationToken).ConfigureAwait(false);
        }

        await FinalizeUploadedMutationsAsync(
            connection,
            (SqliteTransaction)transaction,
            content,
            cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task SetCheckpointAsync(
        SyncCheckpoint checkpoint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(checkpoint);
        ArgumentException.ThrowIfNullOrWhiteSpace(checkpoint.Name);

        await using var connection = await connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            INSERT INTO sync_checkpoint (name, value, updated_utc)
            VALUES ($name, $value, $updatedUtc)
            ON CONFLICT(name) DO UPDATE SET
                value = excluded.value,
                updated_utc = excluded.updated_utc;
            """;
        command.Parameters.AddWithValue("$name", checkpoint.Name);
        command.Parameters.AddWithValue("$value", checkpoint.Value);
        command.Parameters.AddWithValue(
            "$updatedUtc",
            checkpoint.UpdatedAt.ToUnixTimeMilliseconds());
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<SyncCheckpoint?> GetCheckpointAsync(
        string name,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        await using var connection = await connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            SELECT value, updated_utc
            FROM sync_checkpoint
            WHERE name = $name;
            """;
        command.Parameters.AddWithValue("$name", name);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            return null;
        }

        return new SyncCheckpoint(
            name,
            reader.GetString(0),
            DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(1)));
    }

    public async Task<int> DeleteStoriesOlderThanAsync(
        DateTimeOffset cutoff,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.CommandText =
            """
            DELETE FROM story
            WHERE published_utc < $cutoff
              AND is_read = 1
              AND is_saved = 0
              AND NOT EXISTS (
                  SELECT 1
                  FROM pending_mutation
                  WHERE pending_mutation.story_hash = story.story_hash)
              AND NOT EXISTS (
                  SELECT 1
                  FROM uploaded_mutation
                  WHERE uploaded_mutation.story_hash = story.story_hash);
            """;
        command.Parameters.AddWithValue("$cutoff", cutoff.ToUnixTimeSeconds());
        return await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    public Task<DatabaseResetResult> ResetDatabaseAsync(
        CancellationToken cancellationToken = default) =>
        _database.ResetAsync(cancellationToken);

    public async Task ClearAccountDataAsync(
        CancellationToken cancellationToken = default)
    {
        await using var connection = await connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        await ExecuteAsync(
            connection,
            (SqliteTransaction)transaction,
            """
            DELETE FROM pending_mutation;
            DELETE FROM uploaded_mutation;
            DELETE FROM story;
            DELETE FROM folder_feed;
            DELETE FROM folder;
            DELETE FROM feed;
            DELETE FROM sync_checkpoint;
            DELETE FROM account;
            """,
            cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private async Task SetStoryStateAsync(
        string storyHash,
        string mutationKind,
        string columnName,
        bool value,
        CancellationToken cancellationToken)
    {
        await using var connection = await connectionFactory
            .OpenAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var transaction = await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);

        await using (var storyCommand = connection.CreateCommand())
        {
            storyCommand.Transaction = (SqliteTransaction)transaction;
            storyCommand.CommandText =
                $"UPDATE story SET {columnName} = $value WHERE story_hash = $hash;";
            storyCommand.Parameters.AddWithValue("$hash", storyHash);
            storyCommand.Parameters.AddWithValue("$value", value ? 1 : 0);
            var changed = await storyCommand.ExecuteNonQueryAsync(cancellationToken)
                .ConfigureAwait(false);
            if (changed != 1)
            {
                throw new KeyNotFoundException($"Story '{storyHash}' was not found.");
            }
        }

        await using (var mutationCommand = connection.CreateCommand())
        {
            mutationCommand.Transaction = (SqliteTransaction)transaction;
            mutationCommand.CommandText =
                """
                INSERT INTO pending_mutation (story_hash, kind, value, created_utc)
                VALUES ($hash, $kind, $value, $createdUtc)
                ON CONFLICT(story_hash, kind) DO UPDATE SET
                    value = excluded.value,
                    created_utc = excluded.created_utc;
                """;
            mutationCommand.Parameters.AddWithValue("$hash", storyHash);
            mutationCommand.Parameters.AddWithValue("$kind", mutationKind);
            mutationCommand.Parameters.AddWithValue("$value", value ? 1 : 0);
            mutationCommand.Parameters.AddWithValue(
                "$createdUtc",
                _timeProvider.GetUtcNow().ToUnixTimeMilliseconds());
            await mutationCommand.ExecuteNonQueryAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static void AddStoryParameters(SqliteCommand command, Story story)
    {
        command.Parameters.AddWithValue("$hash", story.Hash);
        command.Parameters.AddWithValue("$feedId", story.FeedId);
        command.Parameters.AddWithValue("$serviceId", (object?)story.ServiceId ?? DBNull.Value);
        command.Parameters.AddWithValue("$guidHash", (object?)story.GuidHash ?? DBNull.Value);
        command.Parameters.AddWithValue("$title", story.Title);
        command.Parameters.AddWithValue("$author", (object?)story.Author ?? DBNull.Value);
        command.Parameters.AddWithValue("$permalink", (object?)story.Permalink ?? DBNull.Value);
        command.Parameters.AddWithValue("$content", story.Content);
        command.Parameters.AddWithValue("$summary", story.Summary);
        command.Parameters.AddWithValue("$imageUri", (object?)story.ImageUri ?? DBNull.Value);
        command.Parameters.AddWithValue("$publishedUtc", story.PublishedAt.ToUnixTimeSeconds());
        command.Parameters.AddWithValue("$isRead", story.IsRead ? 1 : 0);
        command.Parameters.AddWithValue("$isSaved", story.IsSaved ? 1 : 0);
    }

    private static void AddFeedParameters(SqliteCommand command, Feed feed)
    {
        command.Parameters.AddWithValue("$id", feed.Id);
        command.Parameters.AddWithValue("$title", feed.Title);
        command.Parameters.AddWithValue("$feedUri", feed.FeedUri);
        command.Parameters.AddWithValue("$iconUri", (object?)feed.IconUri ?? DBNull.Value);
        command.Parameters.AddWithValue("$unreadCount", feed.UnreadCount);
        command.Parameters.AddWithValue(
            "$latestStoryUtc",
            feed.LatestStoryAt is null
                ? DBNull.Value
                : feed.LatestStoryAt.Value.ToUnixTimeSeconds());
        command.Parameters.AddWithValue("$isActive", feed.IsActive ? 1 : 0);
    }

    private static async Task EnsureFeedExistsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        int feedId,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            """
            INSERT OR IGNORE INTO feed (
                id, title, feed_uri, unread_count, is_active)
            VALUES ($id, $title, '', 0, 0);
            """;
        command.Parameters.AddWithValue("$id", feedId);
        command.Parameters.AddWithValue("$title", $"Feed {feedId}");
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task ApplyRemoteStateSetAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string columnName,
        string mutationKind,
        IReadOnlySet<string> storyHashes,
        bool valueWhenPresent,
        bool? valueWhenMissing,
        CancellationToken cancellationToken)
    {
        var tableName = $"remote_{mutationKind}_state";
        await ExecuteAsync(
            connection,
            transaction,
            $"CREATE TEMP TABLE IF NOT EXISTS {tableName} (story_hash TEXT PRIMARY KEY); DELETE FROM {tableName};",
            cancellationToken).ConfigureAwait(false);

        foreach (var storyHash in storyHashes)
        {
            await using var insert = connection.CreateCommand();
            insert.Transaction = transaction;
            insert.CommandText =
                $"INSERT OR IGNORE INTO {tableName} (story_hash) VALUES ($hash);";
            insert.Parameters.AddWithValue("$hash", storyHash);
            await insert.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText =
            $"""
            UPDATE story
            SET {columnName} = CASE
                WHEN EXISTS (
                    SELECT 1 FROM {tableName}
                    WHERE {tableName}.story_hash = story.story_hash)
                THEN $presentValue
                ELSE $missingValue
            END
            WHERE NOT EXISTS (
                    SELECT 1 FROM pending_mutation
                    WHERE pending_mutation.story_hash = story.story_hash
                      AND pending_mutation.kind = $kind)
              AND NOT EXISTS (
                    SELECT 1 FROM uploaded_mutation
                    WHERE uploaded_mutation.story_hash = story.story_hash
                      AND uploaded_mutation.kind = $kind)
              AND (
                    EXISTS (
                        SELECT 1 FROM {tableName}
                        WHERE {tableName}.story_hash = story.story_hash)
                    OR $updateMissing = 1);
            """;
        command.Parameters.AddWithValue("$presentValue", valueWhenPresent ? 1 : 0);
        command.Parameters.AddWithValue("$missingValue", valueWhenMissing == true ? 1 : 0);
        command.Parameters.AddWithValue("$updateMissing", valueWhenMissing.HasValue ? 1 : 0);
        command.Parameters.AddWithValue("$kind", mutationKind);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task FinalizeUploadedMutationsAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        RemoteContentBatch content,
        CancellationToken cancellationToken)
    {
        var uploaded = new List<PendingStoryMutation>();
        await using (var query = connection.CreateCommand())
        {
            query.Transaction = transaction;
            query.CommandText =
                """
                SELECT mutation_id, story_hash, kind, value, created_utc
                FROM uploaded_mutation;
                """;
            await using var reader = await query.ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                uploaded.Add(new PendingStoryMutation(
                    reader.GetInt64(0),
                    reader.GetString(1),
                    ParseMutationKind(reader.GetString(2)),
                    reader.GetInt32(3) == 1,
                    DateTimeOffset.FromUnixTimeMilliseconds(reader.GetInt64(4))));
            }
        }

        foreach (var mutation in uploaded)
        {
            var observedValue = GetObservedRemoteValue(content, mutation);
            if (observedValue != mutation.Value)
            {
                continue;
            }

            await using (var pendingCommand = connection.CreateCommand())
            {
                pendingCommand.Transaction = transaction;
                pendingCommand.CommandText =
                    """
                    DELETE FROM pending_mutation
                    WHERE id = $id
                      AND story_hash = $hash
                      AND kind = $kind
                      AND value = $value
                      AND created_utc = $createdUtc;
                    """;
                pendingCommand.Parameters.AddWithValue("$id", mutation.Id);
                pendingCommand.Parameters.AddWithValue("$hash", mutation.StoryHash);
                pendingCommand.Parameters.AddWithValue(
                    "$kind",
                    FormatMutationKind(mutation.Kind));
                pendingCommand.Parameters.AddWithValue(
                    "$value",
                    mutation.Value ? 1 : 0);
                pendingCommand.Parameters.AddWithValue(
                    "$createdUtc",
                    mutation.CreatedAt.ToUnixTimeMilliseconds());
                await pendingCommand.ExecuteNonQueryAsync(cancellationToken)
                    .ConfigureAwait(false);
            }

            await using var acknowledgementCommand = connection.CreateCommand();
            acknowledgementCommand.Transaction = transaction;
            acknowledgementCommand.CommandText =
                "DELETE FROM uploaded_mutation WHERE mutation_id = $id;";
            acknowledgementCommand.Parameters.AddWithValue("$id", mutation.Id);
            await acknowledgementCommand.ExecuteNonQueryAsync(cancellationToken)
                .ConfigureAwait(false);
        }
    }

    private static bool? GetObservedRemoteValue(
        RemoteContentBatch content,
        PendingStoryMutation mutation) =>
        mutation.Kind switch
        {
            StoryMutationKind.Read when content.UnreadSetIsComplete =>
                !content.UnreadStoryHashes.Contains(mutation.StoryHash),
            StoryMutationKind.Saved when content.SavedSetIsComplete =>
                content.SavedStoryHashes.Contains(mutation.StoryHash),
            StoryMutationKind.Saved
                when content.SavedStoryHashes.Contains(mutation.StoryHash) => true,
            _ => null
        };

    private static async Task ExecuteAsync(
        SqliteConnection connection,
        SqliteTransaction transaction,
        string sql,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = sql;
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private const string StoryUpsertSql =
        """
        INSERT INTO story (
            story_hash, feed_id, service_id, guid_hash, title, author, permalink,
            content, summary, image_uri, published_utc, is_read, is_saved)
        VALUES (
            $hash, $feedId, $serviceId, $guidHash, $title, $author, $permalink,
            $content, $summary, $imageUri, $publishedUtc, $isRead, $isSaved)
        ON CONFLICT(story_hash) DO UPDATE SET
            feed_id = excluded.feed_id,
            service_id = excluded.service_id,
            guid_hash = excluded.guid_hash,
            title = excluded.title,
            author = excluded.author,
            permalink = excluded.permalink,
            content = excluded.content,
            summary = excluded.summary,
            image_uri = excluded.image_uri,
            published_utc = excluded.published_utc,
            is_read = CASE
                WHEN EXISTS (
                    SELECT 1 FROM pending_mutation
                    WHERE story_hash = excluded.story_hash AND kind = 'read')
                  OR EXISTS (
                    SELECT 1 FROM uploaded_mutation
                    WHERE story_hash = excluded.story_hash AND kind = 'read')
                THEN story.is_read
                ELSE excluded.is_read
            END,
            is_saved = CASE
                WHEN EXISTS (
                    SELECT 1 FROM pending_mutation
                    WHERE story_hash = excluded.story_hash AND kind = 'saved')
                  OR EXISTS (
                    SELECT 1 FROM uploaded_mutation
                    WHERE story_hash = excluded.story_hash AND kind = 'saved')
                THEN story.is_saved
                ELSE excluded.is_saved
            END;
        """;

    private static StoryMutationKind ParseMutationKind(string value) =>
        value switch
        {
            "read" => StoryMutationKind.Read,
            "saved" => StoryMutationKind.Saved,
            _ => throw new InvalidOperationException(
                $"Unknown pending mutation kind '{value}'.")
        };

    private static string FormatMutationKind(StoryMutationKind kind) =>
        kind switch
        {
            StoryMutationKind.Read => "read",
            StoryMutationKind.Saved => "saved",
            _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, null)
        };
}
