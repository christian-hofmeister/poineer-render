
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using POIneer.Render.Infrastructure.FileSystem;

namespace POIneer.Render.TestHelpers;

public static class TestTemporaryDirectories
{
    public static TemporaryDirectory Create(
        string purpose,
        bool keepOnDispose = false,
        ILogger<TemporaryDirectory>? logger = null)
    {
        if (string.IsNullOrWhiteSpace(purpose))
        {
            throw new ArgumentException("Purpose must be a non-empty string.", nameof(purpose));
        }

        logger ??= NullLogger<TemporaryDirectory>.Instance;

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