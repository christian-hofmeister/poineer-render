namespace POIneer.Render.Infrastructure.Osm.Models;

public sealed record RawPoi(
    long OsmId,
    double Latitude,
    double Longitude,
    string Amenity,
    string? Name,
    IReadOnlyDictionary<string, string> Tags);