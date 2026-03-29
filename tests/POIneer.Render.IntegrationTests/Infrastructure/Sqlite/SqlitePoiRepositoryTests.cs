using System.Diagnostics;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POIneer.Render.Domain.Models;
using POIneer.Render.Infrastructure.FileSystem;
using POIneer.Render.Infrastructure.Process;
using POIneer.Render.Infrastructure.Sqlite;
using Xunit;
using Xunit.Abstractions;

namespace POIneer.Render.IntegrationTests.Infrastructure.Sqlite;

public sealed class SqlitePoiRepositoryTests(ITestOutputHelper output)
{
    private readonly ITestOutputHelper _output = output;

    [Fact]
    public async Task AddAndList_ReturnsInsertedPoi()
    {
        var logger = new XunitLogger<SqlitePoiRepositoryTests>(_output);
        logger.LogInformation("Starting SqlitePoiRepositoryTests.AddAndList_ReturnsInsertedPoi test.");

        // Arrange
        var tempDirOptions = Options.Create(new TempOptions
        {
            RootFolderName = "poineer-tests",
            KeepOnDispose = false
        });

        ITemporaryDirectoryFactory tempDirectoryFactory =
            new TemporaryDirectoryFactory(tempDirOptions);

        await using var tempDir = tempDirectoryFactory.Create("flyway-sqlite-database-initializer-tests");

        var root = tempDir.DirectoryPath;
        var dbPath = Path.Combine(root, $"{Guid.NewGuid():N}.sqlite");
        var cs = new SqliteConnectionStringBuilder { DataSource = dbPath }.ToString();

        await using (var con = new SqliteConnection(cs))
        {
            await con.OpenAsync();

            // Minimal schema for happy path - adapt to your real table/columns
            var cmd = con.CreateCommand();
            cmd.CommandText = """
                CREATE TABLE poi (
                    id              INTEGER PRIMARY KEY,
                    osm_id          TEXT NOT NULL,
                    name            TEXT,
                    amenity         TEXT,
                    latitude        REAL NOT NULL,
                    longitude       REAL NOT NULL
                );
               """;
            await cmd.ExecuteNonQueryAsync();
        }
        var sut = CreateSut(dbPath);
        var poiId = 1234L;

        // Act
        await sut.AddAsync(new Poi(
            Id: poiId,
            OsmId: "osm123",
            Name: "Test POI",
            Amenity: "cafe",
            Latitude: 52.5200,
            Longitude: 13.4050
        ), CancellationToken.None);

        var all = (await sut.GetAllAsync(100, CancellationToken.None)).ToList();


        // Assert
        Assert.Contains(all, p => p.Id == poiId && p.Name == "Test POI"); // Ensure the inserted POI is returned
        Assert.Single(all); // Ensure only one record exists

        // Cleanup
        if (File.Exists(dbPath))
            File.Delete(dbPath);
    }

    private static SqlitePoiRepository CreateSut(string dbPath) => new SqlitePoiRepository(() =>
    {
        var cs = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath
        }.ToString();

        return new SqliteConnection(cs);
    });
}
