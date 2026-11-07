using FluentAssertions;
using POIneer.Render.Infrastructure.FileSystem;
using POIneer.Render.TestHelpers;
using Xunit;

namespace POIneer.Render.IntegrationTests.Infrastructure.FileSystem;

// Exercises FileHashDatasetVersionCalculator against the real filesystem. The whole point
// of this type is that identical input always produces the identical version - that's what
// lets IDatasetPublisher handle republishing unchanged data idempotently under the
// configured overwrite policy - so these tests focus on that stability property rather than on the
// exact hash algorithm/format.
public sealed class FileHashDatasetVersionCalculatorTests
{
    [Fact]
    public async Task CalculateAsync_ReturnsTheSameVersion_ForByteIdenticalFiles()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("version-calculator-stable-for-identical-content", false);
        var pathA = Path.Combine(tempDir.DirectoryPath, "a.sqlite");
        var pathB = Path.Combine(tempDir.DirectoryPath, "b.sqlite");
        TestFiles.WriteAllText(pathA, "identical sqlite bytes");
        TestFiles.WriteAllText(pathB, "identical sqlite bytes");

        var sut = new FileHashDatasetVersionCalculator(TestOptionsFactory.CreatePublisherOptions("unused-destination-dir"));

        // Act
        var versionA = await sut.CalculateAsync(new[] { pathA }, CancellationToken.None);
        var versionB = await sut.CalculateAsync(new[] { pathB }, CancellationToken.None);

