using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using HyperVGpuShareManager.Core.Models;
using HyperVGpuShareManager.Core.Utilities;

namespace HyperVGpuShareManager.Core.Services;

public sealed class HyperVService : IHyperVService
{
    private readonly IPowerShellService _powerShell;
    private readonly ILoggingService _logger;

    public HyperVService(IPowerShellService powerShell, ILoggingService logger)
    {
        _powerShell = powerShell;
        _logger = logger;
    }

    public async Task<SystemStatus> GetSystemStatusAsync(bool isAdministrator, CancellationToken cancellationToken = default)
    {
        var result = await _powerShell.RunAsync(new PowerShellRequest
        {
            Description = "Read system and Hyper-V status",
            Timeout = TimeSpan.FromSeconds(45),
            Script = """
$os = Get-CimInstance Win32_OperatingSystem
$feature = $null
if (Get-Command Get-WindowsOptionalFeature -ErrorAction SilentlyContinue) {
    $feature = Get-WindowsOptionalFeature -Online -FeatureName Microsoft-Hyper-V-All -ErrorAction SilentlyContinue
}
$processor = @(Get-CimInstance Win32_Processor | Select-Object -First 1)
$hyperVState = if ($null -ne $feature) { [string]$feature.State } else { "Unknown" }
[pscustomobject]@{
    WindowsCaption = [string]$os.Caption
    WindowsVersion = "$($os.Version) build $($os.BuildNumber)"
    HyperVState = $hyperVState
    VirtualizationFirmwareEnabled = if ($processor.Count -gt 0) { [bool]$processor[0].VirtualizationFirmwareEnabled } else { $null }
    VmMonitorModeExtensions = if ($processor.Count -gt 0) { [bool]$processor[0].VMMonitorModeExtensions } else { $null }
} | ConvertTo-Json -Depth 3
"""
        }, cancellationToken);

        var status = new SystemStatus { IsAdministrator = isAdministrator };
        if (result.IsSuccess)
        {
            var dto = PowerShellJson.DeserializeObject<SystemStatusDto>(result.StandardOutput);
            if (dto is not null)
            {
                status.WindowsCaption = string.IsNullOrWhiteSpace(dto.WindowsCaption) ? "No detectado" : dto.WindowsCaption;
                status.WindowsVersion = string.IsNullOrWhiteSpace(dto.WindowsVersion) ? "No detectado" : dto.WindowsVersion;
                status.IsHyperVInstalled = dto.HyperVState.Equals("Enabled", StringComparison.OrdinalIgnoreCase) ||
                                           dto.HyperVState.Equals("Disabled", StringComparison.OrdinalIgnoreCase);
                status.IsHyperVEnabled = dto.HyperVState.Equals("Enabled", StringComparison.OrdinalIgnoreCase);
                status.VirtualizationFirmwareEnabled = dto.VirtualizationFirmwareEnabled;
                status.VmMonitorModeExtensions = dto.VmMonitorModeExtensions;
            }
        }

        status.Checks = BuildStatusChecks(status);
        return status;
    }

    public async Task<bool> AreHyperVCmdletsAvailableAsync(CancellationToken cancellationToken = default)
    {
        var result = await _powerShell.RunAsync(new PowerShellRequest
        {
            Description = "Check Hyper-V PowerShell cmdlets",
            Timeout = TimeSpan.FromSeconds(30),
            Script = """
$required = @(
    "Get-VM",
    "New-VM",
    "Add-VMGpuPartitionAdapter",
    "Get-VMGpuPartitionAdapter",
    "Set-VMGpuPartitionAdapter",
    "Remove-VMGpuPartitionAdapter",
    "Get-VMHostPartitionableGpu"
)
$missing = @($required | Where-Object { -not (Get-Command $_ -ErrorAction SilentlyContinue) })
[pscustomobject]@{
    Available = ($missing.Count -eq 0)
    Missing = ($missing -join ", ")
} | ConvertTo-Json -Depth 3
"""
        }, cancellationToken);

        if (!result.IsSuccess)
        {
            return false;
        }

        using var document = JsonDocument.Parse(result.StandardOutput);
        var available = document.RootElement.TryGetProperty("Available", out var element) && element.GetBoolean();
        if (!available && document.RootElement.TryGetProperty("Missing", out var missing))
        {
            _logger.Warning($"Missing Hyper-V cmdlets: {missing.GetString()}");
        }

        return available;
    }

