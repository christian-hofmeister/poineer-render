using POIneer.Render.Domain;
using POIneer.Render.Domain.Models;

namespace POIneer.Render.TestHelpers.Sqlite;

public static class PoiSeedHelper
{
    public static async Task SeedAsync(
        IPoiRepository repository,
        params Poi[] pois)
    {
        foreach (var poi in pois)
        {
            await repository.AddAsync(poi);
        }
    }
}