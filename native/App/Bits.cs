using System.Runtime.InteropServices;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Media;
using Windows.Foundation;
using Shapes = Microsoft.UI.Xaml.Shapes;

namespace Modak;

// Soft two-note chime (the same notes as the Electron build's Web Audio one), made in memory: no sound file to ship.
static class Chime
{
    [DllImport("winmm.dll")] static extern bool PlaySound(byte[] sound, IntPtr module, uint flags);
    const uint SND_MEMORY = 0x4, SND_ASYNC = 0x1, SND_NODEFAULT = 0x2;
    static byte[]? wav;

    public static void Play()
    {
        wav ??= Build();
        PlaySound(wav, IntPtr.Zero, SND_MEMORY | SND_ASYNC | SND_NODEFAULT);
    }

    static byte[] Build()
    {
        const int rate = 44100;
        var samples = new short[(int)(rate * 0.8)];
        foreach (var (freq, at) in new[] { (784.0, 0.0), (1175.0, 0.12) })
            for (int i = (int)(at * rate); i < samples.Length; i++)
            {
                double t = (double)i / rate - at;
                if (t > 0.65) break;
                double env = t < 0.02 ? t / 0.02 * 0.18 : 0.18 * Math.Pow(0.001 / 0.18, (t - 0.02) / 0.58); // ramp up, exponential fall
                samples[i] = (short)Math.Clamp(samples[i] + env * Math.Sin(2 * Math.PI * freq * t) * short.MaxValue, short.MinValue, short.MaxValue);
            }
        var ms = new MemoryStream();
        var w = new BinaryWriter(ms);
        w.Write("RIFF"u8); w.Write(36 + samples.Length * 2); w.Write("WAVEfmt "u8); w.Write(16); w.Write((short)1); w.Write((short)1);
        w.Write(rate); w.Write(rate * 2); w.Write((short)2); w.Write((short)16); w.Write("data"u8); w.Write(samples.Length * 2);
        foreach (var s in samples) w.Write(s);
        return ms.ToArray();
    }
}

static class Dialogs
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] static extern int MessageBoxW(IntPtr owner, string text, string caption, uint type);
    public static void Error(string text) => MessageBoxW(IntPtr.Zero, text, "Modak", 0x10 | 0x40000); // MB_ICONERROR | MB_TOPMOST
    public static bool Ask(string text) => MessageBoxW(IntPtr.Zero, text, "Modak", 0x4 | 0x20 | 0x40000) == 6; // MB_YESNO | MB_ICONQUESTION | MB_TOPMOST, IDYES
}

// A progress ring: a track circle and an arc from 12 o'clock, like the Electron SVG ring.
sealed class Ring
{
    readonly Shapes.Path arc = new() { StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };
    readonly double size, stroke;
    public FrameworkElement Root { get; }
    public Microsoft.UI.Xaml.Controls.Grid Inner { get; } = new();

    public Ring(double size, double stroke, string color, string track = "#26262c")
    {
        this.size = size;
        this.stroke = stroke;
        arc.Stroke = Ui.Brush(color);
        arc.StrokeThickness = stroke;
        var circle = new Shapes.Ellipse { Width = size, Height = size, Stroke = Ui.Brush(track), StrokeThickness = stroke };
        Root = new Microsoft.UI.Xaml.Controls.Grid { Width = size, Height = size, Children = { circle, arc, Inner }, VerticalAlignment = VerticalAlignment.Center };
    }

    public double Value
    {
        set
        {
            var v = Math.Clamp(value, 0, 0.9999);
            arc.Visibility = v > 0 ? Visibility.Visible : Visibility.Collapsed;
            double r = (size - stroke) / 2, c = size / 2, a = v * 2 * Math.PI;
            var fig = new PathFigure { StartPoint = new Point(c, c - r), IsClosed = false };
            fig.Segments.Add(new ArcSegment { Point = new Point(c + r * Math.Sin(a), c - r * Math.Cos(a)), Size = new Size(r, r), SweepDirection = SweepDirection.Clockwise, IsLargeArc = v > 0.5 });
            arc.Data = new PathGeometry { Figures = { fig } };
        }
    }
}

