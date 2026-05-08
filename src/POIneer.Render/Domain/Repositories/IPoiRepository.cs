using POIneer.Render.Domain.Models;

namespace POIneer.Render.Domain;

public interface IPoiRepository
{
    Task AddAsync(Poi poi, CancellationToken ct = default);
    Task<IReadOnlyList<Poi>> GetAllAsync(int limit = 100, CancellationToken ct = default);

    Task<IReadOnlyList<Poi>> GetByAmenityAsync(
        string amenity,
        int limit = 100,
        CancellationToken ct = default);
    Task<IReadOnlyList<Poi>> GetByNameAsync(
        string name,
        int limit = 100,
        CancellationToken ct = default);

    Task<IReadOnlyList<Poi>> GetByBoundingBoxAsync(
        BoundingBox boundingBox,
        int limit = 100,
        CancellationToken ct = default);
}
