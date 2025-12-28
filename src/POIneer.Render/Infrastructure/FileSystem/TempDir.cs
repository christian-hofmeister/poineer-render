public sealed class TempDir : IAsyncDisposable
{
    public string Path { get; }
    private readonly bool _keep;

    private TempDir(string path, bool keep)
    {
        Path = path;
        _keep = keep;
    }

    public static TempDir Create(string prefix, bool keep = false)
    {
        var path = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(),
            prefix + Guid.NewGuid().ToString("N"));

        Directory.CreateDirectory(path);
        return new TempDir(path, keep);
    }

    public ValueTask DisposeAsync()
    {
        if (_keep) return ValueTask.CompletedTask;

        TryDeleteDirectory(Path);
        return ValueTask.CompletedTask;
    }

    private static void TryDeleteDirectory(string dir)
    {
        try
        {
            if (!Directory.Exists(dir)) return;

            // Make files writable (Windows edge case)
            foreach (var file in Directory.EnumerateFiles(dir, "*", SearchOption.AllDirectories))
            {
                try { File.SetAttributes(file, FileAttributes.Normal); }
                catch { /* ignore */ }
            }

            Directory.Delete(dir, recursive: true);
        }
        catch
        {
            // Optional: log, but don't fail tests because cleanup failed
        }
    }
}