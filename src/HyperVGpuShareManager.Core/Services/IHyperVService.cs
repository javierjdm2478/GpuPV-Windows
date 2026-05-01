using HyperVGpuShareManager.Core.Models;

namespace HyperVGpuShareManager.Core.Services;

public interface IHyperVService
{
    Task<SystemStatus> GetSystemStatusAsync(bool isAdministrator, CancellationToken cancellationToken = default);
    Task<bool> AreHyperVCmdletsAvailableAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<VmInfo>> GetVirtualMachinesAsync(CancellationToken cancellationToken = default);
    Task StopVmAsync(string vmName, bool forceTurnOff, CancellationToken cancellationToken = default);
    Task ApplyGpuPartitionAsync(string vmName, GpuInfo gpu, GpuPartitionSettings settings, bool createCheckpoint, CancellationToken cancellationToken = default);
    Task RemoveGpuPartitionAsync(string vmName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ValidationResult>> ValidateGpuPartitionAsync(string vmName, bool tryStartVm, CancellationToken cancellationToken = default);
    Task StartVmAsync(string vmName, CancellationToken cancellationToken = default);
    void OpenHyperVManager();
    void OpenVmConnect(string vmName);
}
