using POIneer.Render.Infrastructure.Osm.Models;
using System.Runtime.CompilerServices;

namespace POIneer.Render.Ports;

public interface IOsmReader
{
    IAsyncEnumerable<RawPoi> ReadAmenityNodesAsync(
        string pbfPath,
        CancellationToken cancellationToken = default);
}