using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using POIneer.Render.Infrastructure.Azure;
using Xunit;

namespace POIneer.Render.UnitTests.Infrastructure.Azure;

public sealed class AzureBlobRegionMetadataStoreTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("\"current\"")]
    public async Task TryWriteAsync_UsesConditionalUploadAtMetadataPrefix(string? revision)
    {
        var container = Substitute.For<BlobContainerClient>();
        var blob = Substitute.For<BlobClient>();
        container.GetBlobClient("published/berlin/manifest.json").Returns(blob);
        blob.UploadAsync(Arg.Any<Stream>(), Arg.Any<BlobUploadOptions>(), Arg.Any<CancellationToken>())
            .ThrowsAsync(new RequestFailedException(412, "Precondition failed"));
        var store = new AzureBlobRegionMetadataStore(container, "published");

        Assert.False(await store.TryWriteAsync("berlin/manifest.json", [], revision, CancellationToken.None));

        await blob.Received(1).UploadAsync(
            Arg.Any<Stream>(),
            Arg.Is<BlobUploadOptions>(options => options.HttpHeaders.ContentType == "application/json"
                && (revision == null ? options.Conditions.IfNoneMatch == ETag.All
                    : options.Conditions.IfMatch == new ETag(revision))),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ReadAsync_DistinguishesMissingBlobFromMissingContainer()
    {
        var container = Substitute.For<BlobContainerClient>();
        var blob = Substitute.For<BlobClient>();
        container.GetBlobClient("regions.json").Returns(blob);
        blob.DownloadContentAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new RequestFailedException(404, "Blob missing", "BlobNotFound", null));
        var store = new AzureBlobRegionMetadataStore(container, null);
        Assert.Null(await store.ReadAsync("regions.json", CancellationToken.None));

        blob.DownloadContentAsync(Arg.Any<CancellationToken>())
            .ThrowsAsync(new RequestFailedException(404, "Container missing", "ContainerNotFound", null));
        await Assert.ThrowsAsync<RequestFailedException>(() => store.ReadAsync("regions.json", CancellationToken.None));
    }
}
