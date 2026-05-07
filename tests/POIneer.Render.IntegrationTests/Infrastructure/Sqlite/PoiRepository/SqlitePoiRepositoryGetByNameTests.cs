using POIneer.Render.TestHelpers;
using Xunit;

namespace POIneer.Render.IntegrationTests.Infrastructure.Sqlite.PoiRepository;

public sealed class SqlitePoiRepositoryGetByNameTests
{
    [Fact]
    public async Task GetByNameAsync_ReturnsMatchingPois()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("GetByNameAsync_ReturnsMatchingPois", false);
    }
}