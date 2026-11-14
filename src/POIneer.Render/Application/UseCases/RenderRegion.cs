using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POIneer.Render.Application.Contracts;
using POIneer.Render.Application.Mapping;
using POIneer.Render.Application.Options;
using POIneer.Render.Application.Ports;
using POIneer.Render.Application.Ports.Model;
using POIneer.Render.Domain.Models;
using POIneer.Render.Ports;

namespace POIneer.Render.Application.UseCases;

public sealed class RenderRegion : IRenderRegion
{
    private readonly ILogger<RenderRegion> _logger;
    private readonly IPolygonCutter _polygonCutter;
    private readonly IOsmReader _osmReader;
    private readonly ISqliteDatabaseInitializer _dbInit;
    private readonly IExporter _exporter;
    private readonly IRawPoiMapper _rawPoiMapper;
    private readonly RendererOptions _rendererOptions;
    private readonly IDatasetValidator _datasetValidator;
    private readonly IDatasetPublisher _datasetPublisher;
    private readonly IDatasetVersionCalculator _datasetVersionCalculator;
    private readonly IDatasetArtifactMetadataFactory _datasetArtifactMetadataFactory;
    private readonly IPublishedDatasetVerifier _publishedDatasetVerifier;
    private readonly IVectorTileGenerator _vectorTileGenerator;
    private readonly VectorTileOptions _vectorTileOptions;
    private readonly RegionMetadataPublisher? _metadataPublisher;

    public RenderRegion(
        ILogger<RenderRegion> log,
        IPolygonCutter polygonCutter,
        ISqliteDatabaseInitializer dbInit,
        IOsmReader osmReader,
        IExporter exporter,
        IRawPoiMapper rawPoiMapper,
        IOptions<RendererOptions> options,
        IDatasetValidator datasetValidator,
        IDatasetPublisher datasetPublisher,
        IDatasetVersionCalculator datasetVersionCalculator,
        IDatasetArtifactMetadataFactory datasetArtifactMetadataFactory,
        IPublishedDatasetVerifier publishedDatasetVerifier,
        IVectorTileGenerator vectorTileGenerator,
        IOptions<VectorTileOptions> vectorTileOptions,
        RegionMetadataPublisher? metadataPublisher = null)
    {
        _logger = log;
        _polygonCutter = polygonCutter;
        _osmReader = osmReader;
        _dbInit = dbInit;
        _exporter = exporter;
        _rawPoiMapper = rawPoiMapper;
        _rendererOptions = options.Value;
        _datasetValidator = datasetValidator;
        _datasetPublisher = datasetPublisher;
        _datasetVersionCalculator = datasetVersionCalculator;
        _datasetArtifactMetadataFactory = datasetArtifactMetadataFactory;
        _publishedDatasetVerifier = publishedDatasetVerifier;
        _vectorTileGenerator = vectorTileGenerator;
        _vectorTileOptions = vectorTileOptions.Value;
        _metadataPublisher = metadataPublisher;
    }

    public Task RunAsync(
        RegionDto regionDto,
        string workDir,
        string outDir,
        CancellationToken ct = default,
        bool forceRebuild = false)
        => RunCoreAsync(regionDto, workDir, outDir, null, ct, forceRebuild);

    public Task RunAsync(
        RegionDto regionDto,
        string workDir,
        string outDir,
        RegionPublicationContext publication,
        CancellationToken ct,
        bool forceRebuild)
        => RunCoreAsync(regionDto, workDir, outDir, publication, ct, forceRebuild);

