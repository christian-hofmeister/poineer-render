using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Configuration;
using POIneer.Render.Cli;
using POIneer.Render.Ports;
using POIneer.Render.Adapters.Input;
using POIneer.Render.Adapters.Osm;
using POIneer.Render.Adapters.Output;
using POIneer.Render.Application.UseCases;

internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(args);
        var section = builder.Configuration.GetSection("Renderer");
        var bindSource = section.Exists() ? (IConfiguration)section : builder.Configuration;

        // Load configuration with sane defaults and environment overlays
        builder.Configuration
            .SetBasePath(AppContext.BaseDirectory)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
            // Environment variables like: POINEER_RENDER__RENDERER__WORKDIR or POINEER_RENDER__WORKDIR (see binding below)
            .AddEnvironmentVariables(prefix: "POINEER_RENDER__")
            .AddCommandLine(args);

        // Bind Options
        // Prefer "Renderer" section; if not present, bind from root (fallback to keep compatibility)
        builder.Services
            .AddOptions<Options>()
            .Bind(bindSource)
            .Validate(o =>
                !string.IsNullOrWhiteSpace(o.WorkDir) &&
                !string.IsNullOrWhiteSpace(o.OutDir) &&
                !string.IsNullOrWhiteSpace(o.RegionsJson),
                "WorkDir, OutDir, and RegionsJson must be set");


        // Wire ports/adapters + use cases
        builder.Services.AddSingleton<IRegionSource, GeofabrikRegionSource>();
        builder.Services.AddSingleton<IPolygonCutter, OsmiumPolygonCutter>();
        builder.Services.AddSingleton<IOsmReader, OsmPbfReader>();
        builder.Services.AddSingleton<IExporter, SqliteExporter>();
        builder.Services.AddSingleton<RenderRegion>();

        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(o => o.SingleLine = true);

        var app = builder.Build();

        var logger = app.Services.GetRequiredService<ILoggerFactory>().CreateLogger("CLI");
        var optsService = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<Options>>().Value;
        var hostApplicationlifetimeServcie = app.Services.GetRequiredService<IHostApplicationLifetime>();
        var renderRegionService = app.Services.GetRequiredService<RenderRegion>();
        var regionSourceService = app.Services.GetRequiredService<IRegionSource>();

        // Ensure directories exist
        Directory.CreateDirectory(optsService.WorkDir);
        Directory.CreateDirectory(optsService.OutDir);

        // Resolve relative regions path against base directory
        var regionsPath = Path.IsPathRooted(optsService.RegionsJson)
            ? optsService.RegionsJson
            : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, optsService.RegionsJson));

        if (!File.Exists(regionsPath))
        {
            throw new FileNotFoundException($"Regions file not found: {regionsPath}");
        }

        var regions = await regionSourceService.GetRegionsAsync(
            regionsPath,
            hostApplicationlifetimeServcie.ApplicationStopping);

        // Simple loop; add downloader etc. as needed
        foreach (var r in regions.Where(
            r => string.IsNullOrEmpty(optsService.OnlyRegionId) || r.Id == optsService.OnlyRegionId))
        {
            var pbfPath = Path.Combine(optsService.WorkDir, $"{r.Id}.osm.pbf");
            if (!File.Exists(pbfPath))
            {
                logger.LogInformation("({Id}) Downloading PBF...", r.Id);
                await FileDownloader.DownloadAsync(
                    r.PbfUrl,
                    pbfPath,
                    hostApplicationlifetimeServcie.ApplicationStopping);
            }

            await renderRegionService.RunAsync(
                r,
                optsService.WorkDir,
                optsService.OutDir,
                hostApplicationlifetimeServcie.ApplicationStopping);
        }

        return 0;
    }
}