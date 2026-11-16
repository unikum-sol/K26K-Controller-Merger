using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32;
using Microsoft.Win32.SafeHandles;

namespace ControllerMerger.HidHide;

/// <summary>Zustand des HidHide-Treibers aus Sicht der App.</summary>
public enum HidHideStatus
{
    /// <summary>Noch nicht geprüft.</summary>
    Unknown,
    /// <summary>Kein HidHide-Dienst vorhanden.</summary>
    NotInstalled,
    /// <summary>Dienst ist registriert, das Steuergerät fehlt aber (meist: Neustart nach der Installation nötig).</summary>
    RestartRequired,
    /// <summary>Steuergerät erreichbar.</summary>
    Ready,
    /// <summary>Steuergerät belegt (z. B. HidHide-Konfiguration offen) oder anderer Fehler.</summary>
    Busy,
}

/// <summary>
/// Schlanke, eigene Anbindung an das HidHide-Steuergerät <c>\\.\HidHide</c> (Architektur-Council 1.3:
/// eigene P/Invoke-Schicht statt NuGet, einstimmig). Keine Adminrechte nötig.
/// <para>
/// Wichtig: Der Treiber erlaubt nur EINEN offenen Handle systemweit. Deshalb öffnet jede Sitzung
/// (<see cref="Open"/>) das Gerät nur kurz, erledigt alle Lese-/Schreibzugriffe und schließt es sofort wieder.
/// Listenänderungen sind immer Lesen-Ändern-Schreiben innerhalb derselben Sitzung.
/// </para>
/// </summary>
internal sealed unsafe partial class HidHideClient : IDisposable
{
    private const string DevicePath = @"\\.\HidHide";
    private const string ServiceKey = @"SYSTEM\CurrentControlSet\Services\HidHide";

    // CTL_CODE(32769, 2048 + n, METHOD_BUFFERED, FILE_READ_DATA) – siehe HidHide/Shared/HidHideIoctlContract.h
    private static uint Ioctl(uint function) => (32769u << 16) | (1u << 14) | (function << 2);
    private static readonly uint IoctlGetWhitelist = Ioctl(2048);
    private static readonly uint IoctlSetWhitelist = Ioctl(2049);
    private static readonly uint IoctlGetBlacklist = Ioctl(2050);
    private static readonly uint IoctlSetBlacklist = Ioctl(2051);
    private static readonly uint IoctlGetActive = Ioctl(2052);
    private static readonly uint IoctlSetActive = Ioctl(2053);
    private static readonly uint IoctlGetInverse = Ioctl(2054);

    private const int ErrorFileNotFound = 2;
    private const int ErrorPathNotFound = 3;
    private const int ErrorAccessDenied = 5;
    private const int ErrorSharingViolation = 32;
    private const int ErrorMoreData = 234;
    private const int ErrorInsufficientBuffer = 122;

    private readonly SafeFileHandle _handle;

    private HidHideClient(SafeFileHandle handle) => _handle = handle;

