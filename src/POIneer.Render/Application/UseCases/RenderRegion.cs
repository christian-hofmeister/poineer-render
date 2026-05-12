namespace POIneer.Render.Application.UseCases;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POIneer.Render.Application.Contracts;
using POIneer.Render.Application.Mapping;
using POIneer.Render.Application.Options;
using POIneer.Render.Domain.Models;
using POIneer.Render.Ports;

public sealed class RenderRegion : IRenderRegion
{
    private readonly ILogger<RenderRegion> _logger;
    private readonly IPolygonCutter _polygonCutter;
    private readonly IOsmReader _osmReader;
    private readonly ISqliteDatabaseInitializer _dbInit;
    private readonly IExporter _exporter;
    private readonly IRawPoiMapper _rawPoiMapper;
    private readonly RendererOptions _rendererOptions;

    public RenderRegion(
        ILogger<RenderRegion> log,
        IPolygonCutter polygonCutter,
        ISqliteDatabaseInitializer dbInit,
        IOsmReader osmReader,
        IExporter exporter,
        IRawPoiMapper rawPoiMapper,
        IOptions<RendererOptions> options)
    {
        _logger = log;
        _polygonCutter = polygonCutter;
        _osmReader = osmReader;
        _dbInit = dbInit;
        _exporter = exporter;
        _rawPoiMapper = rawPoiMapper;
        _rendererOptions = options.Value;
    }

    public async Task RunAsync(
        RegionDto regionDto,
        string workDir,
        string outDir,
        CancellationToken ct = default)
    {
        var pbfPath = Path.Combine(workDir, regionDto.Id, "osm.pbf");
        //TODO: consider moving pbf downloading to a separate use case, to keep this one focused on the rendering logic
        var recreateDatabase = _rendererOptions.OverwriteDatabase || _rendererOptions.OverwritePbf;

        if (!File.Exists(pbfPath))
            throw new FileNotFoundException($"PBF not found: {pbfPath}");

        _logger.LogInformation("({Id}) Cutting polygon...", regionDto.Id);
        var cutPbf = await _polygonCutter.CutAsync(pbfPath, regionDto.Poly, ct);

        _logger.LogInformation("({Id}) Reading OSM → POIs...", regionDto.Id);

        var rawPois = _osmReader.ReadAmenityNodesAsync(cutPbf, ct);

        async IAsyncEnumerable<Poi> MapRawPoisAsync()
        {
            await foreach (var rawPoi in rawPois.WithCancellation(ct))
            {
                yield return _rawPoiMapper.Map(rawPoi);
            }
        }

        var regionOutDir = Path.Combine(outDir, regionDto.Id);
        Directory.CreateDirectory(regionOutDir);

        var outRegionPath = Path.Combine(regionOutDir, "poi.sqlite");
        if (File.Exists(outRegionPath) && !_rendererOptions.OverwriteDatabase)
        {
            _logger.LogInformation("({Id}) Output SQLite already exists at {Out}, skipping rendering (overwrite disabled).", regionDto.Id, outRegionPath);
            return;
        }
        else if (File.Exists(outRegionPath) && recreateDatabase)
        {
            _logger.LogInformation("({Id}) Output SQLite already exists at {Out}, but overwrite is enabled, re-rendering.", regionDto.Id, outRegionPath);
            File.Delete(outRegionPath);
        }
        var outputSqlitePathFull = Path.GetFullPath(outRegionPath);

        _logger.LogInformation("({Id}) Initializing SQLite database: {Out}", regionDto.Id, outputSqlitePathFull);

        await _dbInit.InitializeAsync(
            outputSqlitePathFull,
            ct);

        _logger.LogInformation("({Id}) Exporting to SQLite: {Out}", regionDto.Id, outputSqlitePathFull);
        await _exporter.ExportAsync(MapRawPoisAsync(), outputSqlitePathFull, ct);

        _logger.LogInformation("({Id}) Done.", regionDto.Id);
    }
}
