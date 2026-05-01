using HyperVGpuShareManager.Core.Models;

namespace HyperVGpuShareManager.Core.Services;

public interface IVmCreationService
{
    Task<IReadOnlyList<VirtualSwitchInfo>> GetVirtualSwitchesAsync(CancellationToken cancellationToken = default);
    Task<VmInfo?> CreateWindowsVmAsync(VmCreationRequest request, CancellationToken cancellationToken = default);
}
