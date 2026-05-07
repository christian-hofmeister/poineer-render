using Microsoft.Data.Sqlite;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POIneer.Render.Adapters.Output;
using POIneer.Render.Application.Contracts;
using POIneer.Render.Infrastructure.Flyway;
using POIneer.Render.TestHelpers;
using POIneer.Render.TestHelpers.Sqlite;
using Xunit;
using Xunit.Abstractions;

namespace POIneer.Render.IntegrationTests.Adapters.Output;

public sealed class SqliteExporterTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public async Task ExportAsync_inserts_rows_into_existing_poi_table()
    {
        await using var tempDir = TestTemporaryDirectories.Create("insert-rows-into-existing-poi-table", false);

        var sqliteFilePath = Path.Combine(tempDir.DirectoryPath, "poi.sqlite");

        await CreatePoiSchemaAsync(sqliteFilePath);

        var sut = new SqliteExporter();

        var pois = ToAsyncEnumerable(
            new PoiDto("node/1001", "Test Cafe", "cafe", 52.5200, 13.4050),
            new PoiDto("node/1002", "Test Pharmacy", "pharmacy", 52.5205, 13.4055));

        await sut.ExportAsync(pois, sqliteFilePath, CancellationToken.None);

        var count = await GetPoiCountAsync(sqliteFilePath);
        Assert.Equal(2, count);
    }

    [Fact]
    public async Task ExportAsync_HappyPath_InsertsMultiplePois()
    {
        await using var tempDir = TestTemporaryDirectories.Create("sqlite-insert-multiple-pois", false);

        var dbPath = await SqliteTestDatabase.CreateAsync(tempDir);

        Assert.True(File.Exists(dbPath));
        var tables = await GetTableNamesAsync(dbPath);
        Assert.Contains("flyway_schema_history", tables);
        Assert.Contains("poi", tables);

        var sut = new SqliteExporter();

        var pois = ToAsyncEnumerable(
            new PoiDto("node/1001", "Test Cafe", "cafe", 52.5200, 13.4050),
            new PoiDto("node/1002", "Test Pharmacy", "pharmacy", 52.5205, 13.4055));

        await sut.ExportAsync(pois, dbPath, CancellationToken.None);

        var connectionString = SqliteTestDatabase.CreateConnectionString(dbPath);

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        await using (var countCommand = connection.CreateCommand())
        {
            countCommand.CommandText = "SELECT COUNT(*) FROM poi;";
            var count = (long)(await countCommand.ExecuteScalarAsync())!;
            Assert.Equal(2, count);
        }

        await using (var selectCommand = connection.CreateCommand())
        {
            selectCommand.CommandText = """
                SELECT osm_id, name, amenity, latitude, longitude
                FROM poi
                ORDER BY osm_id;
                """;

            await using var reader = await selectCommand.ExecuteReaderAsync();

            Assert.True(await reader.ReadAsync());
            Assert.Equal("node/1001", reader.GetString(0));
            Assert.Equal("Test Cafe", reader.GetString(1));
            Assert.Equal("cafe", reader.GetString(2));
            Assert.Equal(52.5200, reader.GetDouble(3));
            Assert.Equal(13.4050, reader.GetDouble(4));

            Assert.True(await reader.ReadAsync());
            Assert.Equal("node/1002", reader.GetString(0));
            Assert.Equal("Test Pharmacy", reader.GetString(1));
            Assert.Equal("pharmacy", reader.GetString(2));
            Assert.Equal(52.5205, reader.GetDouble(3));
            Assert.Equal(13.4055, reader.GetDouble(4));

            Assert.False(await reader.ReadAsync());
        }
    }

    private static async IAsyncEnumerable<PoiDto> ToAsyncEnumerable(params PoiDto[] pois)
    {
        foreach (var poi in pois)
        {
            yield return poi;
            await Task.Yield();
        }
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "Tests";
        public string ContentRootPath { get; set; } = Directory.GetCurrentDirectory();
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }

    private static async Task<List<string>> GetTableNamesAsync(string dbPath)
    {
        var result = new List<string>();

        await using var connection = new SqliteConnection(SqliteTestDatabase.CreateConnectionString(dbPath));
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
        SELECT name
        FROM sqlite_master
        WHERE type = 'table'
        ORDER BY name;
        """;

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            result.Add(reader.GetString(0));
        }

        return result;
    }

    private static async Task CreatePoiSchemaAsync(string dbPath)
    {
        var connectionString = SqliteTestDatabase.CreateConnectionString(dbPath);

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
        CREATE TABLE poi (
            id INTEGER PRIMARY KEY,
            osm_id INTEGER NOT NULL,
            name TEXT NULL,
            amenity TEXT NULL,
            latitude REAL NOT NULL,
            longitude REAL NOT NULL
        );
        """;

        await command.ExecuteNonQueryAsync();
    }

    private static async Task<long> GetPoiCountAsync(string dbPath)
    {
        var connectionString = SqliteTestDatabase.CreateConnectionString(dbPath);

        await using var connection = new SqliteConnection(connectionString);
        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT COUNT(*) FROM poi;";

        return (long)(await command.ExecuteScalarAsync())!;
    }
}