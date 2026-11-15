using Microsoft.Win32;

namespace ControllerMerger.Services;

/// <summary>Autostart pro Benutzer über HKCU\...\Run (keine Admin-Rechte nötig).</summary>
internal static class Autostart
{
    private const string RunKey = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunOnceKey = @"Software\Microsoft\Windows\CurrentVersion\RunOnce";
    private const string ValueName = "K26K Controller Merger";

    private static string Command => $"\"{Environment.ProcessPath}\" --autostart";

    public static bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKey);
        return key?.GetValue(ValueName) is string s && s.Contains(Environment.ProcessPath ?? "\u0000", StringComparison.OrdinalIgnoreCase);
    }

    public static void Set(bool enabled)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunKey);
            if (enabled) key.SetValue(ValueName, Command);
            else key.DeleteValue(ValueName, throwOnMissingValue: false);
        }
        catch (Exception ex)
        {
            Log.Error("Autostart konnte nicht gesetzt werden", ex);
        }
    }

    /// <summary>Startet das Tool einmalig nach dem nächsten Neustart (nach der Treiberinstallation).</summary>
    public static void ResumeAfterReboot()
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(RunOnceKey);
            key.SetValue(ValueName, $"\"{Environment.ProcessPath}\"");
        }
        catch (Exception ex)
        {
            Log.Error("RunOnce konnte nicht gesetzt werden", ex);
        }
    }
}
