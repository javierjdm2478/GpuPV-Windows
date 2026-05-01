namespace HyperVGpuShareManager.Core.Models;

public sealed class PartitionableGpuInfo
{
    public string Name { get; set; } = string.Empty;
    public string ValidPartitionCounts { get; set; } = string.Empty;
    public string PartitionCount { get; set; } = string.Empty;
    public string TotalVRAM { get; set; } = string.Empty;
    public string AvailableVRAM { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}
