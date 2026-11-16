using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace ControllerMerger.HidHide;

/// <summary>Ein XInput-Gerät (XUSB-Schnittstelle) mit zugeordnetem XInput-Slot.</summary>
internal readonly record struct XusbDevice(string InstanceId, int Slot, bool IsViGEm);

/// <summary>
/// Ermittelt die Windows-Geräteinstanz-IDs, die HidHide zum Verstecken braucht.
/// Läuft ausschließlich auf dem HidHide-Worker – nie auf dem Input-Thread (Council 1.3: blockierende
/// Geräte-Aufrufe und Allokationen gehören nicht in die 4-ms-Schleife).
/// <list type="bullet">
/// <item>HID-/DirectInput-Controller: SDL liefert den Schnittstellenpfad → Instanz-ID über CfgMgr32.</item>
/// <item>XInput-Controller: SDL kennt nur den Slot. Alle XUSB-Schnittstellen werden aufgezählt und über den
///       LED-Zustand ("Spieler 1–4") ihrem Slot zugeordnet – genau so vergibt Windows die Slots.</item>
/// </list>
/// </summary>
internal static unsafe partial class DeviceResolver
{
    private static readonly Guid XusbInterface = new("EC87F1E3-C13B-4100-B5F7-8B84D54260CB");
    private const uint IoctlXusbGetLedState = 0x8000E008;

    /// <summary>Instanz-ID zu einem Geräteschnittstellen-Pfad (z. B. "\\?\HID#VID_054C&amp;PID_09CC#…").</summary>
    public static string? InstanceIdFromInterfacePath(string? path)
    {
        if (string.IsNullOrEmpty(path) || !path.StartsWith(@"\\?\", StringComparison.Ordinal)) return null;
        var key = DevPropKeyInstanceId;
        var buffer = new char[512];
        uint size = (uint)(buffer.Length * sizeof(char));
        uint type;
        fixed (char* p = buffer)
        {
            if (CM_Get_Device_Interface_PropertyW(path, &key, &type, (byte*)p, &size, 0) != 0) return null;
        }
        var id = new string(buffer, 0, (int)(size / sizeof(char))).TrimEnd('\0');
        return id.Length == 0 ? null : id;
    }

    /// <summary>
    /// Zählt alle XUSB-Geräte auf und ordnet jedem seinen XInput-Slot zu. Der LED-Zustand wird zweimal im
    /// Abstand gelesen und muss übereinstimmen (beim Verbinden blinkt die LED kurz). Unklare Zustände → Slot -1.
    /// </summary>
    public static List<XusbDevice> EnumerateXusb()
    {
        var result = new List<XusbDevice>();
        var guid = XusbInterface;
        uint len = 0;
        if (CM_Get_Device_Interface_List_SizeW(&len, &guid, null, 0) != 0 || len <= 1) return result;
        var buffer = new char[len];
        fixed (char* p = buffer)
        {
            if (CM_Get_Device_Interface_ListW(&guid, null, p, len, 0) != 0) return result;
        }

        foreach (var path in new string(buffer).Split('\0', StringSplitOptions.RemoveEmptyEntries))
        {
            var id = InstanceIdFromInterfacePath(path);
            if (id is null) continue;
            int first = ReadLedSlot(path);
            int slot = -1;
            if (first >= 0)
            {
                Thread.Sleep(30);
                slot = ReadLedSlot(path) == first ? first : -1;
            }
            result.Add(new XusbDevice(id, slot, IsViGEmChild(id)));
        }
        return result;
    }

    /// <summary>XUSB "GET_LED_STATE": 2–5 = blinkt für Spieler 1–4, 6–9 = leuchtet für Spieler 1–4.</summary>
    private static int ReadLedSlot(string path)
    {
        using SafeFileHandle h = CreateFileW(path, GenericRead | GenericWrite, FileShareRead | FileShareWrite, 0, OpenExisting, 0, 0);
        if (h.IsInvalid) return -1;
        byte* input = stackalloc byte[3] { 0x01, 0x01, 0x00 }; // XUSB-Protokoll 1.1, Geräteindex 0
        byte* output = stackalloc byte[3];
        uint got = 0;
        if (!DeviceIoControl(h, IoctlXusbGetLedState, input, 3, output, 3, &got, 0) || got < 3) return -1;
        return output[2] switch
        {
            >= 2 and <= 5 => output[2] - 2,
            >= 6 and <= 9 => output[2] - 6,
            _ => -1,
        };
    }

    /// <summary>Hängt das Gerät am ViGEmBus (virtueller Controller – unserer oder z. B. einer von Sunshine)?</summary>
    public static bool IsViGEmChild(string instanceId)
    {
        uint node;
        if (CM_Locate_DevNodeW(&node, instanceId, 0) != 0) return false;
        uint parent;
        if (CM_Get_Parent(&parent, node, 0) != 0) return false;
        var key = DevPropKeyService;
        var buffer = new char[128];
        uint size = (uint)(buffer.Length * sizeof(char));
        uint type;
        fixed (char* p = buffer)
        {
            if (CM_Get_DevNode_PropertyW(parent, &key, &type, (byte*)p, &size, 0) != 0) return false;
        }
        return new string(buffer, 0, (int)(size / sizeof(char))).TrimEnd('\0')
            .Equals("ViGEmBus", StringComparison.OrdinalIgnoreCase);
    }

    // ------------------------------------------------------------------ Win32

    [StructLayout(LayoutKind.Sequential)]
    private struct DevPropKey { public Guid FmtId; public uint Pid; }

    private static readonly DevPropKey DevPropKeyInstanceId = new() { FmtId = new("78c34fc8-104a-4aca-9ea4-524d52996e57"), Pid = 256 };
    private static readonly DevPropKey DevPropKeyService = new() { FmtId = new("a45c254e-df1c-4efd-8020-67d146a850e0"), Pid = 6 };

    private const uint GenericRead = 0x80000000, GenericWrite = 0x40000000;
    private const uint FileShareRead = 1, FileShareWrite = 2;
    private const uint OpenExisting = 3;

    [LibraryImport("cfgmgr32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint CM_Get_Device_Interface_PropertyW(string path, DevPropKey* key, uint* type, byte* buffer, uint* size, uint flags);

    [LibraryImport("cfgmgr32.dll")]
    private static partial uint CM_Get_Device_Interface_List_SizeW(uint* len, Guid* guid, char* deviceId, uint flags);

    [LibraryImport("cfgmgr32.dll")]
    private static partial uint CM_Get_Device_Interface_ListW(Guid* guid, char* deviceId, char* buffer, uint len, uint flags);

    [LibraryImport("cfgmgr32.dll", StringMarshalling = StringMarshalling.Utf16)]
    private static partial uint CM_Locate_DevNodeW(uint* node, string id, uint flags);

    [LibraryImport("cfgmgr32.dll")]
    private static partial uint CM_Get_Parent(uint* parent, uint node, uint flags);

    [LibraryImport("cfgmgr32.dll")]
    private static partial uint CM_Get_DevNode_PropertyW(uint node, DevPropKey* key, uint* type, byte* buffer, uint* size, uint flags);

    [LibraryImport("kernel32.dll", StringMarshalling = StringMarshalling.Utf16, SetLastError = true)]
    private static partial SafeFileHandle CreateFileW(string name, uint access, uint share, nint security,
        uint disposition, uint flags, nint template);

    [LibraryImport("kernel32.dll", SetLastError = true)][return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool DeviceIoControl(SafeFileHandle device, uint code, void* inBuffer, uint inSize,
        void* outBuffer, uint outSize, uint* returned, nint overlapped);
}
