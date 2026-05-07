using Microsoft.Data.Sqlite;
using POIneer.Render.Domain.Models;
using POIneer.Render.Infrastructure.Sqlite;
using POIneer.Render.TestHelpers;
using POIneer.Render.TestHelpers.Sqlite;
using Xunit;

namespace POIneer.Render.IntegrationTests.Infrastructure.Sqlite;

public sealed class SqlitePoiRepositoryTests()
{
    [Fact]
    public async Task AddAndList_ReturnsInsertedPoi()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("AddAndList_ReturnsInsertedPoi", false);

        var root = tempDir.DirectoryPath;
        var dbPath = Path.Combine(root, $"{Guid.NewGuid():N}.sqlite");
        var cs = SqliteTestDatabase.CreateConnectionString(dbPath);

        await InitializeDatabaseAsync(cs);
        var sut = CreateSut(dbPath);

        var osmId = 5678L;
        var location = new GeoPoint(Latitude: 52.5200, Longitude: 13.4050);

        // Act
        await sut.AddAsync(new Poi(
            Id: null,
            OsmId: osmId,
            Name: "Test POI",
            Amenity: "cafe",
            Location: location
        ), CancellationToken.None);

        var all = (await sut.GetAllAsync(100, CancellationToken.None)).ToList();

        // Assert
        var poi = Assert.Single(all);
        Assert.True(poi.Id > 0);
        Assert.Equal(osmId, poi.OsmId);
        Assert.Equal("Test POI", poi.Name);
        Assert.Equal("cafe", poi.Amenity);
        Assert.Equal(location.Latitude, poi.Location.Latitude);
        Assert.Equal(location.Longitude, poi.Location.Longitude);
    }

    [Fact]
    public async Task AddAndList_ReturnsInsertedPoi_WithNullOptionalFields()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("AddAndList_ReturnsInsertedPoiWithNullOptionalFields", false);

        var root = tempDir.DirectoryPath;
        var dbPath = Path.Combine(root, $"{Guid.NewGuid():N}.sqlite");
        var cs = SqliteTestDatabase.CreateConnectionString(dbPath);

        await InitializeDatabaseAsync(cs);

        var sut = CreateSut(dbPath);
        var osmId = 9876L;
        var location = new GeoPoint(
            Latitude: 52.5200,
            Longitude: 13.4050);

        // Act
        await sut.AddAsync(new Poi(
            Id: null,
            OsmId: osmId,
            Name: null,
            Amenity: null,
            Location: location
        ), CancellationToken.None);

        var all = (await sut.GetAllAsync(100, CancellationToken.None)).ToList();

        // Assert
        var poi = Assert.Single(all);
        Assert.True(poi.Id > 0);
        Assert.Equal(osmId, poi.OsmId);
        Assert.Null(poi.Name);
        Assert.Null(poi.Amenity);
        Assert.Equal(location.Latitude, poi.Location.Latitude);
        Assert.Equal(location.Longitude, poi.Location.Longitude);
    }

    private static async Task InitializeDatabaseAsync(string connectionString)
    {
        await using var con = new SqliteConnection(connectionString);
        await con.OpenAsync();

        var cmd = con.CreateCommand();
        cmd.CommandText = """
            CREATE TABLE poi (
                id INTEGER PRIMARY KEY,
                osm_id INTEGER NOT NULL,
                name TEXT,
                amenity TEXT,
                latitude REAL NOT NULL,
                longitude REAL NOT NULL
            );
            """;

        await cmd.ExecuteNonQueryAsync();
    }
    private static SqlitePoiRepository CreateSut(string dbPath) => new SqlitePoiRepository(() =>
    {
        var cs = SqliteTestDatabase.CreateConnectionString(dbPath);

        return new SqliteConnection(cs);
    });
}
