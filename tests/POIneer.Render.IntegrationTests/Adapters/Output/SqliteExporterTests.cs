using System.Data.SQLite;
using POIneer.Render.Adapters.Output;
using POIneer.Render.Application.Contracts;
using Xunit;

namespace POIneer.Render.IntegrationTests.Adapters.Output;

public sealed class SqliteExporterTests
{
    [Fact]
    public async Task ExportAsync_CreatesDbAndInsertsPois_AndHandlesNullName()
    {
        // Arrange
        var tempDir = CreateTempDir();
        var sqlitePath = Path.Combine(tempDir, "out.sqlite");

        var exporter = new SqliteExporter();

        // A small async stream of POIs
        async IAsyncEnumerable<PoiDto> Pois()
        {
            yield return new PoiDto(
                OsmId: 1,
                Name: "Cafe Central",
                Category: "cafe",
                Lon: 13.404954,
                Lat: 52.520008);

            yield return new PoiDto(
                OsmId: 2,
                Name: null, // important: should become NULL in DB
                Category: "bench",
                Lon: 13.40,
                Lat: 52.52);

            await Task.CompletedTask;
        }

        // Act
        await exporter.ExportAsync(Pois(), sqlitePath);

        // Assert
        Assert.True(File.Exists(sqlitePath));

        using var conn = new SQLiteConnection($"Data Source={sqlitePath};");
        await conn.OpenAsync();

        // 1) Table exists?
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='poi';";
            var tableName = (string?)await cmd.ExecuteScalarAsync();
            Assert.Equal("poi", tableName);
        }

        // 2) Row count = 2
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT COUNT(*) FROM poi;";
            var count = Convert.ToInt32(await cmd.ExecuteScalarAsync());
            Assert.Equal(2, count);
        }

        // 3) Verify second row has NULL name and correct category
        using (var cmd = conn.CreateCommand())
        {
            cmd.CommandText = "SELECT osm_id, name, category FROM poi WHERE osm_id = 2;";
            using var reader = await cmd.ExecuteReaderAsync();

            Assert.True(await reader.ReadAsync());

            var osmId = reader.GetInt64(0);
            Assert.Equal(2L, osmId);

            // name column is NULL -> reader.IsDBNull
            Assert.True(reader.IsDBNull(1));

            var category = reader.GetString(2);
            Assert.Equal("bench", category);
        }
    }

    [Fact]
    public async Task ExportAsync_DeletesExistingFile_AndRecreatesDb()
    {
        // Arrange
        var tempDir = CreateTempDir();
        var sqlitePath = Path.Combine(tempDir, "out.sqlite");

        // Create a "dummy" existing file (not necessarily a valid sqlite db)
        await File.WriteAllTextAsync(sqlitePath, "dummy");
        Assert.True(File.Exists(sqlitePath));

        var exporter = new SqliteExporter();

        async IAsyncEnumerable<PoiDto> Pois()
        {
            yield return new PoiDto(1, "A", "cafe", 1.0, 2.0);
            await Task.CompletedTask;
        }

        // Act
        await exporter.ExportAsync(Pois(), sqlitePath);

        // Assert: file exists and is a valid sqlite db with table poi
        using var conn = new SQLiteConnection($"Data Source={sqlitePath};");
        await conn.OpenAsync();

        using var cmd = conn.CreateCommand();
        cmd.CommandText = "SELECT name FROM sqlite_master WHERE type='table' AND name='poi';";
        var tableName = (string?)await cmd.ExecuteScalarAsync();
        Assert.Equal("poi", tableName);
    }

    private static string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), "poineer-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        return dir;
    }
}
