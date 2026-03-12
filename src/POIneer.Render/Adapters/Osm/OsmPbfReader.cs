namespace POIneer.Render.Adapters.Osm;

using POIneer.Render.Ports;
using POIneer.Render.Application.Contracts;
using System.Runtime.CompilerServices;

// TODO: replace with real PBF reader (e.g., via OsmSharp)
public sealed class OsmPbfReader : IOsmReader
{
    public async IAsyncEnumerable<PoiDto> ReadAsync(
        string pbfPath,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        // NOTE: Replace with real implementation; this is a stub.
        // Simulate reading PBF and yielding POIs
        await Task.Yield();
        yield break;
    }
}