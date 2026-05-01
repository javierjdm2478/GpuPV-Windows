namespace HyperVGpuShareManager.Core.Models;

public sealed class PowerShellRequest
{
    public string Script { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, string?> Parameters { get; init; } = new Dictionary<string, string?>();
    public IReadOnlySet<string> SensitiveParameterNames { get; init; } = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
    public TimeSpan Timeout { get; init; } = TimeSpan.FromSeconds(60);
    public string Description { get; init; } = string.Empty;
}
