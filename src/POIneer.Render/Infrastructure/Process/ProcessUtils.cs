using System.Diagnostics;

namespace POIneer.Render.Infrastructure.Process;

public static class ProcessUtils
{
    public static bool IsExecutableAvailable(string executable, string arguments = "--version")
    {
        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = executable,
                Arguments = arguments,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using var process = System.Diagnostics.Process.Start(psi);
            if (process is null)
                return false;

            if (!process.WaitForExit(3000))
            {
                try
                {
                    process.Kill(entireProcessTree: true);
                }
                catch
                {
                }

                return false;
            }

            var stdout = process.StandardOutput.ReadToEnd();
            var stderr = process.StandardError.ReadToEnd();

            Console.WriteLine($"ExitCode: {process.ExitCode}");
            Console.WriteLine($"StdOut: {stdout}");
            Console.WriteLine($"StdErr: {stderr}");

            return process.ExitCode == 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
            return false;
        }
    }
}