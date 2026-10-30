using System;
using System.Numerics;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Composition.SystemBackdrops;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Windows.Graphics;

namespace Modak;

public static class Program
{
    [STAThread]
    static void Main(string[] args)
    {
        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(_ =>
        {
            System.Threading.SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
            new App(Array.IndexOf(args, "--demo") >= 0, Array.IndexOf(args, "--second") >= 0);
        });
    }
}

public class App(bool demo, bool second) : Application
{
    Island? island;
    protected override void OnLaunched(LaunchActivatedEventArgs e) => island = new Island(demo, second);
}

// The island: a fixed transparent window at the top of the screen. The pill is a composition shape whose
// size runs on a spring inside the Windows compositor, so the app's own thread does no work per frame.
public sealed class Island : Window
{
    const float WinW = 1100, WinH = 480; // wider than the dashboard plus its shadow, so the region never reaches the edges
    static readonly Vector2 Notch = new(76, 32), Dash = new(980, 380);
    const float Hidden = 24; // the shape starts above the screen edge so only its bottom corners show
    const int HoverOpenMs = 140;
    const float PadOpen = 40, PadNotch = 12; // room for the shadow inside the window region
    const int SettleMs = 500; // the close spring is done by then, so the region can shrink
    const int Edge = 10; // px the window sits above the screen; Windows' frame line lives in that strip

    readonly Compositor comp;
    readonly CompositionRoundedRectangleGeometry pill;
    readonly Visual dashVisual, notchVisual;
    readonly DispatcherQueueTimer shrink;
    readonly IntPtr hwnd;
    readonly float scale;
    readonly PointInt32 origin;
    bool open;
    int overMs;

