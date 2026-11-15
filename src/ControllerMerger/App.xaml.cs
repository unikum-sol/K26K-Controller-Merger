using System.Windows;
using System.Windows.Threading;
using ControllerMerger.Core;
using ControllerMerger.Engine;
using ControllerMerger.Services;
using ControllerMerger.Setup;
using ControllerMerger.UI;
using ControllerMerger.UI.ViewModels;

namespace ControllerMerger;

/// <summary>
/// Einstiegspunkt. Ablauf:
/// <list type="number">
/// <item>Sonder-Modi (--snapshot, --export-icon) für Entwicklung/Design-Council.</item>
/// <item>Einzelinstanz sicherstellen (zweiter Start öffnet die Einstellungen der laufenden Instanz).</item>
/// <item>ViGEmBus prüfen → bei Bedarf Einrichtungsassistent.</item>
/// <item>Engine starten, Tray-Symbol anzeigen. Fenster werden nur bei Bedarf erzeugt.</item>
/// </list>
/// </summary>
public partial class App : Application
{
    private const string MutexName = @"Local\K26KControllerMerger.Instance";
    private const string ShowEventName = @"Local\K26KControllerMerger.ShowSettings";

    private Mutex? _mutex;
    private EventWaitHandle? _showEvent;
    private SettingsStore? _store;
    private InputService? _engine;
    private TrayController? _tray;
    private MainViewModel? _vm;
    private SettingsWindow? _settings;
    private int _openPopups;
    private DispatcherTimer? _uiTimer;
    private ThemePreference _appliedTheme;
    private bool _appliedAutostart;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // ---------------------------------------------------------- Entwickler-Modi
        int snap = Array.IndexOf(e.Args, "--snapshot");
        if (snap >= 0)
        {
            SnapshotRenderer.Run(e.Args.Length > snap + 1 ? e.Args[snap + 1] : "snapshots");
            Shutdown();
            return;
        }
        int icon = Array.IndexOf(e.Args, "--export-icon");
        if (icon >= 0)
        {
            File.WriteAllBytes(e.Args.Length > icon + 1 ? e.Args[icon + 1] : "AppIcon.ico", IconFactory.CreateAppIcoBytes());
            Shutdown();
            return;
        }

        // ---------------------------------------------------------- Einzelinstanz
        _mutex = new Mutex(true, MutexName, out bool isFirst);
        if (!isFirst)
        {
            try { EventWaitHandle.OpenExisting(ShowEventName).Set(); } catch (Exception) { }
            _mutex.Dispose();
            _mutex = null;
            Shutdown();
            return;
        }
        _showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        ThreadPool.RegisterWaitForSingleObject(_showEvent, (_, _) => Dispatcher.BeginInvoke(OpenSettings), null, -1, false);

        DispatcherUnhandledException += (_, ex) =>
        {
            Log.Error("Unbehandelter UI-Fehler", ex.Exception);
            ex.Handled = true;
        };
        AppDomain.CurrentDomain.UnhandledException += (_, ex) => Log.Error("Unbehandelter Fehler", ex.ExceptionObject as Exception);

        Log.Info($"Start {string.Join(' ', e.Args)}");
        _store = new SettingsStore();
        _appliedTheme = _store.Current.Theme;
        ThemeManager.Apply(_appliedTheme);
        Loc.SetLanguage(_store.Current.Language ?? Loc.SystemDefault);

        // ---------------------------------------------------------- Treiber / Ersteinrichtung
        var driver = DriverCheck.Check();
        if (!driver.IsOk)
        {
            Log.Warn($"ViGEmBus nicht verfügbar: {driver.Status} – {driver.Detail}");
            var setupVm = new SetupViewModel(driver, firstRun: !_store.Current.SetupCompleted);
            bool ok = new SetupWindow(setupVm).ShowDialog() == true;
            if (!ok)
            {
                Shutdown();
                return;
            }
            driver = setupVm.Driver;
        }
        if (!_store.Current.SetupCompleted) _store.Update(s => s.SetupCompleted = true);

