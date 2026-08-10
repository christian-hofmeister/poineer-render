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

        var outputSqlitePathFull = GetOutputSqlitePath(outDir, regionDto.Id, recreateDatabase, regionDto.Id);
        if (outputSqlitePathFull is null)
        {
            return;
        }

        _logger.LogInformation("({Id}) Cutting polygon...", regionDto.Id);
        var cutPbf = await _polygonCutter.CutAsync(pbfPath, regionDto.Poly, ct);

        _logger.LogInformation("({Id}) Reading OSM → POIs...", regionDto.Id);
        var rawPois = _osmReader.ReadAmenityNodesAsync(cutPbf, ct);

        _logger.LogInformation("({Id}) Initializing SQLite database: {Out}", regionDto.Id, outputSqlitePathFull);
        await _dbInit.InitializeAsync(
            outputSqlitePathFull,
            ct);

        _logger.LogInformation("({Id}) Exporting to SQLite: {Out}", regionDto.Id, outputSqlitePathFull);
        await _exporter.ExportAsync(
            MapRawPoisAsync(rawPois, ct),
            outputSqlitePathFull,
            ct);

        _logger.LogInformation("({Id}) Validating generated dataset: {Out}", regionDto.Id, outputSqlitePathFull);
        var result = await _datasetValidator.ValidateAsync(
            outputSqlitePathFull,
            ct);

        if (!result.IsValid)
        {
            var quarantinePath = QuarantineInvalidDataset(outDir, regionDto.Id, outputSqlitePathFull);

            _logger.LogError(
                "({Id}) Generated dataset failed validation and was moved to quarantine: {QuarantinePath}. Errors: {Errors}",
                regionDto.Id,
                quarantinePath,
                string.Join("; ", result.Errors));

            throw new InvalidOperationException(
                $"Generated dataset for region '{regionDto.Id}' is invalid and was quarantined at {quarantinePath}: {string.Join(", ", result.Errors)}");
        }
        _logger.LogInformation("({Id}) Done.", regionDto.Id);
    }

    /// <summary>
    /// Moves an invalid dataset out of the canonical output location into a per-region
    /// quarantine folder so it doesn't get mistaken for a successfully rendered dataset
    /// on a subsequent run, while still being preserved for post-mortem inspection.
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

    private static string GetPbfPath(string workDir, string regionId) =>
        Path.Combine(workDir, regionId, "osm.pbf");

    private string? GetOutputSqlitePath(string outDir, string regionId, bool recreateDatabase, string logId)
    {
        var regionOutDir = Path.Combine(outDir, regionId);
        Directory.CreateDirectory(regionOutDir);

        var outRegionPath = Path.Combine(regionOutDir, "poi.sqlite");
        if (File.Exists(outRegionPath) && !recreateDatabase)
        {
            _logger.LogInformation("({Id}) Output SQLite already exists at {Out}, skipping rendering (overwrite disabled).", logId, outRegionPath);
            return null;
        }

        if (File.Exists(outRegionPath) && recreateDatabase)
        {
            _logger.LogInformation("({Id}) Output SQLite already exists at {Out}, but overwrite is enabled, re-rendering.", logId, outRegionPath);
            File.Delete(outRegionPath);
        }

        return Path.GetFullPath(outRegionPath);
    }

    private async IAsyncEnumerable<Poi> MapRawPoisAsync(IAsyncEnumerable<RawPoi> rawPois, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        await foreach (var rawPoi in rawPois.WithCancellation(ct))
        {
            yield return _rawPoiMapper.Map(rawPoi);
        }
    }
}
