namespace POIneer.Render.Ports;

using Microsoft.Extensions.Logging;
using POIneer.Render.Application.Contracts;

public interface IRegionSource
{
    // Retrieves a list of regions from a JSON file
    Task<IReadOnlyList<RegionDto>> GetRegionsAsync(
        string regionsJsonPath,
        ILogger<Runner> log,
        CancellationToken ct = default);
}