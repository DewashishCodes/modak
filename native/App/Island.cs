using System.Numerics;
using Microsoft.UI;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Windows.Graphics;

namespace Modak;

// One thing the island can show: the notch, a bar or the dashboard. Update refreshes it from the island's state.
interface IView
{
    FrameworkElement Root { get; }
    void Update();
}

// The island: a fixed transparent window at the top of the screen. The pill is a composition shape whose size
// runs on a spring inside the Windows compositor, so the app's own thread does no work per frame. The window
// only exists (for drawing and for clicks) inside a region around the pill; clicks elsewhere reach the apps below.
public sealed partial class Island : Window
{
    const float WinW = 1100, WinH = 560; // wider and taller than the dashboard plus its shadow, so the region never reaches the edges
    const float Hidden = 24; // the shape starts above the screen edge so only its bottom corners show
    const int Edge = 10; // px the window sits above the screen; Windows' frame line lives in that strip
    const float PadOpen = 40, PadSmall = 14; // room for the shadow inside the region
    const int SettleMs = 500; // a shrinking spring is done by then, so the region can shrink too
    const int HoverOpenMs = 140; // passing the mouse over the top edge should not pop the dashboard open
    const int DoneFlashMs = 5000;
    public const int Port = 47821; // shortcut: fixed port, make it configurable if anyone hits a clash
    public static readonly string HookUrl = $"http://127.0.0.1:{Port}{Hub.HookPath}";

    readonly Compositor comp;
    readonly CompositionRoundedRectangleGeometry pill;
    readonly CompositionColorGradientStop rimStop;
    readonly Grid stage;
    readonly IntPtr hwnd;
    public IntPtr Handle => hwnd;
    readonly float scale;
    readonly PointInt32 origin;
    readonly DispatcherQueueTimer shrink, flashTimer;
    readonly DispatcherQueue ui; // captured on the UI thread: Window.DispatcherQueue throws when read from a server thread

    public readonly Hub Hub = new();
    readonly HookServer server;
    readonly Tray tray;
    public Prefs Prefs { get; private set; } = Prefs.Load();
    public readonly FocusTimer Focus = new();
    public Snapshot State { get; private set; } = new([], []);
    string? flash; // session that just finished
    bool hover, ghost, pinned;
    int overMs;
    public string Page { get; set; } = "grid"; // grid | settings
    public int Index { get; set; }
    public int Love { get; private set; } // bumps when the user allows, for the mascot's heart eyes

    IView? current;
    string currentKey = "";
    Vector2 size;

