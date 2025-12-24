using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
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
        var sectionRenderer = builder.Configuration.GetSection("Renderer");
        var bindSource = sectionRenderer.Exists() ? (IConfiguration)sectionRenderer : builder.Configuration;

        // Load configuration with sane defaults and environment overlays
        builder.Configuration
            .SetBasePath(builder.Environment.ContentRootPath)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
            // Environment variables like: POINEER_RENDER__RENDERER__WORKDIR or POINEER_RENDER__WORKDIR (see binding below)
            .AddEnvironmentVariables(prefix: "POINEER_RENDER__")
            .AddCommandLine(args);



        // Bind Options
        // Prefer "Renderer" section; if not present, bind from root (fallback to keep compatibility)
        builder.Services
            .AddOptions<RendererOptions>()
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

        // CLI entry point
        builder.Services.AddSingleton<Runner>();

        var app = builder.Build();

        var runner = app.Services.GetRequiredService<Runner>();
        return await runner.RunAsync(
            app.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping
        );
    }
}