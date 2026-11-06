namespace POIneer.Render.Adapters.Input;

using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using POIneer.Render.Infrastructure.Pathing;
using POIneer.Render.Ports;

public sealed class OsmiumPolygonCutter(IHostEnvironment environment) : IPolygonCutter
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
            ArgumentList = { "extract", "-p", polyPath, pbfPath, "-o", output },
            RedirectStandardError = true,
            RedirectStandardOutput = true
        };
        using var proc = Process.Start(psi)!;
        await proc.WaitForExitAsync(ct);
        if (proc.ExitCode != 0) throw new InvalidOperationException($"osmium extract failed: {await proc.StandardError.ReadToEndAsync()}");
        return output;
    }
}
