using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using POIneer.Render.Adapters.Input;
using Xunit;

namespace POIneer.Render.IntegrationTests.Adapters.Input;

public class FileDownloaderTests
{
    private static (TestServer Server, string Url) CreateServer(byte[] payload, string path = "/file.bin")
    {
        var builder = new WebHostBuilder()
            .Configure(app =>
            {
                app.Run(async ctx =>
                {
                    if (ctx.Request.Path != path)
                    {
                        ctx.Response.StatusCode = (int)HttpStatusCode.NotFound;
                        return;
                    }

                    ctx.Response.StatusCode = (int)HttpStatusCode.OK;
                    await ctx.Response.Body.WriteAsync(payload);
                });
            });

        var server = new TestServer(builder);
        var url = server.BaseAddress.ToString().TrimEnd('/') + path;
        return (server, url);
    }

    [Fact]
    public async Task DownloadAsync_HappyPath_WritesFileAndReturnsTargetPath()
    {
        var payload = new byte[] { 1, 2, 3, 4, 5 };
        var (server, url) = CreateServer(payload);
        var client = server.CreateClient();
        using var _ = server; // dispose

        var dir = Path.Combine(Path.GetTempPath(), "poineer-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        var targetPath = Path.Combine(dir, "download.bin");

        var returnedPath = await FileDownloader.DownloadAsync(url, targetPath, CancellationToken.None, client);

        Assert.Equal(targetPath, returnedPath);
        Assert.True(File.Exists(targetPath));
        Assert.Equal(payload, await File.ReadAllBytesAsync(targetPath));
    }
}
