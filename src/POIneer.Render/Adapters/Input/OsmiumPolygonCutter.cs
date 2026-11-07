namespace POIneer.Render.Adapters.Input;

using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using POIneer.Render.Infrastructure.Pathing;
using POIneer.Render.Infrastructure.Process;
using POIneer.Render.Ports;

public sealed class OsmiumPolygonCutter(
    IHostEnvironment environment,
    IProcessRunner processRunner)
        : IPolygonCutter
{
    public async Task<string> CutAsync(
        string pbfPath,
        string? polyPath,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(polyPath))
            return pbfPath;

        polyPath = ConfiguredPathResolver.Resolve(environment.ContentRootPath, polyPath);
        if (!File.Exists(polyPath))
            throw new FileNotFoundException("Configured region polygon file was not found.", polyPath);

        var output = Path.Combine(Path.GetDirectoryName(pbfPath)!, Path.GetFileNameWithoutExtension(pbfPath) + ".cut.osm.pbf");
        var psi = new ProcessStartInfo
        {
            FileName = "osmium",
            ArgumentList = { "extract", "--overwrite", "-p", polyPath, pbfPath, "-o", output },
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        var result = await processRunner.RunAsync(psi, ct);
        if (result.ExitCode != 0)
            throw new InvalidOperationException($"osmium extract failed: {result.StandardError}");
        return output;
    }
}
