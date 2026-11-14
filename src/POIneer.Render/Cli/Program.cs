using Azure.Identity;
using Azure.Storage.Blobs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POIneer.Render.Adapters.Input;
using POIneer.Render.Adapters.Output;
using POIneer.Render.Application.Contracts;
using POIneer.Render.Application.Mapping;
using POIneer.Render.Application.Options;
using POIneer.Render.Application.Ports;
using POIneer.Render.Application.UseCases;
using POIneer.Render.Domain.Models;
using POIneer.Render.Infrastructure.Adapters.Osm;
using POIneer.Render.Infrastructure.Azure;
using POIneer.Render.Infrastructure.FileSystem;
using POIneer.Render.Infrastructure.Flyway;
using POIneer.Render.Infrastructure.Pathing;
using POIneer.Render.Infrastructure.Process;
using POIneer.Render.Infrastructure.Sqlite;
using POIneer.Render.Infrastructure.VectorTiles;
using POIneer.Render.Ports;


internal class Program
{
    private static async Task<int> Main(string[] args)
    {
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            Args = args,
            ContentRootPath = ContentRootResolver.Resolve()
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
            .Validate(RendererOptionsValidation.HasValidUpdateInterval, RendererOptionsValidation.UpdateIntervalMessage)
            .Validate(options => options.MinimumFreeDiskSpaceBytes >= 0,
                "Renderer:MinimumFreeDiskSpaceBytes must be zero or greater.")
            .ValidateOnStart();

        builder.Services
            .AddOptions<PublisherOptions>()
            .Bind(builder.Configuration.GetSection("Publisher"))
            .PostConfigure(options =>
            {
                options.DestinationDir = ConfiguredPathResolver.ResolveOptional(
                    builder.Environment.ContentRootPath,
                    options.DestinationDir);
            })
            .Validate(
                _ => PublisherOptionsValidation.IsDefinedTargetName(builder.Configuration["Publisher:Target"]),
                PublisherOptionsValidation.DefinedTargetMessage)
            .Validate(PublisherOptionsValidation.HasDefinedTarget, PublisherOptionsValidation.DefinedTargetMessage)
            .Validate(PublisherOptionsValidation.HasRequiredDestinationDir, PublisherOptionsValidation.RequiredDestinationDirMessage)
            .ValidateOnStart();

        builder.Services
            .AddOptions<AzureBlobPublisherOptions>()
            .Bind(builder.Configuration.GetSection(AzureBlobPublisherOptions.SectionName))
            .Validate(
                options => IsLocalPublisherTarget(builder.Configuration)
                           || AzureBlobPublisherOptionsValidation.HasAccountNameOrBlobEndpoint(options),
                AzureBlobPublisherOptionsValidation.RequiredAccountOrEndpointMessage)
            .Validate(
                options => IsLocalPublisherTarget(builder.Configuration)
                           || AzureBlobPublisherOptionsValidation.HasContainerName(options),
                AzureBlobPublisherOptionsValidation.RequiredContainerNameMessage)
            .Validate(
                AzureBlobPublisherOptionsValidation.HasPositiveMaxUploadsPerRun,
                AzureBlobPublisherOptionsValidation.PositiveMaxUploadsPerRunMessage)
            .Validate(
                AzureBlobPublisherOptionsValidation.HasPositiveMaxUploadBytesPerRun,
                AzureBlobPublisherOptionsValidation.PositiveMaxUploadBytesPerRunMessage)
            .ValidateOnStart();

        builder.Services
            .AddOptions<FlywayOptions>()
            .Bind(builder.Configuration.GetSection(FlywayOptions.SectionName));

        builder.Services
            .AddOptions<VectorTileOptions>()
            .Bind(builder.Configuration.GetSection(VectorTileOptions.SectionName))
            .Validate(VectorTileOptionsValidation.HasRequiredPlanetilerPaths, VectorTileOptionsValidation.RequiredPlanetilerPathsMessage)
            .Validate(VectorTileOptionsValidation.HasValidZoomRange, VectorTileOptionsValidation.ZoomRangeMessage)
            .ValidateOnStart();

        builder.Services.AddHttpClient<IFileDownloader, HttpFileDownloader>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<RendererOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(options.DownloadTimeoutSeconds);
        });

        builder.Services.AddHttpClient<IRemotePbfMetadataReader, HttpRemotePbfMetadataReader>((sp, client) =>
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
        builder.Services.Configure<CleanupOptions>(builder.Configuration.GetSection(CleanupOptions.SectionName));
        builder.Services.AddSingleton<LocalRenderFileCleanup>();
        builder.Services.AddSingleton<ITemporaryDirectoryFactory, TemporaryDirectoryFactory>();
        builder.Services.AddSingleton<IRegionSource, GeofabrikRegionSource>();
        builder.Services.AddSingleton<RegionUpdateIntervalPolicy>();
        builder.Services.AddSingleton(TimeProvider.System);
        builder.Services.AddSingleton<IRegionScheduleStateStore, FileRegionScheduleStateStore>();
        builder.Services.AddSingleton<RegionUpdateSchedule>();
        builder.Services.AddSingleton<IPolygonCutter, OsmiumPolygonCutter>();
        builder.Services.AddSingleton<IOsmReader, OsmPbfReader>();
        builder.Services.AddSingleton<IFlywayInvocationBuilder, FlywayInvocationBuilder>();
        builder.Services.AddSingleton<ISqliteDatabaseInitializer, FlywaySqliteDatabaseInitializer>();
        builder.Services.AddSingleton<IExporter, SqliteExporter>();
        builder.Services.AddSingleton<IProcessRunner, ProcessRunner>();
        builder.Services.AddSingleton<IVectorTileGenerator, PlanetilerVectorTileGenerator>();
        builder.Services.AddSingleton<IRenderRegion, RenderRegion>();
        builder.Services.AddSingleton<IRawPoiMapper, RawPoiMapper>();
        builder.Services.AddSingleton<IDatasetValidator, SqliteDatasetValidator>();
        builder.Services.AddSingleton<LocalDatasetPublisher>();
        builder.Services.AddSingleton<AzureBlobDatasetPublisher>();
        builder.Services.AddSingleton<FilePublishedDatasetVerifier>();
        builder.Services.AddSingleton<IAzureBlobDatasetMetadataReader, AzureBlobDatasetMetadataReader>();
        builder.Services.AddSingleton<IAzureBlobDatasetPublishPlanner, AzureBlobDatasetPublishPlanner>();
        builder.Services.AddSingleton<IAzureBlobDatasetUploader, AzureBlobDatasetUploader>();
        builder.Services.AddSingleton(sp =>
        {
            var options = sp.GetRequiredService<IOptions<AzureBlobPublisherOptions>>().Value;
            var serviceClient = new BlobServiceClient(ResolveBlobServiceEndpoint(options), new DefaultAzureCredential());

            if (string.IsNullOrWhiteSpace(options.ContainerName))
            {
                throw new InvalidOperationException(
                    "AzureBlobPublisher:ContainerName must be set.");
            }

            return serviceClient.GetBlobContainerClient(options.ContainerName);
        });
        builder.Services.AddSingleton<IDatasetPublisher>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<PublisherOptions>>().Value;
            return options.Target switch
            {
                DatasetPublisherTarget.Local => sp.GetRequiredService<LocalDatasetPublisher>(),
                DatasetPublisherTarget.AzureBlob => sp.GetRequiredService<AzureBlobDatasetPublisher>(),
                _ => throw new InvalidOperationException($"Unrecognized publisher target: {options.Target}")
            };
        });
        builder.Services.AddSingleton<IDatasetVersionCalculator, FileHashDatasetVersionCalculator>();
        builder.Services.AddSingleton<IDatasetArtifactMetadataFactory, FileDatasetArtifactMetadataFactory>();
        var metadataOptions = builder.Configuration.GetSection(MetadataPublisherOptions.SectionName)
            .Get<MetadataPublisherOptions>();
        if (metadataOptions?.Target is not null)
        {
            builder.Services.AddSingleton(CreateMetadataStore(metadataOptions, builder.Environment.ContentRootPath));
            builder.Services.AddSingleton<RegionMetadataPublisher>();
        }

        builder.Services.AddSingleton<IPublishedDatasetVerifier>(sp =>
        {
            var options = sp.GetRequiredService<IOptions<PublisherOptions>>().Value;
            return options.Target switch
            {
                DatasetPublisherTarget.Local => sp.GetRequiredService<FilePublishedDatasetVerifier>(),
                DatasetPublisherTarget.AzureBlob => new AzureBlobPublishedDatasetVerifier(
                    sp.GetRequiredService<IAzureBlobDatasetMetadataReader>(),
                    sp.GetRequiredService<ILogger<AzureBlobPublishedDatasetVerifier>>(),
                    metadataOptions?.Target is not null
                    || sp.GetRequiredService<IOptions<CleanupOptions>>().Value.IsEnabledFor(options.Target)
                        ? sp.GetRequiredService<BlobContainerClient>()
                        : null),
                _ => throw new InvalidOperationException($"Unrecognized publisher target: {options.Target}")
            };
        });
        builder.Services.AddSingleton<ISingleInstanceLockFactory, FileSingleInstanceLockFactory>();
        builder.Services.AddSingleton<IDiskSpaceReader, DiskSpaceReader>();
        builder.Services.AddSingleton<IRegionRenderStateStore, FileRegionRenderStateStore>();
        builder.Services.AddSingleton<IRegionDownloadStateStore, FileRegionDownloadStateStore>();
        builder.Services.AddSingleton<IRegionUpdateChecker, RegionUpdateChecker>();

        // CLI entry point
        builder.Services.AddSingleton<Runner>();

        var app = builder.Build();

        var runner = app.Services.GetRequiredService<Runner>();
        return await runner.RunAsync(
            app.Services.GetRequiredService<IHostApplicationLifetime>().ApplicationStopping
        );
    }

    private static IRegionMetadataStore CreateMetadataStore(MetadataPublisherOptions options, string contentRoot)
    {
        if (options.Target == DatasetPublisherTarget.Local)
        {
            if (string.IsNullOrWhiteSpace(options.DestinationDir))
                throw new InvalidOperationException("MetadataPublisher:DestinationDir must be set for the Local target.");
            return new FileRegionMetadataStore(ConfiguredPathResolver.Resolve(contentRoot, options.DestinationDir));
        }

        if (options.Target != DatasetPublisherTarget.AzureBlob)
            throw new InvalidOperationException($"Unrecognized metadata publisher target: {options.Target}.");
        if (string.IsNullOrWhiteSpace(options.ContainerName))
            throw new InvalidOperationException("MetadataPublisher:ContainerName must be set for the AzureBlob target.");
        if (!string.IsNullOrEmpty(options.Prefix))
            RegionIdentifier.ValidateHierarchicalId(options.Prefix, nameof(options.Prefix));

        var endpoint = ResolveBlobServiceEndpoint(options.BlobEndpoint, options.AccountName, MetadataPublisherOptions.SectionName);
        var client = new BlobServiceClient(endpoint, new DefaultAzureCredential());
        return new AzureBlobRegionMetadataStore(client.GetBlobContainerClient(options.ContainerName), options.Prefix);
    }

    private static Uri ResolveBlobServiceEndpoint(AzureBlobPublisherOptions options)
        => ResolveBlobServiceEndpoint(options.BlobEndpoint, options.AccountName, AzureBlobPublisherOptions.SectionName);

    private static Uri ResolveBlobServiceEndpoint(string? blobEndpoint, string? accountName, string section)
    {
        if (!string.IsNullOrWhiteSpace(blobEndpoint))
            return new Uri(blobEndpoint);

        if (!string.IsNullOrWhiteSpace(accountName))
            return new Uri($"https://{accountName}.blob.core.windows.net");

        throw new InvalidOperationException(
            $"{section}:AccountName or {section}:BlobEndpoint must be set.");
    }

    private static bool IsLocalPublisherTarget(IConfiguration configuration)
        => configuration
            .GetSection("Publisher")
            .Get<PublisherOptions>()?
            .Target != DatasetPublisherTarget.AzureBlob;

}
