using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POIneer.Render.Infrastructure.FileSystem;
using POIneer.Render.Infrastructure.Flyway;
using POIneer.Render.Infrastructure.Process;
using POIneer.Render.TestHelpers;
using Xunit;
using Xunit.Abstractions;

namespace POIneer.Render.IntegrationTests.Infrastructure.Flyway;

public sealed class FlywaySqliteDatabaseInitializerTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public async Task InitializeAsync_creates_poi_table()
    {
        var tempDirOptions = Options.Create(new TempOptions
        {
            RootFolderName = "poineer-tests",
            KeepOnDispose = true
        });

        ITemporaryDirectoryFactory tempDirectoryFactory =
            new TemporaryDirectoryFactory(tempDirOptions);

        await using var tempDir = tempDirectoryFactory.Create("flyway-sqlite-database-initializer-tests");

        var logger = new XunitLogger<FlywaySqliteDatabaseInitializer>(_output);

        string executable = "flyway";
        logger.LogInformation("Starting FlywaySqliteDatabaseInitializerTests.InitializeAsync_creates_poi_table test.");

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


        await using var temp = TemporaryDirectory.Create("poineer-flyway-it-");
        var root = temp.DirectoryPath;

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
                osm_id          TEXT NOT NULL,
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

        var env = new FakeHostEnvironment { ContentRootPath = root };

        var options = Options.Create(new FlywayOptions
        {
            Executable = "flyway",
            ConfigFileRelativePath = "flyway.toml",
            Debug = true
        });


        var builder = new FlywayInvocationBuilder(options, env);

        var sut = new FlywaySqliteDatabaseInitializer(runner, builder, logger);


        await sut.InitializeAsync(dbPath);

        // Assert
        Assert.True(await TableExistsAsync(dbPath, "poi"), "Expected table 'poi' to exist.");
        Assert.True(await TableExistsAsync(dbPath, "flyway_schema_history"));
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


