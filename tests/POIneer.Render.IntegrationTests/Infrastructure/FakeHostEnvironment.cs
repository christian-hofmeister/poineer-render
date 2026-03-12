using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace POIneer.Render.IntegrationTests.Infrastructure;

sealed class FakeHostEnvironment : IHostEnvironment
{
    public string EnvironmentName { get; set; } = "Test";
    public string ApplicationName { get; set; } = "POIneer.Render.Tests";
    public string ContentRootPath { get; set; } = "";
    public IFileProvider ContentRootFileProvider { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
}