namespace HyperVGpuShareManager.Core.Models;

public sealed class DriverPackageInfo
{
    public string InfName { get; set; } = string.Empty;
    public string OriginalFileName { get; set; } = string.Empty;
    public string SourceDirectory { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string ClassName { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public string Date { get; set; } = string.Empty;
    public bool Found => !string.IsNullOrWhiteSpace(SourceDirectory);
}
