namespace POIneer.Render.Adapters.Input;
using System.Text.Json;
using POIneer.Render.Application.Contracts;
using POIneer.Render.Ports;

public sealed class GeofabrikRegionSource : IRegionSource
{
    public async Task<IReadOnlyList<RegionDto>> GetRegionsAsync(
        string regionsJsonPath,
        CancellationToken ct = default)
    {
        await using var s = File.OpenRead(regionsJsonPath);
        var regions = await JsonSerializer.DeserializeAsync<List<RegionDto>>(s, cancellationToken: ct)
                      ?? new();
        return regions.Where(r => !string.IsNullOrWhiteSpace(r.PbfUrl)).ToList();
    }
}