    // second: open on a non-primary display, so test runs stay out of the way of the main screen.
    public Island(bool second, int port, bool pinned = false)
    {
        hwnd = WinRT.Interop.WindowNative.GetWindowHandle(this);
        ui = DispatcherQueue;
        SystemBackdrop = new TransparentBackdrop();
        var p = OverlappedPresenter.Create();
        p.SetBorderAndTitleBar(false, false);
        p.IsResizable = p.IsMaximizable = p.IsMinimizable = false;
        AppWindow.SetPresenter(p);
        AppWindow.IsShownInSwitchers = false;
        AppWindow.Title = "Modak";

        scale = Native.GetDpiForWindow(hwnd) / 96f;
        var display = DisplayArea.Primary;
        var all = DisplayArea.FindAll(); // indexed, not enumerated: enumerating this WinRT list throws
        for (int i = 0; second && i < all.Count; i++) if (all[i].DisplayId.Value != display.DisplayId.Value) { display = all[i]; break; }
        var screen = display.OuterBounds; // screen edge, not work area, so the pill joins the edge even with a top taskbar
        int w = (int)(WinW * scale), h = (int)(WinH * scale);
        origin = new PointInt32(screen.X + (screen.Width - w) / 2, screen.Y - Edge);
        AppWindow.MoveAndResize(new RectInt32(origin.X, origin.Y, w, h));

        var root = new Grid { Margin = new Thickness(0, Edge / scale, 0, 0) }; // content starts at the screen edge
        var host = new Grid();
        stage = new Grid();
        root.Children.Add(host);
        root.Children.Add(stage);
        Content = root;

        comp = ElementCompositionPreview.GetElementVisual(root).Compositor;
        pill = comp.CreateRoundedRectangleGeometry();
        pill.CornerRadius = new Vector2(16);
        // The pill stays centred while its size springs: the offset follows the size inside the compositor too.
        Follow(pill, "Offset", "Vector2((W - g.Size.X) / 2, -H)");

        var fill = comp.CreateColorBrush(Ui.Bg);
        var shapes = comp.CreateShapeVisual();
        shapes.Size = new Vector2(WinW, WinH);
        var shape = comp.CreateSpriteShape(pill);
        shape.FillBrush = fill;
        shapes.Shapes.Add(shape);

        // A LayerVisual shadow renders nothing in this window, so a plain rectangle tucked inside the rounded
        // pill casts it instead; the blur hides its square corners.
        var caster = comp.CreateSpriteVisual();
        caster.Brush = fill;
        Follow(caster, "Size", "Vector2(g.Size.X - 2 * g.CornerRadius.X, g.Size.Y - H - g.CornerRadius.X)");
        Follow(caster, "Offset", "Vector3((W - g.Size.X) / 2 + g.CornerRadius.X, 0, 0)");
        var shadow = comp.CreateDropShadow();
        shadow.BlurRadius = 30;
        shadow.Offset = new Vector3(0, 10, 0);
        shadow.Color = Ui.Hex("#000000", 0.45);
        caster.Shadow = shadow;

        // Faint state-coloured rim along the bottom edge, like light leaking out.
        var rim = comp.CreateSpriteVisual();
        var gradient = comp.CreateLinearGradientBrush();
        gradient.StartPoint = new Vector2(0, 0.5f);
        gradient.EndPoint = new Vector2(1, 0.5f);
        gradient.ColorStops.Add(comp.CreateColorGradientStop(0, Colors.Transparent));
        rimStop = comp.CreateColorGradientStop(0.5f, Colors.Transparent);
        gradient.ColorStops.Add(rimStop);
        gradient.ColorStops.Add(comp.CreateColorGradientStop(1, Colors.Transparent));
        rim.Brush = gradient;
        Follow(rim, "Size", "Vector2(g.Size.X * 0.76, 1)");
        Follow(rim, "Offset", "Vector3((W - g.Size.X * 0.76) / 2, g.Size.Y - H - 1, 0)");

        var pillVisual = comp.CreateContainerVisual();
        pillVisual.Children.InsertAtTop(caster);
        pillVisual.Children.InsertAtTop(shapes);
        pillVisual.Children.InsertAtTop(rim);
        ElementCompositionPreview.SetElementChildVisual(host, pillVisual);

        // Views are clipped by the same springing shape, so they are revealed, never resized.
        var clipGeo = comp.CreateRoundedRectangleGeometry();
        Follow(clipGeo, "Size", "g.Size");
        Follow(clipGeo, "Offset", "g.Offset");
        Follow(clipGeo, "CornerRadius", "g.CornerRadius");
        ElementCompositionPreview.GetElementVisual(stage).Clip = comp.CreateGeometricClip(clipGeo);

        AppWindow.Show(false); // never take focus: typing in a terminal must not land here
        p.IsAlwaysOnTop = true; // after Show: set before, it is lost on a window that never activates
        Native.Chrome(hwnd); // after Show too, the presenter resets the frame when it shows the window

        shrink = Timer(SettleMs, false, () => Region(size, PadFor(size)));
        flashTimer = Timer(DoneFlashMs, false, () => { flash = null; Render(); });
        Timer(33, true, HoverTick);
        Timer(250, true, SlowTick);

        server = new HookServer(Hub) { OnError = CrashLog.Write };
        Hub.Changed += s => { CrashLog.Trace($"hub changed: {s.Sessions.Count} sessions, {s.Approvals.Count} approvals"); OnUi(() => { State = s; Render(); }); };
        Hub.Done += id => OnUi(() => OnDone(id));
        tray = new Tray(hwnd, this);
        try { server.Listen(port); }
        catch (Exception err)
        {
            tray.Refresh($"port {port} is busy");
            Dialogs.Error($"Port {port} is already in use, so Modak cannot hear Claude Code.\nIs another copy running?\n\n{err.Message}");
        }
        if (port == Port) Connections.OnStartup(Prefs); // test runs on another port never touch the real hooks or ask

        // Global keys exist only while an approval waits, so they never steal these combos the rest of the time.
        // The window itself never takes focus: Enter typed into a terminal must not approve anything.
        Native.OnMessage(hwnd, (msg, w, _) => { if (msg == 0x0312) Decide(w == 1 ? "allow" : "deny"); }); // WM_HOTKEY

        Dashboard.Warm();
        this.pinned = pinned;
        hover = pinned;
        if (pinned && Environment.GetCommandLineArgs().Contains("settings")) Page = "settings"; // --open settings
        Closed += (_, _) => { server.Dispose(); tray.Dispose(); }; // open approvals go back to the terminal prompt
        Render();
    }

