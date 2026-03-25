namespace POIneer.Render.Infrastructure.FileSystem;

public sealed class TemporaryDirectory : IDisposable, IAsyncDisposable
{
    public string DirectoryPath { get; }
    private readonly bool _keepOnDispose;

    private TemporaryDirectory(string path, bool keepOnDispose)
    {
        DirectoryPath = path;
        _keepOnDispose = keepOnDispose;
    }

    public static TemporaryDirectory Create(string prefix, bool keepOnDispose = false)
    {
        prefix = CreateSafePrefix(prefix);

        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            prefix + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(path);
        return new TemporaryDirectory(path, keepOnDispose);
    }

    public void Dispose()
    {
        if (_keepOnDispose) return;
        TryDeleteDirectory(DirectoryPath);
    }

    public ValueTask DisposeAsync()
    {
        Dispose();
        return ValueTask.CompletedTask;
    }

    private static void TryDeleteDirectory(string dir)
    {
        try
        {
            if (!Directory.Exists(dir)) return;

            // Fast path: try delete directly first
            try
            {
                Directory.Delete(dir, recursive: true);
                return;
            }
            catch
            {
                // retry after resetting file attributes (Windows edge case)
            }

            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                try { File.SetAttributes(file, FileAttributes.Normal); }
                catch { /* ignore */ }
            }

            Directory.Delete(dir, recursive: true);
        }
        catch
        {
            // Best effort cleanup: never throw from Dispose.
        }
    }

    private static string CreateSafePrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            throw new ArgumentException("Prefix must not be empty.", nameof(prefix));

        foreach (var c in System.IO.Path.GetInvalidFileNameChars())
            prefix = prefix.Replace(c, '_');

        prefix = prefix
            .Replace(System.IO.Path.DirectorySeparatorChar, '_')
            .Replace(System.IO.Path.AltDirectorySeparatorChar, '_');

        const int maxPrefixLength = 40;
        if (prefix.Length > maxPrefixLength)
            prefix = prefix[..maxPrefixLength];

        return prefix;
    }
}