    public async Task<IReadOnlyList<VmInfo>> GetVirtualMachinesAsync(CancellationToken cancellationToken = default)
    {
        var result = await _powerShell.RunAsync(new PowerShellRequest
        {
            Description = "List Hyper-V virtual machines",
            Timeout = TimeSpan.FromSeconds(45),
            Script = """
if (-not (Get-Command Get-VM -ErrorAction SilentlyContinue)) {
    @() | ConvertTo-Json
    return
}

$hasGpuAdapterCmdlet = [bool](Get-Command Get-VMGpuPartitionAdapter -ErrorAction SilentlyContinue)
$items = @(Get-VM | Sort-Object Name | ForEach-Object {
    $vm = $_
    $processor = Get-VMProcessor -VMName $vm.Name -ErrorAction SilentlyContinue
    $disks = @(Get-VMHardDiskDrive -VMName $vm.Name -ErrorAction SilentlyContinue | Where-Object { $_.Path } | Select-Object -ExpandProperty Path)
    $nics = @(Get-VMNetworkAdapter -VMName $vm.Name -ErrorAction SilentlyContinue | ForEach-Object {
        if ($_.SwitchName) { "$($_.Name) -> $($_.SwitchName)" } else { "$($_.Name) -> sin switch" }
    })
    $hasGpu = $false
    if ($hasGpuAdapterCmdlet) {
        $adapter = Get-VMGpuPartitionAdapter -VMName $vm.Name -ErrorAction SilentlyContinue
        $hasGpu = $null -ne $adapter
    }
    [pscustomobject]@{
        Name = [string]$vm.Name
        State = [string]$vm.State
        Generation = [int]$vm.Generation
        MemoryAssignedMB = [int64]([math]::Round([double]$vm.MemoryAssigned / 1MB))
        ProcessorCount = if ($null -ne $processor) { [int]$processor.Count } else { 0 }
        Path = [string]$vm.Path
        Status = [string]$vm.Status
        DiskPaths = $disks
        NetworkAdapters = $nics
        HasGpuPartitionAdapter = [bool]$hasGpu
    }
})
$items | ConvertTo-Json -Depth 5
"""
        }, cancellationToken);

        return result.IsSuccess
            ? PowerShellJson.DeserializeArray<VmInfo>(result.StandardOutput)
            : Array.Empty<VmInfo>();
    }

    public async Task StopVmAsync(string vmName, bool forceTurnOff, CancellationToken cancellationToken = default)
    {
        var result = await _powerShell.RunAsync(new PowerShellRequest
        {
            Description = forceTurnOff ? "Force stop VM" : "Gracefully stop VM",
            Timeout = TimeSpan.FromMinutes(5),
            Parameters = new Dictionary<string, string?> { ["VmName"] = vmName, ["ForceTurnOff"] = forceTurnOff.ToString(CultureInfo.InvariantCulture) },
            Script = """
$vm = Get-VM -Name $VmName -ErrorAction Stop
if ($vm.State -eq "Off") { return }
if ([bool]::Parse($ForceTurnOff)) {
    Stop-VM -Name $VmName -TurnOff -Force -ErrorAction Stop
} else {
    Stop-VM -Name $VmName -Shutdown -ErrorAction Stop
}
"""
        }, cancellationToken);

        EnsureSuccess(result, "No se pudo apagar la VM.");
    }

