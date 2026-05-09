namespace POIneer.Render.Application.Contracts;

// Data Transfer Object for Points of Interest (POIs)
public sealed record PoiDto(
    long OsmId,
    string? Name,
    string? Amenity,
    double Latitude,
    double Longitude);