    // Hub events come from server threads; an error in the UI must be logged, not lost or fatal.
    void OnUi(Action a) => ui.TryEnqueue(() => { try { a(); } catch (Exception x) { CrashLog.Write(x); } });

    void Follow(CompositionObject target, string property, string expression)
    {
        var e = comp.CreateExpressionAnimation(expression);
        e.SetReferenceParameter("g", pill);
        e.SetScalarParameter("W", WinW);
        e.SetScalarParameter("H", Hidden);
        target.StartAnimation(property, e);
    }

    readonly List<DispatcherQueueTimer> timers = []; // held here: an unreferenced timer is collected and stops firing

    DispatcherQueueTimer Timer(int ms, bool repeat, Action tick)
    {
        var t = DispatcherQueue.CreateTimer();
        t.Interval = TimeSpan.FromMilliseconds(ms);
        t.IsRepeating = repeat;
        t.Tick += (_, _) => tick();
        if (repeat) t.Start();
        timers.Add(t);
        return t;
    }

    // ---------- state the views read ----------

    public Approval? Approval => State.Approvals.FirstOrDefault();
    public Session? Flashed => flash == null ? null : State.Sessions.FirstOrDefault(s => s.Id == flash);
    public int SessionIndex => Math.Min(Index, Math.Max(0, State.Sessions.Count - 1));
    public Session? Selected => State.Sessions.ElementAtOrDefault(SessionIndex);
    public Session? Primary => (Approval is { } a ? State.Sessions.FirstOrDefault(s => s.Id == a.SessionId) : null) ?? Flashed ?? Selected;
    public string Status => Approval != null ? "asking" : Flashed != null ? "done" : Primary?.Status is "done" or null ? "idle" : Primary.Status;

    string View => ghost ? "notch" : Approval != null ? "approval" : hover ? "dashboard" : Flashed != null ? "done" : "notch";

    // ---------- rendering ----------

    public void Render()
    {
        var view = View;
        var key = view switch { "approval" => "approval" + Approval!.Id, "dashboard" => "dashboard-" + Page, "notch" => Focus.Running ? "notch-focus" : "notch", _ => view };
        Glow(Status);
        ApprovalKeys(Approval != null);
        CrashLog.Trace($"render {key} (was {currentKey}) sessions={State.Sessions.Count} approvals={State.Approvals.Count}");
        if (key != currentKey)
        {
            currentKey = key;
            Swap(view switch
            {
                "approval" => new ApprovalBar(this, Approval!),
                "done" => new DoneBar(this, Flashed!),
                "dashboard" => Page == "settings" ? new SettingsView(this) : new Dashboard(this),
                _ => new NotchView(this),
            });
        }
        else current?.Update();
    }

    // Rebuilds the current view, for changes a view cannot pick up in Update (the sound icon, settings).
    public void Rebuild() { currentKey = ""; Render(); }

    void Swap(IView next)
    {
        if (current is { } old)
        {
            var v = ElementCompositionPreview.GetElementVisual(old.Root);
            var batch = comp.CreateScopedBatch(CompositionBatchTypes.Animation);
            v.StartAnimation("Opacity", Fade(0, 100, 0));
            batch.End();
            batch.Completed += (_, _) => stage.Children.Remove(old.Root);
        }
        current = next;
        next.Root.HorizontalAlignment = HorizontalAlignment.Center;
        next.Root.VerticalAlignment = VerticalAlignment.Top;
        stage.Children.Add(next.Root);
        var nv = ElementCompositionPreview.GetElementVisual(next.Root);
        nv.Opacity = 0;
        nv.StartAnimation("Opacity", Fade(1, 220, 60));
        Resize();
    }

