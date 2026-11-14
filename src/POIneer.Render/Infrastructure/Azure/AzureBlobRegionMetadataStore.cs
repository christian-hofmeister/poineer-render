using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using POIneer.Render.Application.Ports;
using POIneer.Render.Domain.Models;

namespace POIneer.Render.Infrastructure.Azure;

public sealed class AzureBlobRegionMetadataStore(BlobContainerClient container, string? prefix) : IRegionMetadataStore
{
    public async Task<StoredRegionMetadata?> ReadAsync(string key, CancellationToken ct)
    {
        try
        {
            var result = await GetBlob(key).DownloadContentAsync(ct);
            return new StoredRegionMetadata(result.Value.Details.ETag.ToString(), result.Value.Content.ToArray());
        }
        catch (RequestFailedException ex) when (ex.Status == 404 && ex.ErrorCode == BlobErrorCode.BlobNotFound.ToString())
        {
            return null;
        }
    }

    public async Task<bool> TryWriteAsync(string key, byte[] content, string? expectedRevision, CancellationToken ct)
    {
        var conditions = expectedRevision is null
            ? new BlobRequestConditions { IfNoneMatch = ETag.All }
            : new BlobRequestConditions { IfMatch = new ETag(expectedRevision) };
        try
        {
            using var stream = new MemoryStream(content, writable: false);
            await GetBlob(key).UploadAsync(stream, new BlobUploadOptions
            {
                Conditions = conditions,
                HttpHeaders = new BlobHttpHeaders { ContentType = "application/json" }
            }, ct);
            return true;
        }
        catch (RequestFailedException ex) when (ex.Status == 412
            || (ex.Status == 409 && ex.ErrorCode == BlobErrorCode.BlobAlreadyExists.ToString()))
        {
            return false;
        }
    }

    private BlobClient GetBlob(string key)
    {
        RegionIdentifier.ValidateHierarchicalId(key, nameof(key));
        return container.GetBlobClient(string.IsNullOrEmpty(prefix) ? key : $"{prefix}/{key}");
    }
}
