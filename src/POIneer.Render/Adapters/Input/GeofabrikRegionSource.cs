namespace POIneer.Render.Adapters.Input;

using System.Text.Json;
using Microsoft.Extensions.Logging;
using POIneer.Render.Application.Contracts;
using POIneer.Render.Domain.Models;
using POIneer.Render.Ports;

public sealed class GeofabrikRegionSource : IRegionSource
{
    private readonly ILogger<GeofabrikRegionSource> _logger;

    public GeofabrikRegionSource(ILogger<GeofabrikRegionSource> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<IReadOnlyList<RegionDto>> GetRegionsAsync(
        string regionsJsonPath,
        CancellationToken ct = default)
    {
        _logger.LogInformation("Loading regions from JSON file: {RegionsJsonPath}", regionsJsonPath);

        await using var fileStream = File.OpenRead(regionsJsonPath);
        var regions = await JsonSerializer.DeserializeAsync<List<RegionDto>>(
            fileStream,
            cancellationToken: ct)
                      ?? new();

        for (var index = 0; index < regions.Count; index++)
        {
            try
            {
                RegionIdentifier.ValidateHierarchicalId(regions[index]?.Id!, nameof(RegionDto.Id));
            }
            catch (ArgumentException ex)
            {
                throw new InvalidDataException(
                    $"Invalid region ID at entry {index + 1} in '{regionsJsonPath}'. {ex.Message}", ex);
            }
        }

        _logger.LogInformation("Loaded {RegionCount} regions from JSON file.", regions.Count);
        return regions;
    }
}