    private async Task RunCoreAsync(
        RegionDto regionDto,
        string workDir,
        string outDir,
        RegionPublicationContext? publication,
        CancellationToken ct,
        bool forceRebuild)
    {
        // regionDto.Id may be a globally unique, hierarchical region identifier (ADR
        // 0007), e.g. "geofabrik/europe/germany/berlin" - RegionIdentifier turns each
        // '/'-separated segment into its own nested directory and validates the id is
        // safe to use as a filesystem path.
        var pbfPath = GetPbfPath(workDir, regionDto.Id);
        var recreateDatabase = forceRebuild || _rendererOptions.OverwriteDatabase || _rendererOptions.OverwritePbf;

        if (!File.Exists(pbfPath))
            throw new FileNotFoundException($"PBF not found: {pbfPath}");

        var regionOutDir = Path.GetFullPath(RegionIdentifier.CombinePath(outDir, regionDto.Id));
        Directory.CreateDirectory(regionOutDir);

        var canonicalPath = Path.Combine(regionOutDir, RenderFilePaths.PoiDataset);
        var canonicalVectorTilePath = Path.Combine(regionOutDir, RenderFilePaths.VectorTiles);
        var artifactPaths = new List<string> { canonicalPath };
        if (_vectorTileOptions.Enabled)
            artifactPaths.Add(canonicalVectorTilePath);

        var checkpointPath = Path.Combine(regionOutDir, "manifest-pending.json");
        string? sourceSha256 = null;
        if (_metadataPublisher is not null && File.Exists(checkpointPath))
        {
            var pending = _rendererOptions.OverwriteDatabase || _rendererOptions.OverwritePbf
                ? null
                : await ReadManifestCheckpointAsync(checkpointPath, ct);
            if (pending?.SourceSha256 is not null && artifactPaths.All(File.Exists))
            {
                sourceSha256 = await CalculateSourceSha256Async(pbfPath, ct);
                if (pending.SourceSha256 == sourceSha256
                    && pending.ReleaseVersion == await _datasetVersionCalculator.CalculateAsync(artifactPaths, ct))
                {
                    _logger.LogInformation("({Id}) Retrying manifest publication using existing artifacts.", regionDto.Id);
                    await PublishReleaseAsync(regionDto.Id, artifactPaths, pending, checkpointPath, ct);
                    return;
                }
            }

            // Keep the checkpoint until it is superseded so a failed rebuild remains retryable.
            recreateDatabase = true;
        }

        var pbfChangedSinceOutput = File.Exists(canonicalPath)
            && File.GetLastWriteTimeUtc(pbfPath) > File.GetLastWriteTimeUtc(canonicalPath);

        if (File.Exists(canonicalPath))
        {
            if (!recreateDatabase && !pbfChangedSinceOutput && (!_vectorTileOptions.Enabled || File.Exists(canonicalVectorTilePath)))
            {
                _logger.LogInformation("({Id}) Output SQLite already exists at {Out}, skipping rendering (overwrite disabled).", regionDto.Id, canonicalPath);
                return;
            }

            _logger.LogInformation(
                "({Id}) Output SQLite already exists at {Out}, but re-rendering is required (overwrite: {Overwrite}, pbfChangedSinceOutput: {PbfChangedSinceOutput}). It will only be replaced once the new dataset passes validation.",
                regionDto.Id,
                canonicalPath,
                recreateDatabase,
                pbfChangedSinceOutput);
        }

        var manifestRevision = publication is null && _metadataPublisher is not null
            ? await _metadataPublisher.ReadManifestRevisionAsync(regionDto.Id, ct)
            : publication?.ExpectedManifestRevision;
        var stagingPath = Path.Combine(regionOutDir, RenderFilePaths.StagingPoiDataset);
        var stagingVectorTilePath = Path.Combine(regionOutDir, RenderFilePaths.StagingVectorTiles);

        if (File.Exists(stagingPath))
        {
            _logger.LogWarning(
                "({Id}) Found a leftover staging database from a previous interrupted run at {Staging}, removing it before re-rendering.",
                regionDto.Id,
                stagingPath);
            DeleteSqliteDatabaseWithSidecars(stagingPath);
        }

        if (File.Exists(stagingVectorTilePath))
        {
            _logger.LogWarning(
                "({Id}) Found a leftover staging PMTiles file from a previous interrupted run at {Staging}, removing it before re-rendering.",
                regionDto.Id,
                stagingVectorTilePath);
            File.Delete(stagingVectorTilePath);
        }

        if (_metadataPublisher is not null)
            sourceSha256 ??= await CalculateSourceSha256Async(pbfPath, ct);

        _logger.LogInformation("({Id}) Cutting polygon...", regionDto.Id);
        var cutPbf = await _polygonCutter.CutAsync(pbfPath, regionDto.Poly, ct);

        _logger.LogInformation("({Id}) Reading OSM → POIs...", regionDto.Id);
        var rawPois = _osmReader.ReadAmenityNodesAsync(cutPbf, ct);

        _logger.LogInformation("({Id}) Initializing SQLite database: {Out}", regionDto.Id, stagingPath);
        await _dbInit.InitializeAsync(
            stagingPath,
            ct);

        _logger.LogInformation("({Id}) Exporting to SQLite: {Out}", regionDto.Id, stagingPath);
        await _exporter.ExportAsync(
            MapRawPoisAsync(rawPois, ct),
            stagingPath,
            ct);

        _logger.LogInformation("({Id}) Validating generated dataset: {Out}", regionDto.Id, stagingPath);
        var result = await _datasetValidator.ValidateAsync(
            stagingPath,
            ct);

        if (!result.IsValid)
        {
            var quarantinePath = QuarantineInvalidDataset(outDir, regionDto.Id, stagingPath);

            _logger.LogError(
                "({Id}) Generated dataset failed validation and was moved to quarantine: {QuarantinePath}. Errors: {Errors}",
                regionDto.Id,
                quarantinePath,
                string.Join("; ", result.Errors));

            throw new InvalidOperationException(
                $"Generated dataset for region '{regionDto.Id}' is invalid and was quarantined at {quarantinePath}: {string.Join(", ", result.Errors)}");
        }

        if (_vectorTileOptions.Enabled)
        {
            _logger.LogInformation(
                "({Id}) Generating vector tile dataset: {Out}",
                regionDto.Id,
                stagingVectorTilePath);

            await _vectorTileGenerator.GenerateAsync(
                cutPbf,
                stagingVectorTilePath,
                ct);
        }

        _logger.LogInformation("({Id}) Promoting validated dataset to canonical location: {Out}", regionDto.Id, canonicalPath);
        PromoteStagingToCanonical(stagingPath, canonicalPath);

        if (_vectorTileOptions.Enabled)
        {
            _logger.LogInformation(
                "({Id}) Promoting vector tile dataset to canonical location: {Out}",
                regionDto.Id,
                canonicalVectorTilePath);
            PromoteVectorTileToCanonical(stagingVectorTilePath, canonicalVectorTilePath);
        }

        var version = await _datasetVersionCalculator.CalculateAsync(artifactPaths, ct);
        var release = new PendingManifest(version, manifestRevision, sourceSha256);
        await PublishReleaseAsync(regionDto.Id, artifactPaths, release, checkpointPath, ct);
    }