        // Autostart-Eintrag reparieren, falls die EXE verschoben wurde.
        _appliedAutostart = _store.Current.Autostart;
        if (_appliedAutostart && !Autostart.IsEnabled()) Autostart.Set(true);

        // ---------------------------------------------------------- Engine + Tray
        _engine = new InputService();
        _engine.ApplySettings(_store.Current);
        _engine.StatusChanged += s => Dispatcher.BeginInvoke(() => _tray?.Update(s));
        _engine.Start();

        _vm = new MainViewModel(_store, OpenSettings, () => Shutdown(), RepairDriver);
        _vm.UpdateDriver(driver);
        _store.Changed += OnSettingsChanged;

        _tray = new TrayController(CreatePopup, OpenSettings);
        _tray.Update(_engine.Snapshot);
    }

    private StatusPopup CreatePopup()
    {
        var popup = new StatusPopup { DataContext = _vm };
        _vm!.Update(_engine!.Snapshot);
        _openPopups++;
        popup.Closed += (_, _) => { _openPopups--; UpdateUiTimer(); };
        UpdateUiTimer();
        return popup;
    }

    private void OpenSettings()
    {
        if (_vm is null || _engine is null) return;
        if (_settings is null)
        {
            _settings = new SettingsWindow { DataContext = _vm };
            _settings.Closed += (_, _) => { _settings = null; UpdateUiTimer(); };
            _vm.UpdateDriver(DriverCheck.Check());
        }
        _vm.Update(_engine.Snapshot);
        _settings.Show();
        if (_settings.WindowState == WindowState.Minimized) _settings.WindowState = WindowState.Normal;
        _settings.Activate();
        UpdateUiTimer();
    }

    /// <summary>Treiber fehlt zur Laufzeit → Assistent im Reparaturmodus. Die Engine verbindet sich danach selbst neu.</summary>
    private void RepairDriver()
    {
        var driver = DriverCheck.Check();
        if (!driver.IsOk)
        {
            var setupVm = new SetupViewModel(driver, firstRun: false);
            new SetupWindow(setupVm).ShowDialog();
            driver = setupVm.Driver;
        }
        _vm?.UpdateDriver(driver);
    }

    /// <summary>UI-Aktualisierung (4 Hz) nur, solange ein Fenster offen ist – im Ruhezustand null UI-Last.</summary>
    private void UpdateUiTimer()
    {
        bool needed = _settings is not null || _openPopups > 0;
        if (needed && _uiTimer is null)
        {
            _uiTimer = new DispatcherTimer(TimeSpan.FromMilliseconds(250), DispatcherPriority.Background,
                (_, _) => _vm?.Update(_engine!.Snapshot), Dispatcher);
            _uiTimer.Start();
        }
        else if (!needed && _uiTimer is not null)
        {
            _uiTimer.Stop();
            _uiTimer = null;
        }
    }

    private void OnSettingsChanged(AppSettings s)
    {
        _engine?.ApplySettings(s);
        Loc.SetLanguage(s.Language ?? Loc.SystemDefault); // no-op if unchanged; switches live otherwise
        if (s.Theme != _appliedTheme)
        {
            _appliedTheme = s.Theme;
            ThemeManager.Apply(s.Theme);
        }
        if (s.Autostart != _appliedAutostart)
        {
            _appliedAutostart = s.Autostart;
            Autostart.Set(s.Autostart);
        }
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _tray?.Dispose();
        _engine?.Dispose();
        _store?.Dispose();
        _showEvent?.Dispose();
        if (_mutex is not null)
        {
            try { _mutex.ReleaseMutex(); } catch (Exception) { }
            _mutex.Dispose();
        }
        Log.Info("Beendet.");
        base.OnExit(e);
    }
}
