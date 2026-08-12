using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POIneer.Render.Application.Contracts;
using POIneer.Render.Application.Options;
using POIneer.Render.Application.Ports;

namespace POIneer.Render.Infrastructure.FileSystem;

// Local filesystem implementation of IDatasetPublisher (issue #132). Copies a validated
// dataset artifact into a configurable destination directory, laid out as
// "{DestinationDir}/{RegionId}/{RegionId}.{Version}{extension}" so the region and dataset
// version stay identifiable from the file layout alone - e.g. when browsing the
// destination directly on the VPS. The destination is always driven by
// PublisherOptions.DestinationDir; the same implementation works unchanged for a local
// development folder and for a filesystem location available on the VPS.
//
// Out of scope (see issue #132): Azure Blob Storage, FTP/SFTP, CDN integration,
// VPS/Azure synchronization, public download URLs.
public sealed class LocalDatasetPublisher : IDatasetPublisher
{
    private const int CopyBufferSize = 81920;
    private const string StagingFileSuffix = ".tmp";

    private readonly ILogger<LocalDatasetPublisher> _logger;
    private readonly PublisherOptions _options;

    public LocalDatasetPublisher(ILogger<LocalDatasetPublisher> logger, IOptions<PublisherOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public async Task<DatasetPublishResult> PublishAsync(
        DatasetPublishRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.RegionId);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.Version);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SourcePath);

        if (!File.Exists(request.SourcePath))
            throw new FileNotFoundException($"Dataset artifact not found: {request.SourcePath}", request.SourcePath);

        var destinationDir = Path.Combine(_options.DestinationDir, request.RegionId);
        Directory.CreateDirectory(destinationDir);

        var extension = Path.GetExtension(request.SourcePath);
        var destinationPath = Path.GetFullPath(
            Path.Combine(destinationDir, $"{request.RegionId}.{request.Version}{extension}"));

        if (File.Exists(destinationPath))
        {
            switch (_options.OverwritePolicy)
            {
                case DatasetPublishOverwritePolicy.Skip:
                    _logger.LogInformation(
                        "Publish target already exists at {DestinationPath}, skipping (overwrite policy: Skip).",
                        destinationPath);
                    return new DatasetPublishResult(destinationPath, WasSkipped: true);

                case DatasetPublishOverwritePolicy.Fail:
                    throw new IOException(
                        $"Publish target already exists at {destinationPath} and the overwrite policy is Fail.");

                case DatasetPublishOverwritePolicy.Overwrite:
                    _logger.LogInformation(
                        "Publish target already exists at {DestinationPath}, replacing it (overwrite policy: Overwrite).",
                        destinationPath);
                    break;

                default:
                    throw new InvalidOperationException($"Unrecognized overwrite policy: {_options.OverwritePolicy}");
            }
        }

        // Copy through a staging file and rename into place: if the copy is interrupted
        // (cancellation, crash, disk full), the half-written bytes land in the ".tmp" file,
        // never at destinationPath - so a later run's existence check for the overwrite
        // policy above never mistakes a partial file for a real, previously published one.
        var stagingDestinationPath = destinationPath + StagingFileSuffix;

        await using (var source = new FileStream(
            request.SourcePath, FileMode.Open, FileAccess.Read, FileShare.Read, CopyBufferSize, useAsync: true))
        await using (var destination = new FileStream(
            stagingDestinationPath, FileMode.Create, FileAccess.Write, FileShare.None, CopyBufferSize, useAsync: true))
        {
            await source.CopyToAsync(destination, cancellationToken);
        }

        File.Move(stagingDestinationPath, destinationPath, overwrite: true);

        _logger.LogInformation(
            "Published dataset for region {RegionId} (version {Version}) to {DestinationPath}.",
            request.RegionId,
            request.Version,
            destinationPath);

        return new DatasetPublishResult(destinationPath, WasSkipped: false);
    }
}
