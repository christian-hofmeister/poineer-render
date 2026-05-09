using POIneer.Render.Domain.Models;
using POIneer.Render.Application.Ports.Model;

namespace POIneer.Render.Application.Mapping;

public sealed class RawPoiMapper : IRawPoiMapper
{
    public Poi Map(RawPoi rawPoi)
    {
        return new Poi(
            Id: null, // Id will be set later when stored in a database
            OsmId: rawPoi.OsmId,
            Name: rawPoi.Name,
            Amenity: rawPoi.Amenity,
            Location: new GeoPoint(rawPoi.Latitude, rawPoi.Longitude)
        );
    }
}