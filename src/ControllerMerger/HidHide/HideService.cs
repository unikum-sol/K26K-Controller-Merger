using System.Collections.Frozen;
using System.Text.Json;
using ControllerMerger.Core;
using ControllerMerger.Engine;
using ControllerMerger.Services;

namespace ControllerMerger.HidHide;

/// <summary>Kann ein bestimmter Controller versteckt werden, und unter welcher Geräte-ID?</summary>
public sealed record PadHideInfo(string? InstanceId, bool CanHide);

/// <summary>Was die Oberfläche über das Verstecken wissen muss (Echtbetrieb: <see cref="HideService"/>).</summary>
public interface IHideSource
{
    HidHideStatus Status { get; }

    /// <summary>Lokalisierungsschlüssel eines Problems, das das Verstecken gerade verhindert (sonst null).</summary>
    string? ProblemKey { get; }

    PadHideInfo? Get(string padKey);

    /// <summary>Wird auf einem Hintergrund-Thread ausgelöst.</summary>
    event Action? Changed;

    /// <summary>Status neu prüfen (z. B. nach der Installation von HidHide).</summary>
    void Refresh();
}

/// <summary>
/// Versteckt Controller über HidHide, solange die App läuft (Nutzerentscheidung 1.3).
/// <para>
/// Alles läuft auf EINEM eigenen Worker-Thread: Geräte-IDs auflösen, HidHide-Listen lesen und schreiben.
/// Änderungen werden kurz gesammelt und dann in einer einzigen Sitzung angewendet. Der Input-Thread wird nie berührt.
/// </para>
/// <para>
/// Ein Journal (<c>%LOCALAPPDATA%\K26KControllerMerger\hidhide.json</c>) hält fest, was die App selbst in HidHide
/// eingetragen hat – und zwar VOR jedem Schreibzugriff. Nach einem Absturz räumt der nächste Start damit auf;
/// beim Beenden werden genau diese Einträge wieder entfernt. Fremde Einträge bleiben immer unangetastet.
/// </para>
/// </summary>
internal sealed class HideService : IHideSource, IDisposable
{
    private static readonly TimeSpan OpenTimeout = TimeSpan.FromSeconds(1);
    private const int DebounceMs = 150;
    private const int RetryMs = 3000;

    private readonly object _gate = new();
    private readonly object _applyGate = new();
    private readonly AutoResetEvent _wake = new(false);
    private readonly Thread _thread;
    private readonly string _journalPath;
    private readonly Journal _journal;
    private volatile bool _stop;

    // Eingänge (unter _gate)
    private EngineSnapshot _snapshot = EngineSnapshot.Initial;
    private bool _padsDirty = true;
    private bool _refresh = true;
    private string[] _userHidden = [];

    // Nur Worker:
    private Dictionary<string, string?> _resolved = new();
    private HashSet<string> _resolvedSafe = new(StringComparer.OrdinalIgnoreCase);
    private HashSet<string> _neverBlock = new(StringComparer.OrdinalIgnoreCase);

    // Ausgänge
    private volatile FrozenDictionary<string, PadHideInfo> _pads = FrozenDictionary<string, PadHideInfo>.Empty;
    private volatile HidHideStatus _status = HidHideStatus.Unknown;
    private volatile string? _problemKey;

    public event Action? Changed;

    public HideService()
    {
        _journalPath = Path.Combine(Log.Directory, "hidhide.json");
        _journal = Journal.Load(_journalPath);
        if (!_journal.IsEmpty)
            Log.Warn($"HidHide: Einträge aus letzter Sitzung gefunden ({_journal.Blacklist.Count} Geräte) – werden abgeglichen.");
        _thread = new Thread(Run) { Name = "ControllerMerger.HidHide", IsBackground = true };
    }

    public HidHideStatus Status => _status;
    public string? ProblemKey => _problemKey;
    public PadHideInfo? Get(string padKey) => _pads.GetValueOrDefault(padKey);

    public void Start() => _thread.Start();

    public void UpdatePads(EngineSnapshot snapshot)
    {
        lock (_gate) { _snapshot = snapshot; _padsDirty = true; }
        _wake.Set();
    }

    public void UpdateSettings(AppSettings settings)
    {
        lock (_gate) _userHidden = settings.HiddenDevices.ToArray();
        _wake.Set();
    }

    public void Refresh()
    {
        lock (_gate) _refresh = true;
        _wake.Set();
    }

    // ------------------------------------------------------------------ Worker

    private void Run()
    {
        while (!_stop)
        {
            bool woken = _wake.WaitOne(_status == HidHideStatus.Busy ? RetryMs : Timeout.Infinite);
            if (_stop) break;
            if (woken) Thread.Sleep(DebounceMs); // schnelle Klickfolgen zu einer Sitzung zusammenfassen
            try { Reconcile(); }
            catch (Exception ex) { Log.Error("HidHide-Abgleich", ex); }
        }
    }

