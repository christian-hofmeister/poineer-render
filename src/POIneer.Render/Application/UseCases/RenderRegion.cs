namespace POIneer.Render.Application.UseCases;

using Microsoft.Extensions.Logging;
using POIneer.Render.Ports;
using POIneer.Render.Application.Contracts;

public sealed class RenderRegion
{
    private readonly ILogger<RenderRegion> _logger;
    private readonly IPolygonCutter _polygonCutter;
    private readonly IOsmReader _osmReader;
    private readonly IExporter _exporter;

    public RenderRegion(
        ILogger<RenderRegion> log,
        IPolygonCutter polygonCutter,
        IOsmReader osmReader,
        IExporter exporter)
    {
        _logger = log;
        _polygonCutter = polygonCutter;
        _osmReader = osmReader;
        _exporter = exporter;
    }

    public async Task RunAsync(
        RegionDto regionDto,
        string workDir,
        string outDir,
        CancellationToken ct = default)
    {
        // 1) Download PBF (done outside, or inject a downloader port if you prefer)
        var pbfPath = Path.Combine(workDir, $"{regionDto.Id}.osm.pbf");
        if (!File.Exists(pbfPath))
            throw new FileNotFoundException($"PBF not found: {pbfPath}");

        _logger.LogInformation("({Id}) Cutting polygon...", regionDto.Id);
        var cutPbf = await _polygonCutter.CutAsync(pbfPath, regionDto.Poly, ct);

        _logger.LogInformation("({Id}) Reading OSM → POIs...", regionDto.Id);
        var pois = _osmReader.ReadAsync(cutPbf, ct);

        var outPath = Path.Combine(outDir, $"{regionDto.Id}.sqlite");
        _logger.LogInformation("({Id}) Exporting to SQLite: {Out}", regionDto.Id, outPath);
        await _exporter.ExportAsync(pois, outPath, ct);

        _logger.LogInformation("({Id}) Done.", regionDto.Id);
    }
}