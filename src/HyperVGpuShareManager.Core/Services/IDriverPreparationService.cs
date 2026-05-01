using HyperVGpuShareManager.Core.Models;

namespace HyperVGpuShareManager.Core.Services;

public interface IDriverPreparationService
{
    Task<DriverPackageInfo> DetectHostDisplayDriverAsync(GpuInfo gpu, CancellationToken cancellationToken = default);
    Task<DriverPreparationResult> PrepareDriversOfflineAsync(VmInfo vm, GpuInfo gpu, CancellationToken cancellationToken = default);
    Task<DriverPreparationResult> PrepareDriversWithPowerShellDirectAsync(VmInfo vm, GpuInfo gpu, string guestUserName, string guestPassword, CancellationToken cancellationToken = default);
    string GetManualInstructions(GpuInfo gpu);
}
