using Microsoft.Extensions.Options;
using POIneer.Render.Application.Options;

public static class TestRendererOptions
{
    public static IOptions<RendererOptions> Create(
        bool overwriteDatabase = false,
        bool overwritePbf = false)
        => Options.Create(new RendererOptions
        {
            WorkDir = "test-work-dir",
            OutDir = "test-out-dir",
            RegionsJson = "test-regions.json",
            OverwriteDatabase = overwriteDatabase,
            OverwritePbf = overwritePbf,
            DownloadTimeoutSeconds = 600
        });
}