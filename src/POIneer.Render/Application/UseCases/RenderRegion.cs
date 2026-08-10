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
    private const string StagingFileSuffix = ".tmp";

    private static readonly string[] SqliteSidecarSuffixes = ["-wal", "-shm", "-journal"];

    private readonly ILogger<RenderRegion> _logger;
    private readonly IPolygonCutter _polygonCutter;
    private readonly IOsmReader _osmReader;
    private readonly ISqliteDatabaseInitializer _dbInit;
    private readonly IExporter _exporter;
    private readonly IRawPoiMapper _rawPoiMapper;
    private readonly RendererOptions _rendererOptions;
    private readonly IDatasetValidator _datasetValidator;

    public RenderRegion(
        ILogger<RenderRegion> log,
        IPolygonCutter polygonCutter,
        ISqliteDatabaseInitializer dbInit,
        IOsmReader osmReader,
        IExporter exporter,
        IRawPoiMapper rawPoiMapper,
        IOptions<RendererOptions> options,
        IDatasetValidator datasetValidator)
    {
        _logger = log;
        _polygonCutter = polygonCutter;
        _osmReader = osmReader;
        _dbInit = dbInit;
        _exporter = exporter;
        _rawPoiMapper = rawPoiMapper;
        _rendererOptions = options.Value;
        _datasetValidator = datasetValidator;
    }

    public async Task RunAsync(
        RegionDto regionDto,
        string workDir,
        string outDir,
        CancellationToken ct = default)
    {
        var pbfPath = GetPbfPath(workDir, regionDto.Id);
        var recreateDatabase = _rendererOptions.OverwriteDatabase || _rendererOptions.OverwritePbf;

        if (!File.Exists(pbfPath))
            throw new FileNotFoundException($"PBF not found: {pbfPath}");

        var regionOutDir = Path.Combine(outDir, regionDto.Id);
        Directory.CreateDirectory(regionOutDir);

        var canonicalPath = Path.GetFullPath(Path.Combine(regionOutDir, "poi.sqlite"));

        if (File.Exists(canonicalPath))
        {
            if (!recreateDatabase)
            {
                _logger.LogInformation("({Id}) Output SQLite already exists at {Out}, skipping rendering (overwrite disabled).", regionDto.Id, canonicalPath);
                return;
            }

            _logger.LogInformation(
                "({Id}) Output SQLite already exists at {Out}, but overwrite is enabled, re-rendering. It will only be replaced once the new dataset passes validation.",
                regionDto.Id,
                canonicalPath);
        }

        var stagingPath = canonicalPath + StagingFileSuffix;

        if (File.Exists(stagingPath))
        {
            _logger.LogWarning(
                "({Id}) Found a leftover staging database from a previous interrupted run at {Staging}, removing it before re-rendering.",
                regionDto.Id,
                stagingPath);
            DeleteSqliteDatabaseWithSidecars(stagingPath);
        }

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

        _logger.LogInformation("({Id}) Publishing validated dataset to {Out}", regionDto.Id, canonicalPath);
        PromoteStagingToCanonical(stagingPath, canonicalPath);

        _logger.LogInformation("({Id}) Done.", regionDto.Id);
    }

    /// <summary>
    /// Atomically publishes a validated staging database as the canonical output. The
    /// canonical path is only ever touched here, after validation has already passed, so
    /// a crash or an invalid render can never leave a partial or unvalidated file behind
    /// at the canonical location - a previously published dataset stays in place until a
    /// new one is confirmed valid.
    /// </summary>
    private static void PromoteStagingToCanonical(string stagingPath, string canonicalPath)
    {
        foreach (var suffix in SqliteSidecarSuffixes)
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

    /// <summary>
    /// Moves an invalid dataset out of the staging location into a per-region quarantine
    /// folder so it doesn't linger next to the canonical output, while still being
    /// preserved for post-mortem inspection.
    /// </summary>
    private static string QuarantineInvalidDataset(string outDir, string regionId, string invalidDatasetPath)
    {
        var quarantineDir = Path.Combine(outDir, regionId, "_failed");
        Directory.CreateDirectory(quarantineDir);

        var timestamp = DateTime.UtcNow.ToString("yyyyMMddHHmmssfff");
        var quarantinePath = Path.Combine(quarantineDir, $"poi.{timestamp}.sqlite");

        File.Move(invalidDatasetPath, quarantinePath, overwrite: true);

        foreach (var suffix in SqliteSidecarSuffixes)
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

        foreach (var suffix in SqliteSidecarSuffixes)
        {
            var sidecarPath = sqliteFilePath + suffix;
            if (File.Exists(sidecarPath))
            {
                File.Delete(sidecarPath);
            }
        }
    }

    private static string GetPbfPath(string workDir, string regionId) =>
        Path.Combine(workDir, regionId, "osm.pbf");

    private async IAsyncEnumerable<Poi> MapRawPoisAsync(IAsyncEnumerable<RawPoi> rawPois, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var rawPoi in rawPois.WithCancellation(ct))
        {
            yield return _rawPoiMapper.Map(rawPoi);
        }
    }
}
