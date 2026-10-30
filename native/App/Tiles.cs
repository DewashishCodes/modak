using System.Numerics;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Hosting;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Media.Imaging;
using Shapes = Microsoft.UI.Xaml.Shapes;

namespace Modak;

// The big tile: who is working, what they are doing right now, and the live terminal or diff box.
sealed class AgentTile
{
    readonly Island island;
    readonly Mascot mascot = new(52, still: true); // starts resting, so opening the dashboard costs no animation frames
    readonly Shapes.Ellipse dot = Ui.Dot(Ui.Dim);
    readonly TextBlock folder = Ui.T("", 13, bold: true), agent = Ui.T("", 11.5, Ui.Muted), status = Ui.T("", 12, Ui.Muted);
    readonly Border counter;
    readonly ContentControl statusIcon = new();
    readonly Border preview = new();
    readonly Grid root = new() { RowSpacing = 8 };
    public FrameworkElement Root { get; }

    public AgentTile(Island island)
    {
        this.island = island;
        counter = Ui.Clickable(Ui.T("", 11, Ui.Muted), () => { island.Index = island.SessionIndex + 1 >= island.State.Sessions.Count ? 0 : island.SessionIndex + 1; island.Render(); }, "Next session", radius: 6, padding: new Thickness(6, 1, 6, 1));
        var name = new Grid { ColumnSpacing = 6 };
        foreach (var w in new[] { GridLength.Auto, GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto }) name.ColumnDefinitions.Add(new ColumnDefinition { Width = w });
        Put(name, 0, dot); Put(name, 1, folder); Put(name, 2, agent); Put(name, 3, counter);
        var statusRow = Ui.Row(6, statusIcon, status);
        statusRow.Margin = new Thickness(0, 3, 0, 0);
        var meta = Ui.Col(0, name, statusRow);
        var head = new Grid { ColumnSpacing = 10 };
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        head.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        Put(head, 0, mascot.Root); Put(head, 1, meta);
        root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        Grid.SetRow(preview, 1);
        root.Children.Add(head);
        root.Children.Add(preview);
        Root = Ui.Card(root);
        Update();
    }

    static void Put(Grid g, int col, FrameworkElement e) { Grid.SetColumn(e, col); g.Children.Add(e); }

    object? shown; // what is on screen, so the 4 times a second update only rebuilds when something changed

    public void Update()
    {
        var s = island.Selected;
        var count = island.State.Sessions.Count;
        var key = (s?.Id, s?.Status, s?.Activity, s?.Preview, s?.Agent, count, island.SessionIndex, island.Status, island.Love);
        if (Equals(key, shown)) return;
        shown = key;
        counter.Visibility = count > 1 ? Visibility.Visible : Visibility.Collapsed;
        ((TextBlock)counter.Child).Text = $"{island.SessionIndex + 1}/{count}";
        if (s == null)
        {
            mascot.Set("idle", null, 0);
            dot.Visibility = Visibility.Collapsed;
            folder.Text = "No sessions yet";
            agent.Text = "";
            status.Text = "Start claude in a terminal";
            status.Foreground = Ui.Muted;
            statusIcon.Content = null;
            preview.Child = PreviewBox.Build(null, false);
            return;
        }
        mascot.Set(island.Status, s.Activity, island.Love);
        dot.Visibility = Visibility.Visible;
        dot.Fill = s.Status switch { "working" => Ui.Working, "asking" => Ui.Asking, "done" => Ui.Done, _ => Ui.Dim };
        folder.Text = s.Folder.Length > 0 ? s.Folder : "session";
        agent.Text = s.Agent == "codex" ? "Codex" : "Claude Code";
        (status.Text, status.Foreground, statusIcon.Content) = s.Status switch
        {
            "working" => (s.Activity.Length > 0 ? s.Activity : "Working", Ui.Brush("#ffd2a6"), (object?)Spinner.Build()),
            "asking" => (s.Activity.Length > 0 ? s.Activity : "Needs you", Ui.Asking, new Border { Width = 14, Height = 14, CornerRadius = new CornerRadius(7), Background = Ui.Brush("#ffcf6e", 0.15), Child = new TextBlock { Text = "?", FontSize = 10, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = Ui.Asking, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } }),
            "done" => ("Done", Ui.Done, Ui.Icon("check", 13, Ui.Done)),
            _ => ("Idle", Ui.Muted, null),
        };
        preview.Child = PreviewBox.Build(s.Preview, s.Status == "working");
    }
}

