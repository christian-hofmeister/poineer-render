using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Options;
using POIneer.Render.Infrastructure.FileSystem;
using POIneer.Render.Infrastructure.Flyway;
using POIneer.Render.Infrastructure.Process;
using Xunit;

namespace POIneer.Render.IntegrationTests.Infrastructure.Flyway;

public sealed class FlywaySqliteDatabaseInitializerTests
{
    [Fact]
    public async Task InitializeAsync_creates_poi_table()
    {
        if (!ProcessUtils.IsExecutableAvailable("/usr/local/bin/flyway"))
            return;
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

        var sut = new FlywaySqliteDatabaseInitializer(runner, builder);


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


