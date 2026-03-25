namespace POIneer.Render.Cli;

// Strongly-typed configuration bound from appsettings/env/CLI
public sealed class RendererOptions
{
    // Default fallbacks if nothing is configured
    public string WorkDir { get; init; } = "renderer-work-dir-fallback";
    public string OutDir { get; init; } = "renderer-out-dir-fallback";
    public string RegionsJson { get; init; } = "src/POIneer.Render/Cli/config/regions.production.json";
    public string? OnlyRegionId { get; init; }
}
