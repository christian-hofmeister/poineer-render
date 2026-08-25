namespace POIneer.Render.Application.Options;

public static class VectorTileOptionsValidation
{
    public const string RequiredPlanetilerPathsMessage =
        "VectorTiles:JavaExecutablePath and VectorTiles:PlanetilerJarPath must be set when vector tile generation is enabled";

    public const string ZoomRangeMessage =
        "VectorTiles:MinZoom and VectorTiles:MaxZoom must be non-negative, and MinZoom must be less than or equal to MaxZoom";

    public static bool HasRequiredPlanetilerPaths(VectorTileOptions options)
        => !options.Enabled
           || (!string.IsNullOrWhiteSpace(options.JavaExecutablePath)
               && !string.IsNullOrWhiteSpace(options.PlanetilerJarPath));

    public static bool HasValidZoomRange(VectorTileOptions options)
    {
        if (options.MinZoom is < 0 || options.MaxZoom is < 0)
            return false;

        return options.MinZoom is null
               || options.MaxZoom is null
               || options.MinZoom <= options.MaxZoom;
    }
}
