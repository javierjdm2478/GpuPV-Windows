using System.Text.Json;
using HyperVGpuShareManager.Core.Models;
using HyperVGpuShareManager.Core.Utilities;

namespace HyperVGpuShareManager.Core.Services;

public sealed class GpuDetectionService : IGpuDetectionService
{
    private readonly IPowerShellService _powerShell;
    private readonly ILoggingService _logger;

    public GpuDetectionService(IPowerShellService powerShell, ILoggingService logger)
    {
        _powerShell = powerShell;
        _logger = logger;
    }

    public async Task<IReadOnlyList<GpuInfo>> DetectGpusAsync(CancellationToken cancellationToken = default)
    {
        var gpuResult = await _powerShell.RunAsync(new PowerShellRequest
        {
            Description = "Detect host GPUs",
            Timeout = TimeSpan.FromSeconds(45),
            Script = """
$items = @(Get-CimInstance Win32_VideoController | ForEach-Object {
    $pnpStatus = $_.Status
    if ($_.PNPDeviceID -and (Get-Command Get-PnpDevice -ErrorAction SilentlyContinue)) {
        $pnp = Get-PnpDevice -InstanceId $_.PNPDeviceID -ErrorAction SilentlyContinue
        if ($null -ne $pnp) { $pnpStatus = $pnp.Status }
    }

    [pscustomobject]@{
        Id = [string]$_.DeviceID
        Name = [string]$_.Name
        AdapterCompatibility = [string]$_.AdapterCompatibility
        DriverVersion = [string]$_.DriverVersion
        DriverDate = if ($_.DriverDate) { [Management.ManagementDateTimeConverter]::ToDateTime($_.DriverDate).ToString("yyyy-MM-dd") } else { "" }
        DeviceInstancePath = [string]$_.PNPDeviceID
        PnpStatus = [string]$pnpStatus
        VideoProcessor = [string]$_.VideoProcessor
        AdapterRamBytes = [UInt64]($_.AdapterRAM)
    }
})
$items | ConvertTo-Json -Depth 4
"""
        }, cancellationToken);

        if (!gpuResult.IsSuccess)
        {
            return Array.Empty<GpuInfo>();
        }

        var gpus = PowerShellJson.DeserializeArray<GpuInfo>(gpuResult.StandardOutput)
            .Select(gpu =>
            {
                gpu.Vendor = DetectVendor(gpu.Name, gpu.AdapterCompatibility, gpu.VideoProcessor);
                gpu.Warning = string.IsNullOrWhiteSpace(gpu.DeviceInstancePath)
                    ? "No se pudo leer el Device Instance Path desde WMI/CIM."
                    : string.Empty;
                return gpu;
            })
            .ToList();

        var partitionable = await GetPartitionableGpusAsync(cancellationToken);
        MarkPartitionableGpus(gpus, partitionable);
        return gpus;
    }

    public async Task<IReadOnlyList<PartitionableGpuInfo>> GetPartitionableGpusAsync(CancellationToken cancellationToken = default)
    {
        var result = await _powerShell.RunAsync(new PowerShellRequest
        {
            Description = "Get partitionable host GPUs",
            Timeout = TimeSpan.FromSeconds(45),
            Script = """
if (-not (Get-Command Get-VMHostPartitionableGpu -ErrorAction SilentlyContinue)) {
    @() | ConvertTo-Json
    return
}

$items = @(Get-VMHostPartitionableGpu | ForEach-Object {
    $validCounts = ""
    if ($_.PSObject.Properties.Name -contains "ValidPartitionCounts" -and $null -ne $_.ValidPartitionCounts) {
        $validCounts = ($_.ValidPartitionCounts -join ", ")
    }
    $partitionCount = if ($_.PSObject.Properties.Name -contains "PartitionCount") { [string]$_.PartitionCount } else { "" }
    $totalVram = if ($_.PSObject.Properties.Name -contains "TotalVRAM") { [string]$_.TotalVRAM } else { "" }
    $availableVram = if ($_.PSObject.Properties.Name -contains "AvailableVRAM") { [string]$_.AvailableVRAM } else { "" }

    [pscustomobject]@{
        Name = [string]$_.Name
        ValidPartitionCounts = $validCounts
        PartitionCount = $partitionCount
        TotalVRAM = $totalVram
        AvailableVRAM = $availableVram
        Details = ($_ | Format-List * | Out-String).Trim()
    }
})
$items | ConvertTo-Json -Depth 5
"""
        }, cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.Warning("Get-VMHostPartitionableGpu failed or is unavailable. GPU-P support cannot be confirmed.");
            return Array.Empty<PartitionableGpuInfo>();
        }

        return PowerShellJson.DeserializeArray<PartitionableGpuInfo>(result.StandardOutput);
    }

    private static void MarkPartitionableGpus(IReadOnlyList<GpuInfo> gpus, IReadOnlyList<PartitionableGpuInfo> partitionableGpus)
    {
        foreach (var gpu in gpus)
        {
            var devicePath = StringNormalizer.NormalizeDevicePath(gpu.DeviceInstancePath);
            var match = partitionableGpus.FirstOrDefault(item =>
            {
                var name = StringNormalizer.NormalizeDevicePath(item.Name);
                return !string.IsNullOrWhiteSpace(devicePath) &&
                       (name.Contains(devicePath, StringComparison.OrdinalIgnoreCase) ||
                        devicePath.Contains(name, StringComparison.OrdinalIgnoreCase));
            });

            if (match is not null)
            {
                gpu.IsPartitionable = true;
                gpu.PartitionableGpuName = match.Name;
                gpu.PartitionableDetails = match.Details;
            }
            else if (partitionableGpus.Count > 0 && gpus.Count == 1)
            {
                var fallback = partitionableGpus[0];
                gpu.IsPartitionable = true;
                gpu.PartitionableGpuName = fallback.Name;
                gpu.PartitionableDetails = fallback.Details;
                gpu.Warning = "La GPU aparece como particionable, pero el nombre del driver no coincide exactamente con WMI.";
            }
            else
            {
                gpu.IsPartitionable = false;
                gpu.Warning = "Esta GPU no aparece en Get-VMHostPartitionableGpu. No todos los drivers o modelos soportan GPU Partitioning.";
            }
        }
    }

    private static GpuVendor DetectVendor(params string[] values)
    {
        var text = string.Join(' ', values).ToUpperInvariant();
        if (text.Contains("NVIDIA", StringComparison.OrdinalIgnoreCase))
        {
            return GpuVendor.Nvidia;
        }

        if (text.Contains("AMD", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("RADEON", StringComparison.OrdinalIgnoreCase) ||
            text.Contains("ADVANCED MICRO DEVICES", StringComparison.OrdinalIgnoreCase))
        {
            return GpuVendor.Amd;
        }

        if (text.Contains("INTEL", StringComparison.OrdinalIgnoreCase))
        {
            return GpuVendor.Intel;
        }

        return GpuVendor.Unknown;
    }
}
