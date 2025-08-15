namespace POIneer.Render.Domain.Models;

public sealed record Poi(long OsmId, string? Name, Category Category, double Lon, double Lat);
