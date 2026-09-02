using POIneer.Render.Application.Contracts;
using POIneer.Render.Application.Ports;

namespace POIneer.Render.Adapters.Input;

public sealed class HttpRemotePbfMetadataReader : IRemotePbfMetadataReader
{
    private readonly HttpClient _httpClient;

    public HttpRemotePbfMetadataReader(HttpClient httpClient)
        => _httpClient = httpClient;

    public async Task<RegionUpdateMetadata> GetMetadataAsync(
        string pbfUrl,
        CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Head, pbfUrl);
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            ct);

        response.EnsureSuccessStatusCode();

        return new RegionUpdateMetadata(
            response.Headers.ETag?.Tag,
            response.Content.Headers.LastModified,
            response.Content.Headers.ContentLength);
    }
}
