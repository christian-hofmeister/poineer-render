using System.Diagnostics;


namespace POIneer.Render.Infrastructure.Process
{
    public interface IProcessRunner
    {
        Task<ProcessResult> RunAsync(ProcessStartInfo startInfo, CancellationToken ct = default);
    }
}