using System.Net;
using FluentAssertions;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using POIneer.Render.Adapters.Input;
using Xunit;

namespace POIneer.Render.IntegrationTests.Adapters.Input;

public class FileDownloaderTests
{
    [Fact]
    public async Task DownloadAsync_HappyPath_WritesFileAndReturnsTargetPath()
    {
        // Arrange: start a real HTTP server on a random free port
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseUrls("http://127.0.0.1:0"); // 0 => choose free port

        var app = builder.Build();

        var payload = "hello"u8.ToArray();
        app.MapGet("/file", () => Results.Bytes(payload, "application/octet-stream"));

        await app.StartAsync();

        try
        {
            var server = app.Services.GetRequiredService<IServer>();
            var addresses = server.Features.Get<IServerAddressesFeature>()!;
            var baseUrl = addresses.Addresses.Single(); // e.g. http://127.0.0.1:51234

            var url = $"{baseUrl}/file";

            var targetPath = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".bin");

            // Act
            var returnedPath = await FileDownloader.DownloadAsync(url, targetPath);

            // Assert
            returnedPath.Should().Be(targetPath);
            File.Exists(targetPath).Should().BeTrue();
            (await File.ReadAllBytesAsync(targetPath)).Should().Equal(payload);
        }
        finally
        {
            await app.StopAsync();
            await app.DisposeAsync();
        }
    }
}
