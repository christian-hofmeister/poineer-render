using System.Diagnostics;
using Microsoft.Data.Sqlite;
using POIneer.Render.Abstractions.InfrastructureAbstractions;
using POIneer.Render.Application.Contracts;
using POIneer.Render.Infrastructure.Flyway;
using Xunit;

namespace POIneer.Render.IntegrationTests.Infrastructure;

public sealed class FlywaySqliteDatabaseInitializerTests
{
    [Fact]
    public async Task InitializeAsync_creates_poi_table()
    {
        // Skip if Flyway is not available.
        if (!IsFlywayAvailable())
            return;

        await using var temp = TempDir.Create("poineer-flyway-it-");
        var root = temp.Path;

        // Create migrations layout:
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

        // Output db path
        var dbPath = Path.Combine(root, "out", "poi.sqlite");
        Directory.CreateDirectory(Path.GetDirectoryName(dbPath)!);

        // Flyway TOML config (locations relative to config file directory).
        // If you're using .conf instead, adjust accordingly.
        var tomlPath = Path.Combine(root, "flyway-poi.toml");
        await File.WriteAllTextAsync(tomlPath, """
            [flyway]
            # IMPORTANT: locations are relative to the config file location
            locations = ["filesystem:sql/poi"]
            cleanDisabled = false
            baselineOnMigrate = true
            """);

        // Real runner (integration test).
        var runner = new RealProcessRunner();
        var sut = new FlywaySqliteDatabaseInitializer(runner);
        var options = new FlywayOptions(ConfigFileRelativePath: tomlPath, Debug: true);

        var invocation = FlywayInvocationBuilder.BuildSqliteMigrate(
            options,
            root,
            dbPath);

        // Act
        await sut.InitializeAsync(invocation);

        // Assert: table exists
        Assert.True(await TableExistsAsync(dbPath, "poi"), "Expected table 'poi' to exist.");

        // (Optional) Assert: schema history exists too
        Assert.True(await TableExistsAsync(dbPath, "flyway_schema_history"));
    }

    private static bool IsFlywayAvailable()
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "flyway",
                Arguments = "-v",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };
            using var p = Process.Start(psi);
            if (p is null) return false;
            p.WaitForExit(3000);
            return p.ExitCode == 0;
        }
        catch
        {
            return false;
        }
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

    private sealed class RealProcessRunner : IProcessRunner
    {
        public async Task<ProcessResult> RunAsync(ProcessStartInfo psi, CancellationToken ct)
        {
            using var p = new Process { StartInfo = psi };

            p.Start();

            var stdOutTask = p.StandardOutput.ReadToEndAsync();
            var stdErrTask = p.StandardError.ReadToEndAsync();

            await p.WaitForExitAsync(ct);

            var stdout = await stdOutTask;
            var stderr = await stdErrTask;

            return new ProcessResult(p.ExitCode, stdout, stderr);
        }
    }
}


