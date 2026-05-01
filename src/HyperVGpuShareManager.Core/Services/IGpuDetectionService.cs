using HyperVGpuShareManager.Core.Models;

namespace HyperVGpuShareManager.Core.Services;

public interface IGpuDetectionService
{
    Task<IReadOnlyList<GpuInfo>> DetectGpusAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PartitionableGpuInfo>> GetPartitionableGpusAsync(CancellationToken cancellationToken = default);
}
