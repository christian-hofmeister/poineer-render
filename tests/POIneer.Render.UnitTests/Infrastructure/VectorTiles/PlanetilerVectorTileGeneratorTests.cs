using System.Diagnostics;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using POIneer.Render.Application.Options;
using POIneer.Render.Infrastructure.Process;
using POIneer.Render.Infrastructure.VectorTiles;
using POIneer.Render.TestHelpers;
using Xunit;

namespace POIneer.Render.UnitTests.Infrastructure.VectorTiles;

public sealed class PlanetilerVectorTileGeneratorTests
{
    [Fact]
    public async Task GenerateAsync_StartsPlanetiler_WithExpectedArguments()
    {
        // Arrange
        var processRunner = Substitute.For<IProcessRunner>();

        await using var tempDir = TestTemporaryDirectories.Create("planetiler-arguments", false);
        var pbfPath = Path.Combine(tempDir.DirectoryPath, "berlin.osm.pbf");
        var outputPath = Path.Combine(tempDir.DirectoryPath, "map.pmtiles");
        TestFiles.WriteAllText(pbfPath, "dummy pbf");

        ProcessStartInfo? capturedStartInfo = null;
        CancellationToken capturedCancellationToken = default;

        processRunner
            .RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                capturedStartInfo = callInfo.ArgAt<ProcessStartInfo>(0);
                capturedCancellationToken = callInfo.ArgAt<CancellationToken>(1);
                TestFiles.WriteAllText(outputPath, "dummy pmtiles");
                return Task.FromResult(new ProcessResult(0, "planetiler stdout", "planetiler stderr"));
            });

        var sut = CreateSut(
            processRunner,
            new VectorTileOptions
            {
                Enabled = true,
                JavaExecutablePath = "java",
                PlanetilerJarPath = "planetiler.jar",
                JavaMaxHeapSize = "1g",
                Profile = "openmaptiles",
                MinZoom = 0,
                MaxZoom = 14,
                AdditionalArguments = ["--download=false"]
            });

        using var cts = new CancellationTokenSource();

        // Act
        await sut.GenerateAsync(pbfPath, outputPath, cts.Token);

        // Assert
        await processRunner
            .Received(1)
            .RunAsync(Arg.Any<ProcessStartInfo>(), cts.Token);

        Assert.NotNull(capturedStartInfo);
        Assert.False(string.IsNullOrWhiteSpace(capturedStartInfo.FileName));
        Assert.True(capturedStartInfo.RedirectStandardOutput);
        Assert.True(capturedStartInfo.RedirectStandardError);
        Assert.False(capturedStartInfo.UseShellExecute);
        Assert.Contains("-Xmx1g", capturedStartInfo.ArgumentList);
        Assert.Contains("-jar", capturedStartInfo.ArgumentList);
        Assert.Contains("planetiler.jar", capturedStartInfo.ArgumentList);
        Assert.Contains($"--osm-path={Path.GetFullPath(pbfPath)}", capturedStartInfo.ArgumentList);
        Assert.Contains($"--output={Path.GetFullPath(outputPath)}", capturedStartInfo.ArgumentList);
        Assert.Contains("--force", capturedStartInfo.ArgumentList);
        Assert.Contains("--profile=openmaptiles", capturedStartInfo.ArgumentList);
        Assert.Contains("--minzoom=0", capturedStartInfo.ArgumentList);
        Assert.Contains("--maxzoom=14", capturedStartInfo.ArgumentList);
        Assert.Contains("--download=false", capturedStartInfo.ArgumentList);
        Assert.Equal(cts.Token, capturedCancellationToken);
    }

    [Fact]
    public async Task GenerateAsync_ThrowsInvalidOperationException_WhenPlanetilerFails()
    {
        // Arrange
        var processRunner = Substitute.For<IProcessRunner>();

        await using var tempDir = TestTemporaryDirectories.Create("planetiler-fails", false);
        var pbfPath = Path.Combine(tempDir.DirectoryPath, "berlin.osm.pbf");
        var outputPath = Path.Combine(tempDir.DirectoryPath, "map.pmtiles");
        TestFiles.WriteAllText(pbfPath, "dummy pbf");

        processRunner
            .RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(23, "", "bad things happened"));

        var sut = CreateSut(processRunner);

        // Act
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
            sut.GenerateAsync(pbfPath, outputPath, CancellationToken.None));

        // Assert
        Assert.Contains("Planetiler failed", ex.Message);
        Assert.Contains("ExitCode=23", ex.Message);
        Assert.Contains("bad things happened", ex.Message);
    }

    [Fact]
    public async Task GenerateAsync_ThrowsFileNotFoundException_WhenExpectedOutputWasNotCreated()
    {
        // Arrange
        var processRunner = Substitute.For<IProcessRunner>();

        await using var tempDir = TestTemporaryDirectories.Create("planetiler-missing-output", false);
        var pbfPath = Path.Combine(tempDir.DirectoryPath, "berlin.osm.pbf");
        var outputPath = Path.Combine(tempDir.DirectoryPath, "map.pmtiles");
        TestFiles.WriteAllText(pbfPath, "dummy pbf");

        processRunner
            .RunAsync(Arg.Any<ProcessStartInfo>(), Arg.Any<CancellationToken>())
            .Returns(new ProcessResult(0, "", ""));

        var sut = CreateSut(processRunner);

        // Act
        var ex = await Assert.ThrowsAsync<FileNotFoundException>(() =>
            sut.GenerateAsync(pbfPath, outputPath, CancellationToken.None));

        // Assert
        Assert.Contains("expected PMTiles output was not created", ex.Message);
    }

    private static PlanetilerVectorTileGenerator CreateSut(
        IProcessRunner processRunner,
        VectorTileOptions? options = null)
        => new(
            processRunner,
            Options.Create(options ?? new VectorTileOptions
            {
                Enabled = true,
                JavaExecutablePath = "java",
                PlanetilerJarPath = "planetiler.jar"
            }),
            NullLogger<PlanetilerVectorTileGenerator>.Instance);
}