// The working spinner: a ring with one bright quarter, turned by the compositor (no app work per frame).
static class Spinner
{
    public static FrameworkElement Build()
    {
        var ring = new Shapes.Ellipse { Width = 10, Height = 10, Stroke = Ui.Brush("#ff9f43", 0.25), StrokeThickness = 2 };
        var arc = new Shapes.Path { Stroke = Ui.Working, StrokeThickness = 2, Data = (Geometry)Microsoft.UI.Xaml.Markup.XamlReader.Load("<Geometry xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'>M1,5 A4,4 0 0 1 5,1</Geometry>") };
        var g = new Grid { Width = 10, Height = 10, Children = { ring, arc } };
        g.Loaded += (_, _) =>
        {
            var v = ElementCompositionPreview.GetElementVisual(g);
            v.CenterPoint = new Vector3(5, 5, 0);
            var a = v.Compositor.CreateScalarKeyFrameAnimation();
            a.InsertKeyFrame(1, 360, v.Compositor.CreateLinearEasingFunction());
            a.Duration = TimeSpan.FromMilliseconds(800);
            a.IterationBehavior = Microsoft.UI.Composition.AnimationIterationBehavior.Forever;
            v.StartAnimation("RotationAngleInDegrees", a);
        };
        return g;
    }
}

// Analog plus digital clock.
sealed class ClockTile
{
    readonly TextBlock time = Ui.T("", 24, bold: true), date = Ui.T("", 11, Ui.Muted);
    readonly Shapes.Line hour = Hand(2.4, "#f2f2f3"), minute = Hand(1.8, "#f2f2f3"), second = Hand(1, "#ff9f43");
    public FrameworkElement Root { get; }

    static Shapes.Line Hand(double w, string color) => new() { X1 = 23, Y1 = 23, StrokeThickness = w, Stroke = Ui.Brush(color), StrokeStartLineCap = PenLineCap.Round, StrokeEndLineCap = PenLineCap.Round };

    public ClockTile()
    {
        var face = new Canvas { Width = 46, Height = 46 };
        face.Children.Add(new Shapes.Ellipse { Width = 42, Height = 42, Fill = Ui.Brush("#1c1c21"), Stroke = Ui.Brush("#2c2c33"), Margin = new Thickness(2) });
        for (int i = 0; i < 12; i++)
        {
            double a = i * Math.PI / 6, r = i % 3 == 0 ? 1.3 : 0.7;
            var tick = new Shapes.Ellipse { Width = r * 2, Height = r * 2, Fill = Ui.Brush("#6b6b75") };
            Canvas.SetLeft(tick, 23 + 17 * Math.Sin(a) - r);
            Canvas.SetTop(tick, 23 - 17 * Math.Cos(a) - r);
            face.Children.Add(tick);
        }
        face.Children.Add(hour);
        face.Children.Add(minute);
        face.Children.Add(second);
        var pin = new Shapes.Ellipse { Width = 3.6, Height = 3.6, Fill = Ui.Working };
        Canvas.SetLeft(pin, 21.2);
        Canvas.SetTop(pin, 21.2);
        face.Children.Add(pin);
        var analog = new Viewbox { Width = 50, Height = 50, Child = face };
        Root = Ui.Card(Ui.Row(12, analog, Ui.Col(0, time, date)));
        Update();
    }

    static void Point(Shapes.Line l, double deg, double len) { var a = deg * Math.PI / 180; l.X2 = 23 + len * Math.Sin(a); l.Y2 = 23 - len * Math.Cos(a); }

    public void Update()
    {
        var now = DateTime.Now;
        time.Text = now.ToString("HH:mm");
        date.Text = now.ToString("ddd d MMM");
        Point(hour, now.Hour % 12 * 30 + now.Minute * 0.5, 10);
        Point(minute, now.Minute * 6, 15);
        Point(second, now.Second * 6, 16);
    }
}

// The ring starts and pauses; the reset button goes back to 25:00.
sealed class FocusTile
{
    readonly Island island;
    readonly Ring ring = new(54, 5, "#ff9f43");
    readonly ContentControl glyph = new() { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center };
    readonly TextBlock left = Ui.T("", 24, bold: true), sub = Ui.T("", 11, Ui.Muted);
    readonly Border reset, card;
    public FrameworkElement Root => card;

