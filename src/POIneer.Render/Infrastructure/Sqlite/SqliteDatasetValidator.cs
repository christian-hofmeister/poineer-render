using Microsoft.Data.Sqlite;
using POIneer.Render.Application.Contracts;
using POIneer.Render.Application.Ports;

namespace POIneer.Render.Infrastructure.Sqlite;

public sealed class SqliteDatasetValidator : IDatasetValidator
{
    private static readonly string[] RequiredTables =
    [
        "poi"
    ];

    public async Task<DatasetValidationResult> ValidateAsync(
        string path,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var errors = new List<string>();

        if (!File.Exists(path))
        {
            return Invalid($"Dataset file does not exist: {path}");
        }

        var fileInfo = new FileInfo(path);

        if (fileInfo.Length == 0)
        {
            return Invalid($"Dataset file is empty: {path}");
        }

        try
        {
            var connectionString = new SqliteConnectionStringBuilder
            {
                DataSource = path,
                Mode = SqliteOpenMode.ReadOnly,
                // Pooling keeps the native sqlite3 handle open in the background even
                // after the SqliteConnection is disposed, which on Windows blocks a
                // subsequent File.Move/Delete of this path (e.g. promoting a validated
                // staging file to its canonical location). Disable it so the file handle
                // is actually released when validation is done.
                Pooling = false
            }.ToString();

            await using var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            await ValidateIntegrityAsync(
                connection,
                errors,
                cancellationToken);

            await ValidateRequiredTablesAsync(
                connection,
                errors,
                cancellationToken);
        }
        catch (SqliteException ex)
        {
            errors.Add(
                $"Dataset could not be opened or queried as SQLite: {ex.Message}");
        }

        return new DatasetValidationResult(
            IsValid: errors.Count == 0,
            Errors: errors);
    }

    private static async Task ValidateIntegrityAsync(
        SqliteConnection connection,
        ICollection<string> errors,
        CancellationToken cancellationToken)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = "PRAGMA integrity_check;";

        var result = await command.ExecuteScalarAsync(cancellationToken);

        if (!string.Equals(
                result?.ToString(),
                "ok",
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add(
                $"SQLite integrity check failed: {result ?? "<no result>"}");
        }
    }

    private static async Task ValidateRequiredTablesAsync(
        SqliteConnection connection,
        ICollection<string> errors,
        CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT name
            FROM sqlite_master
            WHERE type = 'table';
            """;

        await using var command = connection.CreateCommand();
        command.CommandText = sql;

        var existingTables = new HashSet<string>(
            StringComparer.OrdinalIgnoreCase);

        await using var reader =
            await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            existingTables.Add(reader.GetString(0));
        }

        foreach (var requiredTable in RequiredTables)
        {
            if (!existingTables.Contains(requiredTable))
            {
                errors.Add(
                    $"Required table '{requiredTable}' is missing.");
            }
        }
    }

    private static DatasetValidationResult Invalid(string error)
    {
        return new DatasetValidationResult(
            IsValid: false,
            Errors: [error]);
    }
}