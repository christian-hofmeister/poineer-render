namespace POIneer.Render.Adapters.Input;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using POIneer.Render.Application.Contracts;
using POIneer.Render.Ports;

public sealed class GeofabrikRegionSource : IRegionSource
{
    ILogger<Runner> _logger;

    public async Task<IReadOnlyList<RegionDto>> GetRegionsAsync(
        string regionsJsonPath,
        ILogger<Runner> log,
        CancellationToken cancellationToken = default)
    {
        _logger = log;
        _logger.LogInformation("Loading regions from JSON file: {RegionsJsonPath}", regionsJsonPath);

        await using var fileStream = File.OpenRead(regionsJsonPath);
        var regions = await JsonSerializer.DeserializeAsync<List<RegionDto>>(
            fileStream,
            cancellationToken: cancellationToken)
                      ?? new();

        _logger.LogInformation("Loaded {RegionCount} regions from JSON file.", regions.Count);
        return regions.Where(r => !string.IsNullOrWhiteSpace(r.PbfUrl)).ToList();
    }
}