namespace POIneer.Render.Infrastructure.Azure;

public interface IAzureBlobDatasetMetadataReader
{
    Task<AzureBlobDatasetMetadata?> ReadAsync(
        string blobName,
        CancellationToken cancellationToken = default);
}
