using Microsoft.Data.Sqlite;
using POIneer.Render.Domain.Models;
using POIneer.Render.Infrastructure.Sqlite;
using POIneer.Render.TestHelpers;
using POIneer.Render.TestHelpers.Sqlite;
using Xunit;

namespace POIneer.Render.IntegrationTests.Infrastructure.Sqlite.PoiRepository;

public sealed class SqlitePoiRepositoryGetByAmenityTests
{
    [Fact]
    public async Task GetByAmenityAsync_ReturnsMatchingPois()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("GetByAmenityAsync_ReturnsMatchingPois", false);
        var dbPath = await SqliteTestDatabase.CreateAsync(tempDir);
        var repository = CreateSut(dbPath);

        await SeedDefaultPoisAsync(repository);

        // Act
        var restaurants = await repository.GetByAmenityAsync("restaurant", 10, CancellationToken.None);

        // Assert
        Assert.Equal(2, restaurants.Count);
        Assert.All(restaurants, r => Assert.Equal("restaurant", r.Amenity));
        Assert.Contains(restaurants, r => r.Name == "Pizza Napoli");
        Assert.Contains(restaurants, r => r.Name == "Burger House");
        Assert.DoesNotContain(restaurants, r => r.Name == "Cafe Berlin");

    }

    [Fact]
    public async Task GetByAmenityAsync_RespectsLimit()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("GetByAmenityAsync_RespectsLimit", false);
        var dbPath = await SqliteTestDatabase.CreateAsync(tempDir);
        var repository = CreateSut(dbPath);

        await SeedDefaultPoisAsync(repository);

        // Act
        var restaurants = await repository.GetByAmenityAsync("restaurant", 1, CancellationToken.None);

        // Assert
        Assert.Single(restaurants);
        Assert.Equal("restaurant", restaurants[0].Amenity);
        Assert.True(restaurants[0].Name == "Pizza Napoli");
    }

    [Fact]
    public async Task GetByAmenityAsync_ReturnsEmptyList_WhenAmenityDoesNotExist()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("GetByAmenityAsync_ReturnsEmptyList_WhenAmenityDoesNotExist", false);
        var dbPath = await SqliteTestDatabase.CreateAsync(tempDir);
        var repository = CreateSut(dbPath);

        await SeedDefaultPoisAsync(repository);

        // Act
        var result = await repository.GetByAmenityAsync("nonexistent", 10, CancellationToken.None);

        // Assert
        Assert.Empty(result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    public async Task GetByAmenityAsync_Throws_WhenAmenityIsEmpty(string amenity)
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("GetByAmenityAsync_Throws_WhenAmenityIsEmpty", false);
        var dbPath = await SqliteTestDatabase.CreateAsync(tempDir);
        var repository = CreateSut(dbPath);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(async () =>
            await repository.GetByAmenityAsync(amenity, 10, CancellationToken.None));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetByAmenityAsync_Throws_WhenLimitIsInvalid(int limit)
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("GetByAmenityAsync_Throws_WhenLimitIsInvalid", false);
        var dbPath = await SqliteTestDatabase.CreateAsync(tempDir);
        var repository = CreateSut(dbPath);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(async () =>
            await repository.GetByAmenityAsync("restaurant", limit, CancellationToken.None));
    }

    [Fact]
    public async Task GetByAmenityQuery_UsesAmenityIndex()
    {
        await using var tempDir =
            TestTemporaryDirectories.Create("GetByAmenityQuery_UsesAmenityIndex");

        var dbPath = await SqliteTestDatabase.CreateAsync(tempDir);

        await using var connection =
            new SqliteConnection(SqliteTestDatabase.CreateConnectionString(dbPath));

        await connection.OpenAsync();

        await using var command = connection.CreateCommand();
        command.CommandText = """
        EXPLAIN QUERY PLAN
        SELECT id, osm_id, name, amenity, latitude, longitude
        FROM poi
        WHERE amenity = @amenity
        ORDER BY id
        LIMIT @limit;
        """;

        command.Parameters.AddWithValue("@amenity", "restaurant");
        command.Parameters.AddWithValue("@limit", 10);

        var details = new List<string>();

        await using var reader = await command.ExecuteReaderAsync();

        while (await reader.ReadAsync())
        {
            details.Add(reader.GetString(3));
        }

        Assert.Contains(details, detail =>
            detail.Contains("idx_poi_amenity", StringComparison.OrdinalIgnoreCase));
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
            new Poi(1, 1001, "Pizza Napoli", "restaurant", new GeoPoint(52.5201, 13.4051)),
            new Poi(3, 2001, "Cafe Berlin", "cafe", new GeoPoint(52.5202, 13.4052)),
            new Poi(2, 1002, "Burger House", "restaurant", new GeoPoint(52.5203, 13.4053)),
            new Poi(4, 3001, "Library Central", "library", new GeoPoint(52.5204, 13.4054)));
    }
}