    // second: open on a non-primary display, so test runs stay out of the way of the main screen.
    public Island(bool demo, bool second)
    {
        hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        SystemBackdrop = new TransparentBackdrop();
        var p = OverlappedPresenter.Create();
        p.SetBorderAndTitleBar(false, false);
        p.IsResizable = p.IsMaximizable = p.IsMinimizable = false;
        AppWindow.SetPresenter(p);
        AppWindow.IsShownInSwitchers = false;

        scale = Native.GetDpiForWindow(hwnd) / 96f;
        var display = DisplayArea.Primary;
        var all = DisplayArea.FindAll(); // indexed, not enumerated: enumerating this WinRT list throws
        for (int i = 0; second && i < all.Count; i++) if (all[i].DisplayId.Value != display.DisplayId.Value) { display = all[i]; break; }
        var screen = display.OuterBounds; // screen edge, not work area, like the Electron build
        int w = (int)(WinW * scale), h = (int)(WinH * scale);
        // Slightly above the screen: Windows draws a thin line along the window edge, the region starts below it.
        origin = new PointInt32(screen.X + (screen.Width - w) / 2, screen.Y - Edge);
        AppWindow.MoveAndResize(new RectInt32(origin.X, origin.Y, w, h));

        var root = new Grid { Margin = new Thickness(0, Edge / scale, 0, 0) }; // content starts at the screen edge
        var host = new Grid();
        var notch = new TextBlock { Text = "\U0001F95F", FontSize = 18, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top, Margin = new Thickness(0, 3, 0, 0) };
        var dash = Dashboard.Build(Dash);
        root.Children.Add(host);
        root.Children.Add(notch);
        root.Children.Add(dash);
        Content = root;

        comp = ElementCompositionPreview.GetElementVisual(root).Compositor;
        pill = comp.CreateRoundedRectangleGeometry();
        pill.Size = Notch + new Vector2(0, Hidden);
        pill.CornerRadius = new Vector2(16);
        // Keep the pill centred while its size springs: the offset follows the size inside the compositor too.
        var centre = comp.CreateExpressionAnimation("Vector2((W - g.Size.X) / 2, -H)");
        centre.SetReferenceParameter("g", pill);
        centre.SetScalarParameter("W", WinW);
        centre.SetScalarParameter("H", Hidden);
        pill.StartAnimation("Offset", centre);

        var shape = comp.CreateSpriteShape(pill);
        shape.FillBrush = comp.CreateColorBrush(ColorHelper.FromArgb(255, 11, 11, 13));
        var shapes = comp.CreateShapeVisual();
        shapes.Size = new Vector2(WinW, WinH);
        shapes.Shapes.Add(shape);
        // A LayerVisual shadow renders nothing in this window, so a plain rectangle tucked inside the rounded
        // pill casts it instead; the blur hides its square corners. It follows the spring like everything else.
        var caster = comp.CreateSpriteVisual();
        caster.Brush = shape.FillBrush;
        var casterSize = comp.CreateExpressionAnimation("Vector2(g.Size.X - 2 * g.CornerRadius.X, g.Size.Y - H - g.CornerRadius.X)");
        casterSize.SetReferenceParameter("g", pill);
        casterSize.SetScalarParameter("H", Hidden);
        caster.StartAnimation("Size", casterSize);
        var casterAt = comp.CreateExpressionAnimation("Vector3((W - g.Size.X) / 2 + g.CornerRadius.X, 0, 0)");
        casterAt.SetReferenceParameter("g", pill);
        casterAt.SetScalarParameter("W", WinW);
        caster.StartAnimation("Offset", casterAt);
        var shadow = comp.CreateDropShadow();
        shadow.BlurRadius = 30;
        shadow.Offset = new Vector3(0, 10, 0);
        shadow.Color = ColorHelper.FromArgb(115, 0, 0, 0);
        caster.Shadow = shadow;
        var pillVisual = comp.CreateContainerVisual();
        pillVisual.Children.InsertAtTop(caster);
        pillVisual.Children.InsertAtTop(shapes);
        ElementCompositionPreview.SetElementChildVisual(host, pillVisual);

        // The dashboard is clipped by the same springing shape, so it is revealed, never resized.
        dashVisual = ElementCompositionPreview.GetElementVisual(dash);
        var clipGeo = comp.CreateRoundedRectangleGeometry();
        var follow = comp.CreateExpressionAnimation("g.Size");
        follow.SetReferenceParameter("g", pill);
        clipGeo.StartAnimation("Size", follow);
        var follow2 = comp.CreateExpressionAnimation("Vector2((D - g.Size.X) / 2, -H)");
        follow2.SetReferenceParameter("g", pill);
        follow2.SetScalarParameter("D", Dash.X);
        follow2.SetScalarParameter("H", Hidden);
        clipGeo.StartAnimation("Offset", follow2);
        var follow3 = comp.CreateExpressionAnimation("g.CornerRadius");
        follow3.SetReferenceParameter("g", pill);
        clipGeo.StartAnimation("CornerRadius", follow3);
        dashVisual.Clip = comp.CreateGeometricClip(clipGeo);
        dashVisual.Opacity = 0;
        notchVisual = ElementCompositionPreview.GetElementVisual(notch);

        AppWindow.Show(false); // never take focus: typing in a terminal must not land here
        p.IsAlwaysOnTop = true; // after Show: set before, it is lost on a window that never activates
        Native.Chrome(hwnd); // after Show too, the presenter resets the frame when it shows the window
        Region(Notch, PadNotch);
        shrink = DispatcherQueue.CreateTimer();
        shrink.IsRepeating = false;
        shrink.Interval = TimeSpan.FromMilliseconds(SettleMs);
        shrink.Tick += (_, _) => Region(Notch, PadNotch);

        var timer = DispatcherQueue.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(33);
        timer.Tick += demo ? DemoTick : HoverTick;
        if (demo) timer.Interval = TimeSpan.FromMilliseconds(1500);
        timer.Start();
    }

    // The window only exists inside this rectangle, for drawing and for clicks: everywhere else clicks reach
    // the apps underneath. Layered click-through would be simpler but makes the window opaque black.
    void Region(Vector2 size, float pad)
    {
        float l = (WinW - size.X) / 2 - pad;
        Native.Region(hwnd, (int)(l * scale), Edge, (int)((l + size.X + 2 * pad) * scale), (int)((size.Y + pad) * scale));
    }

