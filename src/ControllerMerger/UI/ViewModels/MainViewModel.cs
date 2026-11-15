using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Input;
using ControllerMerger.Core;
using ControllerMerger.Engine;
using ControllerMerger.Services;
using ControllerMerger.Setup;

namespace ControllerMerger.UI.ViewModels;

/// <summary>
/// Gemeinsames ViewModel für Status-Popup und Einstellungsfenster.
/// Status kommt aus <see cref="EngineSnapshot"/>s, Einstellungen werden direkt im <see cref="SettingsStore"/> geändert.
/// </summary>
internal sealed class MainViewModel : ObservableObject
{
    private readonly SettingsStore _store;
    private EngineSnapshot _snapshot = EngineSnapshot.Initial;
    private DriverInfo _driver = new(DriverStatus.Unknown, null, "");

    public MainViewModel(SettingsStore store, Action openSettings, Action exit, Action? repairDriver = null)
    {
        _store = store;
        OpenSettingsCommand = new RelayCommand(openSettings);
        ExitCommand = new RelayCommand(exit);
        RepairDriverCommand = new RelayCommand(() => repairDriver?.Invoke(), () => repairDriver is not null);
        OpenLogCommand = new RelayCommand(() =>
        {
            try
            {
                Directory.CreateDirectory(Log.Directory);
                Process.Start(new ProcessStartInfo(File.Exists(Log.FilePath) ? Log.FilePath : Log.Directory) { UseShellExecute = true });
            }
            catch (Exception ex) { Log.Error("Logdatei öffnen", ex); }
        });
    }

    public ICommand OpenSettingsCommand { get; }
    public ICommand ExitCommand { get; }
    public ICommand OpenLogCommand { get; }
    public ICommand RepairDriverCommand { get; }

    public ObservableCollection<PadViewModel> Pads { get; } = [];

    // ------------------------------------------------------------------ Status
    // Grundsatz (Design-Council R1): jede Information genau einmal, Zustände ehrlich und widerspruchsfrei.

    private int PadCount => _snapshot.Pads.Count;
    private int ActiveCount => _snapshot.ActivePadCount;
    private bool Connected => _snapshot.State == EngineState.Running && _snapshot.VirtualSlot >= 0;

    public bool HasPads => Pads.Count > 0;
    public bool HasActivePads => ActiveCount > 0;

    /// <summary>Treiber- oder Engine-Problem: Listen ausblenden, genau eine Meldung + eine Handlung zeigen.</summary>
    public bool HasProblem => _snapshot.State is EngineState.DriverMissing or EngineState.Error;
    public bool ShowContent => !HasProblem;
    public bool CanRepair => _snapshot.State == EngineState.DriverMissing;

    public string ProblemTitle => _snapshot.State == EngineState.DriverMissing
        ? "Der Controller-Treiber fehlt"
        : "Zusammenführen gestoppt";

    public string ProblemText => _snapshot.State == EngineState.DriverMissing
        ? "Ohne diesen Treiber kann kein virtueller Controller erstellt werden."
        : _snapshot.Message ?? "Ein unerwarteter Fehler ist aufgetreten. Details stehen in der Logdatei.";

    public string StatusText => _snapshot.State switch
    {
        EngineState.DriverMissing or EngineState.Error => "Fehler",
        EngineState.Running when _snapshot.VirtualSlot < 0 => "Verbindet",
        EngineState.Running when ActiveCount > 0 => "Aktiv",
        EngineState.Running when PadCount > 0 => "Pausiert",
        EngineState.Running => "Bereit",
        _ => "Startet",
    };

    public BadgeKind StatusKind => _snapshot.State switch
    {
        EngineState.DriverMissing or EngineState.Error => BadgeKind.Danger,
        EngineState.Running when _snapshot.VirtualSlot < 0 => BadgeKind.Warning,
        EngineState.Running when ActiveCount > 0 => BadgeKind.Success,
        _ => BadgeKind.Neutral,
    };

    /// <summary>Kurzform für den Popup-Kopf: "2 von 3 Controllern aktiv".</summary>
    public string HeaderSummary => _snapshot.State switch
    {
        EngineState.Running when PadCount == 0 => "Wartet auf Controller",
        EngineState.Running when ActiveCount == 0 => "Alle Controller pausiert",
        EngineState.Running when ActiveCount == PadCount => ActiveCount == 1 ? "1 Controller aktiv" : $"{ActiveCount} Controller aktiv",
        EngineState.Running => $"{ActiveCount} von {PadCount} Controllern aktiv",
        EngineState.DriverMissing => "Controller-Treiber fehlt",
        EngineState.Error => "Zusammenführen gestoppt",
        _ => "Wird gestartet …",
    };

    /// <summary>Was Spiele sehen – in Laiensprache ("Spieler 2" statt "XInput-Slot 1").</summary>
    public string VirtualDetail => _snapshot.State switch
    {
        EngineState.Running when Connected => $"Für Spiele sichtbar als Spieler {_snapshot.VirtualSlot + 1}",
        EngineState.Running => "Wird mit Windows verbunden …",
        EngineState.Starting => "Wird eingerichtet …",
        _ => "Spiele sehen gerade keinen Controller",
    };

