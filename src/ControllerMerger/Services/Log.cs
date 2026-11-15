using System.Text;

namespace ControllerMerger.Services;

/// <summary>
/// Minimaler, threadsicherer Datei-Logger (%LOCALAPPDATA%\ControllerMerger\log.txt).
/// Rotiert bei 1 MB. Logging darf die App nie zum Absturz bringen.
/// </summary>
internal static class Log
{
    private static readonly object Gate = new();
    private const long MaxBytes = 1024 * 1024;

    public static string Directory { get; } =
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "ControllerMerger");

    public static string FilePath { get; } = Path.Combine(Directory, "log.txt");

    public static void Info(string message) => Write("INFO ", message);
    public static void Warn(string message) => Write("WARN ", message);
    public static void Error(string message, Exception? ex = null) =>
        Write("ERROR", ex is null ? message : $"{message}: {ex}");

    private static void Write(string level, string message)
    {
        try
        {
            lock (Gate)
            {
                System.IO.Directory.CreateDirectory(Directory);
                var fi = new FileInfo(FilePath);
                if (fi.Exists && fi.Length > MaxBytes)
                    File.Move(FilePath, FilePath + ".1", overwrite: true);
                File.AppendAllText(FilePath, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} {level} {message}{Environment.NewLine}", Encoding.UTF8);
            }
        }
        catch
        {
            // Logging ist "best effort".
        }
    }
}
