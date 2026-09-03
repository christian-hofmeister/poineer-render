namespace POIneer.Render.Infrastructure.Azure;

public sealed record AzureBlobDatasetPublishDecision(
    string BlobName,
    bool ShouldUpload,
    string Reason);
