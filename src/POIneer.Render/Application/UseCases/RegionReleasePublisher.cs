using Microsoft.Extensions.Logging;
using POIneer.Render.Application.Contracts;
using POIneer.Render.Application.Ports;
using POIneer.Render.Application.Ports.Model;

namespace POIneer.Render.Application.UseCases;

public sealed class RegionReleasePublisher(
    ILogger<RegionReleasePublisher> logger,
    IDatasetPublisher datasetPublisher,
    IDatasetVersionCalculator datasetVersionCalculator,
    IDatasetArtifactMetadataFactory datasetArtifactMetadataFactory,
    IPublishedDatasetVerifier publishedDatasetVerifier,
    PendingManifestStore pendingManifests,
    RegionMetadataPublisher? metadataPublisher = null)
{
    internal bool PublishesManifest => metadataPublisher is not null;

    internal Task<string?> ReadManifestRevisionAsync(string regionId, CancellationToken ct)
        => metadataPublisher?.ReadManifestRevisionAsync(regionId, ct) ?? Task.FromResult<string?>(null);

    internal async Task PublishAsync(
        string regionId, IReadOnlyList<string> artifactPaths, PendingManifest release, string regionOutDir, CancellationToken ct)
    {
        if (metadataPublisher is not null)
            RegionMetadataPublisher.ValidateVersion(release.ReleaseVersion);
        var manifestArtifacts = new List<RegionManifestArtifact>(artifactPaths.Count);
        long totalSizeBytes = 0;
        foreach (var artifactPath in artifactPaths)
        {
            var artifactVersion = metadataPublisher is not null && artifactPaths.Count > 1
                ? await datasetVersionCalculator.CalculateAsync([artifactPath], ct)
                : release.ReleaseVersion;
            var (metadata, objectKey) = await PublishAndVerifyArtifactAsync(regionId, artifactVersion, artifactPath, ct);
            totalSizeBytes = checked(totalSizeBytes + metadata.FileSizeBytes);
            if (metadataPublisher is not null)
            {
                manifestArtifacts.Add(new RegionManifestArtifact(
                    metadata.ArtifactType.ToString().ToLowerInvariant(), artifactVersion,
                    objectKey ?? throw new InvalidOperationException("The artifact publisher did not return an object key."),
                    metadata.FileSizeBytes, metadata.Sha256Checksum));
            }
        }

        logger.LogInformation(
            "({Id}) Release artifacts verified: version={Version}, artifactCount={ArtifactCount}, totalSizeBytes={TotalSizeBytes}.",
            regionId, release.ReleaseVersion, artifactPaths.Count, totalSizeBytes);

        if (metadataPublisher is not null)
        {
            await pendingManifests.WriteAsync(regionOutDir, release, ct);
            var published = await metadataPublisher.TryPublishManifestAsync(
                regionId, release.ReleaseVersion, manifestArtifacts, release.ExpectedRevision, ct);
            pendingManifests.Delete(regionOutDir);
            if (!published)
                throw new IOException($"The current manifest for '{regionId}' changed during publication. Retry with a fresh render.");

            logger.LogInformation("({Id}) Current region manifest published: version={Version}.", regionId, release.ReleaseVersion);
        }

        logger.LogInformation("({Id}) Done.", regionId);
    }

    private async Task<(DatasetArtifactMetadata Metadata, string? ObjectKey)> PublishAndVerifyArtifactAsync(
        string regionId, string version, string artifactPath, CancellationToken ct)
    {
        // Metadata is generated from the canonical artifact - only after validation and
        // promotion have both succeeded - and stays independent from IDatasetPublisher, so
        // it describes the artifact itself rather than a specific publish destination
        // (issue #130).
        var artifactMetadata = await datasetArtifactMetadataFactory.CreateAsync(
            regionId,
            version,
            artifactPath,
            ct);

        logger.LogInformation(
            "({Id}) Dataset artifact metadata: type={ArtifactType}, fileName={FileName}, sizeBytes={SizeBytes}, sha256={Sha256}, createdUtc={CreatedUtc}.",
            regionId,
            artifactMetadata.ArtifactType.ToString().ToLowerInvariant(),
            artifactMetadata.FileName,
            artifactMetadata.FileSizeBytes,
            artifactMetadata.Sha256Checksum,
            artifactMetadata.CreatedUtc);

        var publishResult = await datasetPublisher.PublishAsync(
            new DatasetPublishRequest(regionId, version, artifactPath, RequireImmutable: metadataPublisher is not null),
            ct);

        logger.LogInformation(
            "({Id}) Published dataset version {Version} to {DestinationPath} (skipped: {WasSkipped}).",
            regionId,
            version,
            publishResult.DestinationPath,
            publishResult.WasSkipped);

        // Verified on every publish call, not only when WasSkipped is false: a publish is
        // only considered successful once what is actually present at the destination is
        // confirmed to match, regardless of whether this run wrote new bytes there or found
        // a matching file already published by an earlier run (issue #135).
        var verificationResult = await publishedDatasetVerifier.VerifyAsync(
            artifactMetadata,
            publishResult.DestinationPath,
            ct);

        if (!verificationResult.IsVerified)
        {
            logger.LogError(
                "({Id}) Published dataset failed integrity verification at {DestinationPath} and will not be marked as successfully published. Errors: {Errors}",
                regionId,
                publishResult.DestinationPath,
                string.Join("; ", verificationResult.Errors));

            throw new InvalidOperationException(
                $"Published dataset for region '{regionId}' failed integrity verification at {publishResult.DestinationPath}: {string.Join(", ", verificationResult.Errors)}");
        }

        logger.LogInformation(
            "({Id}) Published dataset verified successfully at {DestinationPath}.",
            regionId,
            publishResult.DestinationPath);

        return (artifactMetadata, publishResult.ObjectKey);
    }
}
