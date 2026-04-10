using POIneer.Render.Infrastructure.Process;
using Xunit;

namespace POIneer.Render.IntegrationTests.Infrastructure.Process;

public sealed class ProcessTests
{
    [Fact]
    public void IsExecutableAvailable_ReturnsTrue_ForDotnet()
    {
        var result = ProcessUtils.IsExecutableAvailable("dotnet");

        Assert.True(result, "Expected dotnet to be available.");
    }


    [Fact]
    public void IsExecutableAvailable_ReturnsFalse_ForNonExistingExecutable()
    {
        var result = ProcessUtils.IsExecutableAvailable("/definitely/not/existing/tool");

        Assert.False(result);
    }
}