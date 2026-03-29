using Microsoft.Extensions.Options;

namespace POIneer.Render.Infrastructure.FileSystem;

public sealed class TemporaryDirectoryFactory : ITemporaryDirectoryFactory
{
    private readonly TempOptions _options;

    public TemporaryDirectoryFactory(IOptions<TempOptions> options)
    {
        _options = options.Value;
    }

    public TemporaryDirectory Create(
        string prefix,
        bool? keepOnDispose = null)
    {
        var rootPath = Path.Combine(Path.GetTempPath(), _options.RootFolderName);

        return TemporaryDirectory.Create(
            prefix: prefix,
            rootPath: rootPath,
            keepOnDispose: keepOnDispose ?? _options.KeepOnDispose);
    }
}