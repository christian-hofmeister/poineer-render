using FluentAssertions;
using POIneer.Render.Infrastructure.Pathing;
using POIneer.Render.TestHelpers;
using Xunit;

namespace POIneer.Render.UnitTests.Infrastructure.Pathing;

public sealed class ContentRootResolverTests
{
    [Fact]
    public async Task Resolve_ReturnsCurrentDirectory_WhenItContainsAppsettings()
    {
        await using var tempDir = TestTemporaryDirectories.Create("content-root-current-dir", false);
        TestFiles.WriteAllText(Path.Combine(tempDir.DirectoryPath, "appsettings.json"), "{}");

        var result = ContentRootResolver.Resolve(
            tempDir.DirectoryPath,
            Path.Combine(tempDir.DirectoryPath, "bin", "Debug", "net10.0"));

        result.Should().Be(tempDir.DirectoryPath);
    }

    [Fact]
    public async Task Resolve_PrefersProjectDirectoryOverBuildOutput_WhenBothContainAppsettings()
    {
        await using var tempDir = TestTemporaryDirectories.Create("content-root-project-over-bin", false);
        var projectDirectory = tempDir.DirectoryPath;
        var buildOutputDirectory = Path.Combine(projectDirectory, "bin", "Debug", "net10.0");

        TestFiles.WriteAllText(Path.Combine(projectDirectory, "appsettings.json"), "{}");
        TestFiles.WriteAllText(Path.Combine(buildOutputDirectory, "appsettings.json"), "{}");

        var result = ContentRootResolver.Resolve(
            currentDirectory: Path.GetTempPath(),
            baseDirectory: buildOutputDirectory);

        result.Should().Be(projectDirectory);
    }

    [Fact]
    public async Task Resolve_FallsBackToBuildOutput_WhenProjectDirectoryDoesNotContainAppsettings()
    {
        await using var tempDir = TestTemporaryDirectories.Create("content-root-build-output", false);
        var buildOutputDirectory = Path.Combine(tempDir.DirectoryPath, "bin", "Debug", "net10.0");

        TestFiles.WriteAllText(Path.Combine(buildOutputDirectory, "appsettings.json"), "{}");

        var result = ContentRootResolver.Resolve(
            currentDirectory: Path.GetTempPath(),
            baseDirectory: buildOutputDirectory);

        result.Should().Be(buildOutputDirectory);
    }
}
