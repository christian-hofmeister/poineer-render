using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POIneer.Render.Application.Options;
using POIneer.Render.Ports;

public sealed class Runner
{
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<Runner> _logger;
    private readonly IRegionSource _regionSource;
    private readonly IRenderRegion _renderRegionUseCase;
    private readonly RendererOptions _rendererOptions;

    private readonly IFileDownloader _fileDownloader;

    public Runner(
        IHostEnvironment hostEnvironment,
        ILogger<Runner> log,
        IFileDownloader fileDownloader,
        IRegionSource regionSource,
        IRenderRegion renderRegionUseCase,
        IOptions<RendererOptions> rendererOptions)
    {
        _hostEnvironment = hostEnvironment;
        _logger = log;
        _fileDownloader = fileDownloader;
        _regionSource = regionSource;
        _renderRegionUseCase = renderRegionUseCase;
        _rendererOptions = rendererOptions.Value;
    }

    public async Task<int> RunAsync(CancellationToken ct)
    {
        var contentRoot = _hostEnvironment.ContentRootPath;
        var redownloadPbf = _rendererOptions.OverwritePbf;

        string Resolve(string p) => Path.IsPathRooted(p) ? p : Path.GetFullPath(Path.Combine(contentRoot, p));

        var regionsPath = Resolve(_rendererOptions.RegionsJson);
        var workDir = Resolve(_rendererOptions.WorkDir);
        var outDir = Resolve(_rendererOptions.OutDir);

        _logger.LogInformation("POIneer.Render starting (env: {Env})", _hostEnvironment.EnvironmentName);
        _logger.LogInformation("Using content root: {ContentRoot}", contentRoot);
        _logger.LogInformation("ContentRoot (raw): {ContentRoot}", _hostEnvironment.ContentRootPath);
        _logger.LogInformation("Regions file: {RegionsPath}", regionsPath);
        _logger.LogInformation("Work directory: {WorkDir}", workDir);
        _logger.LogInformation("Work directory (raw): {WorkDir}", _rendererOptions.WorkDir);
        _logger.LogInformation("Output directory: {OutDir}", outDir);
        _logger.LogInformation("Output directory (raw): {OutDir}", _rendererOptions.OutDir);
        _logger.LogInformation("Dry run: {DryRun}", _rendererOptions.DryRun);

        if (_rendererOptions.DryRun)
        {
            _logger.LogInformation("Dry run enabled, exiting without doing anything.");
            return 0;
        }

        _logger.LogInformation("Creating directories (if not exists)...");
        Directory.CreateDirectory(workDir);
        Directory.CreateDirectory(outDir);

        _logger.LogInformation("Directories created (if not exists): {WorkDir}, {OutDir}", workDir, outDir);

        _logger.LogInformation("Using regions file: {RegionsPath}", regionsPath);

        if (!File.Exists(regionsPath))
            throw new FileNotFoundException($"Regions file not found: {regionsPath}");

        var regions = await _regionSource.GetRegionsAsync(regionsPath, ct);

        foreach (var r in regions.Where(r => string.IsNullOrEmpty(_rendererOptions.OnlyRegionId) || r.Id == _rendererOptions.OnlyRegionId))
        {
            using (_logger.BeginScope("region:{RegionId}", r.Id))
            {
                var regionWorkDir = Path.Combine(workDir, r.Id);
                Directory.CreateDirectory(regionWorkDir);
                var pbfPath = Path.Combine(regionWorkDir, "osm.pbf");

                if (!File.Exists(pbfPath))
                {
                    _logger.LogInformation("Downloading PBF for {Region} ... to {TargetPath}", r.Id, pbfPath);
                    await _fileDownloader.DownloadAsync(r.PbfUrl, pbfPath, ct);
                }
                else if (!redownloadPbf)
                {
                    _logger.LogInformation("PBF already exists for {Region} at {TargetPath}, skipping download (overwrite disabled).", r.Id, pbfPath);
                }
                else
                {
                    _logger.LogInformation("PBF already exists for {Region} at {TargetPath}, but overwrite is enabled, re-downloading.", r.Id, pbfPath);
                    await _fileDownloader.DownloadAsync(r.PbfUrl, pbfPath, ct);
                }
                await _renderRegionUseCase.RunAsync(r, workDir, outDir, ct);
            }
        }
        _logger.LogInformation("All regions processed.");
        return 0;
    }
}
