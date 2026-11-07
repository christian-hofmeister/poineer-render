using System.Diagnostics;
using FluentAssertions;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using POIneer.Render.Adapters.Input;
using POIneer.Render.Infrastructure.Process;
using POIneer.Render.TestHelpers;
using Xunit;

namespace POIneer.Render.UnitTests.Adapters.Input;

public sealed class OsmiumPolygonCutterTests
{
    private readonly IProcessRunner _processRunner = Substitute.For<IProcessRunner>();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task CutAsync_UsesOriginalPbf_OnlyWhenNoPolygonIsConfigured(string? polygon)
    {
        var sut = new OsmiumPolygonCutter(Substitute.For<IHostEnvironment>(), _processRunner);

        (await sut.CutAsync("source.pbf", polygon)).Should().Be("source.pbf");
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task CutAsync_ThrowsForMissingConfiguredPolygon_ResolvingRelativePathsAgainstContentRoot(bool absolute)
    {
        await using var tempDir = TestTemporaryDirectories.Create("missing-region-polygon", false);
        var environment = Substitute.For<IHostEnvironment>();
        environment.ContentRootPath.Returns(tempDir.DirectoryPath);
        var fullPath = Path.Combine(tempDir.DirectoryPath, "Cli", "config", "polygons", "missing.poly");
        var configuredPath = absolute ? fullPath : Path.Combine("Cli", "config", "polygons", "missing.poly");
        var sut = new OsmiumPolygonCutter(environment, _processRunner);

        var act = () => sut.CutAsync("source.pbf", configuredPath);

        var exception = await act.Should().ThrowAsync<FileNotFoundException>();
        exception.Which.FileName.Should().Be(fullPath);
    }

    [Fact]
    public async Task CutAsync_AllowsOsmiumToReplaceAnExistingExtract()
    {
        await using var tempDir = TestTemporaryDirectories.Create("existing-region-extract", false);
        var environment = Substitute.For<IHostEnvironment>();
        environment.ContentRootPath.Returns(tempDir.DirectoryPath);
        var polygonPath = Path.Combine(tempDir.DirectoryPath, "region.poly");
        var sourcePath = Path.Combine(tempDir.DirectoryPath, "osm.pbf");
        var outputPath = Path.Combine(tempDir.DirectoryPath, "osm.cut.osm.pbf");
        TestFiles.WriteAllText(polygonPath, "polygon fixture");
        TestFiles.WriteAllText(sourcePath, "source fixture");
        TestFiles.WriteAllText(outputPath, "previous extract");
        _processRunner.RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "", ""));
        var sut = new OsmiumPolygonCutter(environment, _processRunner);

        (await sut.CutAsync(sourcePath, "region.poly")).Should().Be(outputPath);

        await _processRunner.Received(1).RunAsync(Arg.Is<ProcessStartInfo>(info =>
            info.FileName == "osmium" && info.ArgumentList.SequenceEqual(
                new[] { "extract", "--overwrite", "-p", polygonPath, sourcePath, "-o", outputPath })),
            Arg.Any<CancellationToken>());
    }
}
