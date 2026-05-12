using FluentAssertions;
using POIneer.Render.Application.Options;
using Xunit;

namespace POIneer.Render.UnitTests.Application.Options;

public sealed class RendererOptionsValidationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void HasValidDownloadTimeout_ReturnsFalse_WhenTimeoutIsNotPositive(int timeoutSeconds)
    {
        // Arrange
        var options = new RendererOptions
        {
            WorkDir = "work",
            OutDir = "out",
            RegionsJson = "regions.json",
            DownloadTimeoutSeconds = timeoutSeconds
        };

        // Act
        var result = RendererOptionsValidation.HasValidDownloadTimeout(options);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasValidDownloadTimeout_ReturnsTrue_WhenTimeoutIsPositive()
    {
        // Arrange
        var options = new RendererOptions
        {
            WorkDir = "work",
            OutDir = "out",
            RegionsJson = "regions.json",
            DownloadTimeoutSeconds = 1
        };

        // Act
        var result = RendererOptionsValidation.HasValidDownloadTimeout(options);

        // Assert
        result.Should().BeTrue();
    }
}
