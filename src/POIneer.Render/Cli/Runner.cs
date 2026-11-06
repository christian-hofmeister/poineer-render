using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POIneer.Render.Application.Options;
using POIneer.Render.Application.Ports;
using POIneer.Render.Application.UseCases;
using POIneer.Render.Domain.Models;
using POIneer.Render.Infrastructure.Pathing;
using POIneer.Render.Ports;

public sealed class Runner
{
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<Runner> _logger;
    private readonly IRegionSource _regionSource;
    private readonly IRenderRegion _renderRegionUseCase;
    private readonly IRegionUpdateChecker _regionUpdateChecker;
    private readonly RendererOptions _rendererOptions;
    private readonly ISingleInstanceLockFactory _lockFactory;
    private readonly RegionUpdateIntervalPolicy _updateIntervalPolicy;
    private readonly RegionUpdateSchedule _updateSchedule;
    private readonly VectorTileOptions _vectorTileOptions;

    private readonly IFileDownloader _fileDownloader;

    public Runner(
        IHostEnvironment hostEnvironment,
        ILogger<Runner> log,
        IFileDownloader fileDownloader,
        IRegionSource regionSource,
        IRenderRegion renderRegionUseCase,
        IRegionUpdateChecker regionUpdateChecker,
        ISingleInstanceLockFactory lockFactory,
        RegionUpdateIntervalPolicy updateIntervalPolicy,
        IOptions<RendererOptions> rendererOptions,
        RegionUpdateSchedule updateSchedule,
        IOptions<VectorTileOptions> vectorTileOptions)
    {
        _hostEnvironment = hostEnvironment;
        _logger = log;
        _fileDownloader = fileDownloader;
        _regionSource = regionSource;
        _renderRegionUseCase = renderRegionUseCase;
        _regionUpdateChecker = regionUpdateChecker;
        _lockFactory = lockFactory;
        _updateIntervalPolicy = updateIntervalPolicy;
        _rendererOptions = rendererOptions.Value;
        _updateSchedule = updateSchedule;
        _vectorTileOptions = vectorTileOptions.Value;
    }