    public async Task ApplyGpuPartitionAsync(string vmName, GpuInfo gpu, GpuPartitionSettings settings, bool createCheckpoint, CancellationToken cancellationToken = default)
    {
        var instancePath = string.IsNullOrWhiteSpace(gpu.PartitionableGpuName)
            ? gpu.DeviceInstancePath
            : gpu.PartitionableGpuName;

        var parameters = new Dictionary<string, string?>
        {
            ["VmName"] = vmName,
            ["InstancePath"] = instancePath,
            ["CreateCheckpoint"] = createCheckpoint.ToString(CultureInfo.InvariantCulture),
            ["MinPartitionVRAM"] = settings.MinPartitionVRAM.ToString(CultureInfo.InvariantCulture),
            ["MaxPartitionVRAM"] = settings.MaxPartitionVRAM.ToString(CultureInfo.InvariantCulture),
            ["OptimalPartitionVRAM"] = settings.OptimalPartitionVRAM.ToString(CultureInfo.InvariantCulture),
            ["MinPartitionEncode"] = settings.MinPartitionEncode.ToString(CultureInfo.InvariantCulture),
            ["MaxPartitionEncode"] = settings.MaxPartitionEncode.ToString(CultureInfo.InvariantCulture),
            ["OptimalPartitionEncode"] = settings.OptimalPartitionEncode.ToString(CultureInfo.InvariantCulture),
            ["MinPartitionDecode"] = settings.MinPartitionDecode.ToString(CultureInfo.InvariantCulture),
            ["MaxPartitionDecode"] = settings.MaxPartitionDecode.ToString(CultureInfo.InvariantCulture),
            ["OptimalPartitionDecode"] = settings.OptimalPartitionDecode.ToString(CultureInfo.InvariantCulture),
            ["MinPartitionCompute"] = settings.MinPartitionCompute.ToString(CultureInfo.InvariantCulture),
            ["MaxPartitionCompute"] = settings.MaxPartitionCompute.ToString(CultureInfo.InvariantCulture),
            ["OptimalPartitionCompute"] = settings.OptimalPartitionCompute.ToString(CultureInfo.InvariantCulture)
        };

        var result = await _powerShell.RunAsync(new PowerShellRequest
        {
            Description = "Apply GPU-P to VM",
            Timeout = TimeSpan.FromMinutes(5),
            Parameters = parameters,
            Script = """
$vm = Get-VM -Name $VmName -ErrorAction Stop
if ($vm.State -ne "Off") {
    throw "La VM '$VmName' debe estar apagada antes de aplicar GPU-P. Estado actual: $($vm.State)"
}

$partitionableGpu = $null
if ($InstancePath) {
    $partitionableGpu = Get-VMHostPartitionableGpu -Name $InstancePath -ErrorAction SilentlyContinue
}
if ($null -eq $partitionableGpu) {
    $partitionableGpu = Get-VMHostPartitionableGpu | Where-Object { $_.Name -eq $InstancePath } | Select-Object -First 1
}
if ($null -eq $partitionableGpu) {
    throw "La GPU seleccionada no aparece como particionable en Get-VMHostPartitionableGpu."
}

if ([bool]::Parse($CreateCheckpoint)) {
    $snapshotName = "Before GPU-P " + (Get-Date -Format "yyyyMMdd-HHmmss")
    Checkpoint-VM -Name $VmName -SnapshotName $snapshotName -ErrorAction Stop
}

$existingAdapter = Get-VMGpuPartitionAdapter -VMName $VmName -ErrorAction SilentlyContinue
if ($null -eq $existingAdapter) {
    $addCommand = Get-Command Add-VMGpuPartitionAdapter -ErrorAction Stop
    $addParams = @{ VMName = $VmName }
    if ($InstancePath -and $addCommand.Parameters.ContainsKey("InstancePath")) {
        $addParams["InstancePath"] = $InstancePath
    }
    Add-VMGpuPartitionAdapter @addParams -ErrorAction Stop
}

Set-VMGpuPartitionAdapter `
    -VMName $VmName `
    -MinPartitionVRAM ([UInt64]$MinPartitionVRAM) `
    -MaxPartitionVRAM ([UInt64]$MaxPartitionVRAM) `
    -OptimalPartitionVRAM ([UInt64]$OptimalPartitionVRAM) `
    -MinPartitionEncode ([UInt64]$MinPartitionEncode) `
    -MaxPartitionEncode ([UInt64]$MaxPartitionEncode) `
    -OptimalPartitionEncode ([UInt64]$OptimalPartitionEncode) `
    -MinPartitionDecode ([UInt64]$MinPartitionDecode) `
    -MaxPartitionDecode ([UInt64]$MaxPartitionDecode) `
    -OptimalPartitionDecode ([UInt64]$OptimalPartitionDecode) `
    -MinPartitionCompute ([UInt64]$MinPartitionCompute) `
    -MaxPartitionCompute ([UInt64]$MaxPartitionCompute) `
    -OptimalPartitionCompute ([UInt64]$OptimalPartitionCompute) `
    -ErrorAction Stop

Set-VM -Name $VmName -GuestControlledCacheTypes $true -LowMemoryMappedIoSpace 1GB -HighMemoryMappedIoSpace 32GB -ErrorAction Stop
Get-VMGpuPartitionAdapter -VMName $VmName | ConvertTo-Json -Depth 5
"""
        }, cancellationToken);

        EnsureSuccess(result, "No se pudo aplicar GPU-P a la VM.");
    }

