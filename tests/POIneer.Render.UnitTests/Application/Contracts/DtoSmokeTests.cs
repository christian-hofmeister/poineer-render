using POIneer.Render.Application.Contracts;
using Xunit;

namespace POIneer.Render.UnitTests.Application.Contracts;

public class DtoSmokeTests
{
    [Fact]
    public void RegionDto_Roundtrip_Properties()
    {
        var dto = new RegionDto("berlin", "Berlin", "https://download.geofabrik.de/europe/germany/berlin-latest.osm.pbf", null);
        Assert.Equal("berlin", dto.Id);
        Assert.Equal("Berlin", dto.Name);
        Assert.Equal("https://download.geofabrik.de/europe/germany/berlin-latest.osm.pbf", dto.PbfUrl);

    }

    [Fact]
    public void PoiDto_Roundtrip_Properties()
    {
        var dto = new PoiDto(1, "Cafe", "amenity", 13.4, 52.5);
        Assert.Equal(1, dto.OsmId);
        Assert.Equal("Cafe", dto.Name);
        Assert.Equal("amenity", dto.Amenity);
        Assert.Equal(13.4, dto.Latitude);
        Assert.Equal(52.5, dto.Longitude);
    }
}
