namespace POIneer.Render.Adapters.Input;

public static class FileDownloader
{
    // Simple helper; keep it a static for now to avoid over-abstraction
    public static async Task<string> DownloadAsync(
        string url,
        string targetPath,
        CancellationToken ct = default,
        HttpClient? httpClient = null
        )
    {
        httpClient ??= new HttpClient();
        await using var fileStream = File.Create(targetPath);
        using var stream = await httpClient.GetStreamAsync(url, ct);
        await stream.CopyToAsync(fileStream, ct);
        return targetPath;
    }
}