        // Assert
        versionA.Should().Be(versionB);
    }

    [Fact]
    public async Task CalculateAsync_ReturnsTheSameVersion_AcrossRepeatedCalls_ForTheSameFile()
    {
        // Arrange: unchanged artifact bytes must retain the same version.
        await using var tempDir = TestTemporaryDirectories.Create("version-calculator-stable-across-repeated-calls", false);
        var path = Path.Combine(tempDir.DirectoryPath, "berlin.sqlite");
        TestFiles.WriteAllText(path, "some sqlite bytes");

        var sut = new FileHashDatasetVersionCalculator(TestOptionsFactory.CreatePublisherOptions("unused-destination-dir"));

        // Act
        var first = await sut.CalculateAsync(new[] { path }, CancellationToken.None);
        var second = await sut.CalculateAsync(new[] { path }, CancellationToken.None);

        // Assert
        first.Should().Be(second);
    }

    [Fact]
    public async Task CalculateAsync_ReturnsADifferentVersion_WhenFileContentDiffers()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("version-calculator-differs-for-different-content", false);
        var pathA = Path.Combine(tempDir.DirectoryPath, "a.sqlite");
        var pathB = Path.Combine(tempDir.DirectoryPath, "b.sqlite");
        TestFiles.WriteAllText(pathA, "old osm extract");
        TestFiles.WriteAllText(pathB, "new osm extract with an update");

        var sut = new FileHashDatasetVersionCalculator(TestOptionsFactory.CreatePublisherOptions("unused-destination-dir"));

        // Act
        var versionA = await sut.CalculateAsync(new[] { pathA }, CancellationToken.None);
        var versionB = await sut.CalculateAsync(new[] { pathB }, CancellationToken.None);

        // Assert
        versionA.Should().NotBe(versionB);
    }

    [Fact]
    public async Task CalculateAsync_ReturnsADifferentVersion_WhenSchemaVersionDiffers_ForTheSameFile()
    {
        // Arrange: a deliberate SchemaVersion bump must force a new version even though the
        // artifact content is completely unchanged.
        await using var tempDir = TestTemporaryDirectories.Create("version-calculator-differs-for-different-schema-version", false);
        var path = Path.Combine(tempDir.DirectoryPath, "berlin.sqlite");
        TestFiles.WriteAllText(path, "same sqlite bytes");

        var sutV1 = new FileHashDatasetVersionCalculator(
            TestOptionsFactory.CreatePublisherOptions("unused-destination-dir", schemaVersion: "1"));
        var sutV2 = new FileHashDatasetVersionCalculator(
            TestOptionsFactory.CreatePublisherOptions("unused-destination-dir", schemaVersion: "2"));

        // Act
        var versionV1 = await sutV1.CalculateAsync(new[] { path }, CancellationToken.None);
        var versionV2 = await sutV2.CalculateAsync(new[] { path }, CancellationToken.None);

        // Assert
        versionV1.Should().NotBe(versionV2);
    }

    [Fact]
    public async Task CalculateAsync_IncludesTheConfiguredSchemaVersion_InTheResult()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("version-calculator-includes-schema-version", false);
        var path = Path.Combine(tempDir.DirectoryPath, "berlin.sqlite");
        TestFiles.WriteAllText(path, "sqlite bytes");

        var sut = new FileHashDatasetVersionCalculator(
            TestOptionsFactory.CreatePublisherOptions("unused-destination-dir", schemaVersion: "42"));

        // Act
        var version = await sut.CalculateAsync(new[] { path }, CancellationToken.None);

        // Assert
        version.Should().StartWith("42-");
    }

    [Fact]
    public async Task CalculateAsync_ThrowsFileNotFoundException_WhenSourceDoesNotExist()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("version-calculator-throws-when-source-missing", false);
        var missingPath = Path.Combine(tempDir.DirectoryPath, "does-not-exist.sqlite");
        var sut = new FileHashDatasetVersionCalculator(TestOptionsFactory.CreatePublisherOptions("unused-destination-dir"));

        // Act
        var act = () => sut.CalculateAsync(new[] { missingPath }, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("pmtiles")]
    public async Task CalculateAsync_ChangesSharedVersion_WhenEitherArtifactChanges(string changedType)
    {
        await using var tempDir = TestTemporaryDirectories.Create("release-version-change", false);
        var sqlite = Path.Combine(tempDir.DirectoryPath, "poi.sqlite");
        var tiles = Path.Combine(tempDir.DirectoryPath, "map.pmtiles");
        TestFiles.WriteAllText(sqlite, "sqlite with initial Flyway history");
        TestFiles.WriteAllText(tiles, "tiles");
        var sut = new FileHashDatasetVersionCalculator(TestOptionsFactory.CreatePublisherOptions("unused"));
        var before = await sut.CalculateAsync(new[] { sqlite, tiles });

        TestFiles.WriteAllText(changedType == "sqlite" ? sqlite : tiles, "changed artifact bytes");
        var after = await sut.CalculateAsync(new[] { sqlite, tiles });

        after.Should().NotBe(before);
    }

    [Fact]
    public async Task CalculateAsync_IsIndependentOfOrderAndDirectory_ButIncludesArtifactTypesAndSet()
    {
        await using var tempDir = TestTemporaryDirectories.Create("release-version-order", false);
        var sqlite = Path.Combine(tempDir.DirectoryPath, "poi.sqlite");
        var tiles = Path.Combine(tempDir.DirectoryPath, "map.pmtiles");
        var copiedSqlite = Path.Combine(tempDir.DirectoryPath, "copy", "renamed.sqlite");
        TestFiles.WriteAllText(sqlite, "sqlite");
        TestFiles.WriteAllText(tiles, "tiles");
        TestFiles.WriteAllText(copiedSqlite, "sqlite");
        var sut = new FileHashDatasetVersionCalculator(TestOptionsFactory.CreatePublisherOptions("unused"));

        var both = await sut.CalculateAsync(new[] { sqlite, tiles });
        var reversedCopy = await sut.CalculateAsync(new[] { tiles, copiedSqlite });
        var sqliteOnly = await sut.CalculateAsync(new[] { sqlite });
        TestFiles.WriteAllText(sqlite, "tiles");
        TestFiles.WriteAllText(tiles, "sqlite");
        var swappedTypes = await sut.CalculateAsync(new[] { sqlite, tiles });

        reversedCopy.Should().Be(both);
        sqliteOnly.Should().NotBe(both);
        swappedTypes.Should().NotBe(both);
    }

    [Fact]
    public async Task CalculateAsync_RejectsEmptyOrDuplicateArtifactTypes()
    {
        var sut = new FileHashDatasetVersionCalculator(TestOptionsFactory.CreatePublisherOptions("unused"));
        var empty = () => sut.CalculateAsync(Array.Empty<string>());
        var duplicate = () => sut.CalculateAsync(new[] { "poi.sqlite", "copy.sqlite" });

        await empty.Should().ThrowAsync<ArgumentException>();
        await duplicate.Should().ThrowAsync<ArgumentException>();
    }
}
