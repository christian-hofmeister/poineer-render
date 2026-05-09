using POIneer.Render.Domain.Models;
using POIneer.Render.Infrastructure.Osm.Models;

namespace POIneer.Render.Application.Mapping;

public interface IRawPoiMapper
{
    Poi Map(RawPoi rawPoi);
}