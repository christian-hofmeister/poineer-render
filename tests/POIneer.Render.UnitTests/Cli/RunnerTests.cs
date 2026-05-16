using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using POIneer.Render.Application.Contracts;
using POIneer.Render.Application.Options;
using POIneer.Render.Ports;
using POIneer.Render.TestHelpers;
using Xunit;

namespace POIneer.Render.UnitTests.Cli;

public sealed class RunnerTests
{
    private readonly IFileDownloader _fileDownloader = Substitute.For<IFileDownloader>();
    private readonly IRegionSource _regionSource = Substitute.For<IRegionSource>();
    private readonly IRenderRegion _renderRegion = Substitute.For<IRenderRegion>();

    [Fact]
    public async Task RunAsync_ReturnsSuccessAndDoesNothing_WhenDryRunIsEnabled()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("runner-dry-run", false);
        var sut = CreateSut(tempDir.DirectoryPath, CreateOptions(dryRun: true));

        // Act
        var result = await sut.RunAsync(CancellationToken.None);

        // Assert
        result.Should().Be(0);
        await _regionSource.DidNotReceiveWithAnyArgs().GetRegionsAsync(default!, default);
        await _fileDownloader.DidNotReceiveWithAnyArgs().DownloadAsync(default!, default!, default);
        await _renderRegion.DidNotReceiveWithAnyArgs().RunAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task RunAsync_ThrowsFileNotFoundException_WhenRegionsFileDoesNotExist()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("runner-missing-regions", false);
        var options = CreateOptions(regionsJson: "missing-regions.json");
        var expectedRegionsPath = Path.GetFullPath(Path.Combine(tempDir.DirectoryPath, options.RegionsJson));
        var sut = CreateSut(tempDir.DirectoryPath, options);

        // Act
        var act = () => sut.RunAsync(CancellationToken.None);

        // Assert
        var ex = await Assert.ThrowsAsync<FileNotFoundException>(act);
        ex.Message.Should().Contain(expectedRegionsPath);

