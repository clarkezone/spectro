namespace Spectro.Infrastructure;

public sealed class SqliteDatabaseInitializationException(
    string databasePath,
    Exception innerException)
    : InvalidOperationException(
        $"SQLite database '{databasePath}' could not be opened or initialized. "
        + "The database was preserved and must be reset explicitly.",
        innerException)
{
    public string DatabasePath { get; } = databasePath;
}
