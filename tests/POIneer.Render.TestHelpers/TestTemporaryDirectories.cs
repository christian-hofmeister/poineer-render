
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using POIneer.Render.Infrastructure.FileSystem;

namespace POIneer.Render.TestHelpers;

public static class TestTemporaryDirectories
{
    public static TemporaryDirectory Create(
        string purpose,
        bool keepOnDispose = false)
    {
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddConsole();
        });

        var logger = loggerFactory.CreateLogger<TemporaryDirectory>();

        var options = Options.Create(new TempOptions
        {
            RootFolderName = "poineer-tests",
            KeepOnDispose = keepOnDispose
        });

        ITemporaryDirectoryFactory factory =
            new TemporaryDirectoryFactory(logger, options);

        var tempDir = factory.Create(purpose);

        return tempDir;

    }
}