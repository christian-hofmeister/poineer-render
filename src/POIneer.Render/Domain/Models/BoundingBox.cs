namespace POIneer.Render.Domain.Models;

public sealed record BoundingBox(
    GeoPoint NorthWest,
    GeoPoint SouthEast);