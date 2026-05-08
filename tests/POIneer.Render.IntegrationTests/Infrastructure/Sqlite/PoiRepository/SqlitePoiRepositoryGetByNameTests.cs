using Microsoft.Data.Sqlite;
using POIneer.Render.Domain.Models;
using POIneer.Render.Infrastructure.Sqlite;
using POIneer.Render.TestHelpers;
using POIneer.Render.TestHelpers.Sqlite;
using Xunit;

namespace POIneer.Render.IntegrationTests.Infrastructure.Sqlite.PoiRepository;

public sealed class SqlitePoiRepositoryGetByNameTests
{
    [Fact]
    public async Task GetByNameAsync_ReturnsMatchingPois()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("GetByNameAsync_ReturnsMatchingPois", false);
        var dbPath = await SqliteTestDatabase.CreateAsync(tempDir);
        var repository = CreateSut(dbPath);

        await SeedDefaultPoisAsync(repository);

        // Act
        var pois = await repository.GetByNameAsync("Cafe Berlin", 10, CancellationToken.None);

        // Assert
        Assert.Single(pois);
        Assert.Equal("Cafe Berlin", pois[0].Name);
        Assert.Equal("cafe", pois[0].Amenity);
    }

    [Fact]
    public async Task GetByNameAsync_RespectsLimit()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("GetByNameAsync_RespectsLimit", false);
        var dbPath = await SqliteTestDatabase.CreateAsync(tempDir);
        var repository = CreateSut(dbPath);

        await SeedDefaultPoisAsync(repository);

        // Act
        var pois = await repository.GetByNameAsync("Two Same Names", 1, CancellationToken.None);

        // Assert
        Assert.Single(pois);
        Assert.Equal("Two Same Names", pois[0].Name);
    }

    [Fact]
    public async Task GetByNameAsync_ReturnsEmptyList_WhenNameDoesNotExist()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("GetByNameAsync_ReturnsEmptyList_WhenNameDoesNotExist", false);
        var dbPath = await SqliteTestDatabase.CreateAsync(tempDir);
        var repository = CreateSut(dbPath);

        await SeedDefaultPoisAsync(repository);

        // Act
        var pois = await repository.GetByNameAsync("Nonexistent Name", 10, CancellationToken.None);

        // Assert
        Assert.Empty(pois);
    }

    [Fact]
    public async Task GetByNameAsync_ThrowsArgumentException_WhenNameIsNullOrEmpty()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("GetByNameAsync_ThrowsArgumentException_WhenNameIsNullOrEmpty", false);
        var dbPath = await SqliteTestDatabase.CreateAsync(tempDir);
        var repository = CreateSut(dbPath);

        await SeedDefaultPoisAsync(repository);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetByNameAsync(null!, 10, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetByNameAsync(string.Empty, 10, CancellationToken.None));
        await Assert.ThrowsAsync<ArgumentException>(() => repository.GetByNameAsync("   ", 10, CancellationToken.None));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public async Task GetByNameAsync_ThrowsArgumentOutOfRangeException_WhenLimitIsNotPositive(int limit)
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("GetByNameAsync_ThrowsArgumentOutOfRangeException_WhenLimitIsNotPositive", false);
        var dbPath = await SqliteTestDatabase.CreateAsync(tempDir);
        var repository = CreateSut(dbPath);

        await SeedDefaultPoisAsync(repository);

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() => repository.GetByNameAsync("Cafe Berlin", limit, CancellationToken.None));
    }

    [Fact]
    public async Task GetByNameAsync_CanHandleMultiplePoisWithSameName()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("GetByNameAsync_CanHandleMultiplePoisWithSameName", false);
        var dbPath = await SqliteTestDatabase.CreateAsync(tempDir);
        var repository = CreateSut(dbPath);

        await SeedDefaultPoisAsync(repository);

        // Act
        var pois = await repository.GetByNameAsync("Two Same Names", 10, CancellationToken.None);

        // Assert
        Assert.Equal(2, pois.Count);
        Assert.All(pois, poi => Assert.Equal("Two Same Names", poi.Name));
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
            new Poi(2, 1002, "Two Same Names", "restaurant", new GeoPoint(52.5203, 13.4053)),
            new Poi(4, 3001, "Library Central", "library", new GeoPoint(52.5204, 13.4054)),
            new Poi(2, 4001, "Two Same Names", "museum", new GeoPoint(52.5205, 13.4055)));
    }
}