    void DemoTick(DispatcherQueueTimer t, object o) => SetOpen(!open);

    // shortcut: polls the cursor 30 times a second (tiny cost), use a mouse hook if this ever shows up in a profile
    void HoverTick(DispatcherQueueTimer t, object o)
    {
        Native.GetCursorPos(out var pt);
        float x = (pt.X - origin.X) / scale, y = (pt.Y - origin.Y) / scale;
        var size = open ? Dash : Notch;
        bool over = y >= 0 && y <= size.Y && MathF.Abs(x - WinW / 2) <= size.X / 2;
        overMs = over ? overMs + 33 : 0;
        if (!open && overMs >= HoverOpenMs) SetOpen(true);
        else if (open && !over) SetOpen(false);
    }

    void SetOpen(bool value)
    {
        open = value;
        shrink.Stop();
        if (value) Region(Dash, PadOpen); // grow first so the opening spring is never cut off
        else shrink.Start();
        var target = (value ? Dash : Notch) + new Vector2(0, Hidden);
        pill.StartAnimation("Size", Spring(target));
        var r = comp.CreateSpringVector2Animation();
        r.FinalValue = new Vector2(value ? 24 : 16);
        pill.StartAnimation("CornerRadius", r);
        dashVisual.StartAnimation("Opacity", Fade(value ? 1 : 0, value ? 220 : 100, value ? 60 : 0));
        notchVisual.StartAnimation("Opacity", Fade(value ? 0 : 1, value ? 80 : 200, value ? 0 : 120));
    }

    // Damping ratio from the Electron spring (stiffness 420, damping 32); the period is tuned by eye.
    SpringVector2NaturalMotionAnimation Spring(Vector2 to)
    {
        var s = comp.CreateSpringVector2Animation();
        s.FinalValue = to;
        s.DampingRatio = 0.78f;
        s.Period = TimeSpan.FromMilliseconds(48);
        return s;
    }

    ScalarKeyFrameAnimation Fade(float to, int ms, int delayMs)
    {
        var a = comp.CreateScalarKeyFrameAnimation();
        a.InsertKeyFrame(1, to);
        a.Duration = TimeSpan.FromMilliseconds(ms);
        a.DelayTime = TimeSpan.FromMilliseconds(delayMs);
        return a;
    }
}

// Placeholder dashboard with roughly the real one's amount of content, to see what it costs.
static class Dashboard
{
    public static FrameworkElement Build(Vector2 size)
    {
        var grid = new Grid { Width = size.X, Height = size.Y, Padding = new Thickness(10, 36, 10, 10), ColumnSpacing = 8, RowSpacing = 8, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Top };
        foreach (var w in new[] { 2.0, 1.2, 1, 1 }) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition());
        grid.RowDefinitions.Add(new RowDefinition());
        void Tile(int col, int row, int colSpan, string title, string body)
        {
            var t = new Border { Background = new SolidColorBrush(ColorHelper.FromArgb(255, 21, 21, 24)), CornerRadius = new CornerRadius(14), Padding = new Thickness(14) };
            var s = new StackPanel { Spacing = 4 };
            s.Children.Add(new TextBlock { Text = title, FontSize = 22, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = new SolidColorBrush(Colors.White) });
            s.Children.Add(new TextBlock { Text = body, FontSize = 12, Foreground = new SolidColorBrush(ColorHelper.FromArgb(255, 150, 150, 160)) });
            t.Child = s;
            Grid.SetColumn(t, col); Grid.SetRow(t, row); Grid.SetColumnSpan(t, colSpan);
            grid.Children.Add(t);
        }
        Tile(0, 0, 1, "webshop", "Editing checkout.ts");
        Tile(1, 0, 1, "Photos", "slideshow");
        Tile(2, 0, 1, "21:43", "Fri 30 Oct");
        Tile(3, 0, 1, "25:00", "Focus");
        Tile(0, 1, 2, "546K out today", "533 replies");
        Tile(2, 1, 2, "feat/checkout", "6 changed files");
        return grid;
    }
}

