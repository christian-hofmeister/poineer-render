using System.Security.Cryptography;
using System.Text;
using FluentAssertions;
using POIneer.Render.Infrastructure.FileSystem;
using POIneer.Render.TestHelpers;
using Xunit;

namespace POIneer.Render.IntegrationTests.Infrastructure.FileSystem;

// Exercises FileDatasetArtifactMetadataFactory against the real filesystem (issue #130).
public sealed class FileDatasetArtifactMetadataFactoryTests
{
    [Fact]
    public async Task CreateAsync_ReturnsMetadata_WithRegionIdVersionFileNameAndSize()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("artifact-metadata-basic-fields", false);
        var artifactPath = Path.Combine(tempDir.DirectoryPath, "poi.sqlite");
        var content = "some dataset bytes";
        TestFiles.WriteAllText(artifactPath, content);

        var sut = new FileDatasetArtifactMetadataFactory();

        // Act
        var metadata = await sut.CreateAsync("berlin", "1-abc123", artifactPath, CancellationToken.None);

        // Assert
        metadata.RegionId.Should().Be("berlin");
        metadata.Version.Should().Be("1-abc123");
        metadata.FileName.Should().Be("poi.sqlite");
        metadata.FileSizeBytes.Should().Be(Encoding.UTF8.GetByteCount(content));
    }

    [Fact]
    public async Task CreateAsync_ComputesTheCorrectSha256Checksum_ForTheArtifactContent()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("artifact-metadata-checksum", false);
        var artifactPath = Path.Combine(tempDir.DirectoryPath, "poi.sqlite");
        var content = "deterministic dataset content for hashing";
        TestFiles.WriteAllText(artifactPath, content);

        var expectedChecksum = Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

        var sut = new FileDatasetArtifactMetadataFactory();

        // Act
        var metadata = await sut.CreateAsync("berlin", "1-abc123", artifactPath, CancellationToken.None);

        // Assert
        metadata.Sha256Checksum.Should().Be(expectedChecksum);
    }

    [Fact]
    public async Task CreateAsync_ReturnsTheSameChecksum_AcrossRepeatedCalls_ForAnUnchangedFile()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("artifact-metadata-checksum-stable", false);
        var artifactPath = Path.Combine(tempDir.DirectoryPath, "poi.sqlite");
        TestFiles.WriteAllText(artifactPath, "unchanged dataset content");

        var sut = new FileDatasetArtifactMetadataFactory();

        // Act
        var first = await sut.CreateAsync("berlin", "1-abc123", artifactPath, CancellationToken.None);
        var second = await sut.CreateAsync("berlin", "1-abc123", artifactPath, CancellationToken.None);

        // Assert
        first.Sha256Checksum.Should().Be(second.Sha256Checksum);
    }

    [Fact]
    public async Task CreateAsync_SetsCreatedUtc_CloseToNow()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("artifact-metadata-created-utc", false);
        var artifactPath = Path.Combine(tempDir.DirectoryPath, "poi.sqlite");
        TestFiles.WriteAllText(artifactPath, "some dataset bytes");

        var sut = new FileDatasetArtifactMetadataFactory();
        var before = DateTimeOffset.UtcNow;

        // Act
        var metadata = await sut.CreateAsync("berlin", "1-abc123", artifactPath, CancellationToken.None);

        // Assert
        var after = DateTimeOffset.UtcNow;
        metadata.CreatedUtc.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public async Task CreateAsync_ThrowsFileNotFoundException_WhenArtifactDoesNotExist()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("artifact-metadata-missing-file", false);
        var missingPath = Path.Combine(tempDir.DirectoryPath, "does-not-exist.sqlite");

        var sut = new FileDatasetArtifactMetadataFactory();

        // Act
        var act = () => sut.CreateAsync("berlin", "1-abc123", missingPath, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Theory]
    [InlineData("", "1-abc123")]
    [InlineData("   ", "1-abc123")]
    [InlineData("berlin", "")]
    [InlineData("berlin", "   ")]
    public async Task CreateAsync_ThrowsArgumentException_WhenRegionIdOrVersionIsMissing(
        string regionId,
        string version)
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("artifact-metadata-invalid-arguments", false);
        var artifactPath = Path.Combine(tempDir.DirectoryPath, "poi.sqlite");
        TestFiles.WriteAllText(artifactPath, "some dataset bytes");

        var sut = new FileDatasetArtifactMetadataFactory();

        // Act
        var act = () => sut.CreateAsync(regionId, version, artifactPath, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