    // Springs the pill to fit the current view; views call this when their own height changes.
    public void Resize()
    {
        if (current == null) return;
        current.Root.Measure(new Windows.Foundation.Size(double.PositiveInfinity, double.PositiveInfinity));
        var d = current.Root.DesiredSize;
        var target = new Vector2((float)d.Width, (float)d.Height);
        bool notch = current is NotchView;
        var radius = notch ? 16 : 24;
        bool grows = target.X > size.X || target.Y > size.Y;
        size = target;
        pill.StartAnimation("Size", Spring(target + new Vector2(0, Hidden)));
        var r = comp.CreateSpringVector2Animation();
        r.FinalValue = new Vector2(radius);
        pill.StartAnimation("CornerRadius", r);
        shrink.Stop();
        if (grows) Region(target, PadFor(target)); // grow first so the spring is never cut off
        else shrink.Start();
    }

    static float PadFor(Vector2 s) => s.Y > 100 ? PadOpen : PadSmall;

    void Region(Vector2 s, float pad)
    {
        if (ghost) { Native.Region(hwnd, 0, 0, 0, 0); return; } // Shift held: nothing to click or see
        float l = (WinW - s.X) / 2 - pad;
        Native.Region(hwnd, (int)(l * scale), Edge, (int)((l + s.X + 2 * pad) * scale), Edge + (int)((s.Y + pad) * scale));
    }

    void Glow(string status)
    {
        var c = status switch { "working" => Ui.Hex("#ff9f43", 0.65), "asking" => Ui.Hex("#ffcf6e", 0.7), "done" => Ui.Hex("#4ade80", 0.65), _ => Colors.Transparent };
        var a = comp.CreateColorKeyFrameAnimation();
        a.InsertKeyFrame(1, c);
        a.Duration = TimeSpan.FromMilliseconds(400);
        rimStop.StartAnimation("Color", a);
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

    // ---------- input ----------

    // shortcut: polls the cursor 30 times a second (tiny cost), use a mouse hook if this ever shows up in a profile
    void HoverTick()
    {
        if (pinned) return;
        Native.GetCursorPos(out var pt);
        float x = (pt.X - origin.X) / scale, y = (pt.Y - origin.Y - Edge) / scale;
        bool over = y >= 0 && y <= size.Y && MathF.Abs(x - WinW / 2) <= size.X / 2;
        bool shift = Native.ShiftDown;

        // Shift over Modak: it fades away so the user can click whatever is behind it.
        if (over && shift && !ghost) { SetGhost(true); return; }
        if (ghost) { if (!shift || !over) SetGhost(false); return; }

        overMs = over ? overMs + 33 : 0;
        if (!hover && overMs >= HoverOpenMs) { hover = true; Render(); }
        else if (hover && !over) { hover = false; Page = "grid"; Render(); }
    }

    void SetGhost(bool on)
    {
        ghost = on;
        hover = false;
        overMs = 0;
        Region(size, PadFor(size));
        ElementCompositionPreview.GetElementVisual((FrameworkElement)Content).StartAnimation("Opacity", Fade(on ? 0 : 1, 150, 0));
        Render();
    }

    void SlowTick()
    {
        if (Focus.Tick(DateTime.Now) && Prefs.Sound) Chime.Play();
        current?.Update(); // clock, focus countdown
        if (Focus.Running != currentKey.EndsWith("focus") && current is NotchView) Render();
    }

    void OnDone(string sessionId)
    {
        if (Prefs.Sound) Chime.Play();
        flash = sessionId;
        flashTimer.Stop();
        flashTimer.Start();
        Render();
    }

    bool keys;
    void ApprovalKeys(bool on)
    {
        if (on == keys) return;
        keys = on;
        if (on) { Native.Hotkey(hwnd, 1, 'Y'); Native.Hotkey(hwnd, 2, 'N'); }
        else { Native.Unhotkey(hwnd, 1); Native.Unhotkey(hwnd, 2); }
    }

    // ---------- actions ----------

    public void Decide(string behavior)
    {
        if (Approval is not { } a) return;
        if (behavior == "allow") Love++;
        Hub.Decide(a.Id, behavior);
    }

    public void Quit()
    {
        server.Dispose(); // open approvals go back to the terminal prompt
        tray.Dispose();
        Application.Current.Exit();
    }

    public void SavePrefs(Action<Prefs> change)
    {
        change(Prefs);
        Prefs.Save();
        Render();
    }
}