    public FocusTile(Island island)
    {
        this.island = island;
        ring.Inner.Children.Add(glyph);
        var start = Ui.Clickable(ring.Root, () => { island.Focus.Toggle(DateTime.Now); island.Render(); Update(); }, "Start or pause the focus timer", radius: 27);
        reset = Ui.IconButton("reset", "Reset to 25:00", () => { island.Focus.Reset(); island.Render(); Update(); });
        reset.VerticalAlignment = VerticalAlignment.Top;
        var g = new Grid { ColumnSpacing = 12 };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var text = Ui.Col(0, left, sub);
        Grid.SetColumn(text, 1);
        Grid.SetColumn(reset, 2);
        g.Children.Add(start);
        g.Children.Add(text);
        g.Children.Add(reset);
        card = Ui.Card(g);
        Update();
    }

    public void Update()
    {
        var f = island.Focus;
        ring.Value = f.Progress;
        glyph.Content = Ui.Icon(f.Running ? "pause" : "play", 14);
        left.Text = FocusTimer.Mmss(f.Left);
        left.Foreground = f.Running ? Ui.Working : Ui.Text;
        sub.Text = f.Running ? "Focusing" : f.Progress > 0 ? "Paused" : "Focus · 25 min";
        reset.Visibility = f.Progress > 0 ? Visibility.Visible : Visibility.Collapsed;
        card.BorderBrush = f.Running ? Ui.Brush("#ff9f43", 0.35) : Ui.Line;
    }
}

// CPU and RAM rings.
sealed class SystemTile
{
    readonly Ring cpu = new(58, 6, "#38bdf8"), ram = new(58, 6, "#a78bfa");
    readonly TextBlock cpuText = Ui.T("·", 14, bold: true), ramText = Ui.T("·", 14, bold: true), ramLabel = Ui.T("RAM", 10.5, Ui.Muted);
    public FrameworkElement Root { get; }

    public SystemTile()
    {
        cpu.Inner.Children.Add(Percent(cpuText));
        ram.Inner.Children.Add(Percent(ramText));
        var cpuLabel = Ui.Row(3, Ui.Icon("chip", 11, Ui.Muted), Ui.T("CPU", 10.5, Ui.Muted));
        cpuLabel.HorizontalAlignment = HorizontalAlignment.Center;
        ramLabel.HorizontalAlignment = HorizontalAlignment.Center;
        var g = new Grid();
        g.ColumnDefinitions.Add(new ColumnDefinition());
        g.ColumnDefinitions.Add(new ColumnDefinition());
        var a = Gauge(cpu.Root, cpuLabel);
        var b = Gauge(ram.Root, ramLabel);
        Grid.SetColumn(b, 1);
        g.Children.Add(a);
        g.Children.Add(b);
        Root = Ui.Card(g);
    }

    static StackPanel Gauge(FrameworkElement ring, FrameworkElement label) { var s = Ui.Col(4, ring, label); ring.HorizontalAlignment = HorizontalAlignment.Center; s.HorizontalAlignment = HorizontalAlignment.Center; return s; }
    static StackPanel Percent(TextBlock value) { var s = Ui.Row(1, value, Ui.T("%", 9, Ui.Muted)); s.HorizontalAlignment = HorizontalAlignment.Center; return s; }

    public void Update(SystemStats? s)
    {
        if (s == null) return;
        cpu.Value = s.Cpu / 100.0;
        ram.Value = s.Ram / 100.0;
        cpuText.Text = s.Cpu.ToString();
        ramText.Text = s.Ram.ToString();
        ramLabel.Text = $"RAM {s.RamUsedGb:0.#} of {s.RamTotalGb} GB";
    }
}

// Today's tokens: output total, the token mix bar and an hourly chart.
sealed class UsageTile
{
    static readonly (string Label, string Color)[] Mix = [("Output", "#ec4899"), ("Input", "#f59e0b"), ("Cache write", "#8b5cf6"), ("Cache read", "#38bdf8")];
    readonly TextBlock big = Ui.T("·", 22, bold: true), sub = Ui.T("Reading usage…", 11, Ui.Muted);
    readonly Grid mixBar = new() { Height = 8, ColumnSpacing = 2, CornerRadius = new CornerRadius(4) };
    readonly TextBlock[] legend = Mix.Select(_ => Ui.T("·", 10.5, bold: true)).ToArray();
    readonly Grid chart = new() { ColumnSpacing = 3 };
    readonly Border[] bars = new Border[24];
    public FrameworkElement Root { get; }

