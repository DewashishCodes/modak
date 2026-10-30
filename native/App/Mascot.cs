using System.Numerics;
using Microsoft.UI.Composition;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;

namespace Modak;

// Modak, the mascot, played from the sprite sheets that scripts/modak-sprites.js renders from the approved design.
// The frame steps run in the compositor. Like the Electron build it animates for a moment after something
// happens, then rests on frame 0 and only blinks now and then. mood: idle | working | asking | done; it adds
// love (after Allow), wink (one poke), dizzy (five quick pokes) and sleepy (two idle minutes) on its own.
sealed class Mascot
{
    const int Frames = 80, Cols = 10, Rows = 8, Fps = 20; // must match scripts/modak-sprites.js
    const int LodPx = 40; // below this size the simplified art reads better
    const int RestAfterMs = 5000, BlinkEveryMs = 4500, SleepAfterMs = 2 * 60 * 1000;
    const int BlinkFrom = 72, BlinkTo = 79; // the idle sheet blinks near the end of its 4 second cycle
    const int PokesToDizzy = 5, PokeWindowMs = 2500;
    static readonly Dictionary<string, LoadedImageSurface> Sheets = [];

    readonly double size;
    readonly Compositor comp;
    readonly ContainerVisual squash;
    readonly CompositionSurfaceBrush brush;
    readonly DispatcherQueueTimer rest, blink, sleep, flashEnd;
    readonly List<DateTime> pokes = [];
    string mood = "idle", face = "";
    string? overrideFace, beat;
    int love = -1;
    bool sleepy, resting;
    public FrameworkElement Root { get; }

    // still: start resting, so mounting it (the dashboard tile) costs no animation frames.
    public Mascot(double size, bool still = false)
    {
        this.size = size;
        var root = new Grid { Width = size, Height = size, Background = Ui.Transparent }; // transparent, not null: it must take clicks
        Root = root;
        comp = ElementCompositionPreview.GetElementVisual(root).Compositor;
        brush = comp.CreateSurfaceBrush();
        brush.Stretch = CompositionStretch.Fill;
        brush.Scale = new Vector2(Cols, Rows); // the whole sheet, Cols x Rows cells of `size`, slid under a one-cell window
        var sprite = comp.CreateSpriteVisual();
        sprite.Size = new Vector2((float)size);
        sprite.Brush = brush;
        squash = comp.CreateContainerVisual();
        squash.Size = sprite.Size;
        squash.CenterPoint = new Vector3((float)size / 2, (float)size, 0); // squish from the bottom, like originY: 1
        squash.Children.InsertAtTop(sprite);
        ElementCompositionPreview.SetElementChildVisual(root, squash);

        var q = DispatcherQueue.GetForCurrentThread();
        rest = Once(q, RestAfterMs, Rest);
        flashEnd = Once(q, 0, () => { overrideFace = null; Show(); });
        sleep = Once(q, SleepAfterMs, () => { sleepy = true; Show(); });
        blink = q.CreateTimer();
        blink.Interval = TimeSpan.FromMilliseconds(BlinkEveryMs);
        blink.Tick += (_, _) => { if (resting && face == "idle") Play(BlinkFrom, BlinkTo, false); };
        resting = still;
        root.Tapped += (_, e) => { e.Handled = true; Poke(); };
    }

    static DispatcherQueueTimer Once(DispatcherQueue q, int ms, Action a)
    {
        var t = q.CreateTimer();
        t.IsRepeating = false;
        t.Interval = TimeSpan.FromMilliseconds(ms);
        t.Tick += (_, _) => a();
        return t;
    }

    // Called on every state change; only a new face or a new beat (the agent did something) wakes it up.
    public void Set(string mood, string? beat, int love)
    {
        if (this.love >= 0 && love > this.love) Flash("love", 1600); // heart eyes whenever the user approves
        this.love = love;
        if (mood != this.mood)
        {
            this.mood = mood;
            sleepy = false;
            sleep.Stop();
            if (mood == "idle") sleep.Start(); // falls asleep after a long idle
        }
        else if (this.beat != beat && face.Length > 0) { this.beat = beat; Wake(); return; }
        this.beat = beat;
        Show();
    }

    void Show()
    {
        var next = overrideFace ?? (mood == "idle" && sleepy ? "sleepy" : mood);
        if (next == face) return;
        bool first = face.Length == 0;
        face = next;
        brush.Surface = Sheet(face);
        if (first && resting) { Frame(0); blink.Start(); return; } // a still mascot shows its first frame and waits
        Wake();
    }

    void Wake()
    {
        resting = false;
        Play(0, Frames - 1, true);
        rest.Stop();
        rest.Start();
        blink.Start();
    }

    void Rest()
    {
        resting = true;
        brush.StopAnimation("Offset");
        Frame(0);
    }

    LoadedImageSurface Sheet(string name)
    {
        var file = Path.Combine(AppContext.BaseDirectory, "Assets", "modak", $"{name}-{(size < LodPx ? "small" : "full")}.png");
        if (!Sheets.TryGetValue(file, out var s)) Sheets[file] = s = LoadedImageSurface.StartLoadFromUri(new Uri(file));
        return s;
    }

    Vector2 Cell(int f) => new(-(f % Cols) * (float)size, -(f / Cols) * (float)size);
    void Frame(int f) => brush.Offset = Cell(f);

    // Steps through frames from..to in the compositor: no app work per frame.
    void Play(int from, int to, bool loop)
    {
        var a = comp.CreateVector2KeyFrameAnimation();
        var step = comp.CreateStepEasingFunction();
        int n = to - from + 1;
        for (int i = 0; i < n; i++) a.InsertKeyFrame((float)i / n, Cell(from + i), step);
        a.InsertKeyFrame(1, Cell(loop ? from : 0), step);
        a.Duration = TimeSpan.FromSeconds((double)n / Fps);
        a.IterationBehavior = loop ? AnimationIterationBehavior.Forever : AnimationIterationBehavior.Count;
        a.IterationCount = 1;
        brush.StartAnimation("Offset", a);
    }

    void Flash(string f, int ms)
    {
        overrideFace = f;
        flashEnd.Stop();
        flashEnd.Interval = TimeSpan.FromMilliseconds(ms);
        flashEnd.Start();
        Show();
    }

    void Poke()
    {
        var a = comp.CreateVector3KeyFrameAnimation();
        a.InsertKeyFrame(0.3f, new Vector3(1.18f, 0.82f, 1));
        a.InsertKeyFrame(0.65f, new Vector3(0.94f, 1.06f, 1));
        a.InsertKeyFrame(1, Vector3.One);
        a.Duration = TimeSpan.FromMilliseconds(380);
        squash.StartAnimation("Scale", a);
        sleepy = false;
        var now = DateTime.Now;
        pokes.RemoveAll(t => (now - t).TotalMilliseconds >= PokeWindowMs);
        pokes.Add(now);
        if (pokes.Count >= PokesToDizzy) { pokes.Clear(); Flash("dizzy", 2400); }
        else if (pokes.Count == 1) Flash("wink", 700);
    }
}
