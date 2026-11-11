using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POIneer.Render.Application;
using POIneer.Render.Application.Contracts;
using POIneer.Render.Application.Options;
using POIneer.Render.Domain.Models;

namespace POIneer.Render.Infrastructure.FileSystem;

public sealed class LocalRenderFileCleanup(
    IOptions<CleanupOptions> cleanupOptions,
    IOptions<PublisherOptions> publisherOptions,
    ILogger<LocalRenderFileCleanup> logger)
{
    public bool Enabled => cleanupOptions.Value.IsEnabledFor(publisherOptions.Value.Target);

    public void Run(RegionDto region, string workDir, string outDir, bool vectorTilesEnabled)
    {
        if (!Enabled)
            return;

        var regionId = region.Id;
        workDir = Path.TrimEndingDirectorySeparator(Path.GetFullPath(workDir));
        outDir = Path.TrimEndingDirectorySeparator(Path.GetFullPath(outDir));
        var regionWorkDir = RegionIdentifier.CombinePath(workDir, regionId);
        var sourcePath = Path.Combine(regionWorkDir, RenderFilePaths.SourcePbf);
        var workFiles = new List<string> { sourcePath };
        if (!string.IsNullOrWhiteSpace(region.Poly))
            workFiles.Add(RenderFilePaths.GetCutPbfPath(sourcePath));
        var scratchDirectory = vectorTilesEnabled
            ? Path.Combine(regionWorkDir, RenderFilePaths.PlanetilerScratchDirectory)
            : null;
        DeleteFiles(regionId, workDir, regionWorkDir, workFiles, scratchDirectory);

        var regionOutDir = RegionIdentifier.CombinePath(outDir, regionId);
        var database = Path.Combine(regionOutDir, RenderFilePaths.PoiDataset);
        var stagingDatabase = Path.Combine(regionOutDir, RenderFilePaths.StagingPoiDataset);
        List<string> outputFiles =
        [
            database,
            stagingDatabase,
            .. RenderFilePaths.SqliteSidecarSuffixes.Select(suffix => database + suffix),
            .. RenderFilePaths.SqliteSidecarSuffixes.Select(suffix => stagingDatabase + suffix)
        ];
        if (vectorTilesEnabled)
        {
            outputFiles.Add(Path.Combine(regionOutDir, RenderFilePaths.VectorTiles));
            outputFiles.Add(Path.Combine(regionOutDir, RenderFilePaths.StagingVectorTiles));
            outputFiles.Add(Path.Combine(regionOutDir, RenderFilePaths.StagingVectorTiles + ".layerstats.tsv.gz"));
        }
        DeleteFiles(regionId, outDir, regionOutDir, outputFiles);
    }

    private void DeleteFiles(string regionId, string rootDir, string regionDir, IEnumerable<string> paths,
        string? scratchDirectory = null)
    {
        var path = regionDir;
        try
        {
            // Do not traverse directory links within the configured cleanup roots.
            while (true)
            {
                if (File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint))
                    throw new IOException($"Cleanup cannot traverse directory link '{path}'.");

                if (path == rootDir)
                    break;

                path = Path.GetDirectoryName(path)
                    ?? throw new IOException($"Cleanup path '{regionDir}' is outside '{rootDir}'.");
            }
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogFailure(regionId, path, ex);
            return;
        }

        if (scratchDirectory is not null)
            DeletePath(regionId, scratchDirectory, isScratchDirectory: true);

        foreach (var file in paths)
            DeletePath(regionId, file);
    }

    private void DeletePath(string regionId, string path, bool isScratchDirectory = false)
    {
        try
        {
            // Check attributes to distinguish an absent path from a permission error.
            var attributes = File.GetAttributes(path);
            if (isScratchDirectory)
            {
                if (attributes.HasFlag(FileAttributes.ReparsePoint))
                    throw new IOException($"Cleanup cannot traverse directory link '{path}'.");

                Directory.Delete(path, recursive: true);
            }
            else
            {
                File.Delete(path);
            }
            logger.LogInformation("({Region}) Deleted local render path {Path} after verified publishing.", regionId, path);
        }
        catch (Exception ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            // An earlier step may have already removed the path.
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            LogFailure(regionId, path, ex);
        }
    }

    private void LogFailure(string regionId, string path, Exception ex)
        => logger.LogWarning(ex, "({Region}) Could not clean up local render path {Path} after verified publishing.", regionId, path);
}
