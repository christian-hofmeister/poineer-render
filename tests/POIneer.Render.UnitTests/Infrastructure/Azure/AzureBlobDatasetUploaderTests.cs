using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using NSubstitute;
using POIneer.Render.Infrastructure.Azure;
using POIneer.Render.TestHelpers;
using Xunit;

namespace POIneer.Render.UnitTests.Infrastructure.Azure;

public sealed class AzureBlobDatasetUploaderTests
{
    [Theory]
    [InlineData("poi.sqlite", "application/vnd.sqlite3")]
    [InlineData("map.pmtiles", "application/octet-stream")]
    public async Task UploadAsync_SetsContentTypeForTheArtifact(string fileName, string expectedContentType)
    {
        await using var tempDir = TestTemporaryDirectories.Create("azure-blob-upload");
        var sourcePath = Path.Combine(tempDir.DirectoryPath, fileName);
        await File.WriteAllTextAsync(sourcePath, "test artifact");

        var container = Substitute.For<BlobContainerClient>();
        var blob = Substitute.For<BlobClient>();
        container.GetBlobClient(fileName).Returns(blob);
        blob.UploadAsync(Arg.Any<Stream>(), Arg.Any<BlobUploadOptions>(), Arg.Any<CancellationToken>())
            .Returns(Substitute.For<Response<BlobContentInfo>>());
        var uploader = new AzureBlobDatasetUploader(container);

        await uploader.UploadAsync(fileName, sourcePath, new Dictionary<string, string>(), overwriteExisting: false);

        await blob.Received(1).UploadAsync(
            Arg.Any<Stream>(),
            Arg.Is<BlobUploadOptions>(options => options.HttpHeaders.ContentType == expectedContentType),
            Arg.Any<CancellationToken>());
    }
}
