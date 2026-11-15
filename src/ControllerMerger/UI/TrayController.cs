using System.Windows;
using ControllerMerger.Engine;
using Hardcodet.Wpf.TaskbarNotification;

namespace ControllerMerger.UI;

/// <summary>
/// Tray-Symbol: Rechtsklick → Status-Popup, Doppelklick → Einstellungen.
/// Symbol und Tooltip spiegeln den Engine-Status (aktiv / kein Controller / Problem).
/// </summary>
internal sealed class TrayController : IDisposable
{
    private readonly TaskbarIcon _icon;
    private readonly Func<StatusPopup> _createPopup;
    private readonly Action _openSettings;
    private StatusPopup? _popup;
    private TrayState? _state;
    private bool? _lightTaskbar;
    private System.Drawing.Icon? _currentIcon;

    public TrayController(Func<StatusPopup> createPopup, Action openSettings)
    {
        _createPopup = createPopup;
        _openSettings = openSettings;
        _icon = new TaskbarIcon { NoLeftClickDelay = true, ToolTipText = "ControllerMerger" };
        _icon.TrayRightMouseUp += (_, _) => ShowPopup();
        _icon.TrayMouseDoubleClick += (_, _) => { ClosePopup(); _openSettings(); };
        ThemeManager.Changed += () => Update(_lastSnapshot, force: true);
    }

    private EngineSnapshot _lastSnapshot = EngineSnapshot.Initial;

    public void Update(EngineSnapshot snap, bool force = false)
    {
        _lastSnapshot = snap;
        var state = snap.State switch
        {
            EngineState.Running when snap.VirtualSlot >= 0 && snap.ActivePadCount > 0 => TrayState.Active,
            EngineState.Running or EngineState.Starting => TrayState.Idle,
            _ => TrayState.Problem,
        };
        bool light = ThemeManager.TaskbarIsLight();
        if (force || state != _state || light != _lightTaskbar)
        {
            _state = state;
            _lightTaskbar = light;
            var old = _currentIcon;
            _currentIcon = IconFactory.CreateTrayIcon(state, light);
            _icon.Icon = _currentIcon;
            old?.Dispose();
        }

        _icon.ToolTipText = snap.State switch
        {
            EngineState.Running => snap.Pads.Count switch
            {
                0 => "ControllerMerger – kein Controller verbunden",
                1 => "ControllerMerger – 1 Controller aktiv",
                var n => $"ControllerMerger – {snap.ActivePadCount} von {n} Controllern aktiv",
            },
            EngineState.DriverMissing => "ControllerMerger – ViGEmBus nicht verfügbar",
            EngineState.Error => "ControllerMerger – Fehler",
            _ => "ControllerMerger – startet …",
        };
    }

    private void ShowPopup()
    {
        if (_popup is not null) { ClosePopup(); return; }
        _popup = _createPopup();
        _popup.Closed += (_, _) => _popup = null;
        _popup.ShowNearCursor();
    }

    private void ClosePopup()
    {
        _popup?.Close();
        _popup = null;
    }

    public void Dispose()
    {
        ClosePopup();
        _icon.Dispose();
        _currentIcon?.Dispose();
    }
}
