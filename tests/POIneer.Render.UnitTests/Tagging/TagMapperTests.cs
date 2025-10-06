using Xunit;
using POIneer.Render.Domain.Services;
using POIneer.Render.Domain.Models;
using System.Collections.Generic;

namespace POIneer.Render.UnitTests.Tagging;

public class TagMapperTheoryTests
{
    // --- amenity → Category --------------------------------------------------

    [Theory]
    [InlineData("restaurant", Category.Food)]
    [InlineData("cafe", Category.Food)]
    [InlineData("bar", Category.Food)]
    [InlineData("fast_food", Category.Food)]
    [InlineData("pub", Category.Food)]
    [InlineData("biergarten", Category.Food)]
    [InlineData("school", Category.Education)]
    [InlineData("university", Category.Education)]
    [InlineData("kindergarten", Category.Education)]
    [InlineData("library", Category.Education)]
    [InlineData("hospital", Category.Health)]
    [InlineData("clinic", Category.Health)]
    [InlineData("doctors", Category.Health)]
    [InlineData("dentist", Category.Health)]
    [InlineData("pharmacy", Category.Health)]
    [InlineData("bus_station", Category.Transport)]
    [InlineData("ferry_terminal", Category.Transport)]
    [InlineData("parking", Category.Transport)]
    [InlineData("bicycle_parking", Category.Transport)]
    [InlineData("bank", Category.Shopping)] // finance mapped to Shopping per current rules
    [InlineData("post_office", Category.Other)]
    public void Amenity_values_map_to_expected_category(string amenity, Category expected)
    {
        var cat = PoiTagMapper.Map(new Dictionary<string, string>
        {
            ["amenity"] = amenity
        });

        Assert.Equal(expected, cat);
    }

    // --- shop → Category -----------------------------------------------------

    [Theory]
    [InlineData("supermarket", Category.Food)]
    [InlineData("convenience", Category.Food)]
    [InlineData("bakery", Category.Food)]
    [InlineData("butcher", Category.Food)]
    [InlineData("greengrocer", Category.Food)]
    [InlineData("clothes", Category.Shopping)]
    [InlineData("shoes", Category.Shopping)]
    [InlineData("fashion", Category.Shopping)]
    [InlineData("electronics", Category.Shopping)]
    [InlineData("mobile_phone", Category.Shopping)]
    [InlineData("furniture", Category.Shopping)]
    [InlineData("hardware", Category.Shopping)]
    [InlineData("books", Category.Shopping)]
    [InlineData("gift", Category.Shopping)]
    [InlineData("florist", Category.Shopping)]
    [InlineData("hairdresser", Category.Shopping)]
    public void Shop_values_map_to_expected_category(string shop, Category expected)
    {
        var cat = PoiTagMapper.Map(new Dictionary<string, string>
        {
            ["shop"] = shop
        });

        Assert.Equal(expected, cat);
    }

    // --- tourism → Category --------------------------------------------------

    [Theory]
    [InlineData("hotel", Category.Culture)]
    [InlineData("motel", Category.Culture)]
    [InlineData("guest_house", Category.Culture)]
    [InlineData("hostel", Category.Culture)]
    [InlineData("museum", Category.Culture)]
    [InlineData("gallery", Category.Culture)]
    [InlineData("attraction", Category.Culture)]
    [InlineData("viewpoint", Category.Culture)]
    [InlineData("theme_park", Category.Culture)]
    [InlineData("zoo", Category.Culture)]
    [InlineData("aquarium", Category.Culture)]
    [InlineData("camp_site", Category.Culture)]
    [InlineData("caravan_site", Category.Culture)]
    public void Tourism_values_map_to_expected_category(string tourism, Category expected)
    {
        var cat = PoiTagMapper.Map(new Dictionary<string, string>
        {
            ["tourism"] = tourism
        });

        Assert.Equal(expected, cat);
    }

    // --- leisure → Category --------------------------------------------------

    [Theory]
    [InlineData("park", Category.Leisure)]
    [InlineData("playground", Category.Leisure)]
    [InlineData("garden", Category.Leisure)]
    [InlineData("sports_centre", Category.Leisure)]
    [InlineData("stadium", Category.Leisure)]
    [InlineData("pitch", Category.Leisure)]
    [InlineData("fitness_centre", Category.Leisure)]
    [InlineData("swimming_pool", Category.Leisure)]
    [InlineData("golf_course", Category.Leisure)]
    [InlineData("ice_rink", Category.Leisure)]
    public void Leisure_values_map_to_expected_category(string leisure, Category expected)
    {
        var cat = PoiTagMapper.Map(new Dictionary<string, string>
        {
            ["leisure"] = leisure
        });

        Assert.Equal(expected, cat);
    }

    // --- healthcare → Category ----------------------------------------------

    [Theory]
    [InlineData("hospital", Category.Health)]
    [InlineData("clinic", Category.Health)]
    [InlineData("doctor", Category.Health)]
    [InlineData("dentist", Category.Health)]
    [InlineData("pharmacy", Category.Health)]
    [InlineData("laboratory", Category.Health)]
    public void Healthcare_values_map_to_expected_category(string healthcare, Category expected)
    {
        var cat = PoiTagMapper.Map(new Dictionary<string, string>
        {
            ["healthcare"] = healthcare
        });

        Assert.Equal(expected, cat);
    }

    // --- precedence / tie-breaker -------------------------------------------
    // Your mapper checks in order: amenity → shop → tourism → leisure → healthcare
    // Verify precedence if multiple tags exist.

    [Fact]
    public void Amenity_takes_precedence_over_shop()
    {
        var cat = PoiTagMapper.Map(new Dictionary<string, string>
        {
            ["amenity"] = "restaurant",
            ["shop"] = "electronics"
        });

        Assert.Equal(Category.Food, cat); // amenity wins
    }

    [Fact]
    public void Shop_takes_precedence_over_tourism()
    {
        var cat = PoiTagMapper.Map(new Dictionary<string, string>
        {
            ["shop"] = "books",
            ["tourism"] = "museum"
        });

        Assert.Equal(Category.Shopping, cat); // shop wins
    }

    [Fact]
    public void Tourism_takes_precedence_over_leisure()
    {
        var cat = PoiTagMapper.Map(new Dictionary<string, string>
        {
            ["tourism"] = "zoo",
            ["leisure"] = "park"
        });

        Assert.Equal(Category.Culture, cat); // tourism wins
    }

    // --- unknown / missing ---------------------------------------------------

    [Fact]
    public void Unknown_or_missing_tags_map_to_other()
    {
        Assert.Equal(Category.Other, PoiTagMapper.Map(new Dictionary<string, string>()));

        Assert.Equal(Category.Other, PoiTagMapper.Map(new Dictionary<string, string>
        {
            ["amenity"] = "totally_unknown_value"
        }));

        Assert.Equal(Category.Other, PoiTagMapper.Map(new Dictionary<string, string>
        {
            ["shop"] = "not_a_real_shop"
        }));
    }
}
