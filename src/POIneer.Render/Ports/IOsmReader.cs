using POIneer.Render.Application.Ports.Model;

namespace POIneer.Render.Ports;

public interface IOsmReader
{
    IAsyncEnumerable<RawPoi> ReadAmenityNodesAsync(
        string pbfPath,
        CancellationToken cancellationToken = default);
}