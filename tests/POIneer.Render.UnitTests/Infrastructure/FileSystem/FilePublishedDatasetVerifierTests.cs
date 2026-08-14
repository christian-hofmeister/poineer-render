using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using POIneer.Render.Application.Contracts;
using POIneer.Render.Application.Ports;
using POIneer.Render.Infrastructure.FileSystem;
using POIneer.Render.TestHelpers;
using Xunit;

namespace POIneer.Render.UnitTests.Infrastructure.FileSystem;

// Covers FilePublishedDatasetVerifier's behavior around a mocked IDatasetArtifactMetadataFactory
// - specifically the race between the File.Exists check and the CreateAsync call in
// VerifyAsync, which needs no real filesystem/hashing behavior to exercise (that side is
// already covered by the real-filesystem integration tests). A real temp file is still used
// for destinationPath so the initial File.Exists check passes and CreateAsync is actually
// reached.
public sealed class FilePublishedDatasetVerifierTests
{
    // NullLogger, not a real LoggerFactory: none of these tests assert log output, and a
    // LoggerFactory instance would never be disposed here (see tests/README.md).
    private static readonly ILogger<FilePublishedDatasetVerifier> Logger =
        NullLogger<FilePublishedDatasetVerifier>.Instance;

    private static DatasetArtifactMetadata CreateExpectedMetadata() => new(
        RegionId: "berlin",
        Version: "1-abc123",
        FileName: "poi.sqlite",
        FileSizeBytes: 42,
        CreatedUtc: DateTimeOffset.UtcNow,
        Sha256Checksum: "expected-checksum");

    [Theory]
    [InlineData(typeof(FileNotFoundException))]
    [InlineData(typeof(IOException))]
    [InlineData(typeof(UnauthorizedAccessException))]
    public async Task VerifyAsync_ReturnsNotVerified_WhenMetadataFactoryThrows_InsteadOfPropagating(Type exceptionType)
    {
        // Arrange: regression test for the Copilot review finding on PR #144 - the published
        // artifact can disappear or become unreadable between the File.Exists check and the
        // CreateAsync call below (a concurrent publish, external cleanup, a permissions/disk
        // issue). That must surface as a DatasetVerificationResult failure, not an exception
        // that bypasses RenderRegion's verification-failure handling entirely.
        await using var tempDir = TestTemporaryDirectories.Create("verify-converts-race-exception", false);
        var destinationPath = Path.Combine(tempDir.DirectoryPath, "poi.sqlite");
        TestFiles.WriteAllText(destinationPath, "present for the File.Exists check");

        var metadataFactory = Substitute.For<IDatasetArtifactMetadataFactory>();
        var exception = (Exception)Activator.CreateInstance(exceptionType, "simulated read failure")!;

        metadataFactory
            .CreateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<DatasetArtifactMetadata>(_ => throw exception);

        var sut = new FilePublishedDatasetVerifier(metadataFactory, Logger);
        var expectedMetadata = CreateExpectedMetadata();

        // Act: if the exception were left to propagate, this await would throw and fail the
        // test right here instead of reaching the assertions below.
        var result = await sut.VerifyAsync(expectedMetadata, destinationPath, CancellationToken.None);

        // Assert
        result.IsVerified.Should().BeFalse();
        result.Errors.Should().ContainSingle(e => e.Contains(destinationPath) && e.Contains("could not be read"));
    }

    [Fact]
    public async Task VerifyAsync_PropagatesUnexpectedExceptions_FromTheMetadataFactory()
    {
        // Arrange: only the specific, expected operational failure modes (missing/unreadable
        // file) are converted to a result - anything else (e.g. a programming error inside
        // the factory) should still surface as an exception rather than being silently
        // swallowed into a generic verification failure.
        await using var tempDir = TestTemporaryDirectories.Create("verify-propagates-unexpected-exceptions", false);
        var destinationPath = Path.Combine(tempDir.DirectoryPath, "poi.sqlite");
        TestFiles.WriteAllText(destinationPath, "present for the File.Exists check");

        var metadataFactory = Substitute.For<IDatasetArtifactMetadataFactory>();

        metadataFactory
            .CreateAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns<DatasetArtifactMetadata>(_ => throw new InvalidOperationException("unexpected"));

        var sut = new FilePublishedDatasetVerifier(metadataFactory, Logger);

        // Act
        var act = () => sut.VerifyAsync(CreateExpectedMetadata(), destinationPath, CancellationToken.None);

        // Assert
        await act.Should().ThrowAsync<InvalidOperationException>();
    }
}
