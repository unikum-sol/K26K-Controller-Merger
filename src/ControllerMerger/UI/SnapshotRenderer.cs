using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using ControllerMerger.Core;
using ControllerMerger.Engine;
using ControllerMerger.Services;
using ControllerMerger.Setup;
using ControllerMerger.UI.ViewModels;

namespace ControllerMerger.UI;

/// <summary>
/// Entwickler-Modus <c>K26KControllerMerger.exe --snapshot &lt;ordner&gt;</c>:
/// rendert alle Oberflächen mit Beispieldaten in Hell und Dunkel als PNG (1,5-fach skaliert).
/// Grundlage für die Bewertungsrunden des Design-Councils – reproduzierbar, ohne Treiber oder Controller.
/// </summary>
internal static class SnapshotRenderer
{
    private const double Scale = 1.5;

    public static void Run(string dir)
    {
        Directory.CreateDirectory(dir);
        var store = new SettingsStore(persist: false);
        store.Update(s => s.DisabledDevices.Add("switch"));
        var vm = new MainViewModel(store, () => { }, () => { }, () => { });
        vm.UpdateDriver(new DriverInfo(DriverStatus.Ok, "1.22.0", ""));

        var pads = new List<PadSnapshot>
        {
            new("ds", 1, "DualSense", "PS5 Controller", PadKind.DualSense, ConnectionKind.Bluetooth, 80, false, true, true, -1, 0x054C, 0x0CE6),
            new("xb", 2, "Xbox Series Controller", "Xbox Series X Controller", PadKind.XboxOne, ConnectionKind.Usb, -1, false, true, true, 0, 0x045E, 0x0B12),
            new("switch", 3, "Switch Pro Controller", "Nintendo Switch Pro Controller", PadKind.SwitchPro, ConnectionKind.Bluetooth, 45, false, true, false, -1, 0x057E, 0x2009),
        };
        var running = new EngineSnapshot(EngineState.Running, 1, pads, 250, 4, "3.4.20", null, GamepadState.Neutral);
        var empty = running with { Pads = [] };
        var missing = new EngineSnapshot(EngineState.DriverMissing, -1, [], 0, 4, "3.4.20",
            "ViGEmBus wurde nicht gefunden.", GamepadState.Neutral);

        foreach (bool dark in new[] { true, false })
        {
            string t = dark ? "dark" : "light";
            ThemeManager.Force(dark);
            var backdrop = new SolidColorBrush(dark ? Color.FromRgb(0x2A, 0x2F, 0x38) : Color.FromRgb(0xC9, 0xD2, 0xDE));

            vm.Update(running);
            foreach (var page in Enum.GetValues<SettingsWindow.Page>())
            {
                var w = new SettingsWindow { DataContext = vm };
                w.ShowPage(page);
                RenderWindow(w, 920, 680, Path.Combine(dir, $"settings-{page.ToString().ToLowerInvariant()}-{t}.png"));
            }

            RenderWindow(new StatusPopup { DataContext = vm }, 384, null, Path.Combine(dir, $"popup-{t}.png"), backdrop);

            vm.Update(empty);
            RenderWindow(new SettingsWindow { DataContext = vm }, 920, 680, Path.Combine(dir, $"settings-empty-{t}.png"));

            var setup = new SetupViewModel(new DriverInfo(DriverStatus.NotInstalled, null, "ViGEmBus ist nicht installiert."), firstRun: true);
            RenderWindow(new SetupWindow { DataContext = setup }, 540, 600, Path.Combine(dir, $"setup-welcome-{t}.png"));

            setup.GoTo(SetupStage.Working, activeStep: 1);
            setup.InstallSteps[0].Progress = 1;
            setup.InstallSteps[1].Progress = 0.62;
            RenderWindow(new SetupWindow { DataContext = setup }, 540, 600, Path.Combine(dir, $"setup-working-{t}.png"));

            setup.GoTo(SetupStage.Success, activeStep: 3);
            RenderWindow(new SetupWindow { DataContext = setup }, 540, 600, Path.Combine(dir, $"setup-success-{t}.png"));

            var repair = new SetupViewModel(new DriverInfo(DriverStatus.AccessFailed, "1.22.0",
                "ViGEmBus ist installiert, aber nicht aktiv. Meist hilft ein Neustart von Windows."), firstRun: false);
            RenderWindow(new SetupWindow { DataContext = repair }, 540, 600, Path.Combine(dir, $"setup-repair-{t}.png"));

            vm.Update(missing);
            vm.UpdateDriver(new DriverInfo(DriverStatus.NotInstalled, null, ""));
            RenderWindow(new StatusPopup { DataContext = vm }, 384, null, Path.Combine(dir, $"popup-error-{t}.png"), backdrop);
            if (dark)
            {
                var w = new SettingsWindow { DataContext = vm };
                RenderWindow(w, 920, 680, Path.Combine(dir, $"settings-error-{t}.png"));
            }
            vm.UpdateDriver(new DriverInfo(DriverStatus.Ok, "1.22.0", ""));
        }

        foreach (var state in Enum.GetValues<TrayState>())
            foreach (bool light in new[] { false, true })
                SaveTrayPreview(state, light, Path.Combine(dir, $"tray-{state.ToString().ToLowerInvariant()}-{(light ? "light" : "dark")}.png"));

        Log.Info($"Snapshots geschrieben nach {Path.GetFullPath(dir)}");
    }

