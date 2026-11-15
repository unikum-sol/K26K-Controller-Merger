using ControllerMerger.Core;

namespace ControllerMerger.Tests;

public class MergeEngineTests
{
    private static readonly MergeOptions NoDeadzone = new() { StickDeadzone = 0, TriggerDeadzone = 0 };

    private static GamepadState Pad(PadButtons b = 0, short lx = 0, short ly = 0, short rx = 0, short ry = 0,
        byte lt = 0, byte rt = 0) => new()
    {
        Buttons = b, LeftX = lx, LeftY = ly, RightX = rx, RightY = ry, LeftTrigger = lt, RightTrigger = rt,
    };

    [Fact]
    public void NoInputs_IsNeutral()
    {
        Assert.Equal(GamepadState.Neutral, MergeEngine.Merge([], MergeOptions.Default));
    }

    [Fact]
    public void Buttons_AreOred()
    {
        var r = MergeEngine.Merge([Pad(PadButtons.A), Pad(PadButtons.B | PadButtons.Start)], NoDeadzone);
        Assert.Equal(PadButtons.A | PadButtons.B | PadButtons.Start, r.Buttons);
    }

    [Fact]
    public void Triggers_TakeMaximum()
    {
        var r = MergeEngine.Merge([Pad(lt: 40, rt: 255), Pad(lt: 200, rt: 10)], NoDeadzone);
        Assert.Equal(200, r.LeftTrigger);
        Assert.Equal(255, r.RightTrigger);
    }

    [Fact]
    public void PerAxis_LargestAbsoluteValueWinsPerAxis()
    {
        var opts = NoDeadzone with { StickMode = StickMergeMode.PerAxis };
        var r = MergeEngine.Merge([Pad(lx: 1000, ly: -30000), Pad(lx: -20000, ly: 500)], opts);
        Assert.Equal(-20000, r.LeftX);
        Assert.Equal(-30000, r.LeftY);
    }

    [Fact]
    public void PerStick_StrongestStickWinsAsWhole()
    {
        // Pad 1: diagonal mittel (Betrag ~ 21213), Pad 2: voll nach rechts (Betrag 30000)
        var r = MergeEngine.Merge([Pad(lx: 15000, ly: 15000), Pad(lx: 30000, ly: 0)], NoDeadzone);
        Assert.Equal(30000, r.LeftX);
        Assert.Equal(0, r.LeftY); // kein Mischen von X aus Pad 2 mit Y aus Pad 1
    }

    [Fact]
    public void Sticks_AreIndependentBetweenLeftAndRight()
    {
        var r = MergeEngine.Merge([Pad(lx: 20000), Pad(rx: -25000)], NoDeadzone);
        Assert.Equal(20000, r.LeftX);
        Assert.Equal(-25000, r.RightX);
    }

    [Fact]
    public void Deadzone_SuppressesDriftOfIdlePad()
    {
        var opts = new MergeOptions { StickDeadzone = 0.10f };
        // Pad 1 driftet leicht (2000 ~ 6 %), Pad 2 bewegt aktiv minimal mehr als die Drift, aber in Gegenrichtung.
        var r = MergeEngine.Merge([Pad(lx: 2000), Pad(lx: -9000)], opts);
        Assert.True(r.LeftX < 0, "Die echte Eingabe muss gewinnen, nicht die Drift.");
    }

    [Fact]
    public void Deadzone_RescalesToFullRange()
    {
        var opts = new MergeOptions { StickDeadzone = 0.2f };
        var r = MergeEngine.Merge([Pad(lx: short.MaxValue)], opts);
        Assert.Equal(short.MaxValue, r.LeftX);
    }

    [Fact]
    public void TriggerDeadzone_ZeroesSmallValues()
    {
        var opts = new MergeOptions { TriggerDeadzone = 0.1f };
        var r = MergeEngine.Merge([Pad(lt: 20, rt: 255)], opts);
        Assert.Equal(0, r.LeftTrigger);
        Assert.Equal(255, r.RightTrigger);
    }

    [Fact]
    public void Dpad_OppositeDirectionsCancel()
    {
        var r = MergeEngine.Merge([Pad(PadButtons.DpadUp | PadButtons.DpadLeft), Pad(PadButtons.DpadDown)], NoDeadzone);
        Assert.Equal(PadButtons.DpadLeft, r.Buttons);
    }

    [Fact]
    public void Dpad_DiagonalFromTwoPadsIsKept()
    {
        var r = MergeEngine.Merge([Pad(PadButtons.DpadUp), Pad(PadButtons.DpadRight)], NoDeadzone);
        Assert.Equal(PadButtons.DpadUp | PadButtons.DpadRight, r.Buttons);
    }

    [Fact]
    public void Inversion_IsSaturating()
    {
        var opts = NoDeadzone with { Inversion = AxisInversion.LeftY | AxisInversion.RightX };
        var r = MergeEngine.Merge([Pad(ly: short.MinValue, rx: 1234)], opts);
        Assert.Equal(short.MaxValue, r.LeftY);
        Assert.Equal(-1234, r.RightX);
    }

    [Theory]
    [InlineData(5, -7, -7)]
    [InlineData(-7, 5, -7)]
    [InlineData(6, -6, 6)] // Gleichstand: erster Wert bleibt
    public void MaxAbs_Works(short a, short b, short expected) => Assert.Equal(expected, MergeEngine.MaxAbs(a, b));
}
