using System.Text;
using HyperVGpuShareManager.Core.Models;
using HyperVGpuShareManager.Core.Utilities;

namespace HyperVGpuShareManager.Core.Services;

public sealed class DriverPreparationService : IDriverPreparationService
{
    private readonly IPowerShellService _powerShell;
    private readonly ILoggingService _logger;

    public DriverPreparationService(IPowerShellService powerShell, ILoggingService logger)
    {
        _powerShell = powerShell;
        _logger = logger;
    }

    public async Task<DriverPackageInfo> DetectHostDisplayDriverAsync(GpuInfo gpu, CancellationToken cancellationToken = default)
    {
        var result = await _powerShell.RunAsync(new PowerShellRequest
        {
            Description = "Detect host display driver package",
            Timeout = TimeSpan.FromMinutes(2),
            Parameters = new Dictionary<string, string?> { ["DeviceInstancePath"] = gpu.DeviceInstancePath, ["Vendor"] = gpu.VendorLabel },
            Script = """
$signed = $null
if ($DeviceInstancePath) {
    $signed = Get-CimInstance Win32_PnPSignedDriver |
        Where-Object { $_.DeviceID -eq $DeviceInstancePath -and $_.DeviceClass -eq "DISPLAY" } |
        Select-Object -First 1
}

if ($null -eq $signed) {
    $signed = Get-CimInstance Win32_PnPSignedDriver |
        Where-Object { $_.DeviceClass -eq "DISPLAY" -and ($_.DriverProviderName -like "*$Vendor*" -or $_.DeviceName -like "*$Vendor*") } |
        Select-Object -First 1
}

if ($null -eq $signed) {
    [pscustomobject]@{
        InfName = ""
        OriginalFileName = ""
        SourceDirectory = ""
        ProviderName = ""
        ClassName = ""
        Version = ""
        Date = ""
    } | ConvertTo-Json -Depth 3
    return
}

$windowsDriver = $null
if (Get-Command Get-WindowsDriver -ErrorAction SilentlyContinue) {
    $windowsDriver = Get-WindowsDriver -Online -All |
        Where-Object {
            $_.Driver -eq $signed.InfName -or
            $_.OriginalFileName -like ("*" + $signed.InfName) -or
            $_.ProviderName -eq $signed.DriverProviderName
        } |
        Select-Object -First 1
}

$original = if ($null -ne $windowsDriver) { [string]$windowsDriver.OriginalFileName } else { [string]$signed.InfName }
$source = ""
if ($original -and (Test-Path -LiteralPath $original)) {
    $source = Split-Path -Parent $original
}

if (-not $source) {
    $fileRepository = Join-Path $env:windir "System32\DriverStore\FileRepository"
    $infBase = [IO.Path]::GetFileName($original)
    if (-not $infBase) { $infBase = [string]$signed.InfName }
    $match = Get-ChildItem -LiteralPath $fileRepository -Directory -ErrorAction SilentlyContinue |
        Where-Object {
            Test-Path -LiteralPath (Join-Path $_.FullName $infBase)
        } |
        Sort-Object LastWriteTime -Descending |
        Select-Object -First 1
    if ($null -ne $match) { $source = $match.FullName }
}

[pscustomobject]@{
    InfName = [string]$signed.InfName
    OriginalFileName = $original
    SourceDirectory = $source
    ProviderName = [string]$signed.DriverProviderName
    ClassName = [string]$signed.DeviceClass
    Version = [string]$signed.DriverVersion
    Date = if ($signed.DriverDate) { [Management.ManagementDateTimeConverter]::ToDateTime($signed.DriverDate).ToString("yyyy-MM-dd") } else { "" }
} | ConvertTo-Json -Depth 4
"""
        }, cancellationToken);

        if (!result.IsSuccess)
        {
            _logger.Warning("No se pudo detectar automáticamente el paquete de driver de pantalla.");
            return new DriverPackageInfo();
        }

        return PowerShellJson.DeserializeObject<DriverPackageInfo>(result.StandardOutput) ?? new DriverPackageInfo();
    }

