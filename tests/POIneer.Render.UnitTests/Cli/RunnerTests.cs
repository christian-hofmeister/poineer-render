using FluentAssertions;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using NSubstitute;
using NSubstitute.ExceptionExtensions;
using POIneer.Render.Application.Contracts;
using POIneer.Render.Adapters.Input;
using POIneer.Render.Application.UseCases;
using POIneer.Render.Infrastructure.FileSystem;
using POIneer.Render.Application.Options;
using POIneer.Render.Application.Ports;
using POIneer.Render.Ports;
using POIneer.Render.TestHelpers;
using Xunit;

namespace POIneer.Render.UnitTests.Cli;

public sealed class RunnerTests
{
    private readonly IFileDownloader _fileDownloader = Substitute.For<IFileDownloader>();
    private readonly IRegionSource _regionSource = Substitute.For<IRegionSource>();
    private readonly IRenderRegion _renderRegion = Substitute.For<IRenderRegion>();
    private readonly IRegionUpdateChecker _regionUpdateChecker = Substitute.For<IRegionUpdateChecker>();
    private readonly ISingleInstanceLockFactory _lockFactory = Substitute.For<ISingleInstanceLockFactory>();
    private readonly ISingleInstanceLock _instanceLock = Substitute.For<ISingleInstanceLock>();
    private readonly IDiskSpaceReader _diskSpaceReader = Substitute.For<IDiskSpaceReader>();

    private readonly FileRegionScheduleStateStore _scheduleStore = new(NullLogger<FileRegionScheduleStateStore>.Instance);
    private readonly ManualTimeProvider _clock = new();

    public RunnerTests()
    {
        // By default the lock is always available, so existing rendering behavior is unaffected.
        _instanceLock.TryAcquire().Returns(true);
        _lockFactory.Create(Arg.Any<string>()).Returns(_instanceLock);
        _diskSpaceReader.GetAvailableBytes(Arg.Any<string>()).Returns(long.MaxValue);
        _regionUpdateChecker
            .CheckAsync(Arg.Any<RegionDto>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(callInfo => Task.FromResult(ChangedResult(callInfo.ArgAt<RegionDto>(0))));
    }

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
        await _regionUpdateChecker.DidNotReceiveWithAnyArgs().CheckAsync(default!, default!, default);
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
        await _regionUpdateChecker.DidNotReceiveWithAnyArgs().CheckAsync(default!, default!, default);
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
        await _renderRegion.Received(1).RunAsync(region, workDir, outDir, Arg.Any<CancellationToken>(), Arg.Any<bool>());
        await _regionUpdateChecker.Received(1).MarkProcessedAsync(
            region,
            Path.Combine(workDir, region.Id, "render-state.json"),
            Arg.Any<RegionUpdateMetadata>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RunAsync_SkipsDownloadButRunsRenderUseCase_WhenPbfAlreadyExistsAndRemoteMetadataIsUnchanged()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("runner-skip-existing-pbf", false);
        var options = CreateOptions(overwriteDatabase: true, overwritePbf: false);
        var sut = CreateSut(tempDir.DirectoryPath, options);

        var regionsPath = Path.GetFullPath(Path.Combine(tempDir.DirectoryPath, options.RegionsJson));
        TestFiles.WriteAllText(regionsPath, "[]");

        var region = BerlinRegion();
        _regionSource.GetRegionsAsync(regionsPath, Arg.Any<CancellationToken>())
            .Returns(new[] { region });

        var workDir = Path.GetFullPath(Path.Combine(tempDir.DirectoryPath, options.WorkDir));
        var pbfPath = Path.Combine(workDir, region.Id, "osm.pbf");
        var statePath = Path.Combine(workDir, region.Id, "download-state.json");
        TestFiles.WriteAllText(pbfPath, "existing pbf");
        var metadata = new RegionUpdateMetadata("\"abc\"", DateTimeOffset.Parse("2026-01-01T00:00:00Z"), 42);
        _regionUpdateChecker
            .CheckAsync(region, statePath, Arg.Any<CancellationToken>())
            .Returns(new RegionUpdateCheckResult(
                ShouldRedownloadPbf: false,
                Reason: "Remote PBF metadata is unchanged.",
                RemoteMetadata: metadata,
                StoredState: new RegionDownloadState(region.Id, region.PbfUrl, metadata, DateTimeOffset.UtcNow)));

        // Act
        await sut.RunAsync(CancellationToken.None);

        // Assert
        await _fileDownloader.DidNotReceiveWithAnyArgs().DownloadAsync(default!, default!, default);
        await _renderRegion.Received(1).RunAsync(
            region,
            workDir,
            Path.GetFullPath(Path.Combine(tempDir.DirectoryPath, options.OutDir)),
            Arg.Any<CancellationToken>(), Arg.Any<bool>());
        await _regionUpdateChecker.Received(1).MarkProcessedAsync(
            region,
            Path.Combine(workDir, region.Id, "render-state.json"),
            metadata,
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
            Arg.Any<CancellationToken>(), Arg.Any<bool>());
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

        await _renderRegion.Received(1).RunAsync(berlin, workDir, outDir, Arg.Any<CancellationToken>(), Arg.Any<bool>());
        await _renderRegion.DidNotReceive().RunAsync(hamburg, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task RunAsync_ContinuesWithRemainingRegions_AndReturnsNonZero_WhenARegionFails()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("runner-continues-after-region-failure", false);
        var options = CreateOptions();
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

        _renderRegion
            .RunAsync(berlin, workDir, outDir, Arg.Any<CancellationToken>(), Arg.Any<bool>())
            .ThrowsAsync(new InvalidOperationException("Generated dataset is invalid"));

        // Act
        var result = await sut.RunAsync(CancellationToken.None);

        // Assert
        result.Should().Be(1);
        await _renderRegion.Received(1).RunAsync(berlin, workDir, outDir, Arg.Any<CancellationToken>(), Arg.Any<bool>());
        await _renderRegion.Received(1).RunAsync(hamburg, workDir, outDir, Arg.Any<CancellationToken>(), Arg.Any<bool>());
        await _regionUpdateChecker.DidNotReceive().MarkProcessedAsync(
            berlin,
            Arg.Any<string>(),
            Arg.Any<RegionUpdateMetadata>(),
            Arg.Any<CancellationToken>());
        await _regionUpdateChecker.Received(1).MarkProcessedAsync(
            hamburg,
            Arg.Any<string>(),
            Arg.Any<RegionUpdateMetadata>(),
            Arg.Any<CancellationToken>());
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
        await _regionUpdateChecker.Received(1).CheckAsync(
            region,
            Arg.Any<string>(),
            ct);
        await _renderRegion.Received(1).RunAsync(
            region,
            Arg.Any<string>(),
            Arg.Any<string>(),
            ct, Arg.Any<bool>());
    }

    [Fact]
    public async Task RunAsync_SkipsExecutionAndReturnsZero_WhenAnotherInstanceHoldsTheLock()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("runner-skips-when-locked", false);
        var options = CreateOptions();
        _instanceLock.TryAcquire().Returns(false);
        var sut = CreateSut(tempDir.DirectoryPath, options);

        // Act
        var result = await sut.RunAsync(CancellationToken.None);

        // Assert
        result.Should().Be(0);
        await _regionSource.DidNotReceiveWithAnyArgs().GetRegionsAsync(default!, default);
        await _regionUpdateChecker.DidNotReceiveWithAnyArgs().CheckAsync(default!, default!, default);
        await _fileDownloader.DidNotReceiveWithAnyArgs().DownloadAsync(default!, default!, default);
        await _renderRegion.DidNotReceiveWithAnyArgs().RunAsync(default!, default!, default!, default);
    }

    [Fact]
    public async Task RunAsync_DoesNotAcquireLock_WhenDryRunIsEnabled()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("runner-dry-run-no-lock", false);
        var sut = CreateSut(tempDir.DirectoryPath, CreateOptions(dryRun: true));

        // Act
        await sut.RunAsync(CancellationToken.None);

        // Assert
        _lockFactory.DidNotReceiveWithAnyArgs().Create(default!);
    }