    public UsageTile()
    {
        var head = Ui.Row(10, Ui.IconSquare("#ec4899", "spark"), Ui.Col(0, Ui.Row(4, big, Ui.T("out today", 11, Ui.Muted)), sub));
        for (int i = 0; i < Mix.Length; i++) mixBar.Children.Add(new Border { Background = Ui.Brush(Mix[i].Color) });
        var legendPanel = new Grid { ColumnSpacing = 12, RowSpacing = 3 };
        legendPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        legendPanel.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        legendPanel.RowDefinitions.Add(new RowDefinition());
        legendPanel.RowDefinitions.Add(new RowDefinition());
        for (int i = 0; i < Mix.Length; i++)
        {
            var item = Ui.Row(4, new Border { Width = 7, Height = 7, CornerRadius = new CornerRadius(2), Background = Ui.Brush(Mix[i].Color) }, Ui.T(Mix[i].Label, 10.5, Ui.Muted), legend[i]);
            Grid.SetColumn(item, i % 2);
            Grid.SetRow(item, i / 2);
            legendPanel.Children.Add(item);
        }
        var left = new Grid();
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        left.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        left.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(mixBar, 1);
        Grid.SetRow(legendPanel, 2);
        mixBar.VerticalAlignment = VerticalAlignment.Center;
        legendPanel.VerticalAlignment = VerticalAlignment.Bottom;
        left.Children.Add(head);
        left.Children.Add(mixBar);
        left.Children.Add(legendPanel);

        for (int h = 0; h < 24; h++)
        {
            chart.ColumnDefinitions.Add(new ColumnDefinition());
            bars[h] = new Border { CornerRadius = new CornerRadius(2), VerticalAlignment = VerticalAlignment.Bottom, Height = 4 };
            Grid.SetColumn(bars[h], h);
            chart.Children.Add(bars[h]);
        }
        var axis = new Grid();
        foreach (var (label, at) in new[] { ("0h", HorizontalAlignment.Left), ("12h", HorizontalAlignment.Center), ("24h", HorizontalAlignment.Right) })
            axis.Children.Add(new TextBlock { Text = label, FontFamily = Ui.Mono, FontSize = 9.5, Foreground = Ui.Dim, HorizontalAlignment = at });
        var right = new Grid { RowSpacing = 4 };
        right.RowDefinitions.Add(new RowDefinition());
        right.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        Grid.SetRow(axis, 1);
        right.Children.Add(chart);
        right.Children.Add(axis);

        var g = new Grid { ColumnSpacing = 16 };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1.1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition());
        Grid.SetColumn(right, 1);
        g.Children.Add(left);
        g.Children.Add(right);
        Root = Ui.Card(g);
        Update(null);
    }

    public void Update(UsageToday? u)
    {
        long[] values = u == null ? [0, 0, 0, 0] : [u.Output, u.Input, u.CacheWrite, u.CacheRead];
        long total = Math.Max(1, values.Sum());
        mixBar.ColumnDefinitions.Clear();
        for (int i = 0; i < values.Length; i++)
        {
            mixBar.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(u == null ? 1 : Math.Max(values[i], total * 0.015), GridUnitType.Star) });
            Grid.SetColumn((FrameworkElement)mixBar.Children[i], i);
            legend[i].Text = u == null ? "·" : Ui.Tokens(values[i]);
        }
        if (u != null)
        {
            big.Text = Ui.Tokens(u.Output);
            sub.Text = $"{u.Messages} replies · {Ui.Tokens(u.Total)} processed";
        }
        var hours = u?.Hours ?? new long[24];
        long max = Math.Max(1, hours.Max());
        int now = DateTime.Now.Hour;
        double height = Math.Max(20, chart.ActualHeight > 0 ? chart.ActualHeight : 86);
        for (int h = 0; h < 24; h++)
        {
            bars[h].Height = Math.Max(0.05, (double)hours[h] / max) * height;
            bars[h].Background = h == now ? Ui.Brush("#ec4899") : h > now ? Ui.Brush("#222228") : Ui.Brush("#ec4899", 0.55);
        }
    }
}

// Git for the session's folder: branch, ahead or behind, change chips, changed files, last commit.
sealed class GitTile
{
    readonly Border card = Ui.Card(new Grid());
    public FrameworkElement Root => card;

    public GitTile() => Update(null, false);

