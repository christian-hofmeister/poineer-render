using POIneer.Render.Ports;

namespace POIneer.Render.Adapters.Input;

public sealed class HttpFileDownloader : IFileDownloader
{
    private readonly HttpClient _httpClient;

    public HttpFileDownloader(HttpClient httpClient)
        => _httpClient = httpClient;

    public async Task<string> DownloadAsync(
        string url,
        string targetPath,
        CancellationToken ct = default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);

        // Download with HttpClient and stream directly to file to avoid loading the entire file into memory
        using var response = await _httpClient.GetAsync(
            url,
            HttpCompletionOption.ResponseHeadersRead,
            ct);

        response.EnsureSuccessStatusCode();

        await using var httpStream =
            await response.Content.ReadAsStreamAsync(ct);

        await using var fileStream = File.Create(targetPath);

        await httpStream.CopyToAsync(fileStream, ct);

        return targetPath;
    }
}