// Lets the window be fully transparent: WinUI paints this brush behind the content instead of a solid backdrop.
sealed class TransparentBackdrop : SystemBackdrop
{
    static Windows.UI.Composition.Compositor? system;
    protected override void OnTargetConnected(ICompositionSupportsSystemBackdrop target, XamlRoot root)
    {
        base.OnTargetConnected(target, root);
        if (Windows.System.DispatcherQueue.GetForCurrentThread() == null) Native.EnsureSystemDispatcher();
        system ??= new Windows.UI.Composition.Compositor();
        target.SystemBackdrop = system.CreateColorBrush(Windows.UI.Color.FromArgb(0, 0, 0, 0));
    }
}

static class Native
{
    const int GWL_STYLE = -16, GWL_EXSTYLE = -20;
    const long WS_POPUP = 0x80000000L, WS_VISIBLE = 0x10000000L, WS_EX_TOOLWINDOW = 0x80, WS_EX_NOACTIVATE = 0x08000000;

    [DllImport("user32.dll")] public static extern uint GetDpiForWindow(IntPtr hwnd);
    [DllImport("user32.dll")] public static extern bool GetCursorPos(out PointInt32 pt);
    [DllImport("user32.dll")] static extern bool SetWindowPos(IntPtr h, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] static extern long GetWindowLongPtr(IntPtr h, int i);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] static extern long SetWindowLongPtr(IntPtr h, int i, long v);
    [DllImport("user32.dll")] static extern int SetWindowRgn(IntPtr h, IntPtr region, bool redraw);
    [DllImport("gdi32.dll")] static extern IntPtr CreateRectRgn(int l, int t, int r, int b);
    [DllImport("dwmapi.dll")] static extern int DwmSetWindowAttribute(IntPtr h, int attr, ref int value, int size);
    [DllImport("dwmapi.dll")] static extern int DwmExtendFrameIntoClientArea(IntPtr h, ref Margins m);
    struct Margins { public int L, R, T, B; }
    [DllImport("CoreMessaging.dll")] static extern int CreateDispatcherQueueController(DispatcherQueueOptions o, out IntPtr controller);
    [StructLayout(LayoutKind.Sequential)] struct DispatcherQueueOptions { public int dwSize, threadType, apartmentType; }

    public static void EnsureSystemDispatcher() =>
        CreateDispatcherQueueController(new DispatcherQueueOptions { dwSize = Marshal.SizeOf<DispatcherQueueOptions>(), threadType = 2, apartmentType = 2 }, out _);

    // A borderless popup with no taskbar entry that never activates, composited with the content's alpha.
    public static void Chrome(IntPtr h)
    {
        // Style first: changing it resets the DWM frame attributes set below.
        SetWindowLongPtr(h, GWL_STYLE, WS_POPUP | WS_VISIBLE); // no caption buttons in the extended frame
        SetWindowLongPtr(h, GWL_EXSTYLE, GetWindowLongPtr(h, GWL_EXSTYLE) | WS_EX_TOOLWINDOW | WS_EX_NOACTIVATE);
        SetWindowPos(h, IntPtr.Zero, 0, 0, 0, 0, 0x0001 | 0x0002 | 0x0004 | 0x0010 | 0x0020); // NOSIZE | NOMOVE | NOZORDER | NOACTIVATE | FRAMECHANGED
        int round = 1;
        DwmSetWindowAttribute(h, 33, ref round, 4); // DWMWA_WINDOW_CORNER_PREFERENCE = DONOTROUND
        var glass = new Margins { L = -1, R = -1, T = -1, B = -1 };
        DwmExtendFrameIntoClientArea(h, ref glass); // let DWM use the content's alpha for the whole window
    }

    // The system owns the region after SetWindowRgn, so it is not deleted here.
    public static void Region(IntPtr h, int l, int t, int r, int b) => SetWindowRgn(h, CreateRectRgn(l, t, r, b), true);
}
