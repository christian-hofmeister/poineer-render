namespace POIneer.Render.Ports;

public interface IPolygonCutter
{
    // Returns path to cut PBF (can be same as input if no poly used)
    Task<string> CutAsync(string pbfPath, string? polyPath, CancellationToken ct = default);
}