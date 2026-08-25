namespace POIneer.Render.Application.Options;

public sealed class VectorTileOptions
{
    public const string SectionName = "VectorTiles";

    public bool Enabled { get; init; } = false;

    public string JavaExecutablePath { get; init; } = "java";

    public string? PlanetilerJarPath { get; init; }

    public string? JavaMaxHeapSize { get; init; }

    public string? Profile { get; init; }

    public int? MinZoom { get; init; }

    public int? MaxZoom { get; init; }

    public string[] AdditionalArguments { get; init; } = [];
}
