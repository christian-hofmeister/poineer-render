using POIneer.Render.Domain.Models;

namespace POIneer.Render.Domain;

public interface IPoiRepository
{
    Task AddAsync(Poi poi, CancellationToken ct = default);
    Task<IReadOnlyList<Poi>> GetAllAsync(int limit = 100, CancellationToken ct = default);
}
