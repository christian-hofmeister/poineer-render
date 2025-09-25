using NUnit.Framework;
using POIneer.Render.Domain.Services;
using POIneer.Render.Domain.Models;

namespace POIneer.Render.UnitTests.Tagging;

[TestFixture]
public class TagMapperTests
{
    /// <summary>
    /// Tests that the <see cref="PoiTagMapper.Map"/> method correctly maps an OSM "amenity" tag with the value "restaurant"
    /// to the <see cref="Category.Food"/> category.
    /// </summary>
    [Test]
    public void Maps_Osm_Amenity_To_Category()
    {
        // Arrange

        // Act
        var cat = PoiTagMapper.Map(new Dictionary<string,string> {
            ["amenity"] = "restaurant"
        });

        // Assert
        Assert.That(cat, Is.EqualTo(Category.Food));
    }

    [Test]
    public void Missing_Tags_Result_In_Unknown()
    {
        var cat = PoiTagMapper.Map(new Dictionary<string,string>());
        Assert.That(cat, Is.EqualTo(Category.Other));
    }
}
