using System.Text.Json;
using System.Text.Json.Serialization;

namespace ControllerMerger.Core;

public enum ThemePreference { System = 0, Light = 1, Dark = 2 }

/// <summary>UI language. Not set (null) = follow the Windows display language.</summary>
public enum UiLanguage { German = 0, English = 1 }

/// <summary>
/// Persistente Benutzereinstellungen (JSON unter %APPDATA%\K26KControllerMerger\settings.json).
/// Fehlende Felder fallen auf Standardwerte zurück, ungültige Werte werden in
/// <see cref="Normalize"/> korrigiert – eine kaputte Datei darf den Start nie verhindern.
/// </summary>
public sealed class AppSettings
{
    /// <summary>Erlaubte Abfrage-Intervalle in Millisekunden.</summary>
    public static readonly int[] PollingIntervals = [1, 2, 4, 8];

    public int StickDeadzonePercent { get; set; } = 8;
    public int TriggerDeadzonePercent { get; set; } = 4;
    public StickMergeMode StickMode { get; set; } = StickMergeMode.PerStick;
    public bool InvertLeftX { get; set; }
    public bool InvertLeftY { get; set; }
    public bool InvertRightX { get; set; }
    public bool InvertRightY { get; set; }
    public int PollingIntervalMs { get; set; } = 4;
    public bool RumbleEnabled { get; set; } = true;
    public bool Autostart { get; set; }
    public ThemePreference Theme { get; set; } = ThemePreference.System;

    /// <summary>Explicitly chosen UI language; null = Windows display language.</summary>
    public UiLanguage? Language { get; set; }

    /// <summary>Wurde die Ersteinrichtung (ViGEmBus) einmal erfolgreich abgeschlossen?</summary>
    public bool SetupCompleted { get; set; }

    /// <summary>Geräteschlüssel der Controller, deren Eingaben NICHT übernommen werden.</summary>
    public List<string> DisabledDevices { get; set; } = [];

    /// <summary>
    /// Windows-Geräteinstanz-IDs der Controller, die per HidHide vor anderen Programmen versteckt werden
    /// (nur solange die App läuft).
    /// </summary>
    public List<string> HiddenDevices { get; set; } = [];

    public AppSettings Normalize()
    {
        StickDeadzonePercent = Math.Clamp(StickDeadzonePercent, 0, 40);
        TriggerDeadzonePercent = Math.Clamp(TriggerDeadzonePercent, 0, 40);
        if (!Enum.IsDefined(StickMode)) StickMode = StickMergeMode.PerStick;
        if (!Enum.IsDefined(Theme)) Theme = ThemePreference.System;
        if (Language is { } lang && !Enum.IsDefined(lang)) Language = null;
        if (!PollingIntervals.Contains(PollingIntervalMs)) PollingIntervalMs = 4;
        DisabledDevices = (DisabledDevices ?? []).Where(k => !string.IsNullOrWhiteSpace(k)).Distinct().ToList();
        HiddenDevices = (HiddenDevices ?? []).Where(k => !string.IsNullOrWhiteSpace(k))
            .Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        return this;
    }

    public MergeOptions ToMergeOptions() => new()
    {
        StickDeadzone = StickDeadzonePercent / 100f,
        TriggerDeadzone = TriggerDeadzonePercent / 100f,
        StickMode = StickMode,
        Inversion = (InvertLeftX ? AxisInversion.LeftX : 0) | (InvertLeftY ? AxisInversion.LeftY : 0) |
                    (InvertRightX ? AxisInversion.RightX : 0) | (InvertRightY ? AxisInversion.RightY : 0),
    };

    public AppSettings Clone() => FromJson(ToJson());

    public string ToJson() => JsonSerializer.Serialize(this, SettingsJsonContext.Default.AppSettings);

    public static AppSettings FromJson(string json)
    {
        try { return (JsonSerializer.Deserialize(json, SettingsJsonContext.Default.AppSettings) ?? new()).Normalize(); }
        catch (JsonException) { return new AppSettings().Normalize(); }
    }

    /// <summary>Lädt die Einstellungen; fehlende oder defekte Dateien ergeben Standardwerte.</summary>
    public static AppSettings Load(string path)
    {
        try { return File.Exists(path) ? FromJson(File.ReadAllText(path)) : new AppSettings().Normalize(); }
        catch (IOException) { return new AppSettings().Normalize(); }
        catch (UnauthorizedAccessException) { return new AppSettings().Normalize(); }
    }

    /// <summary>Atomar speichern (temp-Datei + Replace), damit ein Absturz nie eine halbe Datei hinterlässt.</summary>
    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, ToJson());
        File.Move(tmp, path, overwrite: true);
    }
}

[JsonSourceGenerationOptions(WriteIndented = true, UseStringEnumConverter = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
[JsonSerializable(typeof(AppSettings))]
internal partial class SettingsJsonContext : JsonSerializerContext;
