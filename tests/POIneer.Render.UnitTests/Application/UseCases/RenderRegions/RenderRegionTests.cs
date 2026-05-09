using Microsoft.Extensions.Logging;
using NSubstitute;
using POIneer.Render.Application.Contracts;
using POIneer.Render.Application.Mapping;
using POIneer.Render.Application.UseCases;
using POIneer.Render.Domain.Models;
using POIneer.Render.Infrastructure.Osm.Models;
using POIneer.Render.Ports;
using POIneer.Render.TestHelpers;
using Xunit;

namespace POIneer.Render.UnitTests.Application.UseCases.RenderRegions;

public sealed class RenderRegionTests
{
    private readonly ILogger<RenderRegion> _logger =
        Substitute.For<ILogger<RenderRegion>>();

    private readonly IPolygonCutter _polygonCutter = Substitute.For<IPolygonCutter>();
    private readonly IOsmReader _osmReader = Substitute.For<IOsmReader>();
    private readonly ISqliteDatabaseInitializer _dbInit = Substitute.For<ISqliteDatabaseInitializer>();
    private readonly IExporter _exporter = Substitute.For<IExporter>();

    private RenderRegion CreateSut()
        => new(
            _logger,
            _polygonCutter,
            _dbInit,
            _osmReader,
            _exporter,
            Substitute.For<IRawPoiMapper>());

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
        await using var tempDir = TestTemporaryDirectories.Create("throw-when-pbf-does-not-exist", false);

        tempDir.CreateSubDir("work"); // empty -> no pbf
        var workDir = tempDir.CreateSubDir("work").DirectoryPath; // empty -> no pbf
        var outDir = tempDir.CreateSubDir("out").DirectoryPath;

        // Act
        var ex = await Assert.ThrowsAsync<FileNotFoundException>(() =>
            sut.RunAsync(region, workDir, outDir, CancellationToken.None));

        // Assert
        Assert.Contains("PBF not found", ex.Message);

        await _polygonCutter.DidNotReceiveWithAnyArgs().CutAsync(default!, default!, default);
        _osmReader.DidNotReceiveWithAnyArgs().ReadAmenityNodesAsync(default!, default);
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

        await using var tempDir = TestTemporaryDirectories.Create("calls-ports-with-expected-paths", false);
        var workDir = tempDir.CreateSubDir("work").DirectoryPath;
        var outDir = tempDir.CreateSubDir("out").DirectoryPath;

        // create input pbf file
        var pbfPath = Path.Combine(workDir, $"{region.Id}.osm.pbf");
        File.WriteAllText(pbfPath, "dummy");

        var cutPbfPath = Path.Combine(workDir, $"{region.Id}.cut.osm.pbf");
        _polygonCutter
            .CutAsync(pbfPath, region.Poly, Arg.Any<CancellationToken>())
            .Returns(Task.FromResult(cutPbfPath));

        var pois = AsyncEnumerable.Empty<Poi>();
        var rawPois = AsyncEnumerable.Empty<RawPoi>();
        _osmReader
            .ReadAmenityNodesAsync(cutPbfPath, Arg.Any<CancellationToken>())
            .Returns(rawPois);

        _exporter
            .ExportAsync(
                Arg.Any<IAsyncEnumerable<Poi>>(),
                Arg.Any<string>(),
                Arg.Any<CancellationToken>())
            .Returns(Task.CompletedTask);

        // Act
        await sut.RunAsync(region, workDir, outDir, CancellationToken.None);

        // Assert: correct out path
        var expectedOutPath = Path.Combine(outDir, region.Id, "poi.sqlite");

