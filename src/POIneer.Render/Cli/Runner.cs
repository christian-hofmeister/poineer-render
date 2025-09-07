using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POIneer.Render.Adapters.Input;
using POIneer.Render.Application.UseCases;
using POIneer.Render.Cli;
using POIneer.Render.Ports;

public sealed class Runner
{
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<Runner> _logger;
    private readonly IRegionSource _regionSource;
    private readonly RenderRegion _renderRegionUseCase;
    private readonly RendererOptions _rendererOptions;

    public Runner(
        IHostEnvironment hostEnvironment,
        ILogger<Runner> log,
        IRegionSource regionSource,
        RenderRegion renderRegionUseCase,
        IOptions<RendererOptions> rendererOptions)
    {
        _hostEnvironment = hostEnvironment;
        _logger = log;
        _regionSource = regionSource;
        _renderRegionUseCase = renderRegionUseCase;
        _rendererOptions = rendererOptions.Value;
    }

    public async Task<int> RunAsync(CancellationToken ct)
    {
        var contentRoot = _hostEnvironment.ContentRootPath;

        string Resolve(string p)  => Path.IsPathRooted(p) ? p : Path.GetFullPath(Path.Combine(contentRoot, p));

        var regionsPath = Resolve(_rendererOptions.RegionsJson);
        var workDir     = Resolve(_rendererOptions.WorkDir);
        var outDir      = Resolve(_rendererOptions.OutDir);

        _logger.LogInformation("POIneer.Render starting (env: {Env})", _hostEnvironment.EnvironmentName);
        _logger.LogInformation("Using content root: {ContentRoot}", contentRoot);
        _logger.LogInformation("Regions file: {RegionsPath}", regionsPath);
        _logger.LogInformation("Work directory: {WorkDir}", workDir);
        _logger.LogInformation("Output directory: {OutDir}", outDir);
        

        _logger.LogInformation("Creating directories (if not exists)...");
        Directory.CreateDirectory(workDir);
        Directory.CreateDirectory(outDir);

        _logger.LogInformation("Directories created: {WorkDir}, {OutDir}", workDir, outDir);

        _logger.LogInformation("Using regions file: {RegionsPath}", regionsPath);
        
        if (!File.Exists(regionsPath))
            throw new FileNotFoundException($"Regions file not found: {regionsPath}");

        var regions = await _regionSource.GetRegionsAsync(regionsPath, ct);

        foreach (var r in regions.Where(r => string.IsNullOrEmpty(_rendererOptions.OnlyRegionId) || r.Id == _rendererOptions.OnlyRegionId))
        {
            using (_logger.BeginScope("region:{RegionId}", r.Id))
            {
                var pbfPath = Path.Combine(_rendererOptions.WorkDir, $"{r.Id}.osm.pbf");
                if (!File.Exists(pbfPath))
                {
                    _logger.LogInformation("Downloading PBF for {Region} ... to {TargetPath}", r.Id, pbfPath);
                    await FileDownloader.DownloadAsync(r.PbfUrl, pbfPath, ct);
                }
                await _renderRegionUseCase.RunAsync(r, _rendererOptions.WorkDir, _rendererOptions.OutDir, ct);
            }
        }
        _logger.LogInformation("All regions processed.");
        return 0;
    }
}
