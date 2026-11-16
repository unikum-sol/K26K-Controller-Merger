namespace ControllerMerger.Core;

/// <summary>Ergebnis eines Abgleichs der HidHide-Sperrliste.</summary>
public sealed record BlacklistChange(
    IReadOnlyList<string> NewList,
    IReadOnlyList<string> Added,
    IReadOnlyList<string> Removed,
    IReadOnlySet<string> NewOwned)
{
    public bool HasChanges => Added.Count > 0 || Removed.Count > 0;
}

/// <summary>
/// Reine Abgleichslogik für das Verstecken über HidHide (ohne Treiberzugriff, daher testbar).
/// Grundsätze aus dem Architektur-Council 1.3:
/// <list type="bullet">
/// <item>Die App merkt sich, welche Einträge SIE hinzugefügt hat ("owned"), und entfernt nur diese.
///       Einträge anderer Programme (oder von Hand in HidHide gesetzte) bleiben immer unangetastet.</item>
/// <item>HID-Geräte (Instanz-ID "HID\…") sind eindeutig und dürfen schon vorab gesperrt werden – dann sehen Spiele
///       sie auch beim Einstecken nicht kurz.</item>
/// <item>Alle anderen IDs (XInput/XUSB, virtuelle ViGEm-Controller) werden nur gesperrt, solange das Gerät
///       gerade verbunden und sicher zugeordnet ist. ViGEm vergibt IDs wie "…\01" neu – eine gespeicherte ID
///       könnte beim nächsten Start sonst den EIGENEN virtuellen Controller treffen.</item>
/// </list>
/// </summary>
public static class HidePlanner
{
    private static readonly StringComparer Cmp = StringComparer.OrdinalIgnoreCase;

    /// <summary>Darf diese ID auch gesperrt werden, wenn das Gerät gerade nicht verbunden ist?</summary>
    public static bool IsPreblockable(string instanceId) =>
        instanceId.StartsWith(@"HID\", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Welche IDs sollen jetzt gesperrt sein?
    /// </summary>
    /// <param name="userHidden">Vom Nutzer versteckte IDs (Einstellungen).</param>
    /// <param name="resolvedSafe">IDs der gerade verbundenen, eindeutig zugeordneten Controller.</param>
    /// <param name="neverBlock">IDs, die nie gesperrt werden dürfen (der eigene virtuelle Controller).</param>
    public static HashSet<string> Desired(IEnumerable<string> userHidden, IReadOnlySet<string> resolvedSafe,
        IReadOnlySet<string> neverBlock)
    {
        var result = new HashSet<string>(Cmp);
        foreach (var id in userHidden)
        {
            if (string.IsNullOrWhiteSpace(id) || neverBlock.Contains(id)) continue;
            if (IsPreblockable(id) || resolvedSafe.Contains(id)) result.Add(id);
        }
        return result;
    }

    /// <summary>Berechnet die neue Sperrliste aus aktueller Liste, eigenen Einträgen und Soll-Zustand.</summary>
    public static BlacklistChange Apply(IReadOnlyList<string> current, IEnumerable<string> owned, IReadOnlySet<string> desired)
    {
        var currentSet = new HashSet<string>(current, Cmp);
        var ownedSet = new HashSet<string>(owned, Cmp);

        var removed = current.Where(id => ownedSet.Contains(id) && !desired.Contains(id))
            .Distinct(Cmp).ToList();
        var added = desired.Where(id => !currentSet.Contains(id)).OrderBy(id => id, Cmp).ToList();

        var removedSet = new HashSet<string>(removed, Cmp);
        var newList = current.Where(id => !removedSet.Contains(id)).Concat(added).ToList();

        // Eigene Einträge: was wir schon hatten und weiterhin wollen (und noch drin ist) + neu hinzugefügte.
        var newOwned = new HashSet<string>(Cmp);
        foreach (var id in ownedSet)
            if (desired.Contains(id) && currentSet.Contains(id)) newOwned.Add(id);
        foreach (var id in added) newOwned.Add(id);

        return new BlacklistChange(newList, added, removed, newOwned);
    }
}
