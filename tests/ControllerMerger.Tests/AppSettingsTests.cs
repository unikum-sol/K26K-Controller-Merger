using ControllerMerger.Core;

namespace ControllerMerger.Tests;

public class AppSettingsTests
{
    [Fact]
    public void RoundTrip_PreservesValues()
    {
        var s = new AppSettings
        {
            StickDeadzonePercent = 12, StickMode = StickMergeMode.PerAxis, InvertRightY = true,
            PollingIntervalMs = 2, Theme = ThemePreference.Dark, DisabledDevices = ["a|b"],
        };
        var r = AppSettings.FromJson(s.ToJson());
        Assert.Equal(12, r.StickDeadzonePercent);
        Assert.Equal(StickMergeMode.PerAxis, r.StickMode);
        Assert.True(r.InvertRightY);
        Assert.Equal(2, r.PollingIntervalMs);
        Assert.Equal(ThemePreference.Dark, r.Theme);
        Assert.Equal(["a|b"], r.DisabledDevices);
    }

    [Fact]
    public void Garbage_FallsBackToDefaults()
    {
        var r = AppSettings.FromJson("{ this is not json");
        Assert.Equal(8, r.StickDeadzonePercent);
        Assert.Equal(StickMergeMode.PerStick, r.StickMode);
    }

    [Fact]
    public void InvalidValues_AreNormalized()
    {
        var r = AppSettings.FromJson("""{ "stickDeadzonePercent": 400, "pollingIntervalMs": 3 }""");
        Assert.Equal(40, r.StickDeadzonePercent);
        Assert.Equal(4, r.PollingIntervalMs);
    }

    [Fact]
    public void ToMergeOptions_MapsInversionFlags()
    {
        var o = new AppSettings { InvertLeftX = true, InvertRightY = true }.ToMergeOptions();
        Assert.Equal(AxisInversion.LeftX | AxisInversion.RightY, o.Inversion);
    }

    [Fact]
    public void Save_And_Load_File()
    {
        var path = Path.Combine(Path.GetTempPath(), "cm-test-" + Guid.NewGuid() + ".json");
        try
        {
            new AppSettings { TriggerDeadzonePercent = 9 }.Save(path);
            Assert.Equal(9, AppSettings.Load(path).TriggerDeadzonePercent);
        }
        finally { File.Delete(path); }
    }
}
