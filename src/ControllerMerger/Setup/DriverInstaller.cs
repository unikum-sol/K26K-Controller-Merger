using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ControllerMerger.Interop;
using ControllerMerger.Services;

namespace ControllerMerger.Setup;

public sealed class InstallerVerificationException(string message) : Exception(message);

/// <summary>Bewusster Abbruch mit nutzerfreundlicher Meldung (UAC abgelehnt, Installer abgebrochen).</summary>
public sealed class SetupCancelledException(string message) : OperationCanceledException(message);

/// <summary>Eine fest gepinnte Treiber-Installationsdatei (Version, Download-Adresse, SHA-256).</summary>
internal sealed record InstallerSpec(string Name, string Version, string Sha256, string FileName, Uri DownloadUri)
{
    public static readonly InstallerSpec ViGEmBus = new("ViGEmBus", "1.22.0",
        "89220a7865076b342892f98865f3499fb7c4cfd673159e89d352c360fd014c6a",
        "ViGEmBus_1.22.0_x64_x86_arm64.exe",
        new("https://github.com/nefarius/ViGEmBus/releases/download/v1.22.0/ViGEmBus_1.22.0_x64_x86_arm64.exe"));

    /// <summary>HidHide 1.5.230 – neueste Version mit offiziellem GitHub-Release.</summary>
    public static readonly InstallerSpec HidHide = new("HidHide", "1.5.230",
        "f4bbbcb82e6258641b887c74bc81c4c5f66e4aa811808dfc304347687b7605f6",
        "HidHide_1.5.230_x64.exe",
        new("https://github.com/nefarius/HidHide/releases/download/v1.5.230.0/HidHide_1.5.230_x64.exe"));
}

/// <summary>
/// Lädt einen Treiber-Installer (ViGEmBus oder HidHide), verifiziert ihn und startet ihn mit Administratorrechten.
/// Sicherheitskonzept (Architektur-Council, einstimmig):
/// <list type="bullet">
/// <item>Version FEST gepinnt – niemals "latest".</item>
/// <item>SHA-256 der exakten Datei gepinnt.</item>
/// <item>Zusätzlich gültige Authenticode-Signatur mit erwartetem Herausgeber.</item>
/// <item>Download in ein frisches, eigenes Temp-Verzeichnis; Start erst nach erfolgreicher Prüfung.</item>
/// </list>
/// </summary>
internal static class DriverInstaller
{
    public const string ExpectedSigner = "CN=Nefarius Software Solutions e.U.";

    /// <summary>Installer-Rückgabewerte (MSI/Burn-Konvention).</summary>
    public const int ExitSuccess = 0;
    public const int ExitRebootRequired = 3010;
    public const int ExitRebootInitiated = 1641;
    public const int ExitUserCancel = 1602;

    public static async Task<string> DownloadAsync(InstallerSpec spec, IProgress<double> progress, CancellationToken ct)
    {
        string dir = Path.Combine(Path.GetTempPath(), "K26KControllerMerger-Setup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, spec.FileName);

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("K26KControllerMerger-Setup/1.0");
        using var response = await http.GetAsync(spec.DownloadUri, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        long total = response.Content.Headers.ContentLength ?? -1;
        await using var source = await response.Content.ReadAsStreamAsync(ct);
        await using var target = File.Create(file);
        var buffer = new byte[81920];
        long read = 0;
        int n;
        while ((n = await source.ReadAsync(buffer, ct)) > 0)
        {
            await target.WriteAsync(buffer.AsMemory(0, n), ct);
            read += n;
            if (total > 0) progress.Report((double)read / total);
        }
        progress.Report(1);
        Log.Info($"{spec.Name}-Installer geladen: {read} Bytes");
        return file;
    }

    /// <summary>Wirft <see cref="InstallerVerificationException"/>, wenn Hash oder Signatur nicht stimmen.</summary>
    public static void Verify(InstallerSpec spec, string path)
    {
        string actual;
        using (var stream = File.OpenRead(path))
            actual = Convert.ToHexStringLower(SHA256.HashData(stream));
        if (!string.Equals(actual, spec.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InstallerVerificationException(UI.Loc.T("ErrHash"));

        if (!Native.HasValidAuthenticodeSignature(path))
            throw new InstallerVerificationException(UI.Loc.T("ErrSignature"));

#pragma warning disable SYSLIB0057 // nur Lesen des Signaturzertifikats einer bereits geprüften Datei
        using var cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
#pragma warning restore SYSLIB0057
        if (!cert.Subject.StartsWith(ExpectedSigner, StringComparison.OrdinalIgnoreCase))
            throw new InstallerVerificationException(UI.Loc.F("ErrPublisher", cert.Subject));

        Log.Info($"{spec.Name}-Installer verifiziert (SHA-256 + Authenticode).");
    }

    /// <summary>
    /// Startet den Installer über UAC und wartet auf das Ende.
    /// </summary>
    /// <returns>Exit-Code des Installers.</returns>
    /// <exception cref="OperationCanceledException">UAC-Abfrage wurde abgelehnt.</exception>
    public static async Task<int> RunElevatedAsync(InstallerSpec spec, string path, CancellationToken ct)
    {
        Process? process;
        try
        {
            process = Process.Start(new ProcessStartInfo(path)
            {
                UseShellExecute = true,
                Verb = "runas",
                WorkingDirectory = Path.GetDirectoryName(path)!,
            });
        }
        catch (Win32Exception ex) when (ex.NativeErrorCode == 1223) // ERROR_CANCELLED
        {
            throw new SetupCancelledException(UI.Loc.T("ErrUacDenied"));
        }

        if (process is null) throw new InvalidOperationException("Installer konnte nicht gestartet werden.");
        using (process)
        {
            await process.WaitForExitAsync(ct);
            Log.Info($"{spec.Name}-Installer beendet mit Code {process.ExitCode}");
            return process.ExitCode;
        }
    }

    public static void Cleanup(string? installerPath)
    {
        if (installerPath is null) return;
        try { Directory.Delete(Path.GetDirectoryName(installerPath)!, recursive: true); }
        catch (Exception) { /* Temp-Verzeichnis – nicht kritisch */ }
    }
}
