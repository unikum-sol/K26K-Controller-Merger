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
        Loc.Changed += OnLanguageChanged;
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
        ? Loc.T("ProblemDriverTitle")
        : Loc.T("ProblemStoppedTitle");

    public string ProblemText => _snapshot.State == EngineState.DriverMissing
        ? Loc.T("ProblemDriverText")
        : _snapshot.Message ?? Loc.T("ProblemUnknownText");

    public string StatusText => _snapshot.State switch
    {
        EngineState.DriverMissing or EngineState.Error => Loc.T("StError"),
        EngineState.Running when _snapshot.VirtualSlot < 0 => Loc.T("StConnecting"),
        EngineState.Running when ActiveCount > 0 => Loc.T("StActive"),
        EngineState.Running when PadCount > 0 => Loc.T("StPaused"),
        EngineState.Running => Loc.T("StReady"),
        _ => Loc.T("StStarting"),
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
        EngineState.Running when PadCount == 0 => Loc.T("HdWaiting"),
        EngineState.Running when ActiveCount == 0 => Loc.T("HdAllPaused"),
        EngineState.Running when ActiveCount == PadCount => ActiveCount == 1 ? Loc.T("HdOneActive") : Loc.F("HdNActive", ActiveCount),
        EngineState.Running => Loc.F("HdSomeActive", ActiveCount, PadCount),
        EngineState.DriverMissing => Loc.T("HdDriverMissing"),
        EngineState.Error => Loc.T("HdStopped"),
        _ => Loc.T("HdStarting"),
    };

    /// <summary>Was Spiele sehen – in Laiensprache ("Spieler 2" statt "XInput-Slot 1").</summary>
    public string VirtualDetail => _snapshot.State switch
    {
        EngineState.Running when Connected => Loc.F("VdVisibleAs", _snapshot.VirtualSlot + 1),
        EngineState.Running => Loc.T("VdConnecting"),
        EngineState.Starting => Loc.T("VdStarting"),
        _ => Loc.T("VdNone"),
    };

    public string PlayerText => Connected ? Loc.F("Player", _snapshot.VirtualSlot + 1) : "–";

    public string PadsSummary => PadCount switch
    {
        0 => Loc.T("PsNone"),
        1 => Loc.T("PsOne"),
        var n => Loc.F("PsN", n),
    };

    public string RateText => _snapshot.RateHz > 0 ? $"{_snapshot.RateHz:0} Hz" : "–";

    public string DriverVersionText => _driver.IsOk
        ? _driver.Version ?? Loc.T("DrvInstalled")
        : _driver.Status switch
        {
            DriverStatus.NotInstalled => Loc.T("DrvNotInstalled"),
            DriverStatus.VersionMismatch => Loc.T("DrvOutdated"),
            DriverStatus.AccessFailed => Loc.T("DrvInactive"),
            _ => Loc.T("DrvUnknown"),
        };

    public bool DriverOk => _driver.IsOk;

    public string SdlVersionText => _snapshot.SdlVersion;

    public string AppVersionText =>
        Assembly.GetExecutingAssembly().GetName().Version is { } v ? Loc.F("Version", $"{v.Major}.{v.Minor}.{v.Build}") : "";

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

    /// <summary>Effective UI language (explicit choice or Windows display language).</summary>
    public UiLanguage Language
    {
        get => S.Language ?? Loc.SystemDefault;
        set { if (Language != value || S.Language is null) Change(s => s.Language = value, nameof(Language)); }
    }

    /// <summary>After a language switch: recompute all computed texts (pad details etc.) and refresh every binding.</summary>
    private void OnLanguageChanged()
    {
        Update(_snapshot);
        Raise(string.Empty);
    }
}
