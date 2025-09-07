namespace POIneer.Render.Cli;

// Strongly-typed configuration bound from appsettings/env/CLI
public sealed class RendererOptions
{
    // Default fallbacks if nothing is configured
    public string WorkDir { get; init; } = "work";
    public string OutDir { get; init; } = "out";
    public string RegionsJson { get; init; } = "config/regions.renderOptions.json";
    public string? OnlyRegionId { get; init; }
}
