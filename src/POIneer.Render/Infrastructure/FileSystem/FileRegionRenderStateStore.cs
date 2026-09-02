using System.Text.Json;
using POIneer.Render.Application.Contracts;
using POIneer.Render.Application.Ports;

namespace POIneer.Render.Infrastructure.FileSystem;

public sealed class FileRegionRenderStateStore : IRegionRenderStateStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public async Task<RegionRenderState?> ReadAsync(
        string statePath,
        CancellationToken ct = default)
    {
        if (!File.Exists(statePath))
            return null;

        await using var stream = File.OpenRead(statePath);
        return await JsonSerializer.DeserializeAsync<RegionRenderState>(
            stream,
            JsonOptions,
            ct);
    }

    public async Task WriteAsync(
        string statePath,
        RegionRenderState state,
        CancellationToken ct = default)
    {
        var stateDirectory = Path.GetDirectoryName(statePath);
        if (!string.IsNullOrEmpty(stateDirectory))
        {
            Directory.CreateDirectory(stateDirectory);
        }

        await using var stream = File.Create(statePath);
        await JsonSerializer.SerializeAsync(
            stream,
            state,
            JsonOptions,
            ct);
    }
}
