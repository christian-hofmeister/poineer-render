namespace POIneer.Render.Domain.Models;

/// <summary>
/// Represents a Point of Interest (POI) with relevant details.
/// </summary>
public sealed record Poi(
    long? Id, // Id is nullable because it may not be set until the POI is stored in a database that generates the ID.
    long OsmId,
    string? Name,
    string? Amenity,
    GeoPoint Location
);