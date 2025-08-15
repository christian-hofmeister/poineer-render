namespace POIneer.Render.Adapters.Input;

public static class FileDownloader
{
    // Simple helper; keep it a static for now to avoid over-abstraction
    public static async Task<string> DownloadAsync(string url, string targetPath, CancellationToken ct = default)
    {
        using var http = new HttpClient();
        await using var fs = File.Create(targetPath);
        using var stream = await http.GetStreamAsync(url, ct);
        await stream.CopyToAsync(fs, ct);
        return targetPath;
    }
}