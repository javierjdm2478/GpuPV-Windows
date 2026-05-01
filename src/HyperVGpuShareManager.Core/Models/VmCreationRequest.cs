namespace HyperVGpuShareManager.Core.Models;

public sealed class VmCreationRequest
{
    public string Name { get; set; } = "Win10-GPU-P";
    public string StoragePath { get; set; } = string.Empty;
    public string IsoPath { get; set; } = string.Empty;
    public int StartupMemoryMB { get; set; } = 8192;
    public int ProcessorCount { get; set; } = 4;
    public int VhdSizeGB { get; set; } = 80;
    public string SwitchName { get; set; } = string.Empty;
    public int Generation { get; set; } = 2;
}
