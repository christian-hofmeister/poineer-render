using POIneer.Render.Ports;
using POIneer.Render.Application.Ports.Model;
using System.Runtime.CompilerServices;
using OsmSharp.Streams;
using OsmSharp;

namespace POIneer.Render.Infrastructure.Adapters.Osm;

public sealed class OsmPbfReader : IOsmReader
{
    public async IAsyncEnumerable<RawPoi> ReadAmenityNodesAsync(
        string pbfPath,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        if (!File.Exists(pbfPath))
            throw new FileNotFoundException($"PBF file not found: {pbfPath}");
        if (Path.GetExtension(pbfPath)?.ToLower() != ".pbf")
            throw new ArgumentException($"Invalid file type: {pbfPath}. Expected a .pbf file.");

        using var fileStream = File.OpenRead(pbfPath);

        var source = new PBFOsmStreamSource(fileStream);
        foreach (var osmGeo in source)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (osmGeo is not Node node)
            {
                continue;
            }

            if (node.Latitude is null || node.Longitude is null)
            {
                continue;
            }

            if (node.Tags is null || !node.Tags.ContainsKey("amenity"))
            {
                continue;
            }

            yield return new RawPoi(
                OsmId: node.Id!.Value,
                Latitude: node.Latitude.Value,
                Longitude: node.Longitude.Value,
                Amenity: node.Tags["amenity"],
                Name: node.Tags.ContainsKey("name") ? node.Tags["name"] : null,
                Tags: node.Tags.ToDictionary(tag => tag.Key, tag => tag.Value));
        }

    }
}