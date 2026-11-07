using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using POIneer.Render.Application.Contracts;
using POIneer.Render.Infrastructure.FileSystem;
using POIneer.Render.TestHelpers;
using Xunit;

namespace POIneer.Render.UnitTests.Infrastructure.FileSystem;

public sealed class FileRegionDownloadStateStoreTests
{
    public static ILogger<FileRegionDownloadStateStore> Logger { get; } = NullLogger<FileRegionDownloadStateStore>.Instance;

    [Fact]
    public async Task ReadAsync_ReturnsNull_WhenStateFileDoesNotExist()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("missing-download-state", false);
        var sut = new FileRegionDownloadStateStore(Logger);

        // Act
        var state = await sut.ReadAsync(Path.Combine(tempDir.DirectoryPath, "download-state.json"));

        // Assert
        state.Should().BeNull();
    }

    [Fact]
    public async Task ReadAsync_ReturnsNull_WhenStateFileContainsInvalidJson()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("corrupt-download-state", false);
        var statePath = Path.Combine(tempDir.DirectoryPath, "download-state.json");
        TestFiles.WriteAllText(statePath, "{ partially-written");
        var sut = new FileRegionDownloadStateStore(Logger);

        // Act
        var state = await sut.ReadAsync(statePath);

        // Assert
        state.Should().BeNull();
    }

    [Fact]
    public async Task ReadAsync_ReturnsNull_WhenStateFileIsIncomplete()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("incomplete-download-state", false);
        var statePath = Path.Combine(tempDir.DirectoryPath, "download-state.json");
        TestFiles.WriteAllText(statePath, "{}");
        var sut = new FileRegionDownloadStateStore(Logger);

        // Act
        var state = await sut.ReadAsync(statePath);

        // Assert
        state.Should().BeNull();
    }

    [Fact]
    public async Task WriteAsync_AndReadAsync_RoundTripState()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("roundtrip-download-state", false);
        var statePath = Path.Combine(tempDir.DirectoryPath, "berlin", "download-state.json");
        var expectedState = new RegionDownloadState(
            RegionId: "berlin",
            PbfUrl: "https://example.com/berlin.osm.pbf",
            DownloadedMetadata: new RegionUpdateMetadata(
                ETag: "\"abc\"",
                LastModified: DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
                ContentLength: 42),
            DownloadedAtUtc: DateTimeOffset.Parse("2026-01-02T00:00:00Z"));
        var sut = new FileRegionDownloadStateStore(Logger);

        // Act
        await sut.WriteAsync(statePath, expectedState);
        var actualState = await sut.ReadAsync(statePath);

        // Assert
        actualState.Should().Be(expectedState);
    }

    [Fact]
    public async Task WriteAsync_ReplacesExistingState_AndDoesNotLeaveTemporaryFile()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("replace-download-state", false);
        var statePath = Path.Combine(tempDir.DirectoryPath, "berlin", "download-state.json");
        var oldState = new RegionDownloadState(
            RegionId: "berlin",
            PbfUrl: "https://example.com/berlin-old.osm.pbf",
            DownloadedMetadata: new RegionUpdateMetadata(
                ETag: "\"old\"",
                LastModified: DateTimeOffset.Parse("2026-01-01T00:00:00Z"),
                ContentLength: 42),
            DownloadedAtUtc: DateTimeOffset.Parse("2026-01-02T00:00:00Z"));
        var newState = oldState with
        {
            PbfUrl = "https://example.com/berlin-new.osm.pbf",
            DownloadedMetadata = oldState.DownloadedMetadata with { ETag = "\"new\"" },
            DownloadedAtUtc = DateTimeOffset.Parse("2026-01-03T00:00:00Z")
        };
        var sut = new FileRegionDownloadStateStore(Logger);

        // Act
        await sut.WriteAsync(statePath, oldState);
        await sut.WriteAsync(statePath, newState);
        var actualState = await sut.ReadAsync(statePath);

        // Assert
        actualState.Should().Be(newState);
        Directory.GetFiles(Path.GetDirectoryName(statePath)!, "*.tmp").Should().BeEmpty();
    }
}
