using Microsoft.Data.Sqlite;
using POIneer.Render.Domain.Models;
using POIneer.Render.Infrastructure.Sqlite;
using POIneer.Render.TestHelpers;
using POIneer.Render.TestHelpers.Sqlite;
using Xunit;

namespace POIneer.Render.IntegrationTests.Infrastructure.Sqlite.PoiRepository;

public sealed class SqlitePoiRepositoryGetByBoundingBoxTests
{
    [Fact]
    public async Task GetByBoundingBoxAsync_ReturnsPoisWithinBoundingBox()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("GetByBoundingBoxAsync_ReturnsPoisWithinBoundingBox", false);
        var dbPath = await SqliteTestDatabase.CreateAsync(tempDir);
        var repository = CreateSut(dbPath);

        await SeedDefaultPoisAsync(repository);

        // Define a bounding box that includes some of the seeded POIs
        var northWest = new GeoPoint(52.5300, 13.3400);
        var southEast = new GeoPoint(52.4800, 13.4100);

        // Act
        var boundingBox = new BoundingBox(northWest, southEast);
        var pois = await repository.GetByBoundingBoxAsync(boundingBox, 10, CancellationToken.None);

        // Assert
        Assert.Equal(5, pois.Count);
        Assert.Contains(pois, p => p.Name == "Café Einstein Stammhaus");
        Assert.Contains(pois, p => p.Name == "Stadtbibliothek Mitte");
        Assert.Contains(pois, p => p.Name == "Café am Neuen See");
        Assert.Contains(pois, p => p.Name == "Burgermeister Schöneberg");
        Assert.Contains(pois, p => p.Name == "Mustafa's Gemüse Kebap");
    }

    [Fact]
    public async Task GetByBoundingBoxAsync_ReturnsEmptyList_WhenNoPoisWithinBoundingBox()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("GetByBoundingBoxAsync_ReturnsEmptyList_WhenNoPoisWithinBoundingBox", false);
        var dbPath = await SqliteTestDatabase.CreateAsync(tempDir);
        var repository = CreateSut(dbPath);

        await SeedDefaultPoisAsync(repository);

        // Define a bounding box that does not include any of the seeded POIs
        var northWest = new GeoPoint(52.6000, 13.2000);
        var southEast = new GeoPoint(52.5900, 13.2100);

        // Act
        var boundingBox = new BoundingBox(northWest, southEast);
        var pois = await repository.GetByBoundingBoxAsync(boundingBox, 10, CancellationToken.None);

        // Assert
        Assert.Empty(pois);
    }

    private static SqlitePoiRepository CreateSut(string dbPath) => new SqlitePoiRepository(() =>
    {
        var cs = SqliteTestDatabase.CreateConnectionString(dbPath);

        return new SqliteConnection(cs);
    });

    private static Task SeedDefaultPoisAsync(SqlitePoiRepository repository)
    {
        return PoiSeedHelper.SeedAsync(
            repository,
            new Poi(
                Id: 1,
                OsmId: 1001,
                Name: "Café Einstein Stammhaus",
                Amenity: "cafe",
                Location: new GeoPoint(
                    Latitude: 52.5076,
                    Longitude: 13.3509)),
            new Poi(
                Id: 2,
                OsmId: 1002,
                Name: "Burgermeister Schöneberg",
                Amenity: "fast_food",
                Location: new GeoPoint(
                    Latitude: 52.4897,
                    Longitude: 13.3538)),
            new Poi(
                Id: 3,
                OsmId: 1003,
                Name: "Stadtbibliothek Mitte",
                Amenity: "library",
                Location: new GeoPoint(
                    Latitude: 52.5251,
                    Longitude: 13.3888)),
            new Poi(
                Id: 4,
                OsmId: 1004,
                Name: "Mustafa's Gemüse Kebap",
                Amenity: "fast_food",
                Location: new GeoPoint(
                    Latitude: 52.4938,
                    Longitude: 13.3876)),
            new Poi(
                Id: 6,
                OsmId: 1006,
                Name: "Café am Wasserturm",
                Amenity: "cafe",
                Location: new GeoPoint(
                    Latitude: 52.5308,
                    Longitude: 13.2995)),
            new Poi(
                Id: 7,
                OsmId: 1007,
                Name: "Funkturm Restaurant",
                Amenity: "restaurant",
                Location: new GeoPoint(
                    Latitude: 52.5079,
                    Longitude: 13.2746)),
            new Poi(
                Id: 5,
                OsmId: 1005,
                Name: "Café am Neuen See",
                Amenity: "cafe",
                Location: new GeoPoint(
                    Latitude: 52.5145,
                    Longitude: 13.3501))
        );
    }
}