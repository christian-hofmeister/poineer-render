using FluentAssertions;
using Microsoft.Extensions.Hosting;
using NSubstitute;
using POIneer.Render.Adapters.Input;
using POIneer.Render.TestHelpers;
using Xunit;

namespace POIneer.Render.UnitTests.Adapters.Input;

public sealed class OsmiumPolygonCutterTests
{
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task CutAsync_UsesOriginalPbf_OnlyWhenNoPolygonIsConfigured(string? polygon)
    {
        var sut = new OsmiumPolygonCutter(Substitute.For<IHostEnvironment>());

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
        var sut = new OsmiumPolygonCutter(environment);

        var act = () => sut.CutAsync("source.pbf", configuredPath);

        var exception = await act.Should().ThrowAsync<FileNotFoundException>();
        exception.Which.FileName.Should().Be(fullPath);
    }
}
