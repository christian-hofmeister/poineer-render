using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using POIneer.Render.Application.Contracts;
using POIneer.Render.Infrastructure.FileSystem;
using POIneer.Render.TestHelpers;
using Xunit;

namespace POIneer.Render.IntegrationTests.Infrastructure.FileSystem;

// Exercises FilePublishedDatasetVerifier against the real filesystem, using the real
// FileDatasetArtifactMetadataFactory (issue #130) rather than a mock, so these tests also
// cover the two working together the way RenderRegion actually wires them (issue #135).
public sealed class FilePublishedDatasetVerifierTests
{
    // NullLogger, not a real LoggerFactory: none of these tests assert log output, and a
    // LoggerFactory instance would never be disposed here (see tests/README.md).
    private static readonly ILogger<FilePublishedDatasetVerifier> Logger =
        NullLogger<FilePublishedDatasetVerifier>.Instance;

    private static FilePublishedDatasetVerifier CreateSut() =>
        new(new FileDatasetArtifactMetadataFactory(), Logger);

    [Fact]
    public async Task VerifyAsync_ReturnsVerified_WhenDestinationMatchesExpectedMetadataExactly()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("verify-succeeds-on-exact-match", false);
        var sourcePath = Path.Combine(tempDir.DirectoryPath, "poi.sqlite");
        var content = "published dataset bytes";
        TestFiles.WriteAllText(sourcePath, content);

        var expectedMetadata = await new FileDatasetArtifactMetadataFactory()
            .CreateAsync("berlin", "1-abc123", sourcePath, CancellationToken.None);

        var destinationPath = Path.Combine(tempDir.DirectoryPath, "published", "poi.sqlite");
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        TestFiles.WriteAllText(destinationPath, content);

        var sut = CreateSut();

        // Act
        var result = await sut.VerifyAsync(expectedMetadata, destinationPath, CancellationToken.None);

        // Assert
        result.IsVerified.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task VerifyAsync_ReturnsNotVerified_WhenDestinationDoesNotExist()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("verify-fails-when-destination-missing", false);
        var sourcePath = Path.Combine(tempDir.DirectoryPath, "poi.sqlite");
        TestFiles.WriteAllText(sourcePath, "published dataset bytes");

        var expectedMetadata = await new FileDatasetArtifactMetadataFactory()
            .CreateAsync("berlin", "1-abc123", sourcePath, CancellationToken.None);

        var missingDestinationPath = Path.Combine(tempDir.DirectoryPath, "published", "poi.sqlite");

        var sut = CreateSut();

        // Act
        var result = await sut.VerifyAsync(expectedMetadata, missingDestinationPath, CancellationToken.None);

        // Assert
        result.IsVerified.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains(missingDestinationPath));
    }

    [Fact]
    public async Task VerifyAsync_ReturnsNotVerified_WhenFileSizeDiffersFromExpectedMetadata()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("verify-fails-on-size-mismatch", false);
        var sourcePath = Path.Combine(tempDir.DirectoryPath, "poi.sqlite");
        TestFiles.WriteAllText(sourcePath, "short");

        var expectedMetadata = await new FileDatasetArtifactMetadataFactory()
            .CreateAsync("berlin", "1-abc123", sourcePath, CancellationToken.None);

        var destinationPath = Path.Combine(tempDir.DirectoryPath, "published", "poi.sqlite");
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        TestFiles.WriteAllText(destinationPath, "a much longer published payload");

        var sut = CreateSut();

        // Act
        var result = await sut.VerifyAsync(expectedMetadata, destinationPath, CancellationToken.None);

        // Assert
        result.IsVerified.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("size mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task VerifyAsync_ReturnsNotVerified_WhenChecksumDiffersFromExpectedMetadata_ButSizeMatches()
    {
        // Arrange: same length, different bytes - proves the checksum comparison catches
        // corruption that a size-only check would miss.
        await using var tempDir = TestTemporaryDirectories.Create("verify-fails-on-checksum-mismatch", false);
        var sourcePath = Path.Combine(tempDir.DirectoryPath, "poi.sqlite");
        TestFiles.WriteAllText(sourcePath, "AAAAAAAAAA");

        var expectedMetadata = await new FileDatasetArtifactMetadataFactory()
            .CreateAsync("berlin", "1-abc123", sourcePath, CancellationToken.None);

        var destinationPath = Path.Combine(tempDir.DirectoryPath, "published", "poi.sqlite");
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        TestFiles.WriteAllText(destinationPath, "BBBBBBBBBB");

        var sut = CreateSut();

        // Act
        var result = await sut.VerifyAsync(expectedMetadata, destinationPath, CancellationToken.None);

        // Assert
        result.IsVerified.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains("checksum mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task VerifyAsync_ReportsBothMismatches_WhenSizeAndChecksumBothDiffer()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("verify-reports-all-mismatches", false);
        var sourcePath = Path.Combine(tempDir.DirectoryPath, "poi.sqlite");
        TestFiles.WriteAllText(sourcePath, "short");

        var expectedMetadata = await new FileDatasetArtifactMetadataFactory()
            .CreateAsync("berlin", "1-abc123", sourcePath, CancellationToken.None);

        var destinationPath = Path.Combine(tempDir.DirectoryPath, "published", "poi.sqlite");
        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
        TestFiles.WriteAllText(destinationPath, "a completely different and longer payload");

        var sut = CreateSut();

        // Act
        var result = await sut.VerifyAsync(expectedMetadata, destinationPath, CancellationToken.None);

        // Assert: both problems are surfaced in one pass rather than only the first found -
        // an operator diagnosing a failed publish should not have to re-run verification
        // repeatedly to see every mismatch.
        result.IsVerified.Should().BeFalse();
        result.Errors.Should().HaveCount(2);
    }

    [Fact]
    public async Task VerifyAsync_ThrowsArgumentNullException_WhenExpectedMetadataIsNull()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("verify-rejects-null-metadata", false);
        var destinationPath = Path.Combine(tempDir.DirectoryPath, "poi.sqlite");
        TestFiles.WriteAllText(destinationPath, "some bytes");

        var sut = CreateSut();

        // Act
        var act = () => sut.VerifyAsync(null!, destinationPath, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public async Task VerifyAsync_ThrowsArgumentException_WhenDestinationPathIsMissing(string destinationPath)
    {
        // Arrange
        var expectedMetadata = new DatasetArtifactMetadata(
            RegionId: "berlin",
            Version: "1-abc123",
            FileName: "poi.sqlite",
            FileSizeBytes: 5,
            CreatedUtc: DateTimeOffset.UtcNow,
            Sha256Checksum: "irrelevant-for-this-test");

        var sut = CreateSut();

        // Act
        var act = () => sut.VerifyAsync(expectedMetadata, destinationPath, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }
}
