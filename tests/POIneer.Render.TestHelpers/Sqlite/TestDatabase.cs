using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POIneer.Render.Infrastructure.FileSystem;
using POIneer.Render.Infrastructure.Flyway;

namespace POIneer.Render.TestHelpers.Sqlite;

public static class SqliteTestDatabase
{
    public static string CreateConnectionString(string dbPath)
    {
        return new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            // Important for tests to ensure that each connection is independent and doesn't interfere with others
            Pooling = false
        }.ToString();
    }

    public static async Task<string> CreateAsync(
        TemporaryDirectory tempDir,
        string fileName = "poi.sqlite",
        CancellationToken ct = default)
    {
        var dbPath = Path.Combine(tempDir.DirectoryPath, fileName);

        var options = Options.Create(new FlywayOptions
        {
            Executable = "flyway",
            ConfigFileRelativePath = "migrations/flyway-poi.toml",
            Debug = false
        });

        var processRunner = new ProcessRunner();

        var invocationBuilder =
            new FlywayInvocationBuilder(options);

        using var loggerFactory = LoggerFactory.Create(builder => { });

        var logger =
            loggerFactory.CreateLogger<FlywaySqliteDatabaseInitializer>();

        var initializer =
            new FlywaySqliteDatabaseInitializer(
                processRunner,
                invocationBuilder,
                logger);

        await initializer.InitializeAsync(dbPath, ct);

        return dbPath;
    }
}