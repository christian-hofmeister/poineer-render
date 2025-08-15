using POIneer.Render.Domain.Models;

namespace POIneer.Render.Domain.Services;

// Maps OSM tags to POI categories
public static class PoiTagMapper
{
    // Map OSM tags to categories (pure logic, easy to unit test)
    public static Category Map(IDictionary<string, string> tags)
    {
        if (tags.TryGetValue("amenity", out var a))
        {
            if (a is "restaurant" or "cafe" or "bar") return Models.Category.Food;
        }
        if (tags.ContainsKey("tourism")) return Models.Category.Culture;
        if (tags.ContainsKey("natural")) return Models.Category.Nature;
        return Models.Category.Other;
    }
}