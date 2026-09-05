using FluentAssertions;
using NSubstitute;
using POIneer.Render.Application.Contracts;
using POIneer.Render.Application.Ports;
using POIneer.Render.Infrastructure.Azure;
using Xunit;

namespace POIneer.Render.UnitTests.Infrastructure.Azure;

public sealed class AzureBlobDatasetPublishPlannerTests
{
    private readonly IDatasetArtifactMetadataFactory _metadataFactory = Substitute.For<IDatasetArtifactMetadataFactory>();
    private readonly IAzureBlobDatasetMetadataReader _metadataReader = Substitute.For<IAzureBlobDatasetMetadataReader>();

    [Fact]
    public async Task PlanAsync_ReturnsUploadDecision_WhenDestinationBlobIsMissing()
    {
        var request = CreateRequest();
        var expectedMetadata = CreateArtifactMetadata();
        var sut = CreateSut();

        _metadataFactory
            .CreateAsync(request.RegionId, request.Version, request.SourcePath, Arg.Any<CancellationToken>())
            .Returns(expectedMetadata);

        _metadataReader
            .ReadAsync("berlin/berlin.2-abc123.sqlite", Arg.Any<CancellationToken>())
            .Returns(new AzureBlobDatasetMetadataReadResult(BlobExists: false, Metadata: null));

        var decision = await sut.PlanAsync(request);

        decision.BlobName.Should().Be("berlin/berlin.2-abc123.sqlite");
        decision.DestinationExists.Should().BeFalse();
        decision.ShouldUpload.Should().BeTrue();
        decision.Reason.Should().Contain("missing");
    }

    [Fact]
    public async Task PlanAsync_ReturnsSkipDecision_WhenDestinationBlobMetadataMatchesSourceArtifact()
    {
        var request = CreateRequest();
        var expectedMetadata = CreateArtifactMetadata();
        var sut = CreateSut();

        _metadataFactory
            .CreateAsync(request.RegionId, request.Version, request.SourcePath, Arg.Any<CancellationToken>())
            .Returns(expectedMetadata);

        _metadataReader
            .ReadAsync("berlin/berlin.2-abc123.sqlite", Arg.Any<CancellationToken>())
            .Returns(new AzureBlobDatasetMetadataReadResult(
                BlobExists: true,
                AzureBlobDatasetMetadata.FromArtifact(expectedMetadata)));

        var decision = await sut.PlanAsync(request);

        decision.BlobName.Should().Be("berlin/berlin.2-abc123.sqlite");
        decision.DestinationExists.Should().BeTrue();
        decision.ShouldUpload.Should().BeFalse();
        decision.Reason.Should().Contain("matches");
    }

    [Fact]
    public async Task PlanAsync_ReturnsUploadDecision_WhenDestinationBlobMetadataDiffersFromSourceArtifact()
    {
        var request = CreateRequest();
        var expectedMetadata = CreateArtifactMetadata();
        var sut = CreateSut();

        _metadataFactory
            .CreateAsync(request.RegionId, request.Version, request.SourcePath, Arg.Any<CancellationToken>())
            .Returns(expectedMetadata);

        _metadataReader
            .ReadAsync("berlin/berlin.2-abc123.sqlite", Arg.Any<CancellationToken>())
            .Returns(new AzureBlobDatasetMetadataReadResult(
                BlobExists: true,
                new AzureBlobDatasetMetadata(
                    expectedMetadata.RegionId,
                    expectedMetadata.Version,
                    expectedMetadata.FileSizeBytes,
                    "different-checksum")));

        var decision = await sut.PlanAsync(request);

        decision.BlobName.Should().Be("berlin/berlin.2-abc123.sqlite");
        decision.DestinationExists.Should().BeTrue();
        decision.ShouldUpload.Should().BeTrue();
        decision.Reason.Should().Contain("differs");
    }

    [Fact]
    public async Task PlanAsync_ForwardsCancellationToken()
    {
        var request = CreateRequest();
        var expectedMetadata = CreateArtifactMetadata();
        using var cts = new CancellationTokenSource();
        var sut = CreateSut();

        _metadataFactory
            .CreateAsync(request.RegionId, request.Version, request.SourcePath, cts.Token)
            .Returns(expectedMetadata);

        _metadataReader
            .ReadAsync("berlin/berlin.2-abc123.sqlite", cts.Token)
            .Returns(new AzureBlobDatasetMetadataReadResult(BlobExists: false, Metadata: null));

        await sut.PlanAsync(request, cts.Token);

        await _metadataFactory.Received(1)
            .CreateAsync(request.RegionId, request.Version, request.SourcePath, cts.Token);
        await _metadataReader.Received(1)
            .ReadAsync("berlin/berlin.2-abc123.sqlite", cts.Token);
    }

    [Fact]
    public async Task PlanAsync_ThrowsArgumentException_WhenRegionIdIsNotASafeBlobNameSegment()
    {
        var request = CreateRequest(regionId: "../berlin");
        var sut = CreateSut();

        var act = async () => await sut.PlanAsync(request);

        await act.Should().ThrowAsync<ArgumentException>()
            .WithParameterName(nameof(request.RegionId));
    }

    private AzureBlobDatasetPublishPlanner CreateSut()
        => new(_metadataFactory, _metadataReader);

    private static DatasetPublishRequest CreateRequest(string regionId = "berlin")
        => new(regionId, "2-abc123", @"C:\datasets\poi.sqlite");

    private static DatasetArtifactMetadata CreateArtifactMetadata()
        => new(
            "berlin",
            "2-abc123",
            "poi.sqlite",
            123456,
            DateTimeOffset.Parse("2026-09-03T10:00:00Z"),
            "abc123checksum");
}
