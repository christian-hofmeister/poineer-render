using System.Net;
using FluentAssertions;
using POIneer.Render.Adapters.Input;
using Xunit;

namespace POIneer.Render.UnitTests.Adapters.Input;

public sealed class HttpRemotePbfMetadataReaderTests
{
    [Fact]
    public async Task GetMetadataAsync_SendsHeadRequestAndReadsMetadataHeaders()
    {
        // Arrange
        using var handler = new StubHttpMessageHandler();
        using var httpClient = new HttpClient(handler);
        var sut = new HttpRemotePbfMetadataReader(httpClient);

        // Act
        var metadata = await sut.GetMetadataAsync("https://example.com/berlin.osm.pbf");

        // Assert
        handler.RequestMethod.Should().Be(HttpMethod.Head);
        handler.RequestUri.Should().Be("https://example.com/berlin.osm.pbf");
        metadata.ETag.Should().Be("\"abc\"");
        metadata.LastModified.Should().Be(DateTimeOffset.Parse("2026-01-01T00:00:00Z"));
        metadata.ContentLength.Should().Be(42);
    }

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        public HttpMethod? RequestMethod { get; private set; }
        public string? RequestUri { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            RequestMethod = request.Method;
            RequestUri = request.RequestUri?.ToString();

            var response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent([])
            };
            response.Headers.ETag = new("\"abc\"");
            response.Content.Headers.LastModified = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
            response.Content.Headers.ContentLength = 42;

            return Task.FromResult(response);
        }
    }
}
