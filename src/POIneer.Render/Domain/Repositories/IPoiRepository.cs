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

    Task<IReadOnlyList<Poi>> GetByLocationAsync(
        double latitude,
        double longitude,
        double radiusInMeters,
        int limit = 100,
        CancellationToken ct = default);
}
