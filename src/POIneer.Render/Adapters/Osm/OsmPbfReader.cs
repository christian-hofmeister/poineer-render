namespace POIneer.Render.Adapters.Osm;
using POIneer.Render.Ports;
using POIneer.Render.Application.Contracts;

// TODO: replace with real PBF reader (e.g., via OsmSharp)
public sealed class OsmPbfReader : IOsmReader
{
    public async IAsyncEnumerable<PoiDto> ReadAsync(string pbfPath, System.Threading.CancellationToken ct = default)
    {
        // NOTE: Replace with real implementation; this is a stub.
        await Task.Yield();
        yield break;
    }
}