    private async Task PublishReleaseAsync(
        string regionId, IReadOnlyList<string> artifactPaths, PendingManifest release, string checkpointPath, CancellationToken ct)
    {
        if (_metadataPublisher is not null)
            RegionMetadataPublisher.ValidateVersion(release.ReleaseVersion);
        var manifestArtifacts = new List<RegionManifestArtifact>(artifactPaths.Count);
        long totalSizeBytes = 0;
        foreach (var artifactPath in artifactPaths)
        {
            var artifactVersion = _metadataPublisher is not null && artifactPaths.Count > 1
                ? await _datasetVersionCalculator.CalculateAsync([artifactPath], ct)
                : release.ReleaseVersion;
            var (metadata, objectKey) = await PublishAndVerifyArtifactAsync(regionId, artifactVersion, artifactPath, ct);
            totalSizeBytes = checked(totalSizeBytes + metadata.FileSizeBytes);
            if (_metadataPublisher is not null)
            {
                manifestArtifacts.Add(new RegionManifestArtifact(
                    metadata.ArtifactType.ToString().ToLowerInvariant(), artifactVersion,
                    objectKey ?? throw new InvalidOperationException("The artifact publisher did not return an object key."),
                    metadata.FileSizeBytes, metadata.Sha256Checksum));
            }
        }

        _logger.LogInformation(
            "({Id}) Release artifacts verified: version={Version}, artifactCount={ArtifactCount}, totalSizeBytes={TotalSizeBytes}.",
            regionId, release.ReleaseVersion, artifactPaths.Count, totalSizeBytes);

        if (_metadataPublisher is not null)
        {
            await WriteManifestCheckpointAsync(checkpointPath, release, ct);
            var published = await _metadataPublisher.TryPublishManifestAsync(
                regionId, release.ReleaseVersion, manifestArtifacts, release.ExpectedRevision, ct);
            File.Delete(checkpointPath);
            if (!published)
                throw new IOException($"The current manifest for '{regionId}' changed during publication. Retry with a fresh render.");

            _logger.LogInformation("({Id}) Current region manifest published: version={Version}.", regionId, release.ReleaseVersion);
        }

        _logger.LogInformation("({Id}) Done.", regionId);
    }

