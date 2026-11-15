using ControllerMerger.Engine;

namespace ControllerMerger.UI.ViewModels;

/// <summary>Ein physischer Controller in Popup und Einstellungsfenster.</summary>
public sealed class PadViewModel(string key, Action<string, bool> setEnabled) : ObservableObject
{
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
            setEnabled(Key, value);
        }
    }

    public string StateText => Loc.T(IsEnabled ? "Active" : "Paused");

    /// <summary>Detailzeile für pausierte Controller: "Pausiert · PlayStation · …".</summary>
    public string PausedDetail => Loc.T("Paused") + " · " + Detail;

    public void Update(PadSnapshot p)
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

        if (_isEnabled != p.Enabled)
        {
            _isEnabled = p.Enabled;
            Raise(nameof(IsEnabled));
            Raise(nameof(StateText));
        }
    }
}