    /// <summary>Löst den Inhalt aus dem (nie angezeigten) Fenster, layoutet ihn und rendert ihn als PNG.</summary>
    private static void RenderWindow(Window window, double width, double? height, string path, Brush? backdrop = null)
    {
        if (window is SetupWindow sw) sw.UpdateHero();
        var content = (FrameworkElement)window.Content;
        window.Content = null;

        var host = new Border
        {
            Background = backdrop ?? (Brush)Application.Current.FindResource("Brush.Window"),
            Child = content,
            Width = width,
            DataContext = window.DataContext,
            UseLayoutRounding = true,
        };
        if (backdrop is not null) host.Padding = new Thickness(24);
        if (backdrop is not null) host.Width = width + 48;
        foreach (var key in window.Resources.Keys) host.Resources[key] = window.Resources[key];
        TextOptions.SetTextFormattingMode(host, TextFormattingMode.Ideal);
        if (height is { } h) host.Height = h;

        host.Measure(new Size(host.Width, height ?? double.PositiveInfinity));
        host.Arrange(new Rect(host.DesiredSize));
        host.UpdateLayout();

        int pw = (int)Math.Ceiling(host.ActualWidth * Scale), ph = (int)Math.Ceiling(host.ActualHeight * Scale);
        var bmp = new RenderTargetBitmap(pw, ph, 96 * Scale, 96 * Scale, PixelFormats.Pbgra32);
        bmp.Render(host);
        Save(bmp, path);
        window.Close();
    }

    private static void SaveTrayPreview(TrayState state, bool lightTaskbar, string path)
    {
        // Simulierte Taskleiste mit dem Symbol in 16/24/32 px.
        var bar = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            Background = new SolidColorBrush(lightTaskbar ? Color.FromRgb(0xEE, 0xEE, 0xEE) : Color.FromRgb(0x20, 0x20, 0x20)),
        };
        using var icon = IconFactory.CreateTrayIcon(state, lightTaskbar);
        foreach (int size in new[] { 16, 24, 32 })
        {
            using var sized = new System.Drawing.Icon(icon, size, size);
            using var bitmap = sized.ToBitmap();
            using var ms = new MemoryStream();
            bitmap.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
            ms.Position = 0;
            var img = BitmapFrame.Create(ms, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            bar.Children.Add(new Image { Source = img, Width = size, Height = size, Margin = new Thickness(12) });
        }
        bar.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        bar.Arrange(new Rect(bar.DesiredSize));
        var bmp = new RenderTargetBitmap((int)bar.ActualWidth * 2, (int)bar.ActualHeight * 2, 192, 192, PixelFormats.Pbgra32);
        bmp.Render(bar);
        Save(bmp, path);
    }

    private static void Save(BitmapSource bmp, string path)
    {
        var enc = new PngBitmapEncoder();
        enc.Frames.Add(BitmapFrame.Create(bmp));
        using var fs = File.Create(path);
        enc.Save(fs);
    }
}
