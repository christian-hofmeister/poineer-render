using System.Net;
using System.Net.Http.Headers;
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
        if (Uri.TryCreate(pbfUrl, UriKind.Absolute, out var source)
            && source.IdnHost.Equals("download.geofabrik.de", StringComparison.OrdinalIgnoreCase))
        {
            var checksumUrl = new UriBuilder(source) { Path = source.AbsolutePath + ".md5", Fragment = "" }.Uri;
            try
            {
                return await GetChecksumMetadataAsync(checksumUrl, ct);
            }
            catch (OperationCanceledException ex) when (!ct.IsCancellationRequested)
            {
                throw new TimeoutException($"Timed out reading Geofabrik checksum at '{checksumUrl}'.", ex);
            }
        }

        using var request = new HttpRequestMessage(HttpMethod.Head, pbfUrl);
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            ct);

        if (response.StatusCode is HttpStatusCode.MethodNotAllowed or HttpStatusCode.NotImplemented)
            return await GetMetadataWithGetAsync(pbfUrl, ct);

        response.EnsureSuccessStatusCode();

        return ToMetadata(response);
    }

    private async Task<RegionUpdateMetadata> GetChecksumMetadataAsync(Uri checksumUrl, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync(checksumUrl, HttpCompletionOption.ResponseHeadersRead, ct);
        if (!response.IsSuccessStatusCode)
            throw new HttpRequestException($"Geofabrik checksum request for '{checksumUrl}' returned HTTP {(int)response.StatusCode}.",
                null, response.StatusCode);

        using var readTimeout = CancellationTokenSource.CreateLinkedTokenSource(ct);
        readTimeout.CancelAfter(_httpClient.Timeout);
        using var reader = new StreamReader(await response.Content.ReadAsStreamAsync(readTimeout.Token));
        var buffer = new char[4097];
        var length = await reader.ReadBlockAsync(buffer.AsMemory(), readTimeout.Token);
        var line = new string(buffer, 0, length).Trim();
        var separator = line.IndexOfAny([' ', '\t']);
        var checksum = separator < 0 ? line : line[..separator];
        if (length == buffer.Length || line.Contains('\n') || line.Contains('\r')
            || checksum.Length != 32 || !checksum.All(Uri.IsHexDigit))
        {
            throw new InvalidDataException($"Invalid Geofabrik MD5 checksum at '{checksumUrl}'.");
        }

        return new RegionUpdateMetadata(null, null, null, checksum.ToLowerInvariant());
    }

    private async Task<RegionUpdateMetadata> GetMetadataWithGetAsync(string pbfUrl, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Get, pbfUrl);
        request.Headers.Range = new RangeHeaderValue(0, 0);
        using var response = await _httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            ct);

        response.EnsureSuccessStatusCode();

        return ToMetadata(response);
    }

    private static RegionUpdateMetadata ToMetadata(HttpResponseMessage response)
        => new(
            response.Headers.ETag?.Tag,
            response.Content.Headers.LastModified,
            response.Content.Headers.ContentRange?.Length ?? response.Content.Headers.ContentLength);
}
