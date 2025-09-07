namespace POIneer.Render.Adapters.Input;
using System.Text.Json;
using POIneer.Render.Application.Contracts;
using POIneer.Render.Ports;

public sealed class GeofabrikRegionSource : IRegionSource
{
    public async Task<IReadOnlyList<RegionDto>> GetRegionsAsync(
        string regionsJsonPath,
        CancellationToken cancellationToken = default)
    {
        await using var fileStream = File.OpenRead(regionsJsonPath);
        var regions = await JsonSerializer.DeserializeAsync<List<RegionDto>>(
            fileStream,
            cancellationToken: cancellationToken)
                      ?? new();
        return regions.Where(r => !string.IsNullOrWhiteSpace(r.PbfUrl)).ToList();
    }
}