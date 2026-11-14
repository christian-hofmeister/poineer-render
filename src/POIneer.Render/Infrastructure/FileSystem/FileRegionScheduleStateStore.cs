using System.Text.Json;
using Microsoft.Extensions.Logging;
using POIneer.Render.Application.Contracts;
using POIneer.Render.Application.Ports;

namespace POIneer.Render.Infrastructure.FileSystem;

public sealed class FileRegionScheduleStateStore : IRegionScheduleStateStore
{
    public FileRegionScheduleStateStore(ILogger<FileRegionScheduleStateStore> logger)
    {
        _logger = logger;
    }

    private readonly ILogger<FileRegionScheduleStateStore> _logger;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<RegionScheduleState?> ReadAsync(
        string statePath,
        CancellationToken ct = default)
    {
        if (!File.Exists(statePath))
            return null;

        try
        {
            await using var stream = File.OpenRead(statePath);
            var state = await JsonSerializer.DeserializeAsync<RegionScheduleState>(
                stream,
                JsonOptions,
                ct);

            if (state is not null &&
                !string.IsNullOrWhiteSpace(state.RegionId) &&
                !string.IsNullOrWhiteSpace(state.PbfUrl) &&
                (state.ProcessingPending ||
                 (state.LastSuccessfulCheckUtc.HasValue && state.NextDueUtc.HasValue
                  && state.NextDueUtc > state.LastSuccessfulCheckUtc)))
            {
                return state;
            }

            _logger.LogWarning("Schedule state from {StatePath} is incomplete", statePath);
            return null;
        }
        catch (JsonException)
        {
            // Failed to deserialize the schedule state, possibly due to corruption or format changes.
            _logger.LogWarning("Failed to deserialize schedule state from {StatePath}", statePath);
            return null;
        }
    }

    public async Task WriteAsync(
        string statePath,
        RegionScheduleState state,
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
                "Failed to delete temporary schedule state file {TempPath}",
                tempPath);
        }
    }
}
