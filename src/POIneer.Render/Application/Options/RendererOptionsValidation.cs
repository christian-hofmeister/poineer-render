namespace POIneer.Render.Application.Options;

public static class RendererOptionsValidation
{
    public const string RequiredPathsMessage = "WorkDir, OutDir, and RegionsJson must be set";
    public const string DownloadTimeoutMessage = "DownloadTimeoutSeconds must be greater than 0";

    public static bool HasRequiredPaths(RendererOptions options)
        => !string.IsNullOrWhiteSpace(options.WorkDir)
           && !string.IsNullOrWhiteSpace(options.OutDir)
           && !string.IsNullOrWhiteSpace(options.RegionsJson);

    public static bool HasValidDownloadTimeout(RendererOptions options)
        => options.DownloadTimeoutSeconds > 0;
}
