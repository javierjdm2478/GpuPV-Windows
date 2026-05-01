using HyperVGpuShareManager.Core.Models;

namespace HyperVGpuShareManager.Core.Services;

public interface IPowerShellService
{
    Task<PowerShellExecutionResult> RunAsync(PowerShellRequest request, CancellationToken cancellationToken = default);
}