    /// <summary>Ist der HidHide-Dienst registriert (unabhängig davon, ob er läuft)?</summary>
    public static bool IsServiceRegistered()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(ServiceKey);
            return key is not null;
        }
        catch (Exception) { return false; }
    }

    /// <summary>
    /// Öffnet das Steuergerät. Bei "belegt" wird mit kurzer Pause erneut versucht, insgesamt höchstens
    /// <paramref name="timeout"/> lang (beim Beenden wichtig: nie unbegrenzt warten).
    /// </summary>
    public static HidHideClient? Open(TimeSpan timeout, out HidHideStatus status)
    {
        var until = Environment.TickCount64 + (long)timeout.TotalMilliseconds;
        int delay = 20;
        while (true)
        {
            var h = CreateFileW(DevicePath, GenericRead, FileShareRead | FileShareWrite, 0, OpenExisting, 0, 0);
            if (!h.IsInvalid)
            {
                status = HidHideStatus.Ready;
                return new HidHideClient(h);
            }
            int err = Marshal.GetLastPInvokeError();
            h.Dispose();

            if (err is ErrorFileNotFound or ErrorPathNotFound)
            {
                status = IsServiceRegistered() ? HidHideStatus.RestartRequired : HidHideStatus.NotInstalled;
                return null;
            }
            if (err is not (ErrorAccessDenied or ErrorSharingViolation) || Environment.TickCount64 + delay > until)
            {
                status = HidHideStatus.Busy;
                return null;
            }
            Thread.Sleep(delay);
            delay = Math.Min(delay * 2, 200);
        }
    }

    public List<string> GetBlacklist() => GetList(IoctlGetBlacklist);
    public void SetBlacklist(IEnumerable<string> ids) => SetList(IoctlSetBlacklist, ids);
    public List<string> GetWhitelist() => GetList(IoctlGetWhitelist);
    public void SetWhitelist(IEnumerable<string> paths) => SetList(IoctlSetWhitelist, paths);
    public bool GetActive() => GetBool(IoctlGetActive);
    public void SetActive(bool active) => SetBool(IoctlSetActive, active);

    /// <summary>Invertierte App-Liste: Liste enthält dann die BLOCKIERTEN statt der erlaubten Programme.</summary>
    public bool GetInverse() => GetBool(IoctlGetInverse);

    // ------------------------------------------------------------------ MULTI_SZ

    private List<string> GetList(uint code)
    {
        // Größe abfragen, dann mit Reserve lesen; wächst die Liste dazwischen, erneut versuchen.
        for (int attempt = 0; attempt < 4; attempt++)
        {
            uint needed = 0;
            DeviceIoControl(_handle, code, null, 0, null, 0, &needed, 0);
            int bytes = (int)Math.Max(needed, 4u) + 512;
            var buffer = new byte[bytes];
            fixed (byte* p = buffer)
            {
                uint got = 0;
                if (DeviceIoControl(_handle, code, null, 0, p, (uint)bytes, &got, 0))
                    return ParseMultiSz(buffer.AsSpan(0, (int)got));
            }
            int err = Marshal.GetLastPInvokeError();
            if (err is not (ErrorMoreData or ErrorInsufficientBuffer)) throw new Win32Exception(err);
        }
        throw new InvalidOperationException("HidHide-Liste ändert sich zu schnell.");
    }

    private void SetList(uint code, IEnumerable<string> items)
    {
        byte[] buffer = BuildMultiSz(items);
        fixed (byte* p = buffer)
        {
            uint got = 0;
            if (!DeviceIoControl(_handle, code, p, (uint)buffer.Length, null, 0, &got, 0))
                throw new Win32Exception(Marshal.GetLastPInvokeError());
        }
    }

    private bool GetBool(uint code)
    {
        byte value = 0;
        uint got = 0;
        if (!DeviceIoControl(_handle, code, null, 0, &value, 1, &got, 0))
            throw new Win32Exception(Marshal.GetLastPInvokeError());
        return value != 0;
    }

    private void SetBool(uint code, bool value)
    {
        byte b = value ? (byte)1 : (byte)0;
        uint got = 0;
        if (!DeviceIoControl(_handle, code, &b, 1, null, 0, &got, 0))
            throw new Win32Exception(Marshal.GetLastPInvokeError());
    }

    /// <summary>UTF-16, jeder Eintrag NUL-terminiert, abschließend ein weiteres NUL. Leere Liste = zwei NULs.</summary>
    internal static byte[] BuildMultiSz(IEnumerable<string> items)
    {
        var sb = new StringBuilder();
        foreach (var item in items)
        {
            if (string.IsNullOrEmpty(item)) continue;
            sb.Append(item).Append('\0');
        }
        if (sb.Length == 0) sb.Append('\0');
        sb.Append('\0');
        return Encoding.Unicode.GetBytes(sb.ToString());
    }

    internal static List<string> ParseMultiSz(ReadOnlySpan<byte> bytes)
    {
        var text = Encoding.Unicode.GetString(bytes[..(bytes.Length & ~1)]);
        return text.Split('\0', StringSplitOptions.RemoveEmptyEntries).ToList();
    }

    // ------------------------------------------------------------------ Pfade

    /// <summary>
    /// Wandelt einen normalen Pfad (C:\…) in die von HidHide erwartete DOS-Gerätenotation
    /// (\Device\HarddiskVolume3\…). Gibt null zurück, wenn das nicht eindeutig geht (z. B. Netzlaufwerk) –
    /// dann darf nichts versteckt werden, weil die App sich sonst selbst aussperren könnte.
    /// </summary>
    public static string? ToDosDevicePath(string path)
    {
        string full = Path.GetFullPath(path);
        if (full.Length < 3 || full[1] != ':' || full[2] != '\\') return null;
        string drive = full[..2];
        var buffer = new char[1024];
        uint n;
        fixed (char* p = buffer) n = QueryDosDeviceW(drive, p, (uint)buffer.Length);
        if (n == 0) return null;
        string target = new string(buffer, 0, (int)n).Split('\0')[0];
        if (!target.StartsWith(@"\Device\", StringComparison.OrdinalIgnoreCase)) return null;
        if (target.StartsWith(@"\Device\LanmanRedirector", StringComparison.OrdinalIgnoreCase) ||
            target.StartsWith(@"\Device\Mup", StringComparison.OrdinalIgnoreCase)) return null;
        return target + full[2..];
    }

    public void Dispose() => _handle.Dispose();

    // ------------------------------------------------------------------ Win32

    private const uint GenericRead = 0x80000000;
    private const uint FileShareRead = 1, FileShareWrite = 2;
    private const uint OpenExisting = 3;

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial SafeFileHandle CreateFileW(string name, uint access, uint share, nint security,
        uint disposition, uint flags, nint template);

    [LibraryImport("kernel32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeviceIoControl(SafeFileHandle device, uint code, void* inBuffer, uint inSize,
        void* outBuffer, uint outSize, uint* returned, nint overlapped);

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial uint QueryDosDeviceW(string deviceName, char* targetPath, uint max);
}