    public async Task<DriverPreparationResult> PrepareDriversOfflineAsync(VmInfo vm, GpuInfo gpu, CancellationToken cancellationToken = default)
    {
        if (!vm.IsOff)
        {
            return new DriverPreparationResult
            {
                Success = false,
                Method = "VHDX offline",
                Message = "La VM debe estar apagada para montar el VHDX offline."
            };
        }

        var driver = await DetectHostDisplayDriverAsync(gpu, cancellationToken);
        if (!driver.Found)
        {
            return new DriverPreparationResult
            {
                Success = false,
                Method = "Detección de driver",
                Message = "No se pudo localizar la carpeta del driver en DriverStore. Usa las instrucciones manuales."
            };
        }

        var diskPath = vm.DiskPaths.FirstOrDefault(path => path.EndsWith(".vhdx", StringComparison.OrdinalIgnoreCase) || path.EndsWith(".vhd", StringComparison.OrdinalIgnoreCase));
        if (string.IsNullOrWhiteSpace(diskPath))
        {
            return new DriverPreparationResult
            {
                Success = false,
                Method = "VHDX offline",
                Message = "La VM no tiene un disco VHD/VHDX detectable."
            };
        }

        var result = await _powerShell.RunAsync(new PowerShellRequest
        {
            Description = "Copy GPU driver package to VM VHDX",
            Timeout = TimeSpan.FromMinutes(10),
            Parameters = new Dictionary<string, string?>
            {
                ["VhdPath"] = diskPath,
                ["SourceDirectory"] = driver.SourceDirectory
            },
            Script = """
if (-not (Test-Path -LiteralPath $VhdPath)) {
    throw "No existe el VHDX: $VhdPath"
}
if (-not (Test-Path -LiteralPath $SourceDirectory)) {
    throw "No existe la carpeta de driver: $SourceDirectory"
}

$mounted = $null
try {
    $mounted = Mount-VHD -Path $VhdPath -Passthru -ErrorAction Stop
    Start-Sleep -Seconds 2
    $diskNumber = $mounted.DiskNumber
    $volumes = @(Get-Disk -Number $diskNumber -ErrorAction Stop | Get-Partition | Get-Volume -ErrorAction SilentlyContinue | Where-Object { $_.DriveLetter })
    $windowsVolume = $null
    foreach ($volume in $volumes) {
        $root = "$($volume.DriveLetter):\"
        if (Test-Path -LiteralPath (Join-Path $root "Windows\System32")) {
            $windowsVolume = $volume
            break
        }
    }
    if ($null -eq $windowsVolume) {
        throw "No se encontró una instalación de Windows en el VHDX montado."
    }

    $windowsRoot = "$($windowsVolume.DriveLetter):\Windows"
    $destinationRoot = Join-Path $windowsRoot "System32\HostDriverStore\FileRepository"
    New-Item -ItemType Directory -Path $destinationRoot -Force | Out-Null
    $sourceName = Split-Path -Path $SourceDirectory -Leaf
    $destination = Join-Path $destinationRoot $sourceName
    Copy-Item -LiteralPath $SourceDirectory -Destination $destination -Recurse -Force -ErrorAction Stop

    [pscustomobject]@{
        Success = $true
        Method = "VHDX offline"
        Destination = $destination
        Message = "Se copió el paquete de driver al HostDriverStore del guest."
    } | ConvertTo-Json -Depth 4
}
finally {
    if ($null -ne $mounted) {
        Dismount-VHD -Path $VhdPath -ErrorAction SilentlyContinue
    }
}
"""
        }, cancellationToken);

        if (!result.IsSuccess)
        {
            return new DriverPreparationResult
            {
                Success = false,
                Method = "VHDX offline",
                Message = string.IsNullOrWhiteSpace(result.StandardError)
                    ? "La copia offline falló sin detalles adicionales."
                    : result.StandardError
            };
        }

        return PowerShellJson.DeserializeObject<DriverPreparationResult>(result.StandardOutput) ?? new DriverPreparationResult
        {
            Success = true,
            Method = "VHDX offline",
            Message = "Operación completada."
        };
    }