        await _regionSource.DidNotReceiveWithAnyArgs().GetRegionsAsync(default!, default);
    }

    [Fact]
    public async Task RunAsync_ResolvesRelativePathsAgainstContentRoot_AndProcessesRegion()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("runner-resolves-relative-paths", false);
        var options = CreateOptions(
            workDir: "work",
            outDir: "out",
            regionsJson: "config/regions.json");
        var sut = CreateSut(tempDir.DirectoryPath, options);

        var regionsPath = Path.GetFullPath(Path.Combine(tempDir.DirectoryPath, options.RegionsJson));
        TestFiles.WriteAllText(regionsPath, "[]");

        var region = BerlinRegion();
        _regionSource
            .GetRegionsAsync(regionsPath, Arg.Any<CancellationToken>())
            .Returns(new[] { region });

        var workDir = Path.GetFullPath(Path.Combine(tempDir.DirectoryPath, options.WorkDir));
        var outDir = Path.GetFullPath(Path.Combine(tempDir.DirectoryPath, options.OutDir));
        var expectedPbfPath = Path.Combine(workDir, region.Id, "osm.pbf");
        _fileDownloader
            .DownloadAsync(region.PbfUrl, expectedPbfPath, Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                TestFiles.WriteAllText(expectedPbfPath, "downloaded pbf");
                return Task.FromResult(expectedPbfPath);
            });

        // Act
        var result = await sut.RunAsync(CancellationToken.None);

        // Assert
        result.Should().Be(0);
        File.Exists(expectedPbfPath).Should().BeTrue();
        await _fileDownloader.Received(1).DownloadAsync(region.PbfUrl, expectedPbfPath, Arg.Any<CancellationToken>());
        await _renderRegion.Received(1).RunAsync(region, workDir, outDir, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_SkipsDownload_WhenPbfAlreadyExistsAndOverwritePbfIsDisabled()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("runner-skip-existing-pbf", false);
        var options = CreateOptions(overwritePbf: false);
        var sut = CreateSut(tempDir.DirectoryPath, options);

        var regionsPath = Path.GetFullPath(Path.Combine(tempDir.DirectoryPath, options.RegionsJson));
        TestFiles.WriteAllText(regionsPath, "[]");

        var region = BerlinRegion();
        _regionSource.GetRegionsAsync(regionsPath, Arg.Any<CancellationToken>())
            .Returns(new[] { region });

        var workDir = Path.GetFullPath(Path.Combine(tempDir.DirectoryPath, options.WorkDir));
        var pbfPath = Path.Combine(workDir, region.Id, "osm.pbf");
        TestFiles.WriteAllText(pbfPath, "existing pbf");

        // Act
        await sut.RunAsync(CancellationToken.None);

        // Assert
        await _fileDownloader.DidNotReceiveWithAnyArgs().DownloadAsync(default!, default!, default);
        await _renderRegion.Received(1).RunAsync(
            region,
            workDir,
            Path.GetFullPath(Path.Combine(tempDir.DirectoryPath, options.OutDir)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_RedownloadsPbf_WhenPbfAlreadyExistsAndOverwritePbfIsEnabled()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("runner-redownload-existing-pbf", false);
        var options = CreateOptions(overwritePbf: true);
        var sut = CreateSut(tempDir.DirectoryPath, options);

        var regionsPath = Path.GetFullPath(Path.Combine(tempDir.DirectoryPath, options.RegionsJson));
        TestFiles.WriteAllText(regionsPath, "[]");

        var region = BerlinRegion();
        _regionSource.GetRegionsAsync(regionsPath, Arg.Any<CancellationToken>())
            .Returns(new[] { region });

        var workDir = Path.GetFullPath(Path.Combine(tempDir.DirectoryPath, options.WorkDir));
        var pbfPath = Path.Combine(workDir, region.Id, "osm.pbf");
        TestFiles.WriteAllText(pbfPath, "existing pbf");

        // Act
        await sut.RunAsync(CancellationToken.None);

        // Assert
        await _fileDownloader.Received(1).DownloadAsync(region.PbfUrl, pbfPath, Arg.Any<CancellationToken>());
        await _renderRegion.Received(1).RunAsync(
            region,
            workDir,
            Path.GetFullPath(Path.Combine(tempDir.DirectoryPath, options.OutDir)),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_ProcessesOnlyMatchingRegion_WhenOnlyRegionIdIsConfigured()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("runner-only-region", false);
        var options = CreateOptions(onlyRegionId: "berlin");
        var sut = CreateSut(tempDir.DirectoryPath, options);

        var regionsPath = Path.GetFullPath(Path.Combine(tempDir.DirectoryPath, options.RegionsJson));
        TestFiles.WriteAllText(regionsPath, "[]");

        var berlin = BerlinRegion();
        var hamburg = new RegionDto(
            Id: "hamburg",
            Name: "Hamburg",
            PbfUrl: "https://example.com/hamburg.osm.pbf",
            Poly: "hamburg.poly");

        _regionSource.GetRegionsAsync(regionsPath, Arg.Any<CancellationToken>())
            .Returns(new[] { berlin, hamburg });

        var workDir = Path.GetFullPath(Path.Combine(tempDir.DirectoryPath, options.WorkDir));
        var outDir = Path.GetFullPath(Path.Combine(tempDir.DirectoryPath, options.OutDir));

        // Act
        await sut.RunAsync(CancellationToken.None);

        // Assert
        await _fileDownloader.Received(1).DownloadAsync(
            berlin.PbfUrl,
            Path.Combine(workDir, berlin.Id, "osm.pbf"),
            Arg.Any<CancellationToken>());

        await _renderRegion.Received(1).RunAsync(berlin, workDir, outDir, Arg.Any<CancellationToken>());
        await _renderRegion.DidNotReceive().RunAsync(hamburg, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_ForwardsCancellationToken_ToCollaborators()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("runner-forwards-cancellation-token", false);
        var options = CreateOptions();
        var sut = CreateSut(tempDir.DirectoryPath, options);

        var regionsPath = Path.GetFullPath(Path.Combine(tempDir.DirectoryPath, options.RegionsJson));
        TestFiles.WriteAllText(regionsPath, "[]");

        var region = BerlinRegion();
        using var cts = new CancellationTokenSource();
        var ct = cts.Token;

        _regionSource.GetRegionsAsync(regionsPath, ct)
            .Returns(new[] { region });

        // Act
        await sut.RunAsync(ct);

        // Assert
        await _regionSource.Received(1).GetRegionsAsync(regionsPath, ct);
        await _fileDownloader.Received(1).DownloadAsync(
            region.PbfUrl,
            Arg.Any<string>(),
            ct);
        await _renderRegion.Received(1).RunAsync(
            region,
            Arg.Any<string>(),
            Arg.Any<string>(),
            ct);
    }

    private Runner CreateSut(
        string contentRoot,
        RendererOptions options)
        => new(
            new FakeHostEnvironment
            {
                ContentRootPath = contentRoot
            },
            NullLogger<Runner>.Instance,
            _fileDownloader,
            _regionSource,
            _renderRegion,
            Options.Create(options));

    private static RendererOptions CreateOptions(
        string workDir = "work",
        string outDir = "out",
        string regionsJson = "regions.json",
        bool overwritePbf = false,
        bool dryRun = false,
        string? onlyRegionId = null)
        => new()
        {
            WorkDir = workDir,
            OutDir = outDir,
            RegionsJson = regionsJson,
            OverwritePbf = overwritePbf,
            DryRun = dryRun,
            OnlyRegionId = onlyRegionId,
            DownloadTimeoutSeconds = 600
        };

    private static RegionDto BerlinRegion()
        => new(
            Id: "berlin",
            Name: "Berlin",
            PbfUrl: "https://example.com/berlin.osm.pbf",
            Poly: "berlin.poly");

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "POIneer.Render.UnitTests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
