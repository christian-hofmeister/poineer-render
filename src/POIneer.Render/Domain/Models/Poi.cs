namespace POIneer.Render.Domain.Models;

/// <summary>
/// Represents a Point of Interest (POI) with relevant details.
/// </summary>
public sealed record Poi(
    long Id,
    long OsmId,
    string? Name,
    string? Amenity,
    GeoPoint Location
);