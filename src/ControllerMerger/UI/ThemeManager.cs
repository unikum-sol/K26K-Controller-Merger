using System.Windows;
using System.Windows.Interop;
using ControllerMerger.Core;
using ControllerMerger.Interop;
using Microsoft.Win32;

namespace ControllerMerger.UI;

/// <summary>
/// Schaltet zwischen Hell und Dunkel um (Token-Dictionary austauschen) und folgt auf Wunsch live
/// der Windows-Einstellung. Setzt außerdem DWM-Attribute (runde Ecken unter Windows 11, dunkler Rahmen).
/// </summary>
internal static class ThemeManager
{
    private const string PersonalizeKey = @"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize";
    private static ThemePreference _preference = ThemePreference.System;
    private static bool _hooked;

    public static bool IsDark { get; private set; } = true;

    public static bool IsWindows11 { get; } = Environment.OSVersion.Version.Build >= 22000;

    /// <summary>Ausgelöst nach jedem Wechsel (auch Taskleisten-Farbe, für das Tray-Icon).</summary>
    public static event Action? Changed;

    public static void Apply(ThemePreference preference)
    {
        _preference = preference;
        if (!_hooked)
        {
            _hooked = true;
            SystemEvents.UserPreferenceChanged += (_, e) =>
            {
                if (e.Category is UserPreferenceCategory.General or UserPreferenceCategory.Color)
                    Application.Current?.Dispatcher.BeginInvoke(() => Apply(_preference));
            };
        }

        bool dark = preference switch
        {
            ThemePreference.Dark => true,
            ThemePreference.Light => false,
            _ => SystemAppsUseDark(),
        };
        SetDictionary(dark);
        Changed?.Invoke();
    }

    /// <summary>Nur für den Screenshot-Modus: Theme direkt setzen, ohne Systemabhängigkeit.</summary>
    public static void Force(bool dark) => SetDictionary(dark);

    private static void SetDictionary(bool dark)
    {
        IsDark = dark;
        var app = Application.Current;
        if (app is null) return;

        var dict = new ResourceDictionary
        {
            Source = new Uri($"pack://application:,,,/Themes/{(dark ? "Dark" : "Light")}.xaml"),
        };
        var merged = app.Resources.MergedDictionaries;
        if (merged.Count > 0) merged[0] = dict;
        else merged.Insert(0, dict);

        foreach (Window w in app.Windows) ApplyFrame(w);
    }

    public static bool SystemAppsUseDark() => ReadDword("AppsUseLightTheme", 1) == 0;

    /// <summary>Taskleiste hell? (bestimmt die Farbe des Tray-Symbols)</summary>
    public static bool TaskbarIsLight() => ReadDword("SystemUsesLightTheme", 0) == 1;

    private static int ReadDword(string name, int fallback)
    {
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(PersonalizeKey);
            return key?.GetValue(name) is int v ? v : fallback;
        }
        catch (Exception) { return fallback; }
    }

    /// <summary>DWM-Rahmen passend zum Theme setzen (sofern das Fenster bereits ein Handle hat).</summary>
    public static void ApplyFrame(Window window)
    {
        var hwnd = new WindowInteropHelper(window).Handle;
        if (hwnd == 0) return;
        int dark = IsDark ? 1 : 0;
        Native.DwmSetWindowAttribute(hwnd, Native.DwmwaUseImmersiveDarkMode, dark, sizeof(int));
        if (IsWindows11)
        {
            int corner = window.ResizeMode == ResizeMode.NoResize ? Native.DwmwcpRoundSmall : Native.DwmwcpRound;
            Native.DwmSetWindowAttribute(hwnd, Native.DwmwaWindowCornerPreference, corner, sizeof(int));
        }
    }
}
