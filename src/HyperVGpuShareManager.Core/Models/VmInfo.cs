namespace HyperVGpuShareManager.Core.Models;

public sealed class VmInfo
{
    public string Name { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public int Generation { get; set; }
    public long MemoryAssignedMB { get; set; }
    public int ProcessorCount { get; set; }
    public string Path { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public IReadOnlyList<string> DiskPaths { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> NetworkAdapters { get; set; } = Array.Empty<string>();
    public bool HasGpuPartitionAdapter { get; set; }

    public bool IsOff =>
        State.Equals("Off", StringComparison.OrdinalIgnoreCase) ||
        State.Equals("Apagado", StringComparison.OrdinalIgnoreCase);
}
