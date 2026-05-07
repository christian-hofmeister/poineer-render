using POIneer.Render.TestHelpers;
using Xunit;

namespace POIneer.Render.IntegrationTests.Infrastructure.Sqlite.PoiRepository;

public sealed class SqlitePoiRepositoryGetByAmenityTests
{
    [Fact]
    public async Task GetByAmenityAsync_ReturnsMatchingPois()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("GetByAmenityAsync_ReturnsMatchingPois", false);


        // Act

        // Assert
    }

    [Fact]
    public async Task GetByAmenityAsync_RespectsLimit()
    {
        // Arrange

        // Act

        // Assert
    }

    [Fact]
    public async Task GetByAmenityAsync_ReturnsEmptyList_WhenAmenityDoesNotExist()
    {
        // Arrange

        // Act

        // Assert
    }
}