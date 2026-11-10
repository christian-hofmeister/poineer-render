using System.Security.Cryptography;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using POIneer.Render.Application.Contracts;
using POIneer.Render.Infrastructure.Azure;
using Xunit;

namespace POIneer.Render.UnitTests.Infrastructure.Azure;

public sealed class AzureBlobPublishedDatasetVerifierTests
{
    private readonly IAzureBlobDatasetMetadataReader _metadataReader = Substitute.For<IAzureBlobDatasetMetadataReader>();

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task VerifyAsync_ChecksActualBlobBytes_WhenContentVerificationIsEnabled(bool contentsMatch)
    {
        const string key = "berlin/berlin.2-abc123.sqlite";
        byte[] source = [1, 2, 3];
        byte[] downloaded = contentsMatch ? source : [1, 2, 4];
        var expected = CreateExpectedMetadata() with
        {
            FileSizeBytes = source.Length,
            Sha256Checksum = Convert.ToHexStringLower(SHA256.HashData(source))
        };
        _metadataReader.ReadAsync(key, Arg.Any<CancellationToken>())
            .Returns(new AzureBlobDatasetMetadataReadResult(true, AzureBlobDatasetMetadata.FromArtifact(expected)));
        var container = Substitute.For<BlobContainerClient>();
        var blob = Substitute.For<BlobClient>();
        container.GetBlobClient(key).Returns(blob);
        blob.DownloadStreamingAsync(Arg.Any<BlobDownloadOptions>(), Arg.Any<CancellationToken>())
            .Returns(Response.FromValue(BlobsModelFactory.BlobDownloadStreamingResult(
                new MemoryStream(downloaded), BlobsModelFactory.BlobDownloadDetails(contentLength: downloaded.Length)),
                Substitute.For<Response>()));
        var verifier = new AzureBlobPublishedDatasetVerifier(
            _metadataReader, NullLogger<AzureBlobPublishedDatasetVerifier>.Instance, container);

        var result = await verifier.VerifyAsync(expected, key);

        result.IsVerified.Should().Be(contentsMatch);
    }

    [Fact]
    public async Task VerifyAsync_ReturnsVerified_WhenBlobMetadataMatchesExpectedMetadata()
    {
        var expectedMetadata = CreateExpectedMetadata();
        var sut = CreateSut();

        _metadataReader.ReadAsync("berlin/berlin.2-abc123.sqlite", Arg.Any<CancellationToken>())
            .Returns(new AzureBlobDatasetMetadataReadResult(
                BlobExists: true,
                AzureBlobDatasetMetadata.FromArtifact(expectedMetadata)));

        var result = await sut.VerifyAsync(expectedMetadata, "berlin/berlin.2-abc123.sqlite");

        result.IsVerified.Should().BeTrue();
        result.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task VerifyAsync_ReturnsNotVerified_WhenBlobIsMissing()
    {
        var expectedMetadata = CreateExpectedMetadata();
        var sut = CreateSut();

        _metadataReader.ReadAsync("berlin/berlin.2-abc123.sqlite", Arg.Any<CancellationToken>())
            .Returns(new AzureBlobDatasetMetadataReadResult(BlobExists: false, Metadata: null));

        var result = await sut.VerifyAsync(expectedMetadata, "berlin/berlin.2-abc123.sqlite");

        result.IsVerified.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Contain("not found");
    }

    [Fact]
    public async Task VerifyAsync_ReturnsNotVerified_WhenBlobMetadataIsMissing()
    {
        var expectedMetadata = CreateExpectedMetadata();
        var sut = CreateSut();

        _metadataReader.ReadAsync("berlin/berlin.2-abc123.sqlite", Arg.Any<CancellationToken>())
            .Returns(new AzureBlobDatasetMetadataReadResult(BlobExists: true, Metadata: null));

        var result = await sut.VerifyAsync(expectedMetadata, "berlin/berlin.2-abc123.sqlite");

        result.IsVerified.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Contain("does not contain comparable");
    }

    [Fact]
    public async Task VerifyAsync_ReturnsNotVerified_WhenBlobMetadataDiffers()
    {
        var expectedMetadata = CreateExpectedMetadata();
        var sut = CreateSut();

        _metadataReader.ReadAsync("berlin/berlin.2-abc123.sqlite", Arg.Any<CancellationToken>())
            .Returns(new AzureBlobDatasetMetadataReadResult(
                BlobExists: true,
                new AzureBlobDatasetMetadata(
                    expectedMetadata.RegionId,
                    expectedMetadata.Version,
                    expectedMetadata.FileSizeBytes,
                    "different-checksum")));

        var result = await sut.VerifyAsync(expectedMetadata, "berlin/berlin.2-abc123.sqlite");

        result.IsVerified.Should().BeFalse();
        result.Errors.Should().ContainSingle().Which.Should().Contain("Checksum mismatch");
    }

    [Fact]
    public async Task VerifyAsync_ForwardsCancellationToken()
    {
        var expectedMetadata = CreateExpectedMetadata();
        using var cts = new CancellationTokenSource();
        var sut = CreateSut();

        _metadataReader.ReadAsync("berlin/berlin.2-abc123.sqlite", cts.Token)
            .Returns(new AzureBlobDatasetMetadataReadResult(
                BlobExists: true,
                AzureBlobDatasetMetadata.FromArtifact(expectedMetadata)));

        await sut.VerifyAsync(expectedMetadata, "berlin/berlin.2-abc123.sqlite", cts.Token);

        await _metadataReader.Received(1)
            .ReadAsync("berlin/berlin.2-abc123.sqlite", cts.Token);
    }

    private AzureBlobPublishedDatasetVerifier CreateSut()
        => new(_metadataReader, NullLogger<AzureBlobPublishedDatasetVerifier>.Instance);

    private static DatasetArtifactMetadata CreateExpectedMetadata()
        => new(
            "berlin",
            "2-abc123",
            "poi.sqlite",
            123456,
            DateTimeOffset.Parse("2026-09-03T10:00:00Z"),
            "sha256");
}
