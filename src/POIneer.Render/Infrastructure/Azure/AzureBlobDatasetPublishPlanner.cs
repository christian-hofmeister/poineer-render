using POIneer.Render.Application.Contracts;
using POIneer.Render.Application.Ports;

namespace POIneer.Render.Infrastructure.Azure;

public sealed class AzureBlobDatasetPublishPlanner : IAzureBlobDatasetPublishPlanner
{
    private readonly IDatasetArtifactMetadataFactory _metadataFactory;
    private readonly IAzureBlobDatasetMetadataReader _metadataReader;

    public AzureBlobDatasetPublishPlanner(
        IDatasetArtifactMetadataFactory metadataFactory,
        IAzureBlobDatasetMetadataReader metadataReader)
    {
        _metadataFactory = metadataFactory;
        _metadataReader = metadataReader;
    }

    public async Task<AzureBlobDatasetPublishDecision> PlanAsync(
        DatasetPublishRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RegionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Version);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourcePath);

        ValidateBlobNameSegment(request.RegionId, nameof(request.RegionId));
        ValidateBlobNameSegment(request.Version, nameof(request.Version));

        var blobName = BuildBlobName(request);
        var expectedMetadata = await _metadataFactory.CreateAsync(
            request.RegionId,
            request.Version,
            request.SourcePath,
            cancellationToken);

        var readResult = await _metadataReader.ReadAsync(blobName, cancellationToken);

        if (!readResult.BlobExists)
        {
            return new AzureBlobDatasetPublishDecision(
                blobName,
                DestinationExists: false,
                ShouldUpload: true,
                "Destination blob is missing.");
        }

        if (readResult.Metadata is null)
        {
            return new AzureBlobDatasetPublishDecision(
                blobName,
                DestinationExists: true,
                ShouldUpload: true,
                "Destination blob exists but does not contain comparable POIneer dataset metadata.");
        }

        if (readResult.Metadata.Matches(expectedMetadata))
        {
            return new AzureBlobDatasetPublishDecision(
                blobName,
                DestinationExists: true,
                ShouldUpload: false,
                "Destination blob already matches the source artifact metadata.");
        }

        return new AzureBlobDatasetPublishDecision(
            blobName,
            DestinationExists: true,
            ShouldUpload: true,
            "Destination blob metadata differs from the source artifact metadata.");
    }

    private static string BuildBlobName(DatasetPublishRequest request)
    {
        var extension = Path.GetExtension(request.SourcePath);
        return $"{request.RegionId}/{request.RegionId}.{request.Version}{extension}";
    }

    private static void ValidateBlobNameSegment(string value, string parameterName)
    {
        if (value is "." or "..")
        {
            throw new ArgumentException(
                $"'{value}' is not a valid {parameterName}: '.' and '..' are not allowed.",
                parameterName);
        }

        foreach (var c in value)
        {
            if (!char.IsAsciiLetterOrDigit(c) && c is not ('.' or '-' or '_'))
            {
                throw new ArgumentException(
                    $"'{value}' is not a valid {parameterName}: only ASCII letters, digits, '.', '-', and '_' are allowed, but found '{c}'.",
                    parameterName);
            }
        }
    }
}
