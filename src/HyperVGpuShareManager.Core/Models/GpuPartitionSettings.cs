namespace HyperVGpuShareManager.Core.Models;

public sealed class GpuPartitionSettings
{
    public ulong MinPartitionVRAM { get; set; }
    public ulong MaxPartitionVRAM { get; set; }
    public ulong OptimalPartitionVRAM { get; set; }
    public ulong MinPartitionEncode { get; set; }
    public ulong MaxPartitionEncode { get; set; }
    public ulong OptimalPartitionEncode { get; set; }
    public ulong MinPartitionDecode { get; set; }
    public ulong MaxPartitionDecode { get; set; }
    public ulong OptimalPartitionDecode { get; set; }
    public ulong MinPartitionCompute { get; set; }
    public ulong MaxPartitionCompute { get; set; }
    public ulong OptimalPartitionCompute { get; set; }

    public static GpuPartitionSettings CreateRecommended() => new()
    {
        MinPartitionVRAM = 128UL * 1024UL * 1024UL,
        OptimalPartitionVRAM = 1024UL * 1024UL * 1024UL,
        MaxPartitionVRAM = 2UL * 1024UL * 1024UL * 1024UL,
        MinPartitionEncode = 100_000_000UL,
        OptimalPartitionEncode = 100_000_000UL,
        MaxPartitionEncode = 100_000_000UL,
        MinPartitionDecode = 100_000_000UL,
        OptimalPartitionDecode = 100_000_000UL,
        MaxPartitionDecode = 100_000_000UL,
        MinPartitionCompute = 100_000_000UL,
        OptimalPartitionCompute = 100_000_000UL,
        MaxPartitionCompute = 100_000_000UL
    };

    public GpuPartitionSettings Clone() => new()
    {
        MinPartitionVRAM = MinPartitionVRAM,
        MaxPartitionVRAM = MaxPartitionVRAM,
        OptimalPartitionVRAM = OptimalPartitionVRAM,
        MinPartitionEncode = MinPartitionEncode,
        MaxPartitionEncode = MaxPartitionEncode,
        OptimalPartitionEncode = OptimalPartitionEncode,
        MinPartitionDecode = MinPartitionDecode,
        MaxPartitionDecode = MaxPartitionDecode,
        OptimalPartitionDecode = OptimalPartitionDecode,
        MinPartitionCompute = MinPartitionCompute,
        MaxPartitionCompute = MaxPartitionCompute,
        OptimalPartitionCompute = OptimalPartitionCompute
    };
}
