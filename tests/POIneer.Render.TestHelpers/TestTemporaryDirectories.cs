
using Microsoft.Extensions.Options;
using POIneer.Render.Infrastructure.FileSystem;

namespace POIneer.Render.TestHelpers;

public static class TestTemporaryDirectories
{
    public static TemporaryDirectory Create(
        string purpose,
        bool keepOnDispose = false)
    {
        var options = Options.Create(new TempOptions
        {
            RootFolderName = "poineer-tests",
            KeepOnDispose = keepOnDispose
        });

        ITemporaryDirectoryFactory factory =
            new TemporaryDirectoryFactory(options);

        return factory.Create(purpose);
    }
}