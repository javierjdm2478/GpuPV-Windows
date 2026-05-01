using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using HyperVGpuShareManager.Core.Models;

namespace HyperVGpuShareManager.Core.Services;

public sealed class PowerShellService : IPowerShellService
{
    private static readonly Regex ParameterNamePattern = new("^[A-Za-z_][A-Za-z0-9_]*$", RegexOptions.Compiled);
    private readonly ILoggingService _logger;
    private readonly string _powerShellPath;

    public PowerShellService(ILoggingService logger)
    {
        _logger = logger;
        _powerShellPath = ResolveWindowsPowerShellPath();
    }

    public async Task<PowerShellExecutionResult> RunAsync(PowerShellRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Script))
        {
            throw new ArgumentException("The PowerShell script cannot be empty.", nameof(request));
        }

        foreach (var parameter in request.Parameters.Keys)
        {
            if (!ParameterNamePattern.IsMatch(parameter))
            {
                throw new ArgumentException($"Invalid PowerShell parameter name: {parameter}", nameof(request));
            }
        }

        var tempScript = Path.Combine(Path.GetTempPath(), $"hvgpum-{Guid.NewGuid():N}.ps1");
        await File.WriteAllTextAsync(tempScript, BuildScript(request), Encoding.UTF8, cancellationToken);

        var displayCommand = BuildDisplayCommand(tempScript, request);
        _logger.Command($"{request.Description}: {displayCommand}");

        var output = new StringBuilder();
        var error = new StringBuilder();
        var stopwatch = Stopwatch.StartNew();

        using var process = new Process();
        process.StartInfo = new ProcessStartInfo
        {
            FileName = _powerShellPath,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        process.StartInfo.ArgumentList.Add("-NoLogo");
        process.StartInfo.ArgumentList.Add("-NoProfile");
        process.StartInfo.ArgumentList.Add("-NonInteractive");
        process.StartInfo.ArgumentList.Add("-ExecutionPolicy");
        process.StartInfo.ArgumentList.Add("Bypass");
        process.StartInfo.ArgumentList.Add("-File");
        process.StartInfo.ArgumentList.Add(tempScript);

        process.OutputDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                output.AppendLine(args.Data);
            }
        };
        process.ErrorDataReceived += (_, args) =>
        {
            if (args.Data is not null)
            {
                error.AppendLine(args.Data);
            }
        };

        var timedOut = false;
        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(request.Timeout);
            try
            {
                await process.WaitForExitAsync(timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                timedOut = true;
                KillProcess(process);
            }
        }
        finally
        {
            stopwatch.Stop();
            TryDelete(tempScript);
        }

        var result = new PowerShellExecutionResult
        {
            ExitCode = timedOut ? -1 : process.ExitCode,
            StandardOutput = output.ToString().Trim(),
            StandardError = error.ToString().Trim(),
            TimedOut = timedOut,
            Duration = stopwatch.Elapsed,
            DisplayCommand = displayCommand
        };

        if (result.IsSuccess)
        {
            _logger.Info($"PowerShell completed in {result.Duration.TotalSeconds:n1}s.");
        }
        else
        {
            var failure = timedOut ? "PowerShell timed out." : $"PowerShell failed with exit code {result.ExitCode}.";
            _logger.Error($"{failure}{Environment.NewLine}{result.StandardError}");
        }

        _logger.Command(
            $"Result exit={result.ExitCode}, timeout={result.TimedOut}, stdout=\"{Truncate(result.StandardOutput)}\", stderr=\"{Truncate(result.StandardError)}\"");

        return result;
    }

    private static string BuildScript(PowerShellRequest request)
    {
        var builder = new StringBuilder();
        builder.AppendLine("$ErrorActionPreference = 'Stop'");
        builder.AppendLine("Set-StrictMode -Version Latest");
        foreach (var parameter in request.Parameters)
        {
            var bytes = Encoding.UTF8.GetBytes(parameter.Value ?? string.Empty);
            var encoded = Convert.ToBase64String(bytes);
            builder.AppendLine($"${parameter.Key} = [Text.Encoding]::UTF8.GetString([Convert]::FromBase64String('{encoded}'))");
        }

        builder.AppendLine("try {");
        builder.AppendLine(request.Script);
        builder.AppendLine("}");
        builder.AppendLine("catch {");
        builder.AppendLine("    [Console]::Error.WriteLine($_.Exception.Message)");
        builder.AppendLine("    if ($_.ScriptStackTrace) { [Console]::Error.WriteLine($_.ScriptStackTrace) }");
        builder.AppendLine("    exit 1");
        builder.AppendLine("}");
        return builder.ToString();
    }

    private static string BuildDisplayCommand(string scriptPath, PowerShellRequest request)
    {
        var parts = new List<string>
        {
            "powershell.exe",
            "-NoLogo",
            "-NoProfile",
            "-NonInteractive",
            "-ExecutionPolicy Bypass",
            $"-File \"{scriptPath}\""
        };

        foreach (var parameter in request.Parameters)
        {
            var value = ShouldMask(parameter.Key, request.SensitiveParameterNames)
                ? "********"
                : parameter.Value ?? string.Empty;
            parts.Add($"-{parameter.Key} \"{value}\"");
        }

        return string.Join(' ', parts);
    }

    private static bool ShouldMask(string key, IReadOnlySet<string> sensitiveKeys) =>
        sensitiveKeys.Contains(key) || key.Contains("password", StringComparison.OrdinalIgnoreCase);

    private static string Truncate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        const int maxLength = 4000;
        return value.Length <= maxLength ? value : value[..maxLength] + "...";
    }

    private static void KillProcess(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best effort cleanup after timeout.
        }
    }

    private static void TryDelete(string file)
    {
        try
        {
            if (File.Exists(file))
            {
                File.Delete(file);
            }
        }
        catch
        {
            // Temporary script cleanup is non-critical.
        }
    }

    private static string ResolveWindowsPowerShellPath()
    {
        var systemRoot = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        var path = Path.Combine(systemRoot, "System32", "WindowsPowerShell", "v1.0", "powershell.exe");
        return File.Exists(path) ? path : "powershell.exe";
    }
}
