using System.Diagnostics;
using System.Security.Principal;

namespace HyperVGpuShareManager.App.Infrastructure;

public static class AdminRelauncher
{
    public static bool IsAdministrator()
    {
        using var identity = WindowsIdentity.GetCurrent();
        var principal = new WindowsPrincipal(identity);
        return principal.IsInRole(WindowsBuiltInRole.Administrator);
    }

    public static bool TryRelaunchElevated()
    {
        if (IsAdministrator())
        {
            return false;
        }

        var exePath = Environment.ProcessPath;
        if (string.IsNullOrWhiteSpace(exePath))
        {
            return false;
        }

        Process.Start(new ProcessStartInfo
        {
            FileName = exePath,
            UseShellExecute = true,
            Verb = "runas"
        });
        return true;
    }
}
