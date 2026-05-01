namespace HyperVGpuShareManager.Core.Models;

public sealed class PowerShellExecutionResult
{
    public int ExitCode { get; init; }
    public string StandardOutput { get; init; } = string.Empty;
    public string StandardError { get; init; } = string.Empty;
    public bool TimedOut { get; init; }
    public TimeSpan Duration { get; init; }
    public string DisplayCommand { get; init; } = string.Empty;
    public bool IsSuccess => ExitCode == 0 && !TimedOut;
}
