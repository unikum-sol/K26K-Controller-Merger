using ControllerMerger.Core;
using Nefarius.ViGEm.Client;
using Nefarius.ViGEm.Client.Exceptions;
using Nefarius.ViGEm.Client.Targets;
using Nefarius.ViGEm.Client.Targets.Xbox360;
using Nefarius.ViGEm.Client.Targets.Xbox360.Exceptions;

namespace ControllerMerger.Engine;

/// <summary>
/// Der eine virtuelle Xbox-360-Controller (ViGEmBus). Kapselt den Treiber hinter einer kleinen
/// Schnittstelle, damit ein späterer Wechsel des Backends (ViGEmBus ist EOL) lokal bleibt.
/// </summary>
internal sealed class VirtualPad : IDisposable
{
    private ViGEmClient? _client;
    private IXbox360Controller? _pad;

    /// <summary>Rumble-Befehl eines Spiels (großer Motor, kleiner Motor). Kommt auf einem Treiber-Thread an!</summary>
    public event Action<byte, byte>? RumbleReceived;

    public bool IsConnected => _pad is not null;

    /// <exception cref="VigemBusNotFoundException">ViGEmBus ist nicht installiert.</exception>
    public void Connect()
    {
        Dispose();
        _client = new ViGEmClient();
        var pad = _client.CreateXbox360Controller();
        pad.AutoSubmitReport = false;
        pad.FeedbackReceived += OnFeedback;
        pad.Connect();
        _pad = pad;
    }

    private void OnFeedback(object sender, Xbox360FeedbackReceivedEventArgs e) =>
        RumbleReceived?.Invoke(e.LargeMotor, e.SmallMotor);

    /// <summary>XInput-Slot (0–3) des virtuellen Pads oder -1, solange Windows noch keinen zugewiesen hat.</summary>
    public int TryGetUserIndex()
    {
        if (_pad is null) return -1;
        try { return _pad.UserIndex; }
        catch (Xbox360UserIndexNotReportedException) { return -1; }
        catch (Exception) { return -1; }
    }

    /// <summary>Sendet einen vollständigen Report. Wirft bei Treiberverlust.</summary>
    public void Submit(in GamepadState s)
    {
        var pad = _pad ?? throw new InvalidOperationException("Virtueller Controller ist nicht verbunden.");
        pad.SetButtonsFull((ushort)s.Buttons);
        pad.SetAxisValue(Xbox360Axis.LeftThumbX, s.LeftX);
        pad.SetAxisValue(Xbox360Axis.LeftThumbY, s.LeftY);
        pad.SetAxisValue(Xbox360Axis.RightThumbX, s.RightX);
        pad.SetAxisValue(Xbox360Axis.RightThumbY, s.RightY);
        pad.SetSliderValue(Xbox360Slider.LeftTrigger, s.LeftTrigger);
        pad.SetSliderValue(Xbox360Slider.RightTrigger, s.RightTrigger);
        pad.SubmitReport();
    }

    public void Dispose()
    {
        if (_pad is not null)
        {
            try
            {
                _pad.FeedbackReceived -= OnFeedback;
                _pad.Disconnect();
            }
            catch (Exception) { /* Treiber evtl. schon weg */ }
            _pad = null;
        }
        try { _client?.Dispose(); } catch (Exception) { }
        _client = null;
    }
}
