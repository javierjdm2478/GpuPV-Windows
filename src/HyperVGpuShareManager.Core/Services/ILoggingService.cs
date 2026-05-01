using HyperVGpuShareManager.Core.Models;

namespace HyperVGpuShareManager.Core.Services;

public interface ILoggingService
{
    event EventHandler<LogEntry>? LogWritten;
    string LogDirectory { get; }
    void Info(string message);
    void Warning(string message);
    void Error(string message, Exception? exception = null);
    void Command(string message);
    Task<string> ExportDiagnosticsAsync(string destinationDirectory, CancellationToken cancellationToken = default);
}
