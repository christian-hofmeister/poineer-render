namespace POIneer.Render.Ports;

public interface IFileDownloader
{
    Task<string> DownloadAsync(
        string url,
        string targetPath,
        CancellationToken ct = default);
}