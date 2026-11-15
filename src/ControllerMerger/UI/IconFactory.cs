using System.Globalization;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ControllerMerger.UI;

public enum TrayState { Active, Idle, Problem }

/// <summary>
/// Zeichnet Tray- und App-Symbol zur Laufzeit (kein Bitmap-Asset nötig, scharf in jeder Größe).
/// Tray: Controller-Glyph in Taskleisten-Kontrastfarbe + Status-Punkt (grün aktiv, grau ohne Pads, rot bei Problem).
/// </summary>
internal static class IconFactory
{
    private const string Glyph = "";
    private static readonly FontFamily IconFont = new("Segoe Fluent Icons, Segoe MDL2 Assets");

    public static System.Drawing.Icon CreateTrayIcon(TrayState state, bool lightTaskbar)
    {
        var (glyphColor, dot) = TrayColors(state, lightTaskbar);
        var images = new[] { 16, 20, 24, 32, 40, 48 }.Select(size => RenderPng(size, dc => DrawTray(dc, size, state, glyphColor, dot)));
        using var ms = new MemoryStream(BuildIco(images.ToList()));
        return new System.Drawing.Icon(ms);
    }

    /// <summary>Tray-Symbol als WPF-Bild (z. B. zur Erklärung im Einrichtungsassistenten).</summary>
    public static BitmapSource CreateTrayImage(TrayState state, bool light, int size)
    {
        var (glyphColor, dot) = TrayColors(state, light);
        var bmp = Render(size, dc => DrawTray(dc, size, state, glyphColor, dot));
        bmp.Freeze();
        return bmp;
    }

    private static (Color Glyph, Color Dot) TrayColors(TrayState state, bool lightTaskbar) => (
        lightTaskbar ? Color.FromRgb(0x1B, 0x1D, 0x21) : Color.FromRgb(0xF3, 0xF4, 0xF6),
        state switch
        {
            TrayState.Active => lightTaskbar ? Color.FromRgb(0x1E, 0x9E, 0x63) : Color.FromRgb(0x3F, 0xC2, 0x84),
            TrayState.Problem => Color.FromRgb(0xE5, 0x48, 0x4D),
            _ => lightTaskbar ? Color.FromRgb(0x6B, 0x72, 0x80) : Color.FromRgb(0xA9, 0xB0, 0xBA),
        });

    /// <summary>App-Symbol (Taskleiste/Explorer) als ICO-Bytes mit 16…256 px.</summary>
    public static byte[] CreateAppIcoBytes() =>
        BuildIco(new[] { 16, 20, 24, 32, 40, 48, 64, 128, 256 }.Select(s => RenderPng(s, dc => DrawApp(dc, s))).ToList());

    public static BitmapSource CreateAppImage(int size)
    {
        var bmp = Render(size, dc => DrawApp(dc, size));
        bmp.Freeze();
        return bmp;
    }

    private static void DrawTray(DrawingContext dc, int size, TrayState state, Color glyphColor, Color dotColor)
    {
        double s = size;
        var glyphBrush = new SolidColorBrush(glyphColor);
        var glyph = GlyphGeometry(s * 0.5, s * 0.46, s * 0.9);

        // Status-Markierung unten rechts, aus dem Glyph ausgestanzt (bleibt bei 16 px lesbar).
        // Zustand wird zusätzlich über die FORM kodiert (Rot-Grün-Schwäche):
        //   Aktiv = gefüllter Punkt · Bereit = Ring · Problem = Punkt mit Ausrufezeichen.
        // Pixelgenau: Radius und Mittelpunkt auf ganze Pixel, damit der Punkt bei 16 px nicht verschwimmt.
        double r = Math.Max(4, Math.Round(s * 0.23));
        var center = new Point(s - r, s - r);
        double gap = Math.Max(1.5, Math.Round(s * 0.08));
        var cut = new EllipseGeometry(center, r + gap, r + gap);
        var body = new CombinedGeometry(GeometryCombineMode.Exclude, glyph, cut);
        // Leichte Kontur macht die feine Fluent-Glyphe bei 16 px kräftiger.
        dc.DrawGeometry(glyphBrush, new Pen(glyphBrush, Math.Max(0.5, s * 0.035)), body);

        var dotBrush = new SolidColorBrush(dotColor);
        switch (state)
        {
            case TrayState.Idle:
                double t = Math.Max(1.5, Math.Round(s * 0.09));
                dc.DrawEllipse(null, new Pen(dotBrush, t), center, r - t / 2, r - t / 2);
                break;
            case TrayState.Problem:
                // Roter Punkt mit weißem "!" – Strich ≥ 1,5 px, ganzzahlig ausgerichtet.
                dc.DrawEllipse(dotBrush, null, center, r, r);
                double w = Math.Max(1.5, Math.Round(r * 0.4 * 2) / 2);
                double top = center.Y - r * 0.62, barH = r * 0.82;
                dc.DrawRectangle(Brushes.White, null, new Rect(center.X - w / 2, top, w, barH));
                dc.DrawRectangle(Brushes.White, null, new Rect(center.X - w / 2, center.Y + r * 0.36, w, w));
                break;
            default:
                dc.DrawEllipse(dotBrush, null, center, r, r);
                break;
        }
    }