    public async Task<DriverPreparationResult> PrepareDriversWithPowerShellDirectAsync(VmInfo vm, GpuInfo gpu, string guestUserName, string guestPassword, CancellationToken cancellationToken = default)
    {
        if (vm.IsOff)
        {
            return new DriverPreparationResult
            {
                Success = false,
                Method = "PowerShell Direct",
                Message = "PowerShell Direct requiere que la VM este encendida. Usa la copia VHDX offline para VMs apagadas."
            };
        }

        if (string.IsNullOrWhiteSpace(guestUserName) || string.IsNullOrWhiteSpace(guestPassword))
        {
            return new DriverPreparationResult
            {
                Success = false,
                Method = "PowerShell Direct",
                Message = "Introduce credenciales de administrador del Windows guest para usar PowerShell Direct."
            };
        }

        var driver = await DetectHostDisplayDriverAsync(gpu, cancellationToken);
        if (!driver.Found)
        {
            return new DriverPreparationResult
            {
                Success = false,
                Method = "Deteccion de driver",
                Message = "No se pudo localizar la carpeta del driver en DriverStore. Usa las instrucciones manuales."
            };
        }

        var result = await _powerShell.RunAsync(new PowerShellRequest
        {
            Description = "Copy GPU driver package with PowerShell Direct",
            Timeout = TimeSpan.FromMinutes(10),
            SensitiveParameterNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "GuestPassword" },
            Parameters = new Dictionary<string, string?>
            {
                ["VmName"] = vm.Name,
                ["SourceDirectory"] = driver.SourceDirectory,
                ["GuestUserName"] = guestUserName,
                ["GuestPassword"] = guestPassword
            },
            Script = """
if (-not (Test-Path -LiteralPath $SourceDirectory)) {
    throw "No existe la carpeta de driver: $SourceDirectory"
}

$vm = Get-VM -Name $VmName -ErrorAction Stop
if ($vm.State -ne "Running") {
    throw "La VM debe estar encendida para PowerShell Direct. Estado actual: $($vm.State)"
}

$session = $null
$zipPath = Join-Path $env:TEMP ("hvgpum-driver-" + [Guid]::NewGuid().ToString("N") + ".zip")
try {
    $securePassword = ConvertTo-SecureString $GuestPassword -AsPlainText -Force
    $credential = [pscredential]::new($GuestUserName, $securePassword)
    $session = New-PSSession -VMName $VmName -Credential $credential -ErrorAction Stop

    $sourceName = Split-Path -Path $SourceDirectory -Leaf
    Compress-Archive -Path (Join-Path $SourceDirectory "*") -DestinationPath $zipPath -Force -ErrorAction Stop
    $guestZip = "C:\Windows\Temp\" + [IO.Path]::GetFileName($zipPath)
    Copy-Item -Path $zipPath -Destination $guestZip -ToSession $session -Force -ErrorAction Stop

    $destination = Invoke-Command -Session $session -ArgumentList $guestZip, $sourceName -ScriptBlock {
        param([string]$ZipPath, [string]$SourceName)
        $destinationRoot = "C:\Windows\System32\HostDriverStore\FileRepository"
        New-Item -ItemType Directory -Path $destinationRoot -Force | Out-Null
        $destination = Join-Path $destinationRoot $SourceName
        New-Item -ItemType Directory -Path $destination -Force | Out-Null
        Expand-Archive -Path $ZipPath -DestinationPath $destination -Force
        Remove-Item -LiteralPath $ZipPath -Force -ErrorAction SilentlyContinue
        $destination
    } -ErrorAction Stop

    [pscustomobject]@{
        Success = $true
        Method = "PowerShell Direct"
        Destination = [string]$destination
        Message = "Se copio el paquete de driver al HostDriverStore del guest mediante PowerShell Direct."
    } | ConvertTo-Json -Depth 4
}
finally {
    if ($null -ne $session) { Remove-PSSession -Session $session -ErrorAction SilentlyContinue }
    if (Test-Path -LiteralPath $zipPath) { Remove-Item -LiteralPath $zipPath -Force -ErrorAction SilentlyContinue }
}
"""
        }, cancellationToken);

        if (!result.IsSuccess)
        {
            return new DriverPreparationResult
            {
                Success = false,
                Method = "PowerShell Direct",
                Message = string.IsNullOrWhiteSpace(result.StandardError)
                    ? "PowerShell Direct fallo sin detalles adicionales."
                    : result.StandardError
            };
        }

        return PowerShellJson.DeserializeObject<DriverPreparationResult>(result.StandardOutput) ?? new DriverPreparationResult
        {
            Success = true,
            Method = "PowerShell Direct",
            Message = "Operacion completada."
        };
    }

    public string GetManualInstructions(GpuInfo gpu)
    {
        var builder = new StringBuilder();
        builder.AppendLine("Si la copia automática falla, instala Windows 10 Pro en la VM y comprueba el Administrador de dispositivos.");
        builder.AppendLine("GPU-P depende de que el driver del host exponga particionamiento y de que el guest pueda cargar los componentes adecuados.");
        builder.AppendLine("Pasos manuales recomendados:");
        builder.AppendLine("1. Mantén la VM apagada antes de copiar drivers offline.");
        builder.AppendLine("2. Localiza el paquete de pantalla del host en C:\\Windows\\System32\\DriverStore\\FileRepository.");
        builder.AppendLine("3. Copia la carpeta del paquete al guest en Windows\\System32\\HostDriverStore\\FileRepository.");
        builder.AppendLine("4. Inicia la VM y verifica en dxdiag o Administrador de dispositivos.");
        builder.AppendLine($"GPU seleccionada: {gpu.VendorLabel} {gpu.Name}");
        return builder.ToString();
    }
}
