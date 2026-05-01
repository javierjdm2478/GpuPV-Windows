using System.Globalization;
using HyperVGpuShareManager.Core.Models;
using HyperVGpuShareManager.Core.Utilities;
using HyperVGpuShareManager.Core.Validation;

namespace HyperVGpuShareManager.Core.Services;

public sealed class VmCreationService : IVmCreationService
{
    private readonly IPowerShellService _powerShell;
    private readonly InputValidationService _validation;

    public VmCreationService(IPowerShellService powerShell, InputValidationService validation)
    {
        _powerShell = powerShell;
        _validation = validation;
    }

    public async Task<IReadOnlyList<VirtualSwitchInfo>> GetVirtualSwitchesAsync(CancellationToken cancellationToken = default)
    {
        var result = await _powerShell.RunAsync(new PowerShellRequest
        {
            Description = "List Hyper-V virtual switches",
            Timeout = TimeSpan.FromSeconds(30),
            Script = """
if (-not (Get-Command Get-VMSwitch -ErrorAction SilentlyContinue)) {
    @() | ConvertTo-Json
    return
}
@(Get-VMSwitch | Sort-Object Name | ForEach-Object {
    [pscustomobject]@{
        Name = [string]$_.Name
        SwitchType = [string]$_.SwitchType
    }
}) | ConvertTo-Json -Depth 4
"""
        }, cancellationToken);

        return result.IsSuccess
            ? PowerShellJson.DeserializeArray<VirtualSwitchInfo>(result.StandardOutput)
            : Array.Empty<VirtualSwitchInfo>();
    }

    public async Task<VmInfo?> CreateWindowsVmAsync(VmCreationRequest request, CancellationToken cancellationToken = default)
    {
        var nameValidation = _validation.ValidateVmName(request.Name);
        if (!nameValidation.IsSuccess)
        {
            throw new ArgumentException(nameValidation.Message, nameof(request));
        }

        var pathValidation = _validation.ValidateDirectoryPath(request.StoragePath, "Ruta de almacenamiento");
        if (!pathValidation.IsSuccess)
        {
            throw new ArgumentException(pathValidation.Message, nameof(request));
        }

        if (!string.IsNullOrWhiteSpace(request.IsoPath) && !File.Exists(request.IsoPath))
        {
            throw new FileNotFoundException("No se ha encontrado la ISO seleccionada.", request.IsoPath);
        }

        var parameters = new Dictionary<string, string?>
        {
            ["VmName"] = request.Name,
            ["StoragePath"] = request.StoragePath,
            ["IsoPath"] = request.IsoPath,
            ["StartupMemoryMB"] = request.StartupMemoryMB.ToString(CultureInfo.InvariantCulture),
            ["ProcessorCount"] = request.ProcessorCount.ToString(CultureInfo.InvariantCulture),
            ["VhdSizeGB"] = request.VhdSizeGB.ToString(CultureInfo.InvariantCulture),
            ["SwitchName"] = request.SwitchName,
            ["Generation"] = request.Generation.ToString(CultureInfo.InvariantCulture)
        };

        var result = await _powerShell.RunAsync(new PowerShellRequest
        {
            Description = "Create Windows VM",
            Timeout = TimeSpan.FromMinutes(10),
            Parameters = parameters,
            Script = """
if (Get-VM -Name $VmName -ErrorAction SilentlyContinue) {
    throw "Ya existe una VM con el nombre '$VmName'."
}

$vmRoot = Join-Path -Path $StoragePath -ChildPath $VmName
New-Item -ItemType Directory -Path $vmRoot -Force | Out-Null
$vhdPath = Join-Path -Path $vmRoot -ChildPath ($VmName + ".vhdx")
$memoryBytes = [Int64]$StartupMemoryMB * 1MB
$vhdBytes = [UInt64]$VhdSizeGB * 1GB

$newVmParams = @{
    Name = $VmName
    Generation = [int]$Generation
    MemoryStartupBytes = $memoryBytes
    Path = $StoragePath
    NewVHDPath = $vhdPath
    NewVHDSizeBytes = $vhdBytes
}
if ($SwitchName) {
    $newVmParams["SwitchName"] = $SwitchName
}

$vm = New-VM @newVmParams -ErrorAction Stop
Set-VMProcessor -VMName $VmName -Count ([int]$ProcessorCount) -ErrorAction Stop
Set-VMMemory -VMName $VmName -DynamicMemoryEnabled $true -MinimumBytes 1GB -StartupBytes $memoryBytes -MaximumBytes ([math]::Max($memoryBytes, 16GB)) -ErrorAction Stop
Set-VM -Name $VmName -CheckpointType Production -AutomaticCheckpointsEnabled $false -ErrorAction Stop

if ($IsoPath) {
    $dvd = Add-VMDvdDrive -VMName $VmName -Path $IsoPath -Passthru -ErrorAction Stop
    if ([int]$Generation -eq 2) {
        Set-VMFirmware -VMName $VmName -FirstBootDevice $dvd -EnableSecureBoot On -SecureBootTemplate "MicrosoftWindows" -ErrorAction Stop
    }
}

$created = Get-VM -Name $VmName -ErrorAction Stop
$createdProcessor = Get-VMProcessor -VMName $VmName -ErrorAction SilentlyContinue
[pscustomobject]@{
    Name = [string]$created.Name
    State = [string]$created.State
    Generation = [int]$created.Generation
    MemoryAssignedMB = [int64]([math]::Round([double]$created.MemoryAssigned / 1MB))
    ProcessorCount = if ($null -ne $createdProcessor) { [int]$createdProcessor.Count } else { 0 }
    Path = [string]$created.Path
    Status = [string]$created.Status
    DiskPaths = @(Get-VMHardDiskDrive -VMName $VmName | Select-Object -ExpandProperty Path)
    NetworkAdapters = @(Get-VMNetworkAdapter -VMName $VmName | ForEach-Object { if ($_.SwitchName) { "$($_.Name) -> $($_.SwitchName)" } else { "$($_.Name) -> sin switch" } })
    HasGpuPartitionAdapter = $false
} | ConvertTo-Json -Depth 5
"""
        }, cancellationToken);

        if (!result.IsSuccess)
        {
            throw new InvalidOperationException(string.IsNullOrWhiteSpace(result.StandardError)
                ? "No se pudo crear la VM."
                : result.StandardError);
        }

        return PowerShellJson.DeserializeObject<VmInfo>(result.StandardOutput);
    }
}
