using System.Net.Http;
using POIneer.Render.Ports;

namespace POIneer.Render.Adapters.Input;

public sealed class HttpFileDownloader : IFileDownloader
{
    private readonly HttpClient _httpClient;

    public HttpFileDownloader(HttpClient httpClient)
        => _httpClient = httpClient;

    public async Task<string> DownloadAsync(string url, string targetPath, CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        await using var fileStream = File.Create(targetPath);
        await using var stream = await _httpClient.GetStreamAsync(url, ct);
        await stream.CopyToAsync(fileStream, ct);

        return targetPath;
    }
}