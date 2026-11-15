using System.ComponentModel;
using System.Diagnostics;
using System.Net.Http;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using ControllerMerger.Interop;
using ControllerMerger.Services;

namespace ControllerMerger.Setup;

public sealed class InstallerVerificationException(string message) : Exception(message);

/// <summary>
/// Lädt den ViGEmBus-Installer, verifiziert ihn und startet ihn mit Administratorrechten.
/// Sicherheitskonzept (Architektur-Council, einstimmig):
/// <list type="bullet">
/// <item>Version FEST gepinnt – niemals "latest" (ViGEmBus ist EOL, das Repo archiviert).</item>
/// <item>SHA-256 der exakten Datei gepinnt.</item>
/// <item>Zusätzlich gültige Authenticode-Signatur mit erwartetem Herausgeber.</item>
/// <item>Download in ein frisches, eigenes Temp-Verzeichnis; Start erst nach erfolgreicher Prüfung.</item>
/// </list>
/// </summary>
internal static class ViGEmInstaller
{
    public const string Version = "1.22.0";
    public const string Sha256 = "89220a7865076b342892f98865f3499fb7c4cfd673159e89d352c360fd014c6a";
    public const string ExpectedSigner = "CN=Nefarius Software Solutions e.U.";
    public static readonly Uri DownloadUri =
        new($"https://github.com/nefarius/ViGEmBus/releases/download/v{Version}/ViGEmBus_{Version}_x64_x86_arm64.exe");

    /// <summary>Installer-Rückgabewerte (MSI/Burn-Konvention).</summary>
    public const int ExitSuccess = 0;
    public const int ExitRebootRequired = 3010;
    public const int ExitRebootInitiated = 1641;
    public const int ExitUserCancel = 1602;

    public static async Task<string> DownloadAsync(IProgress<double> progress, CancellationToken ct)
    {
        string dir = Path.Combine(Path.GetTempPath(), "K26KControllerMerger-Setup-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        string file = Path.Combine(dir, $"ViGEmBus_{Version}_x64_x86_arm64.exe");

        using var http = new HttpClient { Timeout = TimeSpan.FromMinutes(5) };
        http.DefaultRequestHeaders.UserAgent.ParseAdd("K26KControllerMerger-Setup/1.0");
        using var response = await http.GetAsync(DownloadUri, HttpCompletionOption.ResponseHeadersRead, ct);
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
        Log.Info($"ViGEmBus-Installer geladen: {read} Bytes");
        return file;
    }

    /// <summary>Wirft <see cref="InstallerVerificationException"/>, wenn Hash oder Signatur nicht stimmen.</summary>
    public static void Verify(string path)
    {
        string actual;
        using (var stream = File.OpenRead(path))
            actual = Convert.ToHexStringLower(SHA256.HashData(stream));
        if (!string.Equals(actual, Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InstallerVerificationException("Die Prüfsumme der heruntergeladenen Datei stimmt nicht. Die Datei wurde nicht ausgeführt.");

        if (!Native.HasValidAuthenticodeSignature(path))
            throw new InstallerVerificationException("Die digitale Signatur des Installers ist ungültig. Die Datei wurde nicht ausgeführt.");

#pragma warning disable SYSLIB0057 // nur Lesen des Signaturzertifikats einer bereits geprüften Datei
        using var cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
#pragma warning restore SYSLIB0057
        if (!cert.Subject.StartsWith(ExpectedSigner, StringComparison.OrdinalIgnoreCase))
            throw new InstallerVerificationException($"Unerwarteter Herausgeber: {cert.Subject}");

        Log.Info("ViGEmBus-Installer verifiziert (SHA-256 + Authenticode).");
    }

    /// <summary>
    /// Startet den Installer über UAC und wartet auf das Ende.
    /// </summary>
    /// <returns>Exit-Code des Installers.</returns>
    /// <exception cref="OperationCanceledException">UAC-Abfrage wurde abgelehnt.</exception>
    public static async Task<int> RunElevatedAsync(string path, CancellationToken ct)
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
            throw new OperationCanceledException("Die Administrator-Abfrage wurde abgelehnt.");
        }

        if (process is null) throw new InvalidOperationException("Installer konnte nicht gestartet werden.");
        using (process)
        {
            await process.WaitForExitAsync(ct);
            Log.Info($"ViGEmBus-Installer beendet mit Code {process.ExitCode}");
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