        Received.InOrder(() =>
        {
            _polygonCutter.CutAsync(pbfPath, region.Poly, Arg.Any<CancellationToken>());
            _osmReader.ReadAmenityNodesAsync(cutPbfPath, Arg.Any<CancellationToken>());
            _dbInit.InitializeAsync(expectedOutPath, Arg.Any<CancellationToken>());
            _exporter.ExportAsync(
                Arg.Any<IAsyncEnumerable<Poi>>(),
                expectedOutPath,
                Arg.Any<CancellationToken>());
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

        await using var tempDir = TestTemporaryDirectories.Create("forwards-cancellation-token", false);
        var workDir = tempDir.CreateSubDir("work").DirectoryPath;
        var outDir = tempDir.CreateSubDir("out").DirectoryPath;

        var pbfPath = Path.Combine(workDir, $"{region.Id}.osm.pbf");
        File.WriteAllText(pbfPath, "dummy");

        var cutPbfPath = Path.Combine(workDir, $"{region.Id}.cut.osm.pbf");

        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _polygonCutter.CutAsync(pbfPath, region.Poly, ct)
            .Returns(Task.FromResult(cutPbfPath));

        var pois = AsyncEnumerable.Empty<RawPoi>();
        _osmReader.ReadAmenityNodesAsync(cutPbfPath, ct).Returns(pois);

        // Act
        await sut.RunAsync(region, workDir, outDir, ct);

        // Assert
        await _polygonCutter.Received(1).CutAsync(pbfPath, region.Poly, ct);
        _osmReader.Received(1).ReadAmenityNodesAsync(cutPbfPath, ct);
        //TODO: reenble when exporter is enabled
        /*         await _exporter.Received(1).ExportAsync(
                    pois,
                    Path.Combine(outDir, $"{region.Id}.sqlite"),
                    ct); */
    }

    [Fact]
    public async Task RunAsync_HappyPath_CutsReadsAndExports_WithExpectedPaths()
    {
        // Arrange
        var logger = Substitute.For<ILogger<RenderRegion>>();
        var polygonCutter = Substitute.For<IPolygonCutter>();
        var osmReader = Substitute.For<IOsmReader>();
        var initDb = Substitute.For<ISqliteDatabaseInitializer>();
        var exporter = Substitute.For<IExporter>();

        var sut = new RenderRegion(
            logger,
            polygonCutter,
            initDb,
            osmReader,
            exporter,
            Substitute.For<IRawPoiMapper>());

        var region = new RegionDto(
            Id: "berlin",
            Name: "Berlin",
            PbfUrl: "https://download.geofabrik.de/europe/germany/berlin-latest.osm.pbf",
            Poly: "berlin.poly");

        await using var tempDir = TestTemporaryDirectories.Create("calls-ports-with-expected-paths", false);
        var workDir = tempDir.CreateSubDir("work").DirectoryPath;
        var outDir = tempDir.CreateSubDir("out").DirectoryPath;

        // Dummy input PBF (existence is what matters)
        var pbfPath = Path.Combine(workDir, $"{region.Id}.osm.pbf");
        File.WriteAllText(pbfPath, "dummy");

        // Cut result
        var cutPbfPath = Path.Combine(workDir, $"{region.Id}.cut.osm.pbf");
        polygonCutter
            .CutAsync(pbfPath, region.Poly, Arg.Any<CancellationToken>())
            .Returns(cutPbfPath);

        // Reader returns some POIs
        var pois = new[]
        {
            new RawPoi(
                OsmId: 1,
                Latitude: 52.5,
                Longitude: 13.4,
                Amenity: "cafe",
                Name: "Cafe A",
                Tags: new Dictionary<string, string> { { "amenity", "cafe" }, { "name", "Cafe A" } }),
            new RawPoi(
                OsmId: 2,
                Latitude: 52.6,
                Longitude: 13.5,
                Amenity: "restaurant",
                Name: "Restaurant B",
                Tags: new Dictionary<string, string> { { "amenity", "restaurant" }, { "name", "Restaurant B" } })
        }.ToAsyncEnumerable();

        osmReader
            .ReadAmenityNodesAsync(cutPbfPath, Arg.Any<CancellationToken>())
            .Returns(pois);

        // Act
        await sut.RunAsync(region, workDir, outDir, CancellationToken.None);

        // Assert
        var expectedOutPath = Path.Combine(outDir, $"{region.Id}.sqlite");

        Received.InOrder(() =>
        {
            polygonCutter.CutAsync(pbfPath, region.Poly, Arg.Any<CancellationToken>());
            osmReader.ReadAmenityNodesAsync(cutPbfPath, Arg.Any<CancellationToken>());
            //TODO: reenble when exporter is enabled
            //exporter.ExportAsync(pois, expectedOutPath, Arg.Any<CancellationToken>());
        });
    }
}

