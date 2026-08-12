using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POIneer.Render.Adapters.Input;
using POIneer.Render.Adapters.Output;
using POIneer.Render.Application.Mapping;
using POIneer.Render.Application.Options;
using POIneer.Render.Application.Ports;
using POIneer.Render.Application.UseCases;
using POIneer.Render.Infrastructure.Adapters.Osm;
using POIneer.Render.Infrastructure.FileSystem;
using POIneer.Render.Infrastructure.Flyway;
using POIneer.Render.Infrastructure.Process;
using POIneer.Render.Infrastructure.Sqlite;
using POIneer.Render.Ports;


internal class Program
{
    private static readonly string ProjectDirectoryRelativeToBuildOutput = Path.Combine("..", "..", "..");

    private static async Task<int> Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = ResolveContentRoot()
        });

        builder.Configuration
            .SetBasePath(builder.Environment.ContentRootPath)
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true)
            .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: true)
            .AddEnvironmentVariables(prefix: "POINEER_RENDER__")
            .AddCommandLine(args);

        var rendererSection = builder.Configuration.GetSection("Renderer");

        IConfiguration bindSource = rendererSection.Exists()
            ? rendererSection
            : builder.Configuration;

        builder.Services
            .AddOptions<RendererOptions>()
            .Bind(bindSource)
            .Validate(RendererOptionsValidation.HasRequiredPaths, RendererOptionsValidation.RequiredPathsMessage)
            .Validate(RendererOptionsValidation.HasValidDownloadTimeout, RendererOptionsValidation.DownloadTimeoutMessage)
            .ValidateOnStart();

        builder.Services
            .AddOptions<FlywayOptions>()
            .Bind(builder.Configuration.GetSection(FlywayOptions.SectionName));

        builder.Services.AddHttpClient<IFileDownloader, HttpFileDownloader>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<RendererOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(options.DownloadTimeoutSeconds);
        });

        builder.Logging.ClearProviders();
        builder.Logging.AddSimpleConsole(options =>
        {
            options.TimestampFormat = "yyyy-MM-dd HH:mm:ss ";
            options.SingleLine = true;
            options.IncludeScopes = false;
        });
        builder.Logging.SetMinimumLevel(LogLevel.Debug);
        builder.Services.Configure<TempOptions>(
            builder.Configuration.GetSection("Temp"));
        builder.Services.AddSingleton<ITemporaryDirectoryFactory, TemporaryDirectoryFactory>();
        builder.Services.AddSingleton<IRegionSource, GeofabrikRegionSource>();
        builder.Services.AddSingleton<IPolygonCutter, OsmiumPolygonCutter>();
        builder.Services.AddSingleton<IOsmReader, OsmPbfReader>();
        builder.Services.AddSingleton<IFlywayInvocationBuilder, FlywayInvocationBuilder>();
        builder.Services.AddSingleton<ISqliteDatabaseInitializer, FlywaySqliteDatabaseInitializer>();
        builder.Services.AddSingleton<IExporter, SqliteExporter>();
        builder.Services.AddSingleton<IProcessRunner, ProcessRunner>();
        builder.Services.AddSingleton<IRenderRegion, RenderRegion>();
        builder.Services.AddSingleton<IRawPoiMapper, RawPoiMapper>();
        builder.Services.AddSingleton<IDatasetValidator, SqliteDatasetValidator>();
        builder.Services.AddSingleton<ISingleInstanceLockFactory, FileSingleInstanceLockFactory>();

        // CLI entry point
        builder.Services.AddSingleton<Runner>();

        var app = builder.Build();

        var runner = app.Services.GetRequiredService<Runner>();
        return await runner.RunAsync(
            app.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping
        );
    }

    private static string ResolveContentRoot()
    {
        var currentDirectory = Directory.GetCurrentDirectory();
        if (File.Exists(Path.Combine(currentDirectory, "appsettings.json")))
            return currentDirectory;

        var baseDirectory = Path.GetDirectoryName(typeof(Program).Assembly.Location)!;
        if (File.Exists(Path.Combine(baseDirectory, "appsettings.json")))
            return baseDirectory;

        var projectDirectory = Path.GetFullPath(Path.Combine(baseDirectory, ProjectDirectoryRelativeToBuildOutput));
        if (File.Exists(Path.Combine(projectDirectory, "appsettings.json")))
            return projectDirectory;

        return currentDirectory;
    }
}
