using POIneer.Render.Domain.Models;

namespace POIneer.Render.Domain.Services;

public static class PoiTagMapper
{
    private static readonly Dictionary<string, Category> AmenityMap = new()
    {
        // Food & Drink
        ["restaurant"] = Category.Food,
        ["cafe"] = Category.Food,
        ["bar"] = Category.Food,
        ["fast_food"] = Category.Food,
        ["pub"] = Category.Food,
        ["biergarten"] = Category.Food,

        // Education
        ["school"] = Category.Education,
        ["university"] = Category.Education,
        ["kindergarten"] = Category.Education,
        ["library"] = Category.Education,

        // Health
        ["hospital"] = Category.Health,
        ["clinic"] = Category.Health,
        ["doctors"] = Category.Health,
        ["dentist"] = Category.Health,
        ["pharmacy"] = Category.Health,

        // Transport
        ["bus_station"] = Category.Transport,
        ["ferry_terminal"] = Category.Transport,
        ["parking"] = Category.Transport,
        ["bicycle_parking"] = Category.Transport,

        // Public / Other
        ["bank"] = Category.Shopping, // kann man auch Finance trennen
        ["post_office"] = Category.Other,
        ["police"] = Category.Other,
        ["fire_station"] = Category.Other,
        ["townhall"] = Category.Other
    };

    private static readonly Dictionary<string, Category> ShopMap = new()
    {
        // Food-related shops
        ["supermarket"] = Category.Food,
        ["convenience"] = Category.Food,
        ["bakery"] = Category.Food,
        ["butcher"] = Category.Food,
        ["greengrocer"] = Category.Food,

        // Non-food shops
        ["clothes"] = Category.Shopping,
        ["shoes"] = Category.Shopping,
        ["fashion"] = Category.Shopping,
        ["jewelry"] = Category.Shopping,
        ["electronics"] = Category.Shopping,
        ["mobile_phone"] = Category.Shopping,
        ["furniture"] = Category.Shopping,
        ["hardware"] = Category.Shopping,
        ["books"] = Category.Shopping,
        ["gift"] = Category.Shopping,
        ["florist"] = Category.Shopping,
        ["hairdresser"] = Category.Shopping
    };

    private static readonly Dictionary<string, Category> TourismMap = new()
    {
        ["hotel"] = Category.Culture,
        ["motel"] = Category.Culture,
        ["guest_house"] = Category.Culture,
        ["hostel"] = Category.Culture,
        ["museum"] = Category.Culture,
        ["gallery"] = Category.Culture,
        ["attraction"] = Category.Culture,
        ["viewpoint"] = Category.Culture,
        ["theme_park"] = Category.Culture,
        ["zoo"] = Category.Culture,
        ["aquarium"] = Category.Culture,
        ["camp_site"] = Category.Culture,
        ["caravan_site"] = Category.Culture
    };

    private static readonly Dictionary<string, Category> LeisureMap = new()
    {
        ["park"] = Category.Leisure,
        ["playground"] = Category.Leisure,
        ["garden"] = Category.Leisure,
        ["sports_centre"] = Category.Leisure,
        ["stadium"] = Category.Leisure,
        ["pitch"] = Category.Leisure,
        ["fitness_centre"] = Category.Leisure,
        ["swimming_pool"] = Category.Leisure,
        ["golf_course"] = Category.Leisure,
        ["ice_rink"] = Category.Leisure
    };

    private static readonly Dictionary<string, Category> HealthcareMap = new()
    {
        ["hospital"] = Category.Health,
        ["clinic"] = Category.Health,
        ["doctor"] = Category.Health,
        ["dentist"] = Category.Health,
        ["pharmacy"] = Category.Health,
        ["laboratory"] = Category.Health
    };

    public static Category Map(Dictionary<string, string> tags)
    {
        if (tags.TryGetValue("amenity", out var amenity) && AmenityMap.TryGetValue(amenity, out var cat))
            return cat;

        if (tags.TryGetValue("shop", out var shop) && ShopMap.TryGetValue(shop, out cat))
            return cat;

        if (tags.TryGetValue("tourism", out var tourism) && TourismMap.TryGetValue(tourism, out cat))
            return cat;

        if (tags.TryGetValue("leisure", out var leisure) && LeisureMap.TryGetValue(leisure, out cat))
            return cat;

        if (tags.TryGetValue("healthcare", out var healthcare) && HealthcareMap.TryGetValue(healthcare, out cat))
            return cat;

        return Category.Other;
    }
}
