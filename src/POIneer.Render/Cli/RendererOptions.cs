namespace POIneer.Render.Cli;

// Strongly-typed configuration bound from appsettings/env/CLI
public sealed class RendererOptions
{
    // These are required, but we can't use [Required] because the config binder doesn't respect it
    public required string WorkDir { get; init; }
    public required string OutDir { get; init; }
    public required string RegionsJson { get; init; }

    // If set, only this region will be rendered (e.g. for testing)
    public string? OnlyRegionId { get; init; }

    public bool DryRun { get; init; }
}
