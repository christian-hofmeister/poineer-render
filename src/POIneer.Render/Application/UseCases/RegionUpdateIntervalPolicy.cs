using Microsoft.Extensions.Options;
using POIneer.Render.Application.Contracts;
using POIneer.Render.Application.Options;

namespace POIneer.Render.Application.UseCases;

public sealed class RegionUpdateIntervalPolicy(IOptions<RendererOptions> options)
{
    public int GetIntervalDays(RegionDto region)
    {
        var intervalDays = region.UpdateIntervalDays ?? options.Value.DefaultUpdateIntervalDays;
        if (intervalDays <= 0)
            throw new ArgumentOutOfRangeException(nameof(region),
                $"Region '{region.Id}': UpdateIntervalDays must resolve to a value greater than 0.");

        return intervalDays;
    }
}