    public string PlayerText => Connected ? $"Spieler {_snapshot.VirtualSlot + 1}" : "–";

    public string PadsSummary => PadCount switch
    {
        0 => "Keine verbunden",
        1 => "1 verbunden",
        var n => $"{n} verbunden",
    };

    public string RateText => _snapshot.RateHz > 0 ? $"{_snapshot.RateHz:0} Hz" : "–";

    public string DriverVersionText => _driver.IsOk
        ? _driver.Version ?? "installiert"
        : _driver.Status switch
        {
            DriverStatus.NotInstalled => "Nicht installiert",
            DriverStatus.VersionMismatch => "Veraltet",
            DriverStatus.AccessFailed => "Nicht aktiv",
            _ => "Unbekannt",
        };

    public bool DriverOk => _driver.IsOk;

    public string SdlVersionText => _snapshot.SdlVersion;

    public string AppVersionText =>
        Assembly.GetExecutingAssembly().GetName().Version is { } v ? $"Version {v.Major}.{v.Minor}.{v.Build}" : "";

    public void Update(EngineSnapshot snapshot)
    {
        _snapshot = snapshot;

        // Pads abgleichen (Reihenfolge = Verbindungsreihenfolge, stabile Instanzen → kein Flackern).
        for (int i = Pads.Count - 1; i >= 0; i--)
            if (!snapshot.Pads.Any(p => p.Key == Pads[i].Key))
                Pads.RemoveAt(i);
        foreach (var p in snapshot.Pads)
        {
            var vm = Pads.FirstOrDefault(x => x.Key == p.Key);
            if (vm is null)
            {
                vm = new PadViewModel(p.Key, SetPadEnabled);
                Pads.Add(vm);
            }
            vm.Update(p);
        }
        for (int i = 0; i < Pads.Count; i++) Pads[i].IsFirst = i == 0;

        foreach (var name in StatusProperties) Raise(name);
    }

    public void UpdateDriver(DriverInfo driver)
    {
        _driver = driver;
        Raise(nameof(DriverVersionText));
        Raise(nameof(DriverOk));
    }

    private static readonly string[] StatusProperties =
    [
        nameof(HasPads), nameof(HasActivePads), nameof(HasProblem), nameof(ShowContent), nameof(CanRepair),
        nameof(ProblemTitle), nameof(ProblemText), nameof(StatusText), nameof(StatusKind), nameof(HeaderSummary),
        nameof(VirtualDetail), nameof(PlayerText), nameof(PadsSummary), nameof(RateText), nameof(SdlVersionText),
    ];

    private void SetPadEnabled(string key, bool enabled) => _store.Update(s =>
    {
        s.DisabledDevices.Remove(key);
        if (!enabled) s.DisabledDevices.Add(key);
    });

    // ------------------------------------------------------------------ Einstellungen

    private AppSettings S => _store.Current;

    private void Change(Action<AppSettings> mutate, params string[] alsoRaise)
    {
        _store.Update(mutate);
        foreach (var n in alsoRaise) Raise(n);
    }

    public int StickDeadzone
    {
        get => S.StickDeadzonePercent;
        set { if (S.StickDeadzonePercent != value) Change(s => s.StickDeadzonePercent = value, nameof(StickDeadzone)); }
    }

    public int TriggerDeadzone
    {
        get => S.TriggerDeadzonePercent;
        set { if (S.TriggerDeadzonePercent != value) Change(s => s.TriggerDeadzonePercent = value, nameof(TriggerDeadzone)); }
    }

    public StickMergeMode StickMode
    {
        get => S.StickMode;
        set { if (S.StickMode != value) Change(s => s.StickMode = value, nameof(StickMode)); }
    }

    public bool InvertLeftX { get => S.InvertLeftX; set => Change(s => s.InvertLeftX = value, nameof(InvertLeftX)); }
    public bool InvertLeftY { get => S.InvertLeftY; set => Change(s => s.InvertLeftY = value, nameof(InvertLeftY)); }
    public bool InvertRightX { get => S.InvertRightX; set => Change(s => s.InvertRightX = value, nameof(InvertRightX)); }
    public bool InvertRightY { get => S.InvertRightY; set => Change(s => s.InvertRightY = value, nameof(InvertRightY)); }

    public int PollingIntervalMs
    {
        get => S.PollingIntervalMs;
        set { if (S.PollingIntervalMs != value) Change(s => s.PollingIntervalMs = value, nameof(PollingIntervalMs)); }
    }

    public bool RumbleEnabled
    {
        get => S.RumbleEnabled;
        set => Change(s => s.RumbleEnabled = value, nameof(RumbleEnabled));
    }

    public bool Autostart
    {
        get => S.Autostart;
        set => Change(s => s.Autostart = value, nameof(Autostart));
    }

    public ThemePreference Theme
    {
        get => S.Theme;
        set { if (S.Theme != value) Change(s => s.Theme = value, nameof(Theme)); }
    }
}