    public async Task RemoveGpuPartitionAsync(string vmName, CancellationToken cancellationToken = default)
    {
        var result = await _powerShell.RunAsync(new PowerShellRequest
        {
            Description = "Remove GPU-P from VM",
            Timeout = TimeSpan.FromMinutes(2),
            Parameters = new Dictionary<string, string?> { ["VmName"] = vmName },
            Script = """
$adapter = Get-VMGpuPartitionAdapter -VMName $VmName -ErrorAction SilentlyContinue
if ($null -ne $adapter) {
    Remove-VMGpuPartitionAdapter -VMName $VmName -ErrorAction Stop
}
"""
        }, cancellationToken);

        EnsureSuccess(result, "No se pudo quitar el adaptador GPU-P.");
    }

    public async Task<IReadOnlyList<ValidationResult>> ValidateGpuPartitionAsync(string vmName, bool tryStartVm, CancellationToken cancellationToken = default)
    {
        var result = await _powerShell.RunAsync(new PowerShellRequest
        {
            Description = "Validate VM GPU-P configuration",
            Timeout = TimeSpan.FromMinutes(3),
            Parameters = new Dictionary<string, string?> { ["VmName"] = vmName, ["TryStartVm"] = tryStartVm.ToString(CultureInfo.InvariantCulture) },
            Script = """
$results = New-Object System.Collections.Generic.List[object]
$vm = Get-VM -Name $VmName -ErrorAction Stop
$adapter = Get-VMGpuPartitionAdapter -VMName $VmName -ErrorAction SilentlyContinue
if ($null -ne $adapter) {
    $results.Add([pscustomobject]@{ Severity = "Ok"; Title = "Adaptador GPU-P presente"; Message = "Get-VMGpuPartitionAdapter devuelve una configuración para la VM." })
} else {
    $results.Add([pscustomobject]@{ Severity = "Error"; Title = "Sin adaptador GPU-P"; Message = "La VM no tiene adaptador GPU Partition Adapter." })
}

if ([bool]::Parse($TryStartVm)) {
    if ($vm.State -eq "Off") {
        Start-VM -Name $VmName -ErrorAction Stop
        Start-Sleep -Seconds 3
        $vm = Get-VM -Name $VmName -ErrorAction Stop
    }
    if ($vm.State -eq "Running") {
        $results.Add([pscustomobject]@{ Severity = "Ok"; Title = "VM arrancada"; Message = "Hyper-V informa que la VM está en ejecución." })
    } else {
        $results.Add([pscustomobject]@{ Severity = "Warning"; Title = "Arranque no confirmado"; Message = "Estado actual de la VM: $($vm.State)." })
    }
} else {
    $results.Add([pscustomobject]@{ Severity = "Warning"; Title = "Arranque no probado"; Message = "Activa la opción de probar arranque o usa el botón Iniciar VM." })
}

$results | ConvertTo-Json -Depth 4
"""
        }, cancellationToken);

        if (!result.IsSuccess)
        {
            return new[] { ValidationResult.Error("Validación fallida", ExtractReadableError(result)) };
        }

        return PowerShellJson.DeserializeArray<ValidationDto>(result.StandardOutput)
            .Select(dto => new ValidationResult
            {
                Severity = Enum.TryParse<CheckSeverity>(dto.Severity, ignoreCase: true, out var severity) ? severity : CheckSeverity.Warning,
                Title = dto.Title,
                Message = dto.Message
            })
            .ToList();
    }