    static Border Chip(string text, string bg, double alpha, string fg) => Ui.Pill(new TextBlock { Text = text, FontFamily = Ui.Mono, FontSize = 10.5, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = Ui.Brush(fg) }, Ui.Brush(bg, alpha));

    public void Update(GitInfo? git, bool loaded)
    {
        var icon = Ui.IconSquare("#8b5cf6", "branch");
        if (git == null)
        {
            card.Child = Ui.Row(10, icon, Ui.Col(0, Ui.T(loaded ? "Not a git repo" : "Git", 13, bold: true), Ui.T(loaded ? "This session's folder has no repository" : "Reading…", 11, Ui.Muted)));
            return;
        }
        var badges = Ui.Row(5);
        if (git.Ahead > 0) badges.Children.Add(Chip($"↑ {git.Ahead}", "#38bdf8", 0.14, "#7dd3fc"));
        if (git.Behind > 0) badges.Children.Add(Chip($"↓ {git.Behind}", "#fb923c", 0.14, "#fdba74"));
        if (git.Ahead == 0 && git.Behind == 0) badges.Children.Add(Chip("in sync", "#ffffff", 0.06, "#8d8d96"));
        if (git.Ahead == null) badges.Children.Add(Chip("local only", "#ffffff", 0.06, "#8d8d96"));
        if (git.Changed == 0) badges.Children.Add(Chip("✓ clean", "#ffffff", 0.06, "#8d8d96"));
        if (git.Added > 0) badges.Children.Add(Chip($"+{git.Added}", "#4ade80", 0.14, "#86efac"));
        if (git.Modified > 0) badges.Children.Add(Chip($"~{git.Modified}", "#ffcf6e", 0.14, "#fcd88a"));
        if (git.Deleted > 0) badges.Children.Add(Chip($"−{git.Deleted}", "#f87171", 0.14, "#fca5a5"));
        badges.Margin = new Thickness(0, 3, 0, 0);
        var head = Ui.Row(10, icon, Ui.Col(0, Ui.T(git.Branch, 13, bold: true), badges));

        var files = new Grid { ColumnSpacing = 12, RowSpacing = 2 };
        files.ColumnDefinitions.Add(new ColumnDefinition());
        files.ColumnDefinitions.Add(new ColumnDefinition());
        files.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        files.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var items = git.Files.Select(f =>
        {
            var (letter, color) = f.Kind switch { "add" => ("A", "#86efac"), "del" => ("D", "#fca5a5"), _ => ("M", "#fcd88a") };
            var fk = new Border { Width = 14, Height = 14, CornerRadius = new CornerRadius(4), Background = Ui.Brush(color, 0.16), Child = new TextBlock { Text = letter, FontSize = 9, FontWeight = Microsoft.UI.Text.FontWeights.Bold, Foreground = Ui.Brush(color), HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center } };
            return (FrameworkElement)Ui.Row(6, fk, Ui.T(f.Path, 11, Ui.Brush("#c9c9d1"), mono: true));
        }).ToList();
        if (git.Changed > git.Files.Count) items.Add(Ui.T($"+{git.Changed - git.Files.Count} more", 11, Ui.Muted, mono: true));
        for (int i = 0; i < items.Count && i < 4; i++) { Grid.SetColumn(items[i], i % 2); Grid.SetRow(items[i], i / 2); files.Children.Add(items[i]); }

        var commit = new TextBlock { FontSize = 11.5, TextTrimming = TextTrimming.CharacterEllipsis, VerticalAlignment = VerticalAlignment.Center };
        commit.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = git.Subject.Length > 0 ? git.Subject : "No commits yet", Foreground = Ui.Text });
        commit.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = $" · {git.Author} · {git.When}", Foreground = Ui.Muted });
        var commitRow = Ui.Row(6, new Shapes.Ellipse { Width = 8, Height = 8, Stroke = Ui.Brush("#8b5cf6"), StrokeThickness = 2 }, commit);

        var g = new Grid();
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        g.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        g.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        files.VerticalAlignment = VerticalAlignment.Center;
        Grid.SetRow(files, 1);
        Grid.SetRow(commitRow, 2);
        g.Children.Add(head);
        g.Children.Add(files);
        g.Children.Add(commitRow);
        card.Child = g;
    }
}

// Your favourite photos, crossfading with a slow zoom. Click for the next one.
sealed class PhotosTile
{
    const int SlideMs = 6000;
    static int slideIndex; // survives the dashboard closing, so the show continues where it was
    readonly Island island;
    readonly Grid stage = new();
    readonly StackPanel dots = new() { Orientation = Orientation.Horizontal, Spacing = 4, HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Bottom, Margin = new Thickness(0, 0, 0, 8) };
    IReadOnlyList<string> photos = [];
    DateTime shownAt;
    public FrameworkElement Root { get; }

