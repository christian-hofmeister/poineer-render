namespace POIneer.Render.Ports;

using POIneer.Render.Application.Contracts;

public interface IRenderRegion
{
    Task RunAsync(
        RegionDto regionDto,
        string workDir,
        string outDir,
        CancellationToken ct = default);
}