    public async Task StartVmAsync(string vmName, CancellationToken cancellationToken = default)
    {
        var result = await _powerShell.RunAsync(new PowerShellRequest
        {
            Description = "Start VM",
            Timeout = TimeSpan.FromMinutes(2),
            Parameters = new Dictionary<string, string?> { ["VmName"] = vmName },
            Script = """
$vm = Get-VM -Name $VmName -ErrorAction Stop
if ($vm.State -ne "Running") {
    Start-VM -Name $VmName -ErrorAction Stop
}
"""
        }, cancellationToken);

        EnsureSuccess(result, "No se pudo iniciar la VM.");
    }

    public void OpenHyperVManager()
    {
        Process.Start(new ProcessStartInfo("virtmgmt.msc") { UseShellExecute = true });
    }

    public void OpenVmConnect(string vmName)
    {
        if (string.IsNullOrWhiteSpace(vmName))
        {
            throw new ArgumentException("VM name is required.", nameof(vmName));
        }

        Process.Start(new ProcessStartInfo("vmconnect.exe", $"localhost \"{vmName}\"") { UseShellExecute = true });
    }

    private static IReadOnlyList<ValidationResult> BuildStatusChecks(SystemStatus status)
    {
        var checks = new List<ValidationResult>
        {
            status.IsAdministrator
                ? ValidationResult.Ok("Administrador", "La app está ejecutándose con permisos elevados.")
                : ValidationResult.Error("Administrador requerido", "La app se relanzará como administrador si es posible."),
            status.IsHyperVEnabled
                ? ValidationResult.Ok("Hyper-V habilitado", "La característica Microsoft-Hyper-V-All está habilitada.")
                : ValidationResult.Error("Hyper-V no habilitado", "Instala y habilita Hyper-V desde Características de Windows o PowerShell."),
        };

        checks.Add(status.VirtualizationFirmwareEnabled switch
        {
            true => ValidationResult.Ok("Virtualización en firmware", "La virtualización aparece habilitada en BIOS/UEFI."),
            false => ValidationResult.Error("Virtualización deshabilitada", "Activa Intel VT-x/AMD-V en BIOS/UEFI."),
            null => ValidationResult.Warning("Virtualización no detectada", "No se pudo comprobar de forma fiable desde WMI.")
        });

        checks.Add(status.VmMonitorModeExtensions switch
        {
            true => ValidationResult.Ok("Extensiones VM monitor", "El procesador informa soporte para virtualización."),
            false => ValidationResult.Warning("Extensiones no confirmadas", "WMI no confirma VMMonitorModeExtensions."),
            null => ValidationResult.Warning("CPU no detectada", "No se pudo leer Win32_Processor.")
        });

        return checks;
    }

    private static void EnsureSuccess(PowerShellExecutionResult result, string message)
    {
        if (!result.IsSuccess)
        {
            throw new InvalidOperationException($"{message}{Environment.NewLine}{ExtractReadableError(result)}");
        }
    }

    private static string ExtractReadableError(PowerShellExecutionResult result)
    {
        if (result.TimedOut)
        {
            return "La operación superó el tiempo máximo configurado.";
        }

        return string.IsNullOrWhiteSpace(result.StandardError)
            ? "PowerShell no devolvió detalles adicionales."
            : result.StandardError;
    }

    private sealed class SystemStatusDto
    {
        public string WindowsCaption { get; set; } = string.Empty;
        public string WindowsVersion { get; set; } = string.Empty;
        public string HyperVState { get; set; } = string.Empty;
        public bool? VirtualizationFirmwareEnabled { get; set; }
        public bool? VmMonitorModeExtensions { get; set; }
    }

    private sealed class ValidationDto
    {
        public string Severity { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Message { get; set; } = string.Empty;
    }
}