    public async Task<int> RunAsync(CancellationToken ct)
    {
        var contentRoot = _hostEnvironment.ContentRootPath;
        var redownloadPbf = _rendererOptions.OverwritePbf;

        var regionsPath = ConfiguredPathResolver.Resolve(contentRoot, _rendererOptions.RegionsJson);
        var workDir = ConfiguredPathResolver.Resolve(contentRoot, _rendererOptions.WorkDir);
        var outDir = ConfiguredPathResolver.Resolve(contentRoot, _rendererOptions.OutDir);
        var lockFilePath = string.IsNullOrWhiteSpace(_rendererOptions.LockFilePath)
            ? Path.Combine(workDir, "poineer-render.lock")
            : ConfiguredPathResolver.Resolve(contentRoot, _rendererOptions.LockFilePath);

        _logger.LogInformation("POIneer.Render starting (env: {Env})", _hostEnvironment.EnvironmentName);
        _logger.LogInformation("Using content root: {ContentRoot}", contentRoot);
        _logger.LogInformation("ContentRoot (raw): {ContentRoot}", _hostEnvironment.ContentRootPath);
        _logger.LogInformation("Regions file: {RegionsPath}", regionsPath);
        _logger.LogInformation("Work directory: {WorkDir}", workDir);
        _logger.LogInformation("Work directory (raw): {WorkDir}", _rendererOptions.WorkDir);
        _logger.LogInformation("Output directory: {OutDir}", outDir);
        _logger.LogInformation("Output directory (raw): {OutDir}", _rendererOptions.OutDir);
        _logger.LogInformation("Lock file: {LockFilePath}", lockFilePath);
        _logger.LogInformation("Dry run: {DryRun}", _rendererOptions.DryRun);

        if (_rendererOptions.DryRun)
        {
            _logger.LogInformation("Dry run enabled, exiting without doing anything.");
            return 0;
        }

        using var instanceLock = _lockFactory.Create(lockFilePath);
        if (!instanceLock.TryAcquire())
        {
            _logger.LogWarning(
                "Skipped execution: another POIneer.Render instance is already running (lock file: {LockFilePath}).",
                lockFilePath);
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
        var failedRegionIds = new List<string>();

        foreach (var r in regions.Where(r => string.IsNullOrEmpty(_rendererOptions.OnlyRegionId) || r.Id == _rendererOptions.OnlyRegionId))
        {
            using (_logger.BeginScope("region:{RegionId}", r.Id))
            {
                try
                {
                    // Validate before creating region work files or making remote requests.
                    _updateIntervalPolicy.GetIntervalDays(r);

                    // r.Id may be a globally unique, hierarchical region identifier
                    // (ADR 0007), e.g. "geofabrik/europe/germany/berlin".
                    var regionWorkDir = RegionIdentifier.CombinePath(workDir, r.Id);
                    var schedulePath = Path.Combine(regionWorkDir, "schedule-state.json");
                    var scheduleState = await _updateSchedule.ReadAsync(r, schedulePath, ct);
                    var overwriteRequested = _rendererOptions.OverwriteDatabase || redownloadPbf;
                    if (!overwriteRequested && !_updateSchedule.IsDue(scheduleState))
                    {
                        _logger.LogInformation("({Region}) Skipping region: next update due at {NextDueUtc}.", r.Id, scheduleState!.NextDueUtc);
                        continue;
                    }
                    _logger.LogInformation("({Region}) Checking region (next due: {NextDueUtc}, processing pending: {Pending}, overwrite: {Overwrite}).",
                        r.Id, scheduleState?.NextDueUtc, scheduleState?.ProcessingPending ?? false, overwriteRequested);
                    Directory.CreateDirectory(regionWorkDir);
                    var pbfPath = Path.Combine(regionWorkDir, "osm.pbf");
                    var downloadStatePath = Path.Combine(regionWorkDir, "download-state.json");
                    var renderStatePath = Path.Combine(regionWorkDir, "render-state.json");
                    var updateCheck = await _regionUpdateChecker.CheckAsync(r, downloadStatePath, ct);

                    var regionOutDir = RegionIdentifier.CombinePath(outDir, r.Id);
                    var processingRequired = overwriteRequested || scheduleState is null || scheduleState.ProcessingPending
                        || updateCheck.ShouldRedownloadPbf || !File.Exists(pbfPath)
                        || !File.Exists(Path.Combine(regionOutDir, "poi.sqlite"))
                        || (_vectorTileOptions.Enabled && !File.Exists(Path.Combine(regionOutDir, "map.pmtiles")));
                    if (!processingRequired)
                    {
                        var next = await _updateSchedule.MarkSuccessfulAsync(r, schedulePath, ct);
                        _logger.LogInformation("({Region}) Source unchanged; skipping download and rendering. Next update due at {NextDueUtc}.", r.Id, next.NextDueUtc);
                        continue;
                    }

                    // Persist before downloading/rendering so a failed attempt remains due,
                    // even if canonical outputs were promoted before publishing failed.
                    await _updateSchedule.MarkProcessingAsync(r, schedulePath, scheduleState, ct);

                    if (!File.Exists(pbfPath))
                    {
                        _logger.LogInformation("Downloading PBF for {Region} ... to {TargetPath}", r.Id, pbfPath);
                        await _regionUpdateChecker.InvalidateDownloadAsync(downloadStatePath, ct);
                        await _fileDownloader.DownloadAsync(r.PbfUrl, pbfPath, ct);
                        await _regionUpdateChecker.MarkDownloadedAsync(r, downloadStatePath, updateCheck.RemoteMetadata, ct);
                    }
                    else if (redownloadPbf)
                    {
                        _logger.LogInformation("PBF already exists for {Region} at {TargetPath}, but overwrite is enabled, re-downloading.", r.Id, pbfPath);
                        await _regionUpdateChecker.InvalidateDownloadAsync(downloadStatePath, ct);
                        await _fileDownloader.DownloadAsync(r.PbfUrl, pbfPath, ct);
                        await _regionUpdateChecker.MarkDownloadedAsync(r, downloadStatePath, updateCheck.RemoteMetadata, ct);
                    }
                    else if (updateCheck.ShouldRedownloadPbf)
                    {
                        _logger.LogInformation(
                            "PBF metadata changed for {Region} ({Reason}), re-downloading to {TargetPath}. ETag={ETag}, LastModified={LastModified}, ContentLength={ContentLength}",
                            r.Id,
                            updateCheck.Reason,
                            pbfPath,
                            updateCheck.RemoteMetadata.ETag,
                            updateCheck.RemoteMetadata.LastModified,
                            updateCheck.RemoteMetadata.ContentLength);
                        await _regionUpdateChecker.InvalidateDownloadAsync(downloadStatePath, ct);
                        await _fileDownloader.DownloadAsync(r.PbfUrl, pbfPath, ct);
                        await _regionUpdateChecker.MarkDownloadedAsync(r, downloadStatePath, updateCheck.RemoteMetadata, ct);
                    }
                    else
                    {
                        _logger.LogInformation(
                            "PBF metadata unchanged for {Region}; reusing local PBF for required processing. ETag={ETag}, LastModified={LastModified}, ContentLength={ContentLength}",
                            r.Id,
                            updateCheck.RemoteMetadata.ETag,
                            updateCheck.RemoteMetadata.LastModified,
                            updateCheck.RemoteMetadata.ContentLength);
                    }
                    await _renderRegionUseCase.RunAsync(r, workDir, outDir, ct, forceRebuild: true);
                    await _regionUpdateChecker.MarkProcessedAsync(r, renderStatePath, updateCheck.RemoteMetadata, ct);
                    var completed = await _updateSchedule.MarkSuccessfulAsync(r, schedulePath, ct);
                    _logger.LogInformation("({Region}) Processing successful. Next update due at {NextDueUtc}.", r.Id, completed.NextDueUtc);
                }
                catch (Exception ex) when (ex is not OperationCanceledException)
                {
                    _logger.LogError(ex, "({Region}) Region processing failed: {Message}", r.Id, ex.Message);
                    failedRegionIds.Add(r.Id);
                }
            }
        }

        if (failedRegionIds.Count > 0)
        {
            _logger.LogError(
                "Completed with {FailedCount} failed region(s): {FailedRegions}",
                failedRegionIds.Count,
                string.Join(", ", failedRegionIds));
            return 1;
        }

        _logger.LogInformation("All regions processed.");
        return 0;
    }
}
