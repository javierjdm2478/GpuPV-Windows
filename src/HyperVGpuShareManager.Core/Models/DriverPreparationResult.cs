namespace HyperVGpuShareManager.Core.Models;

public sealed class DriverPreparationResult
{
    public bool Success { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Destination { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}
