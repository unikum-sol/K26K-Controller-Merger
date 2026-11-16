using ControllerMerger.Core;

namespace ControllerMerger.Tests;

public class HidePlannerTests
{
    private static readonly HashSet<string> None = new(StringComparer.OrdinalIgnoreCase);
    private static HashSet<string> Set(params string[] ids) => new(ids, StringComparer.OrdinalIgnoreCase);

    private const string Ds4 = @"HID\VID_054C&PID_09CC\7&1&0&0000";
    private const string XboxUsb = @"USB\VID_045E&PID_02EA\3039363";
    private const string ViGEm1 = @"USB\VID_045E&PID_028E\01";
    private const string ViGEm2 = @"USB\VID_045E&PID_028E\02";

    [Fact]
    public void Desired_HidIdsArePreblocked_OthersOnlyWhenResolved()
    {
        var desired = HidePlanner.Desired([Ds4, XboxUsb, ViGEm2], resolvedSafe: Set(ViGEm2), neverBlock: None);
        Assert.Contains(Ds4, desired);
        Assert.Contains(ViGEm2, desired);
        Assert.DoesNotContain(XboxUsb, desired); // nicht verbunden → nicht vorab sperren
    }

    [Fact]
    public void Desired_NeverBlocksOwnVirtualController()
    {
        // Gespeicherte Sunshine-ID "…\01" ist beim nächsten Start unser eigener Controller.
        var desired = HidePlanner.Desired([ViGEm1], resolvedSafe: Set(ViGEm1), neverBlock: Set(ViGEm1));
        Assert.Empty(desired);
    }

    [Fact]
    public void Apply_AddsMissing_AndOwnsOnlyWhatItAdded()
    {
        var change = HidePlanner.Apply([ViGEm2], owned: [], desired: Set(Ds4, ViGEm2));
        Assert.Equal([Ds4], change.Added);
        Assert.Empty(change.Removed);
        Assert.Equal(Set(Ds4), change.NewOwned); // ViGEm2 stand schon drin (anderes Programm) → nicht unserer
        Assert.Equal(2, change.NewList.Count);
    }

    [Fact]
    public void Apply_RemovesOnlyOwnEntries()
    {
        var foreign = @"HID\VID_1234&PID_5678\1";
        var change = HidePlanner.Apply([foreign, Ds4], owned: [Ds4], desired: None);
        Assert.Equal([Ds4], change.Removed);
        Assert.Equal([foreign], change.NewList);
        Assert.Empty(change.NewOwned);
    }

    [Fact]
    public void Apply_ForeignEntryStaysEvenIfUserUnhides()
    {
        var change = HidePlanner.Apply([Ds4], owned: [], desired: None);
        Assert.False(change.HasChanges);
        Assert.Equal([Ds4], change.NewList);
    }

    [Fact]
    public void Apply_IsCaseInsensitive_AndIdempotent()
    {
        var first = HidePlanner.Apply([], owned: [], desired: Set(Ds4));
        var second = HidePlanner.Apply(first.NewList, first.NewOwned, Set(Ds4.ToLowerInvariant()));
        Assert.False(second.HasChanges);
        Assert.Single(second.NewList);
        Assert.Single(second.NewOwned);
    }

    [Fact]
    public void Apply_ReAddsOwnEntryRemovedByOthers()
    {
        var change = HidePlanner.Apply([], owned: [Ds4], desired: Set(Ds4));
        Assert.Equal([Ds4], change.Added);
        Assert.Equal(Set(Ds4), change.NewOwned);
    }
}
