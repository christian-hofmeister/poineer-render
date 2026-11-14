using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;

namespace POIneer.Render.Infrastructure.Azure;

public sealed class AzureBlobDatasetUploader : IAzureBlobDatasetUploader
{
    private const int UploadBufferSize = 81920;

    private readonly BlobContainerClient _containerClient;

    public AzureBlobDatasetUploader(BlobContainerClient containerClient)
    {
        _containerClient = containerClient;
    }

    public async Task UploadAsync(
        string blobName,
        string sourcePath,
        IReadOnlyDictionary<string, string> metadata,
        bool overwriteExisting,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(blobName);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourcePath);
        ArgumentNullException.ThrowIfNull(metadata);

        var blobClient = _containerClient.GetBlobClient(blobName);
        var uploadOptions = new BlobUploadOptions
        {
            HttpHeaders = new BlobHttpHeaders
            {
                ContentType = Path.GetExtension(sourcePath).ToLowerInvariant() switch
                {
                    ".sqlite" => "application/vnd.sqlite3",
                    ".pmtiles" => "application/octet-stream",
                    _ => throw new NotSupportedException($"Unsupported dataset artifact extension: {sourcePath}")
                }
            },
            Metadata = new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase),
            Conditions = overwriteExisting
                ? null
                : new BlobRequestConditions { IfNoneMatch = ETag.All }
        };

        await using var source = new FileStream(
            sourcePath,
            FileMode.Open,
            FileAccess.Read,
            FileShare.Read,
            UploadBufferSize,
            useAsync: true);

        await blobClient.UploadAsync(source, uploadOptions, cancellationToken);
    }
}
