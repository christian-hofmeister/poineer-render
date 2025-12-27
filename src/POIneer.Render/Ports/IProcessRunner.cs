using System.Diagnostics;

namespace POIneer.Render.Abstractions.InfrastructureAbstractions
{
    public interface IProcessRunner
    {
        Task<ProcessResult> RunAsync(ProcessStartInfo startInfo, CancellationToken ct = default);
    }
}