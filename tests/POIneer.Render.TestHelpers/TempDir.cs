using System;
using System.IO;
using Microsoft.Extensions.Logging;
using System.Collections.Generic;

namespace POIneer.Render.TestHelpers;
public sealed class TempDir : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "poineer-test-" + Guid.NewGuid());
    public TempDir()
    {
        Directory.CreateDirectory(Path);
    }
    public string WriteText(string relative, string content)
    {
        var full = System.IO.Path.Combine(Path, relative);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
        return full;
    }
    public void Dispose()
    {
        try { Directory.Delete(Path, true); } catch { /* ignore */ }
    }
}
