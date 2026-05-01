namespace HyperVGpuShareManager.Core.Models;

public sealed class GpuInfo
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public GpuVendor Vendor { get; set; } = GpuVendor.Unknown;
    public string VendorLabel => Vendor switch
    {
        GpuVendor.Nvidia => "NVIDIA",
        GpuVendor.Amd => "AMD Radeon",
        GpuVendor.Intel => Name.Contains("Arc", StringComparison.OrdinalIgnoreCase) ? "Intel Arc" : "Intel Graphics",
        _ => "GPU"
    };

    public string AdapterCompatibility { get; set; } = string.Empty;
    public string DriverVersion { get; set; } = string.Empty;
    public string DriverDate { get; set; } = string.Empty;
    public string DeviceInstancePath { get; set; } = string.Empty;
    public string PnpStatus { get; set; } = string.Empty;
    public string VideoProcessor { get; set; } = string.Empty;
    public ulong AdapterRamBytes { get; set; }
    public bool IsPartitionable { get; set; }
    public string PartitionableGpuName { get; set; } = string.Empty;
    public string PartitionableDetails { get; set; } = string.Empty;
    public string Warning { get; set; } = string.Empty;
}
