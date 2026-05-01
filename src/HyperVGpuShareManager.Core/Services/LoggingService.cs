using System.IO.Compression;
using HyperVGpuShareManager.Core.Models;

namespace HyperVGpuShareManager.Core.Services;

public sealed class LoggingService : ILoggingService
{
    private readonly object _sync = new();

    public event EventHandler<LogEntry>? LogWritten;

    public string LogDirectory { get; }

    public LoggingService()
    {
        var programData = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
        LogDirectory = Path.Combine(programData, "HyperVGpuShareManager", "logs");
        Directory.CreateDirectory(LogDirectory);
    }

    public void Info(string message) => Write("INFO", message);

    public void Warning(string message) => Write("WARN", message);

    public void Error(string message, Exception? exception = null)
    {
        var text = exception is null ? message : $"{message}{Environment.NewLine}{exception}";
        Write("ERROR", text);
    }

    public void Command(string message) => Write("COMMAND", message);

    public Task<string> ExportDiagnosticsAsync(string destinationDirectory, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(destinationDirectory);
        var fileName = $"HyperVGpuShareManager-diagnostics-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.zip";
        var archivePath = Path.Combine(destinationDirectory, fileName);

        if (File.Exists(archivePath))
        {
            File.Delete(archivePath);
        }

        using var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create);
        foreach (var file in Directory.EnumerateFiles(LogDirectory, "*.log", SearchOption.TopDirectoryOnly))
        {
            cancellationToken.ThrowIfCancellationRequested();
            archive.CreateEntryFromFile(file, Path.Combine("logs", Path.GetFileName(file)), CompressionLevel.Optimal);
        }

        Write("INFO", $"Diagnostic archive exported: {archivePath}");
        return Task.FromResult(archivePath);
    }

    private void Write(string level, string message)
    {
        var entry = new LogEntry
        {
            Timestamp = DateTimeOffset.Now,
            Level = level,
            Message = message
        };

        lock (_sync)
        {
            var file = Path.Combine(LogDirectory, $"app-{DateTimeOffset.Now:yyyyMMdd}.log");
            File.AppendAllText(file, entry + Environment.NewLine);
        }

        LogWritten?.Invoke(this, entry);
    }
}
