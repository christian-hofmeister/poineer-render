namespace POIneer.Render.Domain.Models;

/// <summary>
/// Represents a Point of Interest (POI) with relevant details.
/// </summary>
public sealed record Poi(
    long Id,
    string OsmId,
    string? Name,
    string? Amenity,
    double Latitude,
    double Longitude
);