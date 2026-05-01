namespace HyperVGpuShareManager.Core.Models;

public sealed class ValidationResult
{
    public CheckSeverity Severity { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Message { get; init; } = string.Empty;
    public bool IsSuccess => Severity != CheckSeverity.Error;

    public static ValidationResult Ok(string title, string message) =>
        new() { Severity = CheckSeverity.Ok, Title = title, Message = message };

    public static ValidationResult Warning(string title, string message) =>
        new() { Severity = CheckSeverity.Warning, Title = title, Message = message };

    public static ValidationResult Error(string title, string message) =>
        new() { Severity = CheckSeverity.Error, Title = title, Message = message };
}
