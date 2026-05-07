using Microsoft.Extensions.Logging;

namespace POIneer.Render.Infrastructure.FileSystem;

public sealed class TemporaryDirectory : IDisposable, IAsyncDisposable
{
    public string DirectoryPath { get; }
    private readonly bool _keepOnDispose;
    private readonly ILogger<TemporaryDirectory> _logger;

    public TemporaryDirectory(
        string path,
        ILogger<TemporaryDirectory>
        logger,
        bool keepOnDispose)
    {
        DirectoryPath = path;
        _logger = logger;
        _keepOnDispose = keepOnDispose;
    }

    public TemporaryDirectory Create(
        string prefix,
        string? rootPath = null,
        bool keepOnDispose = false)
    {
        prefix = TemporaryDirectoryNameHelper.CreateSafeFolderName(prefix);

        rootPath ??= Path.GetTempPath();
        Directory.CreateDirectory(rootPath);

        var path = Path.Combine(
            rootPath,
            prefix + "-" + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(path);

        return new TemporaryDirectory(path, _logger, keepOnDispose);
    }

    public TemporaryDirectory CreateSubDir(string name)
    {
        var path = Path.Combine(DirectoryPath, name);
        Directory.CreateDirectory(path);
        return new TemporaryDirectory(path, _logger, _keepOnDispose);
    }

    public void Dispose()
    {
        if (_keepOnDispose)
        {
            return;
        }

        TryDeleteDirectory(DirectoryPath);
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private void TryDeleteDirectory(string dir)
    {
        const int maxRetries = 3;

        for (var attempt = 1; attempt <= maxRetries; attempt++)
        {


            try
            {
                if (!Directory.Exists(dir))
                {
                    return;
                }

                try
                {
                    Directory.Delete(dir, recursive: true);
                    return;
                }
                catch (IOException ex) when (attempt < maxRetries)
                {
                    _logger.LogDebug(
                        ex,
                        "Temporary directory '{Dir}' still in use. Retrying cleanup.",
                        dir);

                    Thread.Sleep(100);
                }
                catch (UnauthorizedAccessException ex) when (attempt < maxRetries)
                {
                    _logger.LogDebug(
                        ex,
                        "Temporary directory '{Dir}' still locked. Retrying cleanup.",
                        dir);

                    Thread.Sleep(100);
                }
                catch (Exception ex)
                {
                    // Log the failure but continue with best-effort cleanup.
                    _logger.LogWarning(ex, "Failed to delete temporary directory '{Dir}' on {Attempt} attempt.", dir, attempt);
                }

                foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
                {
                    try
                    {
                        File.SetAttributes(file, FileAttributes.Normal);
                    }
                    catch
                    {
                        // Ignore best-effort cleanup failures.
                    }
                }

                Directory.Delete(dir, recursive: true);
            }

            catch
            {
                // Best effort cleanup: never throw from Dispose.
                _logger.LogWarning("Failed to delete temporary directory '{Dir}'.", dir);
            }
        }
    }
}