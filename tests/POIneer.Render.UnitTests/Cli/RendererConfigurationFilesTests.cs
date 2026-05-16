using System.Text.Json;
using FluentAssertions;
using Xunit;

namespace POIneer.Render.UnitTests.Cli;

public sealed class RendererConfigurationFilesTests
{
    [Theory]
    [InlineData("appsettings.json", "Cli/config/regions.production.json")]
    [InlineData("appsettings.Development.json", "Cli/config/regions.local.json")]
    [InlineData("appsettings.Production.json", "Cli/config/regions.production.json")]
    public void RendererConfiguration_DefaultsToBerlinAndExistingRegionsFile(
        string settingsFileName,
        string expectedRegionsJson)
    {
        var repositoryRoot = FindRepositoryRoot();
        var settingsPath = Path.Combine(repositoryRoot, "src", "POIneer.Render", settingsFileName);

        using var document = JsonDocument.Parse(File.ReadAllText(settingsPath));
        var renderer = document.RootElement.GetProperty("Renderer");

        renderer.GetProperty("OnlyRegionId").GetString().Should().Be("berlin");

        var configuredRegionsPath = renderer.GetProperty("RegionsJson").GetString();

        configuredRegionsPath.Should().NotBeNull();
        configuredRegionsPath
            .Replace('\\', '/')
            .Should()
            .EndWith(expectedRegionsJson);

        if (!Path.IsPathRooted(configuredRegionsPath!))
        {
            var resolvedRegionsPath = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(settingsPath)!,
                configuredRegionsPath!));

            File.Exists(resolvedRegionsPath).Should().BeTrue();
        }
    }

    private static string FindRepositoryRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (File.Exists(Path.Combine(current.FullName, "POIneerRender.sln")))
                return current.FullName;

            current = current.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate repository root.");
    }
}