    [Fact]
    public async Task RunAsync_DisposesTheLock_AfterProcessingCompletes()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("runner-disposes-lock", false);
        var options = CreateOptions();
        var sut = CreateSut(tempDir.DirectoryPath, options);

        var regionsPath = Path.GetFullPath(Path.Combine(tempDir.DirectoryPath, options.RegionsJson));
        TestFiles.WriteAllText(regionsPath, "[]");
        _regionSource.GetRegionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<RegionDto>());

        // Act
        await sut.RunAsync(CancellationToken.None);

        // Assert
        _instanceLock.Received(1).Dispose();
    }

    [Fact]
    public async Task RunAsync_UsesWorkDirDefaultLockFile_WhenLockFilePathIsNotConfigured()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("runner-default-lock-path", false);
        var options = CreateOptions(workDir: "work");
        var sut = CreateSut(tempDir.DirectoryPath, options);

        var regionsPath = Path.GetFullPath(Path.Combine(tempDir.DirectoryPath, options.RegionsJson));
        TestFiles.WriteAllText(regionsPath, "[]");
        _regionSource.GetRegionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<RegionDto>());

        var workDir = Path.GetFullPath(Path.Combine(tempDir.DirectoryPath, options.WorkDir));
        var expectedLockFilePath = Path.Combine(workDir, "poineer-render.lock");

        // Act
        await sut.RunAsync(CancellationToken.None);

        // Assert
        _lockFactory.Received(1).Create(expectedLockFilePath);
    }

    [Fact]
    public async Task RunAsync_ProcessesRegion_WhenPbfExistsButRemoteMetadataChanged()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("runner-render-changed-region", false);
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
        var result = await sut.RunAsync(CancellationToken.None);

        // Assert
        result.Should().Be(0);
        await _fileDownloader.Received(1).DownloadAsync(region.PbfUrl, pbfPath, Arg.Any<CancellationToken>());
        await _renderRegion.Received(1).RunAsync(
            region,
            workDir,
            Path.GetFullPath(Path.Combine(tempDir.DirectoryPath, options.OutDir)),
            Arg.Any<CancellationToken>(), Arg.Any<bool>());
    }

    [Fact]
    public async Task RunAsync_ResolvesConfiguredLockFilePath_AgainstContentRoot()
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("runner-configured-lock-path", false);
        var options = CreateOptions(lockFilePath: "locks/render.lock");
        var sut = CreateSut(tempDir.DirectoryPath, options);

        var regionsPath = Path.GetFullPath(Path.Combine(tempDir.DirectoryPath, options.RegionsJson));
        TestFiles.WriteAllText(regionsPath, "[]");
        _regionSource.GetRegionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Array.Empty<RegionDto>());

        var expectedLockFilePath = Path.GetFullPath(Path.Combine(tempDir.DirectoryPath, options.LockFilePath!));

        // Act
        await sut.RunAsync(CancellationToken.None);

        // Assert
        _lockFactory.Received(1).Create(expectedLockFilePath);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, false)]
    [InlineData(true, true)]
    public async Task RunAsync_RetriesFailedRun_AndOnlyRedownloadsWhenDownloadDidNotComplete(
        bool downloadFails,
        bool existingDownload)
    {
        // Arrange
        await using var tempDir = TestTemporaryDirectories.Create("runner-download-retry", false);
        var region = BerlinRegion();
        var metadata = new RegionUpdateMetadata("etag", DateTimeOffset.Parse("2026-01-01T00:00:00Z"), 3);
        var metadataReader = Substitute.For<IRemotePbfMetadataReader>();
        metadataReader.GetMetadataAsync(region.PbfUrl, Arg.Any<CancellationToken>()).Returns(metadata);
        var downloadStore = new FileRegionDownloadStateStore(NullLogger<FileRegionDownloadStateStore>.Instance);
        var renderStore = new FileRegionRenderStateStore(NullLogger<FileRegionRenderStateStore>.Instance);
        var checker = new RegionUpdateChecker(metadataReader, renderStore, downloadStore);
        var workDir = Path.Combine(tempDir.DirectoryPath, "work");
        var pbfPath = Path.Combine(workDir, region.Id, "osm.pbf");
        var downloadStatePath = Path.Combine(workDir, region.Id, "download-state.json");
        var renderStatePath = Path.Combine(workDir, region.Id, "render-state.json");
        TestFiles.WriteAllText(Path.Combine(tempDir.DirectoryPath, "regions.json"), "[]");
        _regionSource.GetRegionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new[] { region });
        if (existingDownload)
        {
            TestFiles.WriteAllText(pbfPath, "old");
            await checker.MarkDownloadedAsync(region, downloadStatePath, metadata);
        }
        var downloadAttempts = 0;
        _fileDownloader.DownloadAsync(region.PbfUrl, pbfPath, Arg.Any<CancellationToken>())
            .Returns(_ =>
            {
                downloadAttempts++;
                if (downloadFails && downloadAttempts == 1)
                {
                    TestFiles.WriteAllText(pbfPath, "partial");
                    throw new IOException("Download interrupted");
                }
                TestFiles.WriteAllText(pbfPath, "pbf");
                return Task.FromResult(pbfPath);
            });
        var renderAttempts = 0;
        _renderRegion.RunAsync(region, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<bool>())
            .Returns(_ =>
            {
                renderAttempts++;
                if (!downloadFails && renderAttempts == 1)
                {
                    // Publishing can fail after canonical artifacts have already been promoted.
                    TestFiles.WriteAllText(Path.Combine(tempDir.DirectoryPath, "out", region.Id, "poi.sqlite"), "promoted output");
                    throw new InvalidOperationException("Planetiler failed");
                }
                return Task.CompletedTask;
            });

        // Act: the first run fails after starting the download or rendering.
        var first = CreateSut(tempDir.DirectoryPath, CreateOptions(overwritePbf: existingDownload), checker);
        (await first.RunAsync(CancellationToken.None)).Should().Be(1);

        // Assert: only a completed download has a checkpoint; rendering is never marked successful.
        File.Exists(downloadStatePath).Should().Be(!downloadFails);
        File.Exists(renderStatePath).Should().BeFalse();
        var schedulePath = Path.Combine(workDir, region.Id, "schedule-state.json");
        var failedSchedule = await _scheduleStore.ReadAsync(schedulePath);
        failedSchedule!.ProcessingPending.Should().BeTrue();
        failedSchedule.NextDueUtc.Should().BeNull();

        // Act: a fresh runner retries with overwrite disabled.
        _clock.UtcNow = _clock.UtcNow.AddDays(1);
        var retry = CreateSut(tempDir.DirectoryPath, CreateOptions(), checker);
        (await retry.RunAsync(CancellationToken.None)).Should().Be(0);

        // Assert
        downloadAttempts.Should().Be(downloadFails ? 2 : 1);
        renderAttempts.Should().Be(downloadFails ? 1 : 2);
        (await downloadStore.ReadAsync(downloadStatePath))!.DownloadedMetadata.Should().Be(metadata);
        (await renderStore.ReadAsync(renderStatePath))!.LastProcessedMetadata.Should().Be(metadata);
        var completedSchedule = await _scheduleStore.ReadAsync(schedulePath);
        completedSchedule!.ProcessingPending.Should().BeFalse();
        completedSchedule.NextDueUtc.Should().Be(_clock.UtcNow.AddDays(7));
        await _renderRegion.Received(downloadFails ? 1 : 2).RunAsync(
            region, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), true);
    }

    [Fact]
    public async Task RunAsync_FailsBeforeDownloadAndRetriesWhenDiskSpaceIsAvailable()
    {
        await using var tempDir = TestTemporaryDirectories.Create("runner-disk-space", false);
        var region = BerlinRegion();
        var options = CreateOptions(minimumFreeDiskSpaceBytes: 1024);
        TestFiles.WriteAllText(Path.Combine(tempDir.DirectoryPath, options.RegionsJson), "[]");
        _regionSource.GetRegionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new[] { region });
        var regionOutDir = Path.Combine(tempDir.DirectoryPath, options.OutDir, region.Id);
        _diskSpaceReader.GetAvailableBytes(regionOutDir).Returns(1023);
        var sut = CreateSut(tempDir.DirectoryPath, options);

        (await sut.RunAsync(CancellationToken.None)).Should().Be(1);

        await _fileDownloader.DidNotReceiveWithAnyArgs().DownloadAsync(default!, default!, default);
        await _renderRegion.DidNotReceiveWithAnyArgs().RunAsync(default!, default!, default!, default);
        var schedulePath = Path.Combine(tempDir.DirectoryPath, options.WorkDir, region.Id, "schedule-state.json");
        (await _scheduleStore.ReadAsync(schedulePath))!.ProcessingPending.Should().BeTrue();

        _diskSpaceReader.GetAvailableBytes(regionOutDir).Returns(1024);

        (await sut.RunAsync(CancellationToken.None)).Should().Be(0);
        await _fileDownloader.Received(1).DownloadAsync(region.PbfUrl, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _renderRegion.Received(1).RunAsync(region, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), true);
        (await _scheduleStore.ReadAsync(schedulePath))!.ProcessingPending.Should().BeFalse();
    }

    [Theory]
    [InlineData(DatasetPublisherTarget.Local, 1)]
    [InlineData(DatasetPublisherTarget.AzureBlob, 0)]
    public async Task RunAsync_ChecksPublishDiskOnlyForLocalPublisher_WhenRenderingCachedPbf(
        DatasetPublisherTarget target, int expectedExitCode)
    {
        await using var tempDir = TestTemporaryDirectories.Create("runner-publish-disk-space", false);
        var region = BerlinRegion();
        var options = CreateOptions(overwriteDatabase: true);
        TestFiles.WriteAllText(Path.Combine(tempDir.DirectoryPath, options.RegionsJson), "[]");
        TestFiles.WriteAllText(Path.Combine(tempDir.DirectoryPath, options.WorkDir, region.Id, "osm.pbf"), "cached PBF");
        _regionSource.GetRegionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new[] { region });
        _regionUpdateChecker.CheckAsync(region, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ChangedResult(region) with { ShouldRedownloadPbf = false });
        var publishDir = Path.Combine(tempDir.DirectoryPath, "publish");
        var regionPublishDir = Path.Combine(publishDir, region.Id);
        _diskSpaceReader.GetAvailableBytes(regionPublishDir).Returns(0);
        var sut = CreateSut(tempDir.DirectoryPath, options,
            publisherOptions: new PublisherOptions { Target = target, DestinationDir = publishDir });

        (await sut.RunAsync(CancellationToken.None)).Should().Be(expectedExitCode);

        _diskSpaceReader.Received(expectedExitCode).GetAvailableBytes(regionPublishDir);
        await _fileDownloader.DidNotReceiveWithAnyArgs().DownloadAsync(default!, default!, default);
        await _renderRegion.Received(1 - expectedExitCode).RunAsync(
            region, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), true);
    }

    [Fact]
    public async Task RunAsync_RejectsInvalidRegionIdBeforeCheckingRemoteMetadata()
    {
        await using var tempDir = TestTemporaryDirectories.Create("runner-invalid-region-id");
        var options = CreateOptions();
        var region = BerlinRegion() with { Id = "geofabrik/Berlin" };
        TestFiles.WriteAllText(Path.Combine(tempDir.DirectoryPath, options.RegionsJson), "[]");
        _regionSource.GetRegionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new[] { region });
        var sut = CreateSut(tempDir.DirectoryPath, options);

        var result = await sut.RunAsync(CancellationToken.None);

        result.Should().Be(1);
        await _regionUpdateChecker.DidNotReceiveWithAnyArgs().CheckAsync(default!, default!, default);
        await _renderRegion.DidNotReceiveWithAnyArgs().RunAsync(default!, default!, default!, default);
        Directory.Exists(Path.Combine(tempDir.DirectoryPath, options.WorkDir, "geofabrik")).Should().BeFalse();
    }

    [Theory]
    [InlineData(null, "https://example.com/invalid.pbf")]
    [InlineData("berlin", "https://example.com/invalid.pbf")]
    [InlineData("berlin", "")]
    public async Task RunAsync_RejectsInvalidRegionIdInJsonBeforeProcessingAnyRegion(
        string? onlyRegionId, string invalidRegionPbfUrl)
    {
        await using var tempDir = TestTemporaryDirectories.Create("runner-invalid-region-config");
        var options = CreateOptions(onlyRegionId: onlyRegionId);
        var regionsPath = Path.Combine(tempDir.DirectoryPath, options.RegionsJson);
        TestFiles.WriteAllText(regionsPath, $$"""
            [
              {"Id":"berlin","Name":"Berlin","PbfUrl":"https://example.com/berlin.pbf"},
              {"Id":"geofabrik/Berlin","Name":"Invalid region","PbfUrl":"{{invalidRegionPbfUrl}}"}
            ]
            """);
        var source = new GeofabrikRegionSource(NullLogger<GeofabrikRegionSource>.Instance);
        var sut = CreateSut(tempDir.DirectoryPath, options, regionSource: source);

        var act = () => sut.RunAsync(CancellationToken.None);

        await act.Should().ThrowAsync<InvalidDataException>()
            .WithMessage("*entry 2*regions.json*geofabrik/Berlin*");
        await _regionUpdateChecker.DidNotReceiveWithAnyArgs().CheckAsync(default!, default!, default);
        await _fileDownloader.DidNotReceiveWithAnyArgs().DownloadAsync(default!, default!, default);
        await _renderRegion.DidNotReceiveWithAnyArgs().RunAsync(default!, default!, default!, default);
        Directory.Exists(Path.Combine(tempDir.DirectoryPath, options.WorkDir, "berlin")).Should().BeFalse();
    }

    [Theory]
    [InlineData(0, null, 1)]
    [InlineData(-1, null, 1)]
    [InlineData(0, "before", 0)]
    [InlineData(0, "invalid", 1)]
    public async Task RunAsync_IsolatesInvalidRegionIntervals_AndProcessesOtherSelectedRegions(
        int invalidInterval, string? onlyRegionId, int expectedExitCode)
    {
        // Arrange: load actual JSON to verify invalid values reach per-region handling.
        await using var tempDir = TestTemporaryDirectories.Create("runner-invalid-region-interval", false);
        var options = CreateOptions(onlyRegionId: onlyRegionId);
        var regionsPath = Path.Combine(tempDir.DirectoryPath, options.RegionsJson);
        TestFiles.WriteAllText(regionsPath, $$"""
            [
              {"Id":"before","Name":"Before","PbfUrl":"https://example.com/before.pbf"},
              {"Id":"invalid","Name":"Invalid","PbfUrl":"https://example.com/invalid.pbf","UpdateIntervalDays":{{invalidInterval}}},
              {"Id":"after","Name":"After","PbfUrl":"https://example.com/after.pbf","UpdateIntervalDays":3}
            ]
            """);
        var source = new GeofabrikRegionSource(NullLogger<GeofabrikRegionSource>.Instance);
        var regions = await source.GetRegionsAsync(regionsPath);
        _regionSource.GetRegionsAsync(regionsPath, Arg.Any<CancellationToken>()).Returns(regions);
        var sut = CreateSut(tempDir.DirectoryPath, options);

        // Act
        var result = await sut.RunAsync(CancellationToken.None);

        // Assert
        result.Should().Be(expectedExitCode);
        var invalidRegion = regions.Single(region => region.Id == "invalid");
        await _regionUpdateChecker.DidNotReceive().CheckAsync(
            invalidRegion, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _fileDownloader.DidNotReceive().DownloadAsync(
            invalidRegion.PbfUrl, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _renderRegion.DidNotReceive().RunAsync(
            invalidRegion, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<bool>());
        await _regionUpdateChecker.DidNotReceive().MarkProcessedAsync(
            invalidRegion, Arg.Any<string>(), Arg.Any<RegionUpdateMetadata>(), Arg.Any<CancellationToken>());
        Directory.Exists(Path.Combine(tempDir.DirectoryPath, options.WorkDir, invalidRegion.Id)).Should().BeFalse();

        foreach (var region in regions.Where(region => region.Id != "invalid"))
        {
            var expectedCalls = onlyRegionId is null || onlyRegionId == region.Id ? 1 : 0;
            await _renderRegion.Received(expectedCalls).RunAsync(
                region, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), Arg.Any<bool>());
            await _regionUpdateChecker.Received(expectedCalls).MarkProcessedAsync(
                region, Arg.Any<string>(), Arg.Any<RegionUpdateMetadata>(), Arg.Any<CancellationToken>());
        }
    }

    [Theory]
    [InlineData(-1, 7)]
    [InlineData(0, 7)]
    [InlineData(1, 3)]
    public async Task RunAsync_ChecksOnlyDueRegions_AndAdvancesUnchangedSourcesWithoutProcessing(
        int secondsAfterDue, int intervalDays)
    {
        await using var tempDir = TestTemporaryDirectories.Create("runner-schedule-unchanged", false);
        var region = BerlinRegion() with { UpdateIntervalDays = intervalDays };
        var options = CreateOptions();
        TestFiles.WriteAllText(Path.Combine(tempDir.DirectoryPath, options.RegionsJson), "[]");
        _regionSource.GetRegionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new[] { region });
        var regionWorkDir = Path.Combine(tempDir.DirectoryPath, options.WorkDir, region.Id);
        var schedulePath = Path.Combine(regionWorkDir, "schedule-state.json");
        var due = _clock.UtcNow;
        var initial = new RegionScheduleState(region.Id, region.PbfUrl, due.AddDays(-7), due, false);
        await _scheduleStore.WriteAsync(schedulePath, initial);
        TestFiles.WriteAllText(Path.Combine(regionWorkDir, "osm.pbf"), "pbf");
        TestFiles.WriteAllText(Path.Combine(tempDir.DirectoryPath, options.OutDir, region.Id, "poi.sqlite"), "sqlite");
        _regionUpdateChecker.CheckAsync(region, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(ChangedResult(region) with { ShouldRedownloadPbf = false });
        _clock.UtcNow = due.AddSeconds(secondsAfterDue);
        var sut = CreateSut(tempDir.DirectoryPath, options);

        (await sut.RunAsync(CancellationToken.None)).Should().Be(0);

        _diskSpaceReader.DidNotReceiveWithAnyArgs().GetAvailableBytes(default!);
        await _regionUpdateChecker.Received(secondsAfterDue >= 0 ? 1 : 0)
            .CheckAsync(region, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _fileDownloader.DidNotReceiveWithAnyArgs().DownloadAsync(default!, default!, default);
        await _renderRegion.DidNotReceiveWithAnyArgs().RunAsync(default!, default!, default!, default);
        await _regionUpdateChecker.DidNotReceiveWithAnyArgs().MarkProcessedAsync(default!, default!, default!, default);
        var stored = await _scheduleStore.ReadAsync(schedulePath);
        if (secondsAfterDue < 0)
            stored.Should().Be(initial);
        else
        {
            stored!.LastSuccessfulCheckUtc.Should().Be(_clock.UtcNow);
            stored.NextDueUtc.Should().Be(_clock.UtcNow.AddDays(intervalDays));
            stored.ProcessingPending.Should().BeFalse();
        }
    }

    [Fact]
    public async Task RunAsync_AdvancesDueDateFromCompletionTime_OnlyAfterSuccessfulProcessing()
    {
        await using var tempDir = TestTemporaryDirectories.Create("runner-schedule-completion", false);
        var region = BerlinRegion();
        var options = CreateOptions();
        var regionWorkDir = Path.Combine(tempDir.DirectoryPath, options.WorkDir, region.Id);
        var schedulePath = Path.Combine(regionWorkDir, "schedule-state.json");
        TestFiles.WriteAllText(Path.Combine(tempDir.DirectoryPath, options.RegionsJson), "[]");
        _regionSource.GetRegionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new[] { region });
        var initial = new RegionScheduleState(region.Id, region.PbfUrl, _clock.UtcNow.AddDays(-8), _clock.UtcNow.AddDays(-1), false);
        await _scheduleStore.WriteAsync(schedulePath, initial);
        _renderRegion.RunAsync(region, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), true)
            .Returns(async _ =>
            {
                var pending = await _scheduleStore.ReadAsync(schedulePath);
                pending!.NextDueUtc.Should().Be(initial.NextDueUtc);
                pending.LastSuccessfulCheckUtc.Should().Be(initial.LastSuccessfulCheckUtc);
                pending.ProcessingPending.Should().BeTrue();
                _clock.UtcNow = _clock.UtcNow.AddHours(3);
            });

        (await CreateSut(tempDir.DirectoryPath, options).RunAsync(CancellationToken.None)).Should().Be(0);

        var completed = await _scheduleStore.ReadAsync(schedulePath);
        completed!.LastSuccessfulCheckUtc.Should().Be(_clock.UtcNow);
        completed.NextDueUtc.Should().Be(_clock.UtcNow.AddDays(7));
        completed.ProcessingPending.Should().BeFalse();
    }

    [Theory]
    [InlineData("metadata")]
    [InlineData("download")]
    [InlineData("render")]
    [InlineData("publish")]
    [InlineData("cancel")]
    public async Task RunAsync_DoesNotAdvanceDueDate_WhenCheckOrProcessingFails(string failureStage)
    {
        await using var tempDir = TestTemporaryDirectories.Create("runner-schedule-failure", false);
        var region = BerlinRegion();
        var options = CreateOptions();
        var schedulePath = Path.Combine(tempDir.DirectoryPath, options.WorkDir, region.Id, "schedule-state.json");
        TestFiles.WriteAllText(Path.Combine(tempDir.DirectoryPath, options.RegionsJson), "[]");
        _regionSource.GetRegionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new[] { region });
        var initial = new RegionScheduleState(region.Id, region.PbfUrl, _clock.UtcNow.AddDays(-8), _clock.UtcNow.AddDays(-1), false);
        await _scheduleStore.WriteAsync(schedulePath, initial);
        if (failureStage == "metadata")
            _regionUpdateChecker.CheckAsync(region, Arg.Any<string>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new HttpRequestException("HEAD failed"));
        else if (failureStage == "download")
            _fileDownloader.DownloadAsync(region.PbfUrl, Arg.Any<string>(), Arg.Any<CancellationToken>())
                .ThrowsAsync(new IOException("Download failed"));
        else if (failureStage == "cancel")
            _renderRegion.RunAsync(region, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), true)
                .ThrowsAsync(new OperationCanceledException());
        else
            _renderRegion.RunAsync(region, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), true)
                .ThrowsAsync(new IOException(failureStage + " failed"));
        var sut = CreateSut(tempDir.DirectoryPath, options);

        if (failureStage == "cancel")
            await Assert.ThrowsAsync<OperationCanceledException>(() => sut.RunAsync(CancellationToken.None));
        else
            (await sut.RunAsync(CancellationToken.None)).Should().Be(1);

        var stored = await _scheduleStore.ReadAsync(schedulePath);
        stored!.NextDueUtc.Should().Be(initial.NextDueUtc);
        stored.LastSuccessfulCheckUtc.Should().Be(initial.LastSuccessfulCheckUtc);
        stored.ProcessingPending.Should().Be(failureStage != "metadata");
        new RegionUpdateSchedule(_scheduleStore, new RegionUpdateIntervalPolicy(Options.Create(options)), _clock)
            .IsDue(stored).Should().BeTrue();
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task RunAsync_RetriesFailedForcedMetadataCheck_BeforeScheduledDueDate(bool overwritePbf)
    {
        await using var tempDir = TestTemporaryDirectories.Create("runner-forced-metadata-retry");
        var region = BerlinRegion();
        var options = CreateOptions(overwriteDatabase: !overwritePbf, overwritePbf: overwritePbf);
        var regionWorkDir = Path.Combine(tempDir.DirectoryPath, options.WorkDir, region.Id);
        var schedulePath = Path.Combine(regionWorkDir, "schedule-state.json");
        TestFiles.WriteAllText(Path.Combine(tempDir.DirectoryPath, options.RegionsJson), "[]");
        TestFiles.WriteAllText(Path.Combine(regionWorkDir, "osm.pbf"), "pbf");
        TestFiles.WriteAllText(Path.Combine(tempDir.DirectoryPath, options.OutDir, region.Id, "poi.sqlite"), "sqlite");
        _regionSource.GetRegionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new[] { region });
        var initial = new RegionScheduleState(region.Id, region.PbfUrl, _clock.UtcNow, _clock.UtcNow.AddDays(7), false);
        await _scheduleStore.WriteAsync(schedulePath, initial);
        _regionUpdateChecker.CheckAsync(region, Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(
                _ => Task.FromException<RegionUpdateCheckResult>(new HttpRequestException("HEAD failed")),
                _ => Task.FromResult(ChangedResult(region) with { ShouldRedownloadPbf = false }));

        (await CreateSut(tempDir.DirectoryPath, options).RunAsync(CancellationToken.None)).Should().Be(1);

        (await _scheduleStore.ReadAsync(schedulePath)).Should().Be(initial with { ProcessingPending = true });
        await _renderRegion.DidNotReceiveWithAnyArgs().RunAsync(default!, default!, default!, default);

        _clock.UtcNow = _clock.UtcNow.AddDays(1);
        (await CreateSut(tempDir.DirectoryPath, CreateOptions()).RunAsync(CancellationToken.None)).Should().Be(0);

        await _regionUpdateChecker.Received(2).CheckAsync(region, Arg.Any<string>(), Arg.Any<CancellationToken>());
        await _renderRegion.Received(1).RunAsync(region, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>(), true);
        var completed = await _scheduleStore.ReadAsync(schedulePath);
        completed!.ProcessingPending.Should().BeFalse();
        completed.NextDueUtc.Should().Be(_clock.UtcNow.AddDays(7));
    }

    [Fact]
    public async Task RunAsync_PublishesCompleteCatalogBeforeRenderingOnlySelectedRegion()
    {
        await using var temp = TestTemporaryDirectories.Create("runner-publishes-catalog");
        var options = CreateOptions(onlyRegionId: "berlin");
        TestFiles.WriteAllText(Path.Combine(temp.DirectoryPath, options.RegionsJson), "[]");
        var berlin = BerlinRegion();
        var hamburg = new RegionDto("hamburg", "Hamburg", "https://example.com/hamburg.pbf", null);
        var disabled = new RegionDto("disabled", "Disabled", "", null);
        _regionSource.GetRegionsAsync(Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(new[] { berlin, hamburg, disabled });
        var store = new FileRegionMetadataStore(Path.Combine(temp.DirectoryPath, "metadata"));
        var metadataPublisher = new RegionMetadataPublisher(store, _clock);
        var publication = new RegionPublicationContext(null);
        _renderRegion.RunAsync(berlin, Arg.Any<string>(), Arg.Any<string>(), publication, Arg.Any<CancellationToken>(), true)
            .Returns(async _ =>
            {
                var snapshot = await store.ReadAsync("regions.json", CancellationToken.None);
                Assert.NotNull(snapshot);
                using var json = System.Text.Json.JsonDocument.Parse(snapshot.Content);
                Assert.Equal(new[] { "berlin", "disabled", "hamburg" },
                    json.RootElement.EnumerateArray().Select(entry => entry.GetProperty("Id").GetString()));
            });

        var sut = CreateSut(temp.DirectoryPath, options, metadataPublisher: metadataPublisher);
        (await sut.RunAsync(CancellationToken.None)).Should().Be(0);

        await _renderRegion.Received(1).RunAsync(
            berlin, Arg.Any<string>(), Arg.Any<string>(), publication, Arg.Any<CancellationToken>(), true);
        await _renderRegion.DidNotReceive().RunAsync(
            hamburg, Arg.Any<string>(), Arg.Any<string>(), Arg.Any<RegionPublicationContext>(), Arg.Any<CancellationToken>(), Arg.Any<bool>());
    }

    private Runner CreateSut(
        string contentRoot,
        RendererOptions options,
        IRegionUpdateChecker? updateChecker = null,
        IRegionSource? regionSource = null,
        RegionMetadataPublisher? metadataPublisher = null,
        PublisherOptions? publisherOptions = null)
        => new(
            new FakeHostEnvironment
            {
                ContentRootPath = contentRoot
            },
            NullLogger<Runner>.Instance,
            _fileDownloader,
            regionSource ?? _regionSource,
            _renderRegion,
            updateChecker ?? _regionUpdateChecker,
            _lockFactory,
            new RegionUpdateIntervalPolicy(Options.Create(options)),
            Options.Create(options),
            new RegionUpdateSchedule(_scheduleStore, new RegionUpdateIntervalPolicy(Options.Create(options)), _clock),
            Options.Create(new VectorTileOptions()),
            Options.Create(publisherOptions ?? new PublisherOptions()),
            _diskSpaceReader,
            metadataPublisher);

    private static RendererOptions CreateOptions(
        string workDir = "work",
        string outDir = "out",
        string regionsJson = "regions.json",
        bool overwriteDatabase = false,
        bool overwritePbf = false,
        bool dryRun = false,
        string? onlyRegionId = null,
        string? lockFilePath = null,
        long minimumFreeDiskSpaceBytes = 1024L * 1024 * 1024)
        => new()
        {
            WorkDir = workDir,
            OutDir = outDir,
            RegionsJson = regionsJson,
            OverwriteDatabase = overwriteDatabase,
            OverwritePbf = overwritePbf,
            DryRun = dryRun,
            OnlyRegionId = onlyRegionId,
            DownloadTimeoutSeconds = 600,
            MinimumFreeDiskSpaceBytes = minimumFreeDiskSpaceBytes,
            LockFilePath = lockFilePath
        };

    private static RegionDto BerlinRegion()
        => new(
            Id: "berlin",
            Name: "Berlin",
            PbfUrl: "https://example.com/berlin.osm.pbf",
            Poly: "berlin.poly");

    private static RegionUpdateCheckResult ChangedResult(RegionDto region)
    {
        var metadata = new RegionUpdateMetadata("\"etag\"", DateTimeOffset.Parse("2026-01-01T00:00:00Z"), 1024);
        return new RegionUpdateCheckResult(
            ShouldRedownloadPbf: true,
            Reason: "Remote ETag changed.",
            RemoteMetadata: metadata,
            StoredState: new RegionDownloadState(
                region.Id,
                region.PbfUrl,
                new RegionUpdateMetadata("\"old-etag\"", DateTimeOffset.Parse("2025-01-01T00:00:00Z"), 512),
                DateTimeOffset.Parse("2025-01-01T00:00:00Z")));
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        public DateTimeOffset UtcNow { get; set; } = DateTimeOffset.Parse("2026-01-01T00:00:00Z");
        public override DateTimeOffset GetUtcNow() => UtcNow;
    }

    private sealed class FakeHostEnvironment : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = "Test";
        public string ApplicationName { get; set; } = "POIneer.Render.UnitTests";
        public string ContentRootPath { get; set; } = "";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
