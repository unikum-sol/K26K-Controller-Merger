using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using ControllerMerger.Interop;

namespace ControllerMerger.UI;

/// <summary>
/// Kompaktes Status-Popup (Rechtsklick auf das Tray-Symbol). Positioniert sich wie ein Windows-Flyout
/// am Infobereich und schließt sich, sobald es den Fokus verliert oder Esc gedrückt wird.
/// </summary>
public partial class StatusPopup : Window
{
    private bool _closing;

    public StatusPopup()
    {
        InitializeComponent();
        AppMark.Source = IconFactory.CreateAppImage(64);
        Deactivated += (_, _) => SafeClose();
        KeyDown += (_, e) => { if (e.Key == Key.Escape) SafeClose(); };
        Closing += (_, _) => _closing = true;
    }

    /// <summary>Zeigt das Popup nahe der aktuellen Mausposition, vollständig innerhalb des Arbeitsbereichs.</summary>
    public void ShowNearCursor()
    {
        Opacity = 0;
        Show();
        UpdateLayout();

        var hwnd = new WindowInteropHelper(this).Handle;
        if (Native.GetCursorPos(out var cursor))
        {
            var monitor = Native.MonitorFromPoint(cursor, Native.MonitorDefaultToNearest);
            var info = new Native.MonitorInfo { cbSize = System.Runtime.InteropServices.Marshal.SizeOf<Native.MonitorInfo>() };
            if (Native.GetMonitorInfo(monitor, ref info))
            {
                var dpi = System.Windows.Media.VisualTreeHelper.GetDpi(this);
                int w = (int)Math.Ceiling(ActualWidth * dpi.DpiScaleX);
                int h = (int)Math.Ceiling(ActualHeight * dpi.DpiScaleY);
                var work = info.rcWork;
                const int gap = 0; // Root-Margin enthält bereits Abstand + Schatten

                int x = Math.Clamp(cursor.X - w / 2, work.Left + gap, work.Right - w - gap);
                int y;
                if (cursor.Y >= work.Bottom) y = work.Bottom - h - gap;          // Taskleiste unten
                else if (cursor.Y <= work.Top) y = work.Top + gap;               // Taskleiste oben
                else y = Math.Clamp(cursor.Y - h / 2, work.Top + gap, work.Bottom - h - gap); // seitlich

                if (cursor.X >= work.Right) x = work.Right - w - gap;            // Taskleiste rechts
                else if (cursor.X <= work.Left) x = work.Left + gap;             // Taskleiste links

                Native.SetWindowPos(hwnd, Native.HwndTopmost, x, y, 0, 0,
                    Native.SwpNoSize | Native.SwpNoActivate);
            }
        }

        Opacity = 1;
        Native.SetForegroundWindow(hwnd);
        Activate();
    }

    private void SafeClose()
    {
        if (_closing) return;
        _closing = true;
        Close();
    }

    private void OnActionClick(object sender, RoutedEventArgs e) => SafeClose();
}
