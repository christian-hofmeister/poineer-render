using POIneer.Render.Domain.Models;

namespace POIneer.Render.Domain;

public interface IPoiRepository
{
    Task<IReadOnlyList<Poi>> GetAllAsync(int limit = 100);
}
