using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace POIneer.Render.Infrastructure.FileSystem;

public sealed class TemporaryDirectoryFactory : ITemporaryDirectoryFactory
{
    private readonly ILogger<TemporaryDirectory> _logger;
    private readonly TempOptions _options;

    public TemporaryDirectoryFactory(
        ILogger<TemporaryDirectory> logger,
        IOptions<TempOptions> options)
    {
        _logger = logger;
        _options = options.Value;
    }

    public TemporaryDirectory Create(
        string prefix,
        bool? keepOnDispose = null)
    {
        var path = BuildPath(prefix);

        Directory.CreateDirectory(path);

        return new TemporaryDirectory(
            path,
            _logger,
            keepOnDispose ?? _options.KeepOnDispose);
    }

    private string BuildPath(string? name)
    {
        var root = Path.Combine(
         Path.GetTempPath(),
         _options.RootFolderName);

        Directory.CreateDirectory(root);

        var folder = string.IsNullOrWhiteSpace(name)
            ? Guid.NewGuid().ToString("N")
            : $"{TemporaryDirectoryNameHelper.CreateSafeFolderName(name)}-{Guid.NewGuid():N}";

        return Path.Combine(root, folder);
    }
}