using POIneer.Render.Domain.Models;
using POIneer.Render.Application.Ports.Model;

namespace POIneer.Render.Application.Mapping;

public interface IRawPoiMapper
{
    Poi Map(RawPoi rawPoi);
}