    private async Task<PendingManifest?> ReadManifestCheckpointAsync(string path, CancellationToken ct)
    {
        try
        {
            return JsonSerializer.Deserialize<PendingManifest>(await File.ReadAllBytesAsync(path, ct));
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Ignoring invalid manifest checkpoint at {Path}.", path);
            return null;
        }
    }

    private static async Task WriteManifestCheckpointAsync(string path, PendingManifest pending, CancellationToken ct)
    {
        var stagingPath = path + ".tmp";
        try
        {
            await File.WriteAllBytesAsync(stagingPath, JsonSerializer.SerializeToUtf8Bytes(pending), ct);
            File.Move(stagingPath, path, overwrite: true);
        }
        finally
        {
            File.Delete(stagingPath);
        }
    }

    private static async Task<string> CalculateSourceSha256Async(string path, CancellationToken ct)
    {
        await using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream, ct));
    }

    private sealed record PendingManifest(string ReleaseVersion, string? ExpectedRevision, string? SourceSha256);

    private async Task<(DatasetArtifactMetadata Metadata, string? ObjectKey)> PublishAndVerifyArtifactAsync(
        string regionId, string version, string artifactPath, CancellationToken ct)
    {
        // Metadata is generated from the canonical artifact - only after validation and
        // promotion have both succeeded - and stays independent from IDatasetPublisher, so
        // it describes the artifact itself rather than a specific publish destination
        // (issue #130).
        var artifactMetadata = await _datasetArtifactMetadataFactory.CreateAsync(
            regionId,
            version,
            artifactPath,
            ct);

        _logger.LogInformation(
            "({Id}) Dataset artifact metadata: type={ArtifactType}, fileName={FileName}, sizeBytes={SizeBytes}, sha256={Sha256}, createdUtc={CreatedUtc}.",
            regionId,
            artifactMetadata.ArtifactType.ToString().ToLowerInvariant(),
            artifactMetadata.FileName,
            artifactMetadata.FileSizeBytes,
            artifactMetadata.Sha256Checksum,
            artifactMetadata.CreatedUtc);

        var publishResult = await _datasetPublisher.PublishAsync(
            new DatasetPublishRequest(regionId, version, artifactPath, RequireImmutable: _metadataPublisher is not null),
            ct);

        _logger.LogInformation(
            "({Id}) Published dataset version {Version} to {DestinationPath} (skipped: {WasSkipped}).",
            regionId,
            version,
            publishResult.DestinationPath,
            publishResult.WasSkipped);

        // Verified on every publish call, not only when WasSkipped is false: a publish is
        // only considered successful once what is actually present at the destination is
        // confirmed to match, regardless of whether this run wrote new bytes there or found
        // a matching file already published by an earlier run (issue #135).
        var verificationResult = await _publishedDatasetVerifier.VerifyAsync(
            artifactMetadata,
            publishResult.DestinationPath,
            ct);

        if (!verificationResult.IsVerified)
        {
            _logger.LogError(
                "({Id}) Published dataset failed integrity verification at {DestinationPath} and will not be marked as successfully published. Errors: {Errors}",
                regionId,
                publishResult.DestinationPath,
                string.Join("; ", verificationResult.Errors));

            throw new InvalidOperationException(
                $"Published dataset for region '{regionId}' failed integrity verification at {publishResult.DestinationPath}: {string.Join(", ", verificationResult.Errors)}");
        }

        _logger.LogInformation(
            "({Id}) Published dataset verified successfully at {DestinationPath}.",
            regionId,
            publishResult.DestinationPath);

        return (artifactMetadata, publishResult.ObjectKey);
    }

    /// <summary>
    /// Atomically promotes a validated staging database to become the canonical output at
    /// <paramref name="canonicalPath"/>. This is a purely local, in-place move within
    /// outDir - distinct from IDatasetPublisher, which afterwards copies that already-
    /// canonical file out to a separately configured publish destination (see
    /// PublisherOptions). A crash or an invalid render can never leave a partial or
    /// unvalidated file behind at the canonical location - a previously promoted dataset
    /// stays in place until a new one is confirmed valid.
    /// </summary>
    private static void PromoteStagingToCanonical(string stagingPath, string canonicalPath)
    {
        foreach (var suffix in RenderFilePaths.SqliteSidecarSuffixes)
        {
            var canonicalSidecarPath = canonicalPath + suffix;
            if (File.Exists(canonicalSidecarPath))
            {
                File.Delete(canonicalSidecarPath);
            }

            var stagingSidecarPath = stagingPath + suffix;
            if (File.Exists(stagingSidecarPath))
            {
                File.Move(stagingSidecarPath, canonicalSidecarPath, overwrite: true);
            }
        }

        File.Move(stagingPath, canonicalPath, overwrite: true);
    }

    private static void PromoteVectorTileToCanonical(string stagingPath, string canonicalPath)
        => File.Move(stagingPath, canonicalPath, overwrite: true);

    /// <summary>
    /// Moves an invalid dataset out of the staging location into a per-region quarantine
    /// folder so it doesn't linger next to the canonical output, while still being
    /// preserved for post-mortem inspection.
    /// </summary>
    private static string QuarantineInvalidDataset(string outDir, string regionId, string invalidDatasetPath)
    {
        var quarantineDir = Path.Combine(RegionIdentifier.CombinePath(outDir, regionId), "_failed");
        Directory.CreateDirectory(quarantineDir);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        var quarantinePath = Path.Combine(quarantineDir, $"poi.{timestamp}.sqlite");

        File.Move(invalidDatasetPath, quarantinePath, overwrite: true);

        foreach (var suffix in RenderFilePaths.SqliteSidecarSuffixes)
        {
            var sidecarPath = invalidDatasetPath + suffix;
            if (File.Exists(sidecarPath))
            {
                File.Move(sidecarPath, quarantinePath + suffix, overwrite: true);
            }
        }

        return quarantinePath;
    }

    /// <summary>
    /// Deletes a SQLite database file together with any leftover -wal/-shm/-journal
    /// sidecar files, so a stale sidecar can't cause recovery/lock confusion the next
    /// time this path is opened.
    /// </summary>
    private static void DeleteSqliteDatabaseWithSidecars(string sqliteFilePath)
    {
        File.Delete(sqliteFilePath);

        foreach (var suffix in RenderFilePaths.SqliteSidecarSuffixes)
        {
            var sidecarPath = sqliteFilePath + suffix;
            if (File.Exists(sidecarPath))
            {
                File.Delete(sidecarPath);
            }
        }
    }

    private static string GetPbfPath(string workDir, string regionId) =>
        Path.Combine(RegionIdentifier.CombinePath(workDir, regionId), RenderFilePaths.SourcePbf);

    private async IAsyncEnumerable<Poi> MapRawPoisAsync(IAsyncEnumerable<RawPoi> rawPois, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var rawPoi in rawPois.WithCancellation(ct))
        {
            yield return _rawPoiMapper.Map(rawPoi);
        }
    }
}
