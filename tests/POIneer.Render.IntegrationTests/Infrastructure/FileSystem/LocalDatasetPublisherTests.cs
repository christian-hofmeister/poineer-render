using FluentAssertions;
using Microsoft.Extensions.Logging;
using POIneer.Render.Application.Contracts;
using POIneer.Render.Infrastructure.FileSystem;
using POIneer.Render.TestHelpers;
using Xunit;

namespace POIneer.Render.IntegrationTests.Infrastructure.FileSystem;

// Exercises LocalDatasetPublisher against the real filesystem. Source/destination
// directories are isolated per test via TestTemporaryDirectories, mirroring the
// conventions established by FileSingleInstanceLockTests.
public sealed class LocalDatasetPublisherTests
{
    private static readonly ILogger<LocalDatasetPublisher> Logger =
        new LoggerFactory().CreateLogger<LocalDatasetPublisher>();

    [Fact]
    public async Task PublishAsync_CopiesArtifact_ToDestination_WithRegionAndVersionInFilename()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("publish-copies-artifact", false);
        var sourcePath = Path.Combine(tempDir.DirectoryPath, "poi.sqlite");
        TestFiles.WriteAllText(sourcePath, "dataset bytes");

        var destinationDir = Path.Combine(tempDir.DirectoryPath, "publish");
        var sut = new LocalDatasetPublisher(Logger, TestOptionsFactory.CreatePublisherOptions(destinationDir));

        var request = new DatasetPublishRequest("berlin", "20260101000000000", sourcePath);

        // Act
        var result = await sut.PublishAsync(request, CancellationToken.None);

        // Assert
        var expectedDestinationPath = Path.GetFullPath(
            Path.Combine(destinationDir, "berlin", "berlin.20260101000000000.sqlite"));

