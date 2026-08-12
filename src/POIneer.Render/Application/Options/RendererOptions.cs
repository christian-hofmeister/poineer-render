namespace POIneer.Render.Application.Options;

// Strongly-typed configuration bound from appsettings/env/CLI
public sealed class RendererOptions
{
    // These are required, but we can't use [Required] because the config binder doesn't respect it
    public required string WorkDir { get; init; }
    public required string OutDir { get; init; }
    public required string RegionsJson { get; init; }

    public bool OverwriteDatabase { get; init; } = false;
    public bool OverwritePbf { get; init; } = false;

    // If set, only this region will be rendered (e.g. for testing)
    public string? OnlyRegionId { get; init; }

    public bool DryRun { get; init; } = false;

    public int DownloadTimeoutSeconds { get; init; } = 600;

    // Optional. Path to the lock file used to prevent overlapping executions
    // (e.g. concurrent cron-triggered runs). Resolved relative to the content root
    // if not rooted. Defaults to "<WorkDir>/poineer-render.lock" when not set.
    public string? LockFilePath { get; init; }
}
