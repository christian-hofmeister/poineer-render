namespace POIneer.Render.Adapters.Input;

using System.Diagnostics;
using POIneer.Render.Ports;

public sealed class OsmiumPolygonCutter : IPolygonCutter
{
    public async Task<string> CutAsync(
        string pbfPath,
        string? polyPath,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(polyPath) || !File.Exists(polyPath))
            return pbfPath;

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