        result.DestinationPath.Should().Be(expectedDestinationPath);
        result.WasSkipped.Should().BeFalse();
        File.Exists(expectedDestinationPath).Should().BeTrue();
        (await File.ReadAllTextAsync(expectedDestinationPath)).Should().Be("dataset bytes");
    }

    [Fact]
    public async Task PublishAsync_CreatesMissingDestinationDirectory()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("publish-creates-destination-dir", false);
        var sourcePath = Path.Combine(tempDir.DirectoryPath, "poi.sqlite");
        TestFiles.WriteAllText(sourcePath, "dataset bytes");

        var destinationDir = Path.Combine(tempDir.DirectoryPath, "nested", "publish", "dir");
        var sut = new LocalDatasetPublisher(Logger, TestOptionsFactory.CreatePublisherOptions(destinationDir));

        // Act
        var result = await sut.PublishAsync(
            new DatasetPublishRequest("berlin", "20260101000000000", sourcePath),
            CancellationToken.None);

        // Assert
        File.Exists(result.DestinationPath).Should().BeTrue();
    }

    [Fact]
    public async Task PublishAsync_LeavesTheSourceFile_InPlace()
    {
        // Arrange: publishing copies the already-canonical render output - it must not
        // remove it, since it still needs to remain the canonical file in outDir.
        await using var tempDir = TestTemporaryDirectories.Create("publish-leaves-source-in-place", false);
        var sourcePath = Path.Combine(tempDir.DirectoryPath, "poi.sqlite");
        TestFiles.WriteAllText(sourcePath, "dataset bytes");

        var destinationDir = Path.Combine(tempDir.DirectoryPath, "publish");
        var sut = new LocalDatasetPublisher(Logger, TestOptionsFactory.CreatePublisherOptions(destinationDir));

        // Act
        await sut.PublishAsync(
            new DatasetPublishRequest("berlin", "20260101000000000", sourcePath),
            CancellationToken.None);

        // Assert
        File.Exists(sourcePath).Should().BeTrue();
    }

    [Fact]
    public async Task PublishAsync_SkipsExistingDestination_WhenOverwritePolicyIsSkip()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("publish-skips-when-policy-is-skip", false);
        var sourcePath = Path.Combine(tempDir.DirectoryPath, "poi.sqlite");
        TestFiles.WriteAllText(sourcePath, "new dataset bytes");

        var destinationDir = Path.Combine(tempDir.DirectoryPath, "publish");
        var sut = new LocalDatasetPublisher(
            Logger,
            TestOptionsFactory.CreatePublisherOptions(destinationDir, DatasetPublishOverwritePolicy.Skip));

        var request = new DatasetPublishRequest("berlin", "20260101000000000", sourcePath);

        // Publish once, then change the source and publish the same request again.
        var first = await sut.PublishAsync(request, CancellationToken.None);
        TestFiles.WriteAllText(sourcePath, "changed dataset bytes");

        // Act
        var second = await sut.PublishAsync(request, CancellationToken.None);

        // Assert
        second.WasSkipped.Should().BeTrue();
        second.DestinationPath.Should().Be(first.DestinationPath);
        (await File.ReadAllTextAsync(second.DestinationPath)).Should().Be("new dataset bytes");
    }

    [Fact]
    public async Task PublishAsync_OverwritesExistingDestination_WhenOverwritePolicyIsOverwrite()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("publish-overwrites-when-policy-is-overwrite", false);
        var sourcePath = Path.Combine(tempDir.DirectoryPath, "poi.sqlite");
        TestFiles.WriteAllText(sourcePath, "new dataset bytes");

        var destinationDir = Path.Combine(tempDir.DirectoryPath, "publish");
        var sut = new LocalDatasetPublisher(
            Logger,
            TestOptionsFactory.CreatePublisherOptions(destinationDir, DatasetPublishOverwritePolicy.Overwrite));

        var request = new DatasetPublishRequest("berlin", "20260101000000000", sourcePath);

        var first = await sut.PublishAsync(request, CancellationToken.None);
        TestFiles.WriteAllText(sourcePath, "changed dataset bytes");

        // Act
        var second = await sut.PublishAsync(request, CancellationToken.None);

        // Assert
        second.WasSkipped.Should().BeFalse();
        second.DestinationPath.Should().Be(first.DestinationPath);
        (await File.ReadAllTextAsync(second.DestinationPath)).Should().Be("changed dataset bytes");
    }

    [Fact]
    public async Task PublishAsync_ThrowsIOException_WhenOverwritePolicyIsFail_AndDestinationAlreadyExists()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("publish-throws-when-policy-is-fail", false);
        var sourcePath = Path.Combine(tempDir.DirectoryPath, "poi.sqlite");
        TestFiles.WriteAllText(sourcePath, "dataset bytes");

        var destinationDir = Path.Combine(tempDir.DirectoryPath, "publish");
        var sut = new LocalDatasetPublisher(
            Logger,
            TestOptionsFactory.CreatePublisherOptions(destinationDir, DatasetPublishOverwritePolicy.Fail));

        var request = new DatasetPublishRequest("berlin", "20260101000000000", sourcePath);
        await sut.PublishAsync(request, CancellationToken.None);

        // Act
        var act = () => sut.PublishAsync(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<IOException>();
    }

    [Fact]
    public async Task PublishAsync_ThrowsFileNotFoundException_WhenSourceDoesNotExist()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("publish-throws-when-source-missing", false);
        var missingSourcePath = Path.Combine(tempDir.DirectoryPath, "does-not-exist.sqlite");
        var destinationDir = Path.Combine(tempDir.DirectoryPath, "publish");
        var sut = new LocalDatasetPublisher(Logger, TestOptionsFactory.CreatePublisherOptions(destinationDir));

        var request = new DatasetPublishRequest("berlin", "20260101000000000", missingSourcePath);

        // Act
        var act = () => sut.PublishAsync(request, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<FileNotFoundException>();
    }

    [Fact]
    public async Task PublishAsync_DoesNotLeaveAStagingFile_BehindOnSuccess()
    {
        // Arrange: PublishAsync copies through a ".tmp" staging file before renaming it
        // into place - confirm that staging file is gone once publishing succeeds.
        await using var tempDir = TestTemporaryDirectories.Create("publish-cleans-up-staging-file", false);
        var sourcePath = Path.Combine(tempDir.DirectoryPath, "poi.sqlite");
        TestFiles.WriteAllText(sourcePath, "dataset bytes");

        var destinationDir = Path.Combine(tempDir.DirectoryPath, "publish");
        var sut = new LocalDatasetPublisher(Logger, TestOptionsFactory.CreatePublisherOptions(destinationDir));

        // Act
        var result = await sut.PublishAsync(
            new DatasetPublishRequest("berlin", "20260101000000000", sourcePath),
            CancellationToken.None);

        // Assert
        File.Exists(result.DestinationPath + ".tmp").Should().BeFalse();
    }
}