    private static void DrawApp(DrawingContext dc, int size)
    {
        double s = size;
        var gradient = new LinearGradientBrush(Color.FromRgb(0x2F, 0x62, 0xE9), Color.FromRgb(0x6B, 0x93, 0xFA), 45);
        double inset = s * 0.04;
        dc.DrawRoundedRectangle(gradient, null, new Rect(inset, inset, s - 2 * inset, s - 2 * inset), s * 0.22, s * 0.22);
        dc.DrawGeometry(Brushes.White, null, GlyphGeometry(s * 0.5, s * 0.5, s * 0.62));
    }

    /// <summary>Controller-Glyphe als Geometrie, exakt zentriert anhand ihrer echten Umrisse (nicht der Zeilenhöhe).</summary>
    private static Geometry GlyphGeometry(double cx, double cy, double targetWidth)
    {
        var ft = new FormattedText(Glyph, CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            new Typeface(IconFont, FontStyles.Normal, FontWeights.Normal, FontStretches.Normal), 100, Brushes.Black, 1.0);
        var geo = ft.BuildGeometry(new Point(0, 0));
        var b = geo.Bounds;
        if (b.IsEmpty) return Geometry.Empty;
        double scale = targetWidth / b.Width;
        var tg = new TransformGroup();
        tg.Children.Add(new TranslateTransform(-b.X - b.Width / 2, -b.Y - b.Height / 2));
        tg.Children.Add(new ScaleTransform(scale, scale));
        tg.Children.Add(new TranslateTransform(cx, cy));
        // Transformation "einbacken", damit CombinedGeometry mit echten Koordinaten rechnet.
        return Geometry.Combine(geo, Geometry.Empty, GeometryCombineMode.Union, tg);
    }

    private static RenderTargetBitmap Render(int size, Action<DrawingContext> draw)
    {
        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen()) draw(dc);
        var bmp = new RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        bmp.Render(visual);
        return bmp;
    }

    private static (int Size, byte[] Png) RenderPng(int size, Action<DrawingContext> draw)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(Render(size, draw)));
        using var ms = new MemoryStream();
        encoder.Save(ms);
        return (size, ms.ToArray());
    }

    /// <summary>ICO-Container mit eingebetteten PNGs (seit Windows Vista unterstützt).</summary>
    private static byte[] BuildIco(IReadOnlyList<(int Size, byte[] Png)> images)
    {
        using var ms = new MemoryStream();
        using var w = new BinaryWriter(ms);
        w.Write((ushort)0);
        w.Write((ushort)1);
        w.Write((ushort)images.Count);
        int offset = 6 + 16 * images.Count;
        foreach (var (size, png) in images)
        {
            w.Write((byte)(size >= 256 ? 0 : size));
            w.Write((byte)(size >= 256 ? 0 : size));
            w.Write((byte)0);
            w.Write((byte)0);
            w.Write((ushort)1);
            w.Write((ushort)32);
            w.Write(png.Length);
            w.Write(offset);
            offset += png.Length;
        }
        foreach (var (_, png) in images) w.Write(png);
        return ms.ToArray();
    }
}
