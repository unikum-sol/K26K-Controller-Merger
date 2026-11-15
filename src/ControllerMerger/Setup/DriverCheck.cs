using System.Diagnostics;
using ControllerMerger.Services;
using Microsoft.Win32;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Exceptions;

namespace ControllerMerger.Setup;

public enum DriverStatus { Ok, NotInstalled, VersionMismatch, AccessFailed, Unknown }

public sealed record DriverInfo(DriverStatus Status, string? Version, string Detail)
{
    public bool IsOk => Status == DriverStatus.Ok;
}

/// <summary>Prüft, ob ViGEmBus installiert und benutzbar ist (öffnet testweise einen Client).</summary>
internal static class DriverCheck
{
    public static DriverInfo Check()
    {
        string? version = ReadInstalledVersion();
        try
        {
            using var client = new ViGEmClient();
            return new DriverInfo(DriverStatus.Ok, version, "ViGEmBus ist installiert und aktiv.");
        }
        catch (VigemBusNotFoundException)
        {
            return version is null
                ? new DriverInfo(DriverStatus.NotInstalled, null, "ViGEmBus ist nicht installiert.")
                : new DriverInfo(DriverStatus.AccessFailed, version,
                    "ViGEmBus ist installiert, aber nicht aktiv. Meist hilft ein Neustart von Windows.");
        }
        catch (VigemBusVersionMismatchException)
        {
            return new DriverInfo(DriverStatus.VersionMismatch, version,
                "Die installierte ViGEmBus-Version ist zu alt oder inkompatibel.");
        }
        catch (VigemBusAccessFailedException)
        {
            return new DriverInfo(DriverStatus.AccessFailed, version,
                "Auf ViGEmBus kann nicht zugegriffen werden. Der Treiber ist eventuell beschädigt oder blockiert.");
        }
        catch (Exception ex)
        {
            Log.Error("Treiberprüfung", ex);
            return new DriverInfo(DriverStatus.Unknown, version, "Unerwarteter Fehler: " + ex.Message);
        }
    }

    /// <summary>Liest die Dateiversion von ViGEmBus.sys über den Dienst-Eintrag (null = nicht installiert).</summary>
    private static string? ReadInstalledVersion()
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(@"SYSTEM\CurrentControlSet\Services\ViGEmBus");
            if (key?.GetValue("ImagePath") is not string imagePath) return null;

            string path = imagePath.Replace(@"\SystemRoot", Environment.GetFolderPath(Environment.SpecialFolder.Windows),
                StringComparison.OrdinalIgnoreCase).Replace(@"\??\", "");
            if (!Path.IsPathRooted(path))
                path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), path);
            if (!File.Exists(path)) return "unbekannt";

            var info = FileVersionInfo.GetVersionInfo(path);
            return $"{info.FileMajorPart}.{info.FileMinorPart}.{info.FileBuildPart}";
        }
        catch (Exception)
        {
            return null;
        }
    }
}
