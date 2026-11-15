using System.Runtime.InteropServices;

namespace ControllerMerger.Interop;

/// <summary>Win32-Funktionen für Timing, Thread-Priorität, Fenster-Darstellung und Signaturprüfung.</summary>
internal static unsafe partial class Native
{
    // ---------- Hochauflösender Waitable Timer (Windows 10 1803+) ----------
    public const uint CreateWaitableTimerHighResolution = 0x00000002;
    public const uint TimerAllAccess = 0x1F0003;
    public const uint Infinite = 0xFFFFFFFF;

    [LibraryImport("kernel32.dll", EntryPoint = "CreateWaitableTimerExW", SetLastError = true)]
    public static partial nint CreateWaitableTimerEx(nint attributes, nint name, uint flags, uint access);

    [LibraryImport("kernel32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWaitableTimer(nint timer, in long dueTime, int period, nint completion, nint arg,
        [MarshalAs(UnmanagedType.Bool)] bool resume);

    [LibraryImport("kernel32.dll", SetLastError = true)]
    public static partial uint WaitForSingleObject(nint handle, uint ms);

    [LibraryImport("kernel32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool CloseHandle(nint handle);

    // ---------- MMCSS: Thread als "Games"-Task einstufen ----------
    [LibraryImport("avrt.dll", EntryPoint = "AvSetMmThreadCharacteristicsW", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    public static partial nint AvSetMmThreadCharacteristics(string taskName, ref uint taskIndex);

    [LibraryImport("avrt.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool AvRevertMmThreadCharacteristics(nint handle);

    // ---------- DWM: abgerundete Ecken (Win11), dunkle Rahmen ----------
    public const int DwmwaUseImmersiveDarkMode = 20;
    public const int DwmwaWindowCornerPreference = 33;
    public const int DwmwaBorderColor = 34;
    public const int DwmwcpRound = 2;
    public const int DwmwcpRoundSmall = 3;

    [LibraryImport("dwmapi.dll")]
    public static partial int DwmSetWindowAttribute(nint hwnd, int attribute, in int value, int size);

    // ---------- Fenster ----------
    [LibraryImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetForegroundWindow(nint hwnd);

    [StructLayout(LayoutKind.Sequential)]
    public struct Point { public int X, Y; }

    [StructLayout(LayoutKind.Sequential)]
    public struct Rect { public int Left, Top, Right, Bottom; }

    [StructLayout(LayoutKind.Sequential)]
    public struct MonitorInfo
    {
        public int cbSize;
        public Rect rcMonitor;
        public Rect rcWork;
        public uint dwFlags;
    }

    public const uint MonitorDefaultToNearest = 2;
    public static readonly nint HwndTopmost = -1;
    public const uint SwpNoSize = 0x0001;
    public const uint SwpNoActivate = 0x0010;

    [LibraryImport("user32.dll")][return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetCursorPos(out Point point);

    [LibraryImport("user32.dll")]
    public static partial nint MonitorFromPoint(Point point, uint flags);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")][return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool GetMonitorInfo(nint monitor, ref MonitorInfo info);

    [LibraryImport("user32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)]
    public static partial bool SetWindowPos(nint hwnd, nint insertAfter, int x, int y, int cx, int cy, uint flags);

    // ---------- Authenticode (WinVerifyTrust) ----------
    private static readonly Guid WintrustActionGenericVerifyV2 = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");

    [StructLayout(LayoutKind.Sequential)]
    private struct WinTrustFileInfo
    {
        public uint cbStruct;
        public nint pcwszFilePath;
        public nint hFile;
        public nint pgKnownSubject;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct WinTrustData
    {
        public uint cbStruct;
        public nint pPolicyCallbackData;
        public nint pSIPClientData;
        public uint dwUIChoice;          // 2 = WTD_UI_NONE
        public uint fdwRevocationChecks; // 0 = WTD_REVOKE_NONE
        public uint dwUnionChoice;       // 1 = WTD_CHOICE_FILE
        public WinTrustFileInfo* pFile;
        public uint dwStateAction;       // 0 = WTD_STATEACTION_IGNORE
        public nint hWVTStateData;
        public nint pwszURLReference;
        public uint dwProvFlags;
        public uint dwUIContext;
        public nint pSignatureSettings;
    }

    [LibraryImport("wintrust.dll")]
    private static partial int WinVerifyTrust(nint hwnd, in Guid action, WinTrustData* data);

    /// <summary>Prüft, ob die Datei eine gültige, vertrauenswürdige Authenticode-Signatur hat.</summary>
    public static bool HasValidAuthenticodeSignature(string path)
    {
        nint pathPtr = Marshal.StringToHGlobalUni(path);
        try
        {
            var file = new WinTrustFileInfo { cbStruct = (uint)sizeof(WinTrustFileInfo), pcwszFilePath = pathPtr };
            var data = new WinTrustData
            {
                cbStruct = (uint)sizeof(WinTrustData),
                dwUIChoice = 2,
                fdwRevocationChecks = 0,
                dwUnionChoice = 1,
                pFile = &file,
                dwProvFlags = 0x00000080, // WTD_REVOCATION_CHECK_NONE (offline-tauglich; Hash ist zusätzlich gepinnt)
            };
            return WinVerifyTrust(-1, WintrustActionGenericVerifyV2, &data) == 0;
        }
        finally { Marshal.FreeHGlobal(pathPtr); }
    }
}
