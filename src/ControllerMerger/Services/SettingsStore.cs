using ControllerMerger.Core;

namespace ControllerMerger.Services;

/// <summary>
/// Hält die aktuelle <see cref="AppSettings"/>-Instanz, benachrichtigt Abonnenten bei Änderungen
/// und speichert verzögert (300 ms), damit Slider-Bewegungen nicht hunderte Schreibvorgänge erzeugen.
/// </summary>
internal sealed class SettingsStore : IDisposable
{
    private readonly Timer _saveTimer;
    private readonly object _gate = new();
    private bool _dirty;

    public static string DefaultPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ControllerMerger", "settings.json");

    public string FilePath { get; }

    public AppSettings Current { get; }

    /// <summary>Wird auf dem Thread ausgelöst, der <see cref="Update"/> aufgerufen hat (UI-Thread).</summary>
    public event Action<AppSettings>? Changed;

    public SettingsStore(string? path = null, bool persist = true)
    {
        FilePath = path ?? DefaultPath;
        Current = persist ? AppSettings.Load(FilePath) : new AppSettings().Normalize();
        Persist = persist;
        _saveTimer = new Timer(_ => Flush(), null, Timeout.Infinite, Timeout.Infinite);
    }

    /// <summary>false für Vorschau-/Screenshot-Modus: nichts wird auf die Platte geschrieben.</summary>
    public bool Persist { get; }

    public void Update(Action<AppSettings> mutate)
    {
        lock (_gate)
        {
            mutate(Current);
            Current.Normalize();
            _dirty = true;
        }
        Changed?.Invoke(Current);
        if (Persist) _saveTimer.Change(300, Timeout.Infinite);
    }

    public void Flush()
    {
        if (!Persist) return;
        lock (_gate)
        {
            if (!_dirty) return;
            try
            {
                Current.Save(FilePath);
                _dirty = false;
            }
            catch (Exception ex)
            {
                Log.Error("Einstellungen konnten nicht gespeichert werden", ex);
            }
        }
    }

    public void Dispose()
    {
        _saveTimer.Dispose();
        Flush();
    }
}