    private void Reconcile()
    {
        EngineSnapshot snapshot;
        bool padsDirty, refresh;
        string[] userHidden;
        lock (_gate)
        {
            snapshot = _snapshot;
            padsDirty = _padsDirty;
            refresh = _refresh;
            userHidden = _userHidden;
            _padsDirty = false;
            _refresh = false;
        }

        if (padsDirty) ResolvePads(snapshot);

        var desired = HidePlanner.Desired(userHidden, _resolvedSafe, _neverBlock);
        bool needsDriver = desired.Count > 0 || !_journal.IsEmpty || refresh ||
                           userHidden.Length > 0 && _status != HidHideStatus.Ready;
        if (needsDriver)
        {
            lock (_applyGate)
            {
                using var client = HidHideClient.Open(OpenTimeout, out var status);
                _status = status;
                if (client is null)
                {
                    _problemKey = status == HidHideStatus.Busy ? "HhProblemBusy" : null;
                    if (status == HidHideStatus.Busy) Log.Warn("HidHide ist gerade belegt – neuer Versuch folgt.");
                }
                else
                {
                    _problemKey = null;
                    Apply(client, desired);
                }
            }
        }
        Publish(snapshot);
    }

    /// <summary>Ordnet jedem verbundenen Controller seine Geräte-ID zu und bestimmt, was nie gesperrt werden darf.</summary>
    private void ResolvePads(EngineSnapshot snapshot)
    {
        var resolved = new Dictionary<string, string?>();
        var never = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        List<XusbDevice> xusb = [];
        bool needXusb = snapshot.VirtualSlot >= 0 || snapshot.Pads.Any(p => p.IsXInput);
        if (needXusb)
        {
            try { xusb = DeviceResolver.EnumerateXusb(); }
            catch (Exception ex) { Log.Error("XUSB-Aufzählung", ex); }
        }

        // Eigener virtueller Controller: am ViGEmBus UND im Slot unseres virtuellen Controllers.
        // ViGEm-Geräte mit unklarem Slot könnten unserer sein → ebenfalls nie sperren.
        var self = xusb.Where(d => d.IsViGEm && d.Slot >= 0 && d.Slot == snapshot.VirtualSlot).ToList();
        foreach (var d in xusb.Where(d => d.IsViGEm && (d.Slot < 0 || d.Slot == snapshot.VirtualSlot)))
            never.Add(d.InstanceId);
        // Ohne sicher erkannten eigenen Controller wird kein XInput-Gerät zugeordnet (fail-closed).
        bool xinputReliable = snapshot.VirtualSlot >= 0 && self.Count == 1;

        var bySlot = xusb.Where(d => d.Slot >= 0).GroupBy(d => d.Slot).ToDictionary(g => g.Key, g => g.ToList());

        foreach (var pad in snapshot.Pads)
        {
            string? id = null;
            if (pad.IsXInput)
            {
                if (xinputReliable && pad.XInputSlot >= 0 &&
                    bySlot.TryGetValue(pad.XInputSlot, out var list) && list.Count == 1 &&
                    !never.Contains(list[0].InstanceId))
                    id = list[0].InstanceId;
            }
            else
            {
                try { id = DeviceResolver.InstanceIdFromInterfacePath(pad.DevicePath); }
                catch (Exception ex) { Log.Error("Geräte-ID auflösen", ex); }
            }
            resolved[pad.Key] = id;
        }

        // Zwei Controller mit derselben ID (z. B. mehrere Pads an einem Funkempfänger) → keiner versteckbar.
        foreach (var dup in resolved.Where(kv => kv.Value is not null)
                     .GroupBy(kv => kv.Value!, StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1).ToList())
            foreach (var kv in dup) resolved[kv.Key] = null;

        _resolved = resolved;
        _neverBlock = never;
        _resolvedSafe = new HashSet<string>(resolved.Values.OfType<string>(), StringComparer.OrdinalIgnoreCase);
    }