    public PhotosTile(Island island)
    {
        this.island = island;
        photos = PhotosWidget.List(island.Prefs.Photos());
        var shade = new Border { Background = new LinearGradientBrush { StartPoint = new(0, 0), EndPoint = new(0, 1), GradientStops = { new GradientStop { Color = Ui.Hex("#000000", 0), Offset = 0.65 }, new GradientStop { Color = Ui.Hex("#000000", 0.45), Offset = 1 } } } };
        var frame = new Grid { Children = { stage, shade, dots } };
        if (photos.Count == 0)
        {
            var empty = Ui.Col(6, Ui.IconSquare("#f59e0b", "image"), Ui.T("Add your favourite photos", 13, bold: true), new TextBlock { Text = "Click, then drop images in the folder that opens", FontSize = 11, Foreground = Ui.Muted, TextWrapping = TextWrapping.Wrap });
            empty.HorizontalAlignment = HorizontalAlignment.Left;
            frame = new Grid { Padding = new Thickness(14), Children = { empty } };
        }
        var card = new Border { CornerRadius = new CornerRadius(16), BorderBrush = Ui.Line, BorderThickness = new Thickness(1), Background = Ui.Brush("#111114"), Child = frame };
        var click = Ui.Clickable(card, () => { if (photos.Count == 0) OpenFolder(); else Next(); }, photos.Count == 0 ? "Open the photos folder" : "Next photo", radius: 16, hover: Ui.Transparent);
        click.VerticalAlignment = VerticalAlignment.Stretch;
        Root = click;
        if (photos.Count > 0) Show();
    }

    void OpenFolder()
    {
        var dir = island.Prefs.Photos();
        Directory.CreateDirectory(dir);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dir) { UseShellExecute = true });
    }

    void Next() { slideIndex = (slideIndex + 1) % photos.Count; Show(); }

    void Show()
    {
        shownAt = DateTime.Now;
        var file = photos[slideIndex % photos.Count];
        var image = new Image { Stretch = Stretch.UniformToFill, Source = new BitmapImage(new Uri(file)) { DecodePixelWidth = 480 } }; // decode small: a 12 MP photo is 48 MB at full size
        stage.Children.Add(image);
        var v = ElementCompositionPreview.GetElementVisual(image);
        var c = v.Compositor;
        v.Opacity = 0;
        var fade = c.CreateScalarKeyFrameAnimation();
        fade.InsertKeyFrame(1, 1);
        fade.Duration = TimeSpan.FromMilliseconds(900);
        v.StartAnimation("Opacity", fade);
        image.SizeChanged += (_, e) =>
        {
            v.CenterPoint = new Vector3((float)e.NewSize.Width / 2, (float)e.NewSize.Height / 2, 0);
            var zoom = c.CreateVector3KeyFrameAnimation(); // the slow Ken Burns zoom, run by the compositor
            zoom.InsertKeyFrame(0, new Vector3(1.08f, 1.08f, 1));
            zoom.InsertKeyFrame(1, Vector3.One, c.CreateCubicBezierEasingFunction(new Vector2(0, 0), new Vector2(0.58f, 1)));
            zoom.Duration = TimeSpan.FromSeconds(7);
            v.StartAnimation("Scale", zoom);
        };
        while (stage.Children.Count > 2) stage.Children.RemoveAt(0); // keep the previous one under the fade
        dots.Children.Clear();
        if (photos.Count > 1 && photos.Count <= 8)
            for (int i = 0; i < photos.Count; i++)
                dots.Children.Add(new Border { Width = i == slideIndex ? 14 : 5, Height = 5, CornerRadius = new CornerRadius(3), Background = Ui.Brush("#ffffff", i == slideIndex ? 1 : 0.4) });
        else if (photos.Count > 8)
            dots.Children.Add(Ui.Pill(Ui.T($"{slideIndex + 1} / {photos.Count}", 10, Ui.Text, bold: true, mono: true), Ui.Brush("#000000", 0.45), new Thickness(6, 1, 6, 1)));
    }

    public void Tick()
    {
        if (photos.Count > 1 && (DateTime.Now - shownAt).TotalMilliseconds >= SlideMs) Next();
    }
}
