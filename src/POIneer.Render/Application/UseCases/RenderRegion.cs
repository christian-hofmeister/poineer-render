namespace POIneer.Render.Application.UseCases;
using Microsoft.Extensions.Logging;
using POIneer.Render.Ports;
using POIneer.Render.Application.Contracts;

public sealed class RenderRegion
{
    private readonly ILogger<RenderRegion> _log;
    private readonly IPolygonCutter _cutter;
    private readonly IOsmReader _reader;
    private readonly IExporter _exporter;

    public RenderRegion(
        ILogger<RenderRegion> log,
        IPolygonCutter cutter,
        IOsmReader reader,
        IExporter exporter)
    {
        _log = log;
        _cutter = cutter;
        _reader = reader;
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

        _log.LogInformation("({Id}) Cutting polygon...", regionDto.Id);
        var cutPbf = await _cutter.CutAsync(pbfPath, regionDto.Poly, ct);

        _log.LogInformation("({Id}) Reading OSM → POIs...", regionDto.Id);
        var pois = _reader.ReadAsync(cutPbf, ct);

        var outPath = Path.Combine(outDir, $"{regionDto.Id}.sqlite");
        _log.LogInformation("({Id}) Exporting to SQLite: {Out}", regionDto.Id, outPath);
        await _exporter.ExportAsync(pois, outPath, ct);

        _log.LogInformation("({Id}) Done.", regionDto.Id);
    }
}