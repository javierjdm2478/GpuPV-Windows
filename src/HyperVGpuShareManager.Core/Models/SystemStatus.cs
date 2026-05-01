namespace HyperVGpuShareManager.Core.Models;

public sealed class SystemStatus
{
    public string WindowsCaption { get; set; } = "No detectado";
    public string WindowsVersion { get; set; } = "No detectado";
    public bool IsAdministrator { get; set; }
    public bool IsHyperVInstalled { get; set; }
    public bool IsHyperVEnabled { get; set; }
    public bool? VirtualizationFirmwareEnabled { get; set; }
    public bool? VmMonitorModeExtensions { get; set; }
    public IReadOnlyList<ValidationResult> Checks { get; set; } = Array.Empty<ValidationResult>();
}
