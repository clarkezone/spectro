using Microsoft.Data.Sqlite;
using Spectro.Domain;

namespace Spectro.Infrastructure;

public sealed class SqliteDatabase(
    SqliteConnectionFactory connectionFactory,
    TimeProvider? timeProvider = null)
{
    public const int CurrentSchemaVersion = 2;
    private readonly TimeProvider _timeProvider = timeProvider ?? TimeProvider.System;

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            await using var connection = await connectionFactory
                .OpenAsync(cancellationToken)
                .ConfigureAwait(false);

            await using (var journalCommand = connection.CreateCommand())
            {
                journalCommand.CommandText = "PRAGMA journal_mode = WAL;";
                await journalCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            }

            var version = await GetSchemaVersionAsync(connection, cancellationToken)
                .ConfigureAwait(false);

            if (version > CurrentSchemaVersion)
            {
                throw new InvalidOperationException(
                    $"Database schema version {version} is newer than supported version {CurrentSchemaVersion}.");
            }

            if (version == 0)
            {
                await ApplyVersion1Async(connection, cancellationToken).ConfigureAwait(false);
                version = 1;
            }

            if (version == 1)
            {
                await ApplyVersion2Async(connection, cancellationToken).ConfigureAwait(false);
                version = 2;
            }

            if (version != CurrentSchemaVersion)
            {
                throw new InvalidOperationException(
                    $"Database schema migration stopped at version {version}.");
            }

            await VerifyIntegrityAsync(connection, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception exception) when (
            exception is SqliteException or InvalidOperationException
            && exception is not SqliteDatabaseInitializationException)
        {
            connectionFactory.ClearPools();
            throw new SqliteDatabaseInitializationException(
                connectionFactory.DatabasePath,
                exception);
        }
    }

    public async Task<DatabaseResetResult> ResetAsync(
        CancellationToken cancellationToken = default)
    {
        connectionFactory.ClearPools();

        var preservedPath = PreserveDatabase();
        await InitializeAsync(cancellationToken).ConfigureAwait(false);
        return new DatabaseResetResult(preservedPath);
    }

    private static async Task<int> GetSchemaVersionAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA user_version;";
        var result = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt32(result, System.Globalization.CultureInfo.InvariantCulture);
    }

    private static async Task VerifyIntegrityAsync(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA quick_check;";
        var result = Convert.ToString(
            await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false),
            System.Globalization.CultureInfo.InvariantCulture);

        if (!string.Equals(result, "ok", StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                $"SQLite integrity check failed: {result ?? "no result"}.");
        }
    }

    private string? PreserveDatabase()
    {
        var databasePath = connectionFactory.DatabasePath;
        if (!File.Exists(databasePath))
        {
            return null;
        }

        var timestamp = _timeProvider.GetUtcNow().ToString(
            "yyyyMMddHHmmssfff",
            System.Globalization.CultureInfo.InvariantCulture);
        var preservedPath = $"{databasePath}.failed-{timestamp}";
        var suffix = 1;
        while (File.Exists(preservedPath))
        {
            preservedPath = $"{databasePath}.failed-{timestamp}-{suffix++}";
        }

        File.Move(databasePath, preservedPath);
        MoveSidecarIfPresent($"{databasePath}-wal", $"{preservedPath}-wal");
        MoveSidecarIfPresent($"{databasePath}-shm", $"{preservedPath}-shm");
        return preservedPath;
    }

    private static void MoveSidecarIfPresent(string sourcePath, string destinationPath)
    {
        if (File.Exists(sourcePath))
        {
            File.Move(sourcePath, destinationPath);
        }
    }

    private static async Task ApplyVersion1Async(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var transaction = await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText =
            """
            CREATE TABLE account (
                id INTEGER PRIMARY KEY CHECK (id = 1),
                newsblur_user_id INTEGER,
                username TEXT,
                last_successful_sync_utc INTEGER
            );

            CREATE TABLE folder (
                id TEXT PRIMARY KEY,
                title TEXT NOT NULL,
                sort_order INTEGER NOT NULL
            );

            CREATE TABLE feed (
                id INTEGER PRIMARY KEY,
                title TEXT NOT NULL,
                feed_uri TEXT NOT NULL,
                icon_uri TEXT,
                unread_count INTEGER NOT NULL DEFAULT 0 CHECK (unread_count >= 0),
                latest_story_utc INTEGER,
                is_active INTEGER NOT NULL CHECK (is_active IN (0, 1))
            );

            CREATE TABLE folder_feed (
                folder_id TEXT NOT NULL REFERENCES folder(id) ON DELETE CASCADE,
                feed_id INTEGER NOT NULL REFERENCES feed(id) ON DELETE CASCADE,
                sort_order INTEGER NOT NULL,
                PRIMARY KEY (folder_id, feed_id)
            );

            CREATE TABLE story (
                story_hash TEXT PRIMARY KEY,
                feed_id INTEGER NOT NULL REFERENCES feed(id) ON DELETE CASCADE,
                service_id TEXT,
                guid_hash TEXT,
                title TEXT NOT NULL,
                author TEXT,
                permalink TEXT,
                content TEXT NOT NULL,
                summary TEXT NOT NULL,
                image_uri TEXT,
                published_utc INTEGER NOT NULL,
                is_read INTEGER NOT NULL CHECK (is_read IN (0, 1)),
                is_saved INTEGER NOT NULL CHECK (is_saved IN (0, 1))
            );

            CREATE TABLE pending_mutation (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                story_hash TEXT NOT NULL REFERENCES story(story_hash) ON DELETE CASCADE,
                kind TEXT NOT NULL CHECK (kind IN ('read', 'saved')),
                value INTEGER NOT NULL CHECK (value IN (0, 1)),
                created_utc INTEGER NOT NULL,
                UNIQUE (story_hash, kind)
            );

            CREATE TABLE sync_checkpoint (
                name TEXT PRIMARY KEY,
                value TEXT NOT NULL,
                updated_utc INTEGER NOT NULL
            );

            CREATE INDEX ix_feed_title ON feed(title COLLATE NOCASE);
            CREATE INDEX ix_story_feed_published
                ON story(feed_id, published_utc DESC);
            CREATE INDEX ix_story_unread_published
                ON story(is_read, published_utc DESC);
            CREATE INDEX ix_story_saved_published
                ON story(is_saved, published_utc DESC);
            CREATE INDEX ix_pending_mutation_created
                ON pending_mutation(created_utc, id);

            PRAGMA user_version = 1;
            """;

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task ApplyVersion2Async(
        SqliteConnection connection,
        CancellationToken cancellationToken)
    {
        await using var transaction = await connection
            .BeginTransactionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using var command = connection.CreateCommand();
        command.Transaction = (SqliteTransaction)transaction;
        command.CommandText =
            """
            CREATE TABLE uploaded_mutation (
                mutation_id INTEGER PRIMARY KEY,
                story_hash TEXT NOT NULL,
                kind TEXT NOT NULL CHECK (kind IN ('read', 'saved')),
                value INTEGER NOT NULL CHECK (value IN (0, 1)),
                created_utc INTEGER NOT NULL,
                uploaded_utc INTEGER NOT NULL
            );

            CREATE INDEX ix_uploaded_mutation_story_kind
                ON uploaded_mutation(story_hash, kind);

            PRAGMA user_version = 2;
            """;

        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
    }
}
