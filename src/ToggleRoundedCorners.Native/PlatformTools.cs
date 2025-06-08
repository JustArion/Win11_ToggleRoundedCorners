namespace Dawn.Libs.ToggleRoundedCorners.Native;

using System.Diagnostics;
using System.Security.Principal;

/// <summary>
/// Provides platform-specific tools for Windows.
/// </summary>
public static class PlatformTools
{
    public static bool IsAdmin() => new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    public static void StartAsAdmin(string arguments)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = Environment.ProcessPath,
            Verb = "runas",
            UseShellExecute = true,
            Arguments = arguments
        };

        try
        {
            Process.Start(startInfo);
        }
        catch
        {
            // ignored
        }
    }
}