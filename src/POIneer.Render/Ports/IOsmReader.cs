namespace POIneer.Render.Ports;

using POIneer.Render.Application.Contracts;

public interface IOsmReader
{
    // Reads POIs from a (possibly pre-cut) PBF into a stream of POI DTOs
    IAsyncEnumerable<PoiDto> ReadAsync(string pbfPath, CancellationToken ct = default);
}