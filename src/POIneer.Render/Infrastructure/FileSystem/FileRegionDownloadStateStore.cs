using System.Text.Json;
using Microsoft.Extensions.Logging;
using POIneer.Render.Application.Contracts;
using POIneer.Render.Application.Ports;

namespace POIneer.Render.Infrastructure.FileSystem;

public sealed class FileRegionDownloadStateStore : IRegionDownloadStateStore
{
    public FileRegionDownloadStateStore(ILogger<FileRegionDownloadStateStore> logger)
    {
        _logger = logger;
    }

    private readonly ILogger<FileRegionDownloadStateStore> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<RegionDownloadState?> ReadAsync(
        string statePath,
        CancellationToken ct = default)
    {
        if (!File.Exists(statePath))
            return null;

        try
        {
            await using var stream = File.OpenRead(statePath);
            var state = await JsonSerializer.DeserializeAsync<RegionDownloadState>(
                stream,
                JsonOptions,
                ct);

            if (state is not null &&
                !string.IsNullOrWhiteSpace(state.RegionId) &&
                !string.IsNullOrWhiteSpace(state.PbfUrl) &&
                state.DownloadedMetadata is not null)
            {
                return state;
            }

            _logger.LogWarning("Download state from {StatePath} is incomplete", statePath);
            return null;
        }
        catch (JsonException)
        {
            // Failed to deserialize the download state, possibly due to corruption or format changes.
            _logger.LogWarning("Failed to deserialize download state from {StatePath}", statePath);
            return null;
        }
    }

    public Task DeleteAsync(string statePath, CancellationToken ct = default)
    {
        ct.ThrowIfCancellationRequested();
        File.Delete(statePath);
        return Task.CompletedTask;
    }

    public async Task WriteAsync(
        string statePath,
        RegionDownloadState state,
        CancellationToken ct = default)
    {
        var stateDirectory = Path.GetDirectoryName(statePath);
        if (!string.IsNullOrEmpty(stateDirectory))
        {
            Directory.CreateDirectory(stateDirectory);
        }

        var tempPath = $"{statePath}.{Guid.NewGuid():N}.tmp";

        try
        {
            await using (var stream = File.Create(tempPath))
            {
                await JsonSerializer.SerializeAsync(
                    stream,
                    state,
                    JsonOptions,
                    ct);

                await stream.FlushAsync(ct);
            }

            File.Move(tempPath, statePath, overwrite: true);
        }
        catch
        {
            DeleteTemporaryStateFile(tempPath);
            throw;
        }
    }

    private void DeleteTemporaryStateFile(string tempPath)
    {
        try
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _logger.LogWarning(
                ex,
                "Failed to delete temporary download state file {TempPath}",
                tempPath);
        }
    }
}
