using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using POIneer.Render.Application.Contracts;
using POIneer.Render.Infrastructure.FileSystem;
using POIneer.Render.TestHelpers;
using Xunit;

namespace POIneer.Render.UnitTests.Infrastructure.FileSystem;

public sealed class FileRegionRenderStateStoreTests
{
    private static readonly ILogger<FileRegionRenderStateStore> _logger =
    NullLogger<FileRegionRenderStateStore>.Instance;

    [Fact]
    public async Task ReadAsync_ReturnsNull_WhenStateFileDoesNotExist()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("missing-render-state", false);
        var sut = new FileRegionRenderStateStore(_logger);

        // Act
        var state = await sut.ReadAsync(Path.Combine(tempDir.DirectoryPath, "render-state.json"));

        // Assert
        state.Should().BeNull();
    }

    [Fact]
    public async Task ReadAsync_ReturnsNull_WhenStateFileContainsInvalidJson()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("corrupt-render-state", false);
        var statePath = Path.Combine(tempDir.DirectoryPath, "render-state.json");
        TestFiles.WriteAllText(statePath, "{ partially-written");
        var sut = new FileRegionRenderStateStore(_logger);

        // Act
        var state = await sut.ReadAsync(statePath);

        // Assert
        state.Should().BeNull();
    }

    [Fact]
    public async Task WriteAsync_AndReadAsync_RoundTripState()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("roundtrip-render-state", false);
        var statePath = Path.Combine(tempDir.DirectoryPath, "berlin", "render-state.json");
        var expectedState = new RegionRenderState(
            RegionId: "berlin",
            PbfUrl: "https://example.com/berlin.osm.pbf",
            LastProcessedMetadata: new RegionUpdateMetadata(
                ETag: "\"abc\"",
                LastModified: DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
                ContentLength: 42),
            ProcessedAtUtc: DateTimeOffset.Parse("2026-01-02T00:00:00Z"));
        var sut = new FileRegionRenderStateStore(_logger);

        // Act
        await sut.WriteAsync(statePath, expectedState);
        var actualState = await sut.ReadAsync(statePath);

        // Assert
        actualState.Should().Be(expectedState);
    }
}
