using Microsoft.Extensions.Logging;
using NSubstitute;
using POIneer.Render.Application.Contracts;
using POIneer.Render.Ports;
using Xunit;

namespace POIneer.Render.UnitTests.Application.UseCases.RenderRegion;

public sealed class RenderRegionTests
{
    private readonly ILogger<POIneer.Render.Application.UseCases.RenderRegion> _logger =
        Substitute.For<ILogger<POIneer.Render.Application.UseCases.RenderRegion>>();

    private readonly IPolygonCutter _polygonCutter = Substitute.For<IPolygonCutter>();
    private readonly IOsmReader _osmReader = Substitute.For<IOsmReader>();
    private readonly IExporter _exporter = Substitute.For<IExporter>();

    private POIneer.Render.Application.UseCases.RenderRegion CreateSut()
        => new(_logger, _polygonCutter, _osmReader, _exporter);

    [Fact]
    public async Task RunAsync_ThrowsFileNotFoundException_WhenPbfDoesNotExist()
    {
        // Arrange
        var sut = CreateSut();

        var region = new RegionDto(
            Id: "berlin",
            Name: "Berlin",
            PbfUrl: "http://example.com/berlin.osm.pbf",
            Poly: "dummy.poly"
        );

        using var tmp = new TempDir();
        var workDir = tmp.CreateSubDir("work"); // empty -> no pbf
        var outDir = tmp.CreateSubDir("out");

        // Act
        var ex = await Assert.ThrowsAsync<FileNotFoundException>(() =>
            sut.RunAsync(region, workDir, outDir, CancellationToken.None));

        // Assert
        Assert.Contains("PBF not found", ex.Message);

        await _polygonCutter.DidNotReceiveWithAnyArgs().CutAsync(default!, default!, default);
        _osmReader.DidNotReceiveWithAnyArgs().ReadAsync(default!, default);
        await _exporter.DidNotReceiveWithAnyArgs().ExportAsync(default!, default!, default);
    }

    [Fact]
    public async Task RunAsync_CallsPorts_WithExpectedPaths_AndInOrder()
    {
        // Arrange
        var sut = CreateSut();

        var region = new RegionDto(
            Id: "berlin",
            Name: "Berlin",
            PbfUrl: "http://example.com/berlin.osm.pbf",
            Poly: "berlin.poly"
        );

        using var tmp = new TempDir();
        var workDir = tmp.CreateSubDir("work");
        var outDir = tmp.CreateSubDir("out");

        // create input pbf file
        var pbfPath = Path.Combine(workDir, $"{region.Id}.osm.pbf");
        File.WriteAllText(pbfPath, "dummy");

        var cutPbfPath = Path.Combine(workDir, $"{region.Id}.cut.osm.pbf");
        _polygonCutter
            .CutAsync(pbfPath, region.Poly, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(cutPbfPath));

        var pois = AsyncEnumerable.Empty<PoiDto>();
        _osmReader
            .ReadAsync(cutPbfPath, Arg.Any<CancellationToken>())
            .Returns(pois);

        // Act
        await sut.RunAsync(region, workDir, outDir, CancellationToken.None);

        // Assert: correct out path
        var expectedOutPath = Path.Combine(outDir, $"{region.Id}.sqlite");

        Received.InOrder(() =>
        {
            _polygonCutter.CutAsync(pbfPath, region.Poly, Arg.Any<CancellationToken>());
            _osmReader.ReadAsync(cutPbfPath, Arg.Any<CancellationToken>());
            _exporter.ExportAsync(pois, expectedOutPath, Arg.Any<CancellationToken>());
        });
    }

    [Fact]
    public async Task RunAsync_ForwardsCancellationToken_ToAllPorts()
    {
        // Arrange
        var sut = CreateSut();

        var region = new RegionDto(
            Id: "berlin",
            Name: "Berlin",
            PbfUrl: "http://example.com/berlin.osm.pbf",
            Poly: "berlin.poly"
        );

        using var tmp = new TempDir();
        var workDir = tmp.CreateSubDir("work");
        var outDir = tmp.CreateSubDir("out");

        var pbfPath = Path.Combine(workDir, $"{region.Id}.osm.pbf");
        File.WriteAllText(pbfPath, "dummy");

        var cutPbfPath = Path.Combine(workDir, $"{region.Id}.cut.osm.pbf");

        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _polygonCutter.CutAsync(pbfPath, region.Poly, ct)
            .Returns(Task.FromResult(cutPbfPath));

        var pois = AsyncEnumerable.Empty<PoiDto>();
        _osmReader.ReadAsync(cutPbfPath, ct).Returns(pois);

        // Act
        await sut.RunAsync(region, workDir, outDir, ct);

        // Assert
        await _polygonCutter.Received(1).CutAsync(pbfPath, region.Poly, ct);
        _osmReader.Received(1).ReadAsync(cutPbfPath, ct);
        await _exporter.Received(1).ExportAsync(
            pois,
            Path.Combine(outDir, $"{region.Id}.sqlite"),
            ct);
    }
}

/// <summary>
/// Tiny temp-dir helper for tests.
/// If you already have POIneer.Render.TestHelpers.TempDir, just use that instead and delete this.
/// </summary>
internal sealed class TempDir : IDisposable
{
    public string PathValue { get; } = System.IO.Path.Combine(
        System.IO.Path.GetTempPath(),
        "POIneer.Render.Tests",
        Guid.NewGuid().ToString("N"));

    public TempDir() => Directory.CreateDirectory(PathValue);

    public string CreateSubDir(string name)
    {
        var p = System.IO.Path.Combine(PathValue, name);
        Directory.CreateDirectory(p);
        return p;
    }

    public void Dispose()
    {
        try { Directory.Delete(PathValue, recursive: true); }
        catch { /* ignore */ }
    }
}
