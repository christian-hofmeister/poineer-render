using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using POIneer.Render.Application.Contracts;
using POIneer.Render.Application.Options;
using POIneer.Render.Infrastructure.FileSystem;
using POIneer.Render.TestHelpers;
using Xunit;

namespace POIneer.Render.IntegrationTests.Infrastructure.FileSystem;

public sealed class MultiArtifactPublishingTests
{
    [Fact]
    public async Task VerifyAsync_RejectsWrongArtifactType_EvenWhenBytesMatch()
    {
        await using var temp = TestTemporaryDirectories.Create("artifact-type-mismatch", false);
        var source = Path.Combine(temp.DirectoryPath, "poi.sqlite");
        var destination = Path.Combine(temp.DirectoryPath, "map.pmtiles");
        await File.WriteAllTextAsync(source, "identical bytes");
        File.Copy(source, destination);
        var factory = new FileDatasetArtifactMetadataFactory();
        var metadata = await factory.CreateAsync("berlin", "2-252c39aba2cb1705", source);
        var verifier = new FilePublishedDatasetVerifier(factory, NullLogger<FilePublishedDatasetVerifier>.Instance);

        var result = await verifier.VerifyAsync(metadata, destination);

        result.IsVerified.Should().BeFalse();
        result.Errors.Should().ContainSingle(error => error.Contains("Artifact type mismatch"));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task PublishAsync_PreservesAndVerifiesArtifacts_AndSkipsIdenticalCopies(bool includeTiles)
    {
        await using var temp = TestTemporaryDirectories.Create("multi-artifact-publishing", false);
        var root = temp.CreateSubDir("publish").DirectoryPath;
        var publisher = new LocalDatasetPublisher(NullLogger<LocalDatasetPublisher>.Instance,
            Options.Create(new PublisherOptions { DestinationDir = root, OverwritePolicy = DatasetPublishOverwritePolicy.SkipIfIdentical }));
        var factory = new FileDatasetArtifactMetadataFactory();
        var verifier = new FilePublishedDatasetVerifier(factory, NullLogger<FilePublishedDatasetVerifier>.Instance);
        const string region = "geofabrik/europe/germany/berlin";
        const string version = "2-252c39aba2cb1705";
        var extensions = includeTiles ? new[] { "sqlite", "pmtiles" } : new[] { "sqlite" };

        foreach (var extension in extensions)
        {
            var source = Path.Combine(temp.DirectoryPath, $"source.{extension}");
            await File.WriteAllTextAsync(source, $"artifact bytes for {extension}");
            var metadata = await factory.CreateAsync(region, version, source);
            metadata.ArtifactType.Should().Be(extension == "sqlite" ? DatasetArtifactType.Sqlite : DatasetArtifactType.Pmtiles);
            metadata.FileSizeBytes.Should().Be(new FileInfo(source).Length);
            metadata.Sha256Checksum.Should().MatchRegex("^[a-f0-9]{64}$");

            var request = new DatasetPublishRequest(region, version, source);
            var result = await publisher.PublishAsync(request);
            result.WasSkipped.Should().BeFalse();
            result.DestinationPath.Should().Be(Path.GetFullPath(Path.Combine(root,
                "geofabrik", "europe", "germany", "berlin", $"berlin.{version}.{extension}")));
            (await verifier.VerifyAsync(metadata, result.DestinationPath)).IsVerified.Should().BeTrue();

            var timestamp = File.GetLastWriteTimeUtc(result.DestinationPath);
            (await publisher.PublishAsync(request)).WasSkipped.Should().BeTrue();
            File.GetLastWriteTimeUtc(result.DestinationPath).Should().Be(timestamp);
            (await verifier.VerifyAsync(metadata, result.DestinationPath)).IsVerified.Should().BeTrue();
        }

        Directory.GetFiles(root, "*", SearchOption.AllDirectories).Should().HaveCount(extensions.Length);
    }

    [Theory]
    [InlineData("sqlite")]
    [InlineData("pmtiles")]
    public async Task VerifyAsync_RejectsCorruptSkippedArtifacts(string extension)
    {
        await using var temp = TestTemporaryDirectories.Create("corrupt-artifact-publishing", false);
        var source = Path.Combine(temp.DirectoryPath, $"source.{extension}");
        await File.WriteAllTextAsync(source, "original bytes");
        var options = new PublisherOptions { DestinationDir = temp.CreateSubDir("publish").DirectoryPath,
            OverwritePolicy = DatasetPublishOverwritePolicy.Skip };
        var publisher = new LocalDatasetPublisher(NullLogger<LocalDatasetPublisher>.Instance, Options.Create(options));
        var factory = new FileDatasetArtifactMetadataFactory();
        var verifier = new FilePublishedDatasetVerifier(factory, NullLogger<FilePublishedDatasetVerifier>.Instance);
        var request = new DatasetPublishRequest("geofabrik/europe/germany/berlin", "2-252c39aba2cb1705", source);
        var metadata = await factory.CreateAsync(request.RegionId, request.Version, source);
        var result = await publisher.PublishAsync(request);
        await File.WriteAllTextAsync(result.DestinationPath, "modified bytes");

        (await publisher.PublishAsync(request)).WasSkipped.Should().BeTrue();
        var verification = await verifier.VerifyAsync(metadata, result.DestinationPath);
        verification.IsVerified.Should().BeFalse();
        verification.Errors.Should().Contain(error => error.Contains("Checksum mismatch"));

        options = new PublisherOptions { DestinationDir = options.DestinationDir,
            OverwritePolicy = DatasetPublishOverwritePolicy.SkipIfIdentical };
        var identicalPublisher = new LocalDatasetPublisher(NullLogger<LocalDatasetPublisher>.Instance, Options.Create(options));
        var act = () => identicalPublisher.PublishAsync(request);
        await act.Should().ThrowAsync<IOException>();
    }
}