    /// <summary>Wendet den Soll-Zustand in einer offenen HidHide-Sitzung an (Reihenfolge ist sicherheitsrelevant).</summary>
    private void Apply(HidHideClient client, HashSet<string> desired)
    {
        bool hide = desired.Count > 0;

        // 1) Erst die eigene EXE freigeben – sonst sperrt sich die App selbst aus.
        if (hide)
        {
            string? dosPath = Environment.ProcessPath is { } exe ? HidHideClient.ToDosDevicePath(exe) : null;
            if (dosPath is null)
            {
                _problemKey = "HhProblemPath";
                Log.Warn("HidHide: Programmpfad lässt sich nicht freigeben (z. B. Netzlaufwerk) – nichts wird versteckt.");
                hide = false;
                desired.Clear();
            }
            else
            {
                var whitelist = client.GetWhitelist();
                bool listed = whitelist.Contains(dosPath, StringComparer.OrdinalIgnoreCase);
                if (client.GetInverse())
                {
                    if (listed)
                    {
                        _problemKey = "HhProblemBlocked";
                        Log.Warn("HidHide: Die App steht in der (invertierten) Sperrliste für Programme – nichts wird versteckt.");
                        hide = false;
                        desired.Clear();
                    }
                }
                else if (!listed)
                {
                    _journal.Whitelist.Add(dosPath);
                    _journal.Save(_journalPath);
                    whitelist.Add(dosPath);
                    client.SetWhitelist(whitelist);
                    Log.Info($"HidHide: App freigegeben ({dosPath}).");
                }
            }
        }

        // 2) Verstecken muss aktiv sein.
        if (hide && !client.GetActive())
        {
            _journal.WeActivated = true;
            _journal.Save(_journalPath);
            client.SetActive(true);
            Log.Info("HidHide: Verstecken aktiviert.");
        }

        // 3) Sperrliste abgleichen – Journal VOR dem Schreiben aktualisieren.
        var change = HidePlanner.Apply(client.GetBlacklist(), _journal.Blacklist, desired);
        if (change.HasChanges)
        {
            foreach (var id in change.Added)
                if (!_journal.Blacklist.Contains(id, StringComparer.OrdinalIgnoreCase)) _journal.Blacklist.Add(id);
            _journal.Save(_journalPath);
            client.SetBlacklist(change.NewList);
            foreach (var id in change.Added) Log.Info($"HidHide: versteckt {id}");
            foreach (var id in change.Removed) Log.Info($"HidHide: wieder sichtbar {id}");
        }
        if (!_journal.Blacklist.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(change.NewOwned))
        {
            _journal.Blacklist = change.NewOwned.ToList();
            _journal.Save(_journalPath);
        }

        // 4) Nichts mehr von uns versteckt → eigene Freigabe und Aktivierung zurücknehmen.
        if (!hide && _journal.Blacklist.Count == 0)
        {
            if (_journal.WeActivated)
            {
                client.SetActive(false);
                Log.Info("HidHide: Verstecken wieder deaktiviert.");
            }
            if (_journal.Whitelist.Count > 0)
            {
                var ours = _journal.Whitelist.ToHashSet(StringComparer.OrdinalIgnoreCase);
                var whitelist = client.GetWhitelist();
                if (whitelist.RemoveAll(ours.Contains) > 0) client.SetWhitelist(whitelist);
            }
            if (!_journal.IsEmpty)
            {
                _journal.WeActivated = false;
                _journal.Whitelist.Clear();
                _journal.Save(_journalPath);
            }
        }
    }

    private void Publish(EngineSnapshot snapshot)
    {
        bool usable = _status == HidHideStatus.Ready && _problemKey is null;
        var pads = new Dictionary<string, PadHideInfo>();
        foreach (var pad in snapshot.Pads)
        {
            var id = _resolved.GetValueOrDefault(pad.Key);
            pads[pad.Key] = new PadHideInfo(id, usable && id is not null);
        }
        _pads = pads.ToFrozenDictionary();
        try { Changed?.Invoke(); }
        catch (Exception ex) { Log.Error("HidHide-Changed-Handler", ex); }
    }

    // ------------------------------------------------------------------ Beenden

    /// <summary>
    /// Macht alle von der App versteckten Controller wieder sichtbar (Beenden, Abmelden, Herunterfahren).
    /// Zeitlich begrenzt; was nicht klappt, räumt der nächste Start über das Journal auf.
    /// </summary>
    public void RestoreAll()
    {
        _stop = true;
        _wake.Set();
        if (_thread.IsAlive) _thread.Join(1500);
        if (_journal.IsEmpty) return;
        try
        {
            lock (_applyGate)
            {
                using var client = HidHideClient.Open(TimeSpan.FromSeconds(1.5), out _);
                if (client is null)
                {
                    Log.Warn("HidHide beim Beenden nicht erreichbar – Aufräumen beim nächsten Start.");
                    return;
                }
                Apply(client, new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            }
        }
        catch (Exception ex) { Log.Error("HidHide beim Beenden zurücksetzen", ex); }
    }

    public void Dispose()
    {
        RestoreAll();
        _wake.Dispose();
    }

    // ------------------------------------------------------------------ Journal

    private sealed class Journal
    {
        public List<string> Blacklist { get; set; } = [];
        public List<string> Whitelist { get; set; } = [];
        public bool WeActivated { get; set; }

        [System.Text.Json.Serialization.JsonIgnore]
        public bool IsEmpty => Blacklist.Count == 0 && Whitelist.Count == 0 && !WeActivated;

        private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

        public static Journal Load(string path)
        {
            try
            {
                if (File.Exists(path)) return JsonSerializer.Deserialize<Journal>(File.ReadAllText(path), Options) ?? new();
            }
            catch (Exception ex) { Log.Error("HidHide-Journal lesen", ex); }
            return new();
        }

        public void Save(string path)
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path)!);
                if (IsEmpty)
                {
                    File.Delete(path);
                    return;
                }
                var tmp = path + ".tmp";
                File.WriteAllText(tmp, JsonSerializer.Serialize(this, Options));
                File.Move(tmp, path, overwrite: true);
            }
            catch (Exception ex) { Log.Error("HidHide-Journal schreiben", ex); }
        }
    }
}
