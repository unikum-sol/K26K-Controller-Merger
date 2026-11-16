using ControllerMerger.Engine;
using ControllerMerger.HidHide;

namespace ControllerMerger.UI.ViewModels;

/// <summary>Ein physischer Controller in Popup und Einstellungsfenster.</summary>
public sealed class PadViewModel(string key, Action<string, bool> setEnabled, Action<string, bool> setHidden) : ObservableObject
{
    private bool _isHidden;
    private bool _canHide;
    private string? _instanceId;
    private string _hideTip = "";

    private string _name = "";
    private string _detail = "";
    private string _family = "";
    private bool _isEnabled = true;
    private bool _isFirst;

    public string Key { get; } = key;

    public string Name { get => _name; private set => Set(ref _name, value); }

    /// <summary>z. B. "PlayStation · Bluetooth · Akku 80 %"</summary>
    public string Detail { get => _detail; private set => Set(ref _detail, value); }

    public string Family { get => _family; private set => Set(ref _family, value); }

    private string _monogram = "";

    /// <summary>Kurzzeichen der Controller-Familie für die Kachel ("" = generisches Gamepad-Symbol).</summary>
    public string Monogram { get => _monogram; private set => Set(ref _monogram, value); }

    /// <summary>Erstes Element einer Liste (keine Trennlinie darüber).</summary>
    public bool IsFirst { get => _isFirst; set => Set(ref _isFirst, value); }

    /// <summary>"Eingaben übernehmen" – Änderung wird sofort in die Einstellungen geschrieben.</summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set
        {
            if (!Set(ref _isEnabled, value)) return;
            Raise(nameof(StateText));
            Raise(nameof(PopupTag));
            Raise(nameof(HasPopupTag));
            setEnabled(Key, value);
        }
    }

    public string StateText => Loc.T(IsEnabled ? "Active" : "Paused");

    /// <summary>"Verstecken" – über HidHide für alle anderen Programme unsichtbar (nur solange die App läuft).</summary>
    public bool IsHidden
    {
        get => _isHidden;
        set
        {
            if (!_canHide || _instanceId is null || !Set(ref _isHidden, value)) return;
            Raise(nameof(PopupTag));
            Raise(nameof(HasPopupTag));
            setHidden(_instanceId, value);
        }
    }

    /// <summary>Lässt sich dieser Controller gerade verstecken (HidHide bereit, Gerät eindeutig zugeordnet)?</summary>
    public bool CanHide { get => _canHide; private set => Set(ref _canHide, value); }

    public string HideTip { get => _hideTip; private set => Set(ref _hideTip, value); }

    private bool _showHideSwitch, _showNotHideable;

    /// <summary>Schalter "Verstecken" nur, wenn er auch wirkt – sonst keine toten Bedienelemente.</summary>
    public bool ShowHideSwitch { get => _showHideSwitch; private set => Set(ref _showHideSwitch, value); }

    /// <summary>HidHide ist bereit, dieser Controller lässt sich aber nicht eindeutig zuordnen → sichtbarer Grund.</summary>
    public bool ShowNotHideable { get => _showNotHideable; private set => Set(ref _showNotHideable, value); }

    /// <summary>Rechte Statusangabe im Popup: "Pausiert", "Versteckt" oder beides.</summary>
    public string PopupTag => (IsEnabled, IsHidden) switch
    {
        (false, true) => Loc.T("Paused") + " · " + Loc.T("HiddenTag"),
        (false, false) => Loc.T("Paused"),
        (true, true) => Loc.T("HiddenTag"),
        _ => "",
    };

    public bool HasPopupTag => !IsEnabled || IsHidden;


    /// <summary>Detailzeile für pausierte Controller: "Pausiert · PlayStation · …".</summary>
    public string PausedDetail => Loc.T("Paused") + " · " + Detail;

    public void Update(PadSnapshot p, PadHideInfo? hide, HidHideStatus hideStatus, IReadOnlyCollection<string> hidden)
    {
        Name = p.Name;
        Family = PadNames.Family(p.Kind);
        Monogram = PadNames.Monogram(p.Kind);

        var parts = new List<string>(3) { Family };
        parts.Add(p.Connection switch
        {
            ConnectionKind.Usb => Loc.T("ConnUsb"),
            ConnectionKind.Bluetooth => Loc.T("ConnBt"),
            _ => Loc.T("ConnOther"),
        });
        if (p.BatteryPercent >= 0)
            parts.Add(Loc.F(p.Charging ? "BatCharging" : "Battery", p.BatteryPercent));
        if (!p.HasRumble) parts.Add(Loc.T("NoRumble"));
        Detail = string.Join(" · ", parts);
        Raise(nameof(PausedDetail));

        _instanceId = hide?.InstanceId;
        CanHide = hide?.CanHide == true;
        HideTip = Loc.T(CanHide ? "PadHideTip"
            : hideStatus is HidHideStatus.NotInstalled or HidHideStatus.RestartRequired ? "PadHideNeedsHidHideTip"
            : hideStatus == HidHideStatus.Ready && hide is not null && hide.InstanceId is null ? "PadHideUnavailableTip"
            : "PadHideTip");
        // Nur anzeigen, was tatsächlich wirkt: ohne bereites HidHide ist nichts versteckt.
        bool isHidden = CanHide && _instanceId is not null && hidden.Contains(_instanceId, StringComparer.OrdinalIgnoreCase);
        if (_isHidden != isHidden)
        {
            _isHidden = isHidden;
            Raise(nameof(IsHidden));
        }
        ShowHideSwitch = CanHide;
        ShowNotHideable = hideStatus == HidHideStatus.Ready && hide is not null && hide.InstanceId is null;
        Raise(nameof(PopupTag));
        Raise(nameof(HasPopupTag));

        if (_isEnabled != p.Enabled)
        {
            _isEnabled = p.Enabled;
            Raise(nameof(IsEnabled));
            Raise(nameof(StateText));
        }
    }
}
