using System.ComponentModel;
using System.Runtime.InteropServices;
using POIneer.Render.Application.Ports;

namespace POIneer.Render.Infrastructure.FileSystem;

public sealed class DiskSpaceReader : IDiskSpaceReader
{
    public long GetAvailableBytes(string directoryPath)
    {
        var existingPath = Path.TrimEndingDirectorySeparator(Path.GetFullPath(directoryPath));
        while (!IsExistingDirectory(existingPath))
        {
            existingPath = Path.GetDirectoryName(existingPath)
                ?? throw new DirectoryNotFoundException($"No existing directory found for '{directoryPath}'.");
        }

        if (!OperatingSystem.IsWindows())
            return new DriveInfo(existingPath).AvailableFreeSpace;

        // DriveInfo reduces paths to a drive root on Windows. Query the directory to honor mounted volumes.
        if (!GetDiskFreeSpaceEx(Path.EndsInDirectorySeparator(existingPath) ? existingPath : existingPath + '\\',
                out var availableBytes, out _, out _))
        {
            throw new IOException($"Could not read free disk space for '{existingPath}'.",
                new Win32Exception(Marshal.GetLastWin32Error()));
        }

        return checked((long)availableBytes);
    }

    private static bool IsExistingDirectory(string path)
    {
        try
        {
            if (!File.GetAttributes(path).HasFlag(FileAttributes.Directory))
                throw new IOException($"Expected a directory at '{path}'.");

            return true;
        }
        catch (IOException ex) when (ex is FileNotFoundException or DirectoryNotFoundException)
        {
            return false;
        }
    }

    [DllImport("kernel32.dll", EntryPoint = "GetDiskFreeSpaceExW", CharSet = CharSet.Unicode,
        ExactSpelling = true, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetDiskFreeSpaceEx(
        string directoryPath, out ulong availableBytes, out ulong totalBytes, out ulong totalFreeBytes);
}
