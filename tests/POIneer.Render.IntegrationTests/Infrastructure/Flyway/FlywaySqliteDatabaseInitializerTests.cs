using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POIneer.Render.Infrastructure.FileSystem;
using POIneer.Render.Infrastructure.Flyway;
using POIneer.Render.Infrastructure.Pathing;
using POIneer.Render.Infrastructure.Process;
using POIneer.Render.TestHelpers;
using Xunit;
using Xunit.Abstractions;
using FluentAssertions;
using Microsoft.Extensions.Hosting;

namespace POIneer.Render.IntegrationTests.Infrastructure.Flyway;

public sealed class FlywaySqliteDatabaseInitializerTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public async Task InitializeAsync_creates_poi_table()
    {
        var logger = new XunitLogger<FlywaySqliteDatabaseInitializer>(_output);

        await using var tempDir = TestTemporaryDirectories.Create("init-async-creates-poi-table", false);

        string executable = "flyway";
        logger.LogInformation($"Starting FlywaySqliteDatabaseInitializerTests.InitializeAsync_creates_poi_table test. Executable: '{executable}'");

        if (!ProcessUtils.IsExecutableAvailable(executable))
        {
            var path = Environment.GetEnvironmentVariable("PATH");

            logger.LogError(
            "Flyway executable not found. Executable: {Executable}, PATH: {Path}",
            executable,
            path);

            throw new InvalidOperationException(
                $"Flyway executable not found: {executable}");
        }
        var root = AppContext.BaseDirectory;

        // Layout:
        // root/
        //   flyway-poi.toml
        //   sql/poi/V1__create_poi_table.sql

        var sqlDir = Path.Combine(root, "sql", "poi");
        Directory.CreateDirectory(sqlDir);

        var migrationFile = Path.Combine(sqlDir, "V1__create_poi_table.sql");
        await File.WriteAllTextAsync(migrationFile, """
            CREATE TABLE IF NOT EXISTS poi (
                id              INTEGER PRIMARY KEY,
                osm_id          INTEGER NOT NULL,
                name            TEXT,
                amenity         TEXT,
                latitude        REAL NOT NULL,
                longitude       REAL NOT NULL
            );

            CREATE INDEX IF NOT EXISTS idx_poi_amenity ON poi (amenity);
            """);

        var dbPath = Path.Combine(root, "out", "poi.sqlite");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        var tomlPath = Path.Combine(root, "flyway-poi.toml");
        await File.WriteAllTextAsync(tomlPath, """
            [flyway]
            locations = ["filesystem:sql/poi"]
            cleanDisabled = false
            baselineOnMigrate = true
            """);

        // Arrange SUT (integration: real process runner)
        var runner = new ProcessRunner();

        var flywayOptions = Options.Create(new FlywayOptions
        {
            Executable = "flyway",
            MigrationsRelativePath = "sql/poi",
            ConfigFileRelativePath = "flyway-poi.toml",
            Debug = true
        });


        var builder = new FlywayInvocationBuilder(flywayOptions);

        var sut = new FlywaySqliteDatabaseInitializer(runner, builder, logger);


        await sut.InitializeAsync(dbPath);

        // Assert
        Assert.True(await TableExistsAsync(dbPath, "poi"), "Expected table 'poi' to exist.");
        Assert.True(await TableExistsAsync(dbPath, "flyway_schema_history"));
    }

    [Fact]
    public async Task InitializeAsync_AppliesPoiMigrations()
    {
        await using var tempDir = TestTemporaryDirectories.Create("flyway-applies-migrations", false);

        var dbPath = Path.Combine(tempDir.DirectoryPath, "poi.sqlite");

        var repoRoot = RepoRootLocator.Find();

        var processRunner = new ProcessRunner();

        var options = Options.Create(new FlywayOptions
        {
            Executable = "flyway",
            MigrationsRelativePath = Path.Combine("migrations", "sql", "poi"),
            ConfigFileRelativePath = Path.Combine("migrations", "flyway-poi.toml"),
            Debug = true
        });

        var migrationsPath = Path.Combine(repoRoot, options.Value.MigrationsRelativePath);
        Directory.Exists(migrationsPath).Should().BeTrue("because migrations must be present for Flyway to apply them");

        var invocationBuilder = new FlywayInvocationBuilder(options);
        var logger = new LoggerFactory().CreateLogger<FlywaySqliteDatabaseInitializer>();
        var databaseInitializer = new FlywaySqliteDatabaseInitializer(
            processRunner,
            invocationBuilder,
            logger);

        await databaseInitializer.InitializeAsync(dbPath, CancellationToken.None);

        Assert.True(File.Exists(dbPath), "Expected SQLite database file to be created.");

        // Verify that the 'poi' table exists, which indicates that migrations were applied
        Assert.True(await TableExistsAsync(dbPath, "poi"), "Expected table 'poi' to exist after migrations are applied.");
    }

    private static async Task<bool> TableExistsAsync(string sqlitePath, string tableName)
    {
        var cs = new SqliteConnectionStringBuilder { DataSource = sqlitePath }.ToString();
        await using var con = new SqliteConnection(cs);
        await con.OpenAsync();

        await using var cmd = con.CreateCommand();
        cmd.CommandText = """
            SELECT 1
            FROM sqlite_master
            WHERE type='table' AND name=$name
            LIMIT 1;
            """;
        cmd.Parameters.AddWithValue("$name", tableName);

        var result = await cmd.ExecuteScalarAsync();
        return result is not null;
    }
}


