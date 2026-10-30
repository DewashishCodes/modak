using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Modak;

// The hover dashboard: toolbar, then the tile grid. Widgets are read off the UI thread while it is open;
// git and usage refresh less often than CPU and RAM because they cost more.
sealed class Dashboard : IView
{
    const int PollMs = 2000, GitMs = 10_000, UsageMs = 30_000;
    static readonly UsageWidget usage = new();
    static UsageToday? lastUsage;
    static DateTime usageAt, gitAt;
    static (string? Cwd, GitInfo? Info) lastGit;

    readonly Island island;
    readonly AgentTile agent;
    readonly ClockTile clock = new();
    readonly FocusTile focus;
    readonly SystemTile system = new();
    readonly UsageTile usageTile = new();
    readonly GitTile git = new();
    readonly PhotosTile photos;
    DateTime polledAt;
    bool polling;
    public FrameworkElement Root { get; }

    public Dashboard(Island island)
    {
        this.island = island;
        agent = new AgentTile(island);
        focus = new FocusTile(island);
        photos = new PhotosTile(island);
        var grid = new Grid { Height = 346, ColumnSpacing = 8, RowSpacing = 8 };
        foreach (var w in new[] { 2, 1.15, 1, 1 }) grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(w, GridUnitType.Star) });
        foreach (var h in new[] { 100, 100, 130 }) grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(h) });
        Place(grid, agent.Root, 0, 0, rowSpan: 2);
        Place(grid, photos.Root, 1, 0, rowSpan: 2);
        Place(grid, clock.Root, 2, 0);
        Place(grid, focus.Root, 3, 0);
        Place(grid, system.Root, 2, 1, colSpan: 2);
        Place(grid, usageTile.Root, 0, 2, colSpan: 2);
        Place(grid, git.Root, 2, 2, colSpan: 2);
        Root = new StackPanel { Width = 980, Padding = new Thickness(10, 6, 10, 10), Children = { Toolbar.Build(island, false), grid } };
        if (lastUsage != null) usageTile.Update(lastUsage);
        if (lastGit.Cwd == island.Selected?.Cwd) git.Update(lastGit.Info, lastGit.Cwd != null);
        Poll();
    }

    static void Place(Grid g, FrameworkElement e, int col, int row, int colSpan = 1, int rowSpan = 1)
    {
        Grid.SetColumn(e, col); Grid.SetRow(e, row); Grid.SetColumnSpan(e, colSpan); Grid.SetRowSpan(e, rowSpan);
        g.Children.Add(e);
    }

    // Called on every state change and four times a second.
    public void Update()
    {
        agent.Update();
        clock.Update();
        focus.Update();
        photos.Tick();
        if ((DateTime.Now - polledAt).TotalMilliseconds >= PollMs) Poll();
    }

    async void Poll()
    {
        if (polling) return;
        polling = true;
        polledAt = DateTime.Now;
        var cwd = island.Selected?.Cwd;
        bool wantGit = cwd != lastGit.Cwd || (DateTime.Now - gitAt).TotalMilliseconds >= GitMs;
        bool wantUsage = lastUsage == null || (DateTime.Now - usageAt).TotalMilliseconds >= UsageMs;
        try
        {
            var stats = await Task.Run(SystemWidget.Read);
            system.Update(stats);
            if (wantGit)
            {
                lastGit = (cwd, await GitWidget.Read(cwd));
                gitAt = DateTime.Now;
            }
            git.Update(lastGit.Info, true);
            if (wantUsage)
            {
                lastUsage = await Task.Run(() => usage.Read());
                usageAt = DateTime.Now;
            }
            usageTile.Update(lastUsage);
        }
        catch (Exception x) { CrashLog.Write(x); }
        finally { polling = false; }
    }

    // Reads today's usage once at startup, off the UI thread, so the first hover is instant.
    public static void Warm() => Task.Run(() => { lastUsage = usage.Read(); usageAt = DateTime.Now; });
}

// Settings: connections, sound, autostart and the photos folder.
sealed class SettingsView : IView
{
    readonly Island island;
    public FrameworkElement Root { get; }

    public SettingsView(Island island)
    {
        this.island = island;
        var list = new StackPanel { Spacing = 6, Padding = new Thickness(2, 2, 2, 4) };
        list.Children.Add(Toggle("Connected to Claude Code", "Modak's hooks in ~/.claude/settings.json", Connections.Claude, v => Connections.SetClaude(v, island.Prefs)));
        list.Children.Add(Toggle("Connected to Codex", "A forwarder hook in ~/.codex/hooks.json", Connections.Codex, Connections.SetCodex));
        list.Children.Add(Toggle("Sound", "Chime when work is done", island.Prefs.Sound, v => island.SavePrefs(p => p.Sound = v)));
        list.Children.Add(Toggle("Start with Windows", "Open Modak when you sign in", Connections.Autostart, v => Connections.Autostart = v));

        var folder = new Grid { ColumnSpacing = 12 };
        folder.ColumnDefinitions.Add(new ColumnDefinition());
        folder.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        var actions = Ui.Row(6, Link("Open", OpenFolder), Link("Change", ChangeFolder));
        Grid.SetColumn(actions, 1);
        folder.Children.Add(Ui.Col(0, Ui.T("Photos folder", 13, bold: true), Ui.T(island.Prefs.Photos(), 11, Ui.Muted)));
        folder.Children.Add(actions);
        list.Children.Add(Ui.Card(folder, 12));

        var tip = new TextBlock { FontSize = 11, Foreground = Ui.Muted, Margin = new Thickness(4, 4, 4, 0), TextWrapping = TextWrapping.Wrap,
            Text = "Tips: hold Shift over Modak to click what is behind it. While an approval waits, Ctrl+Alt+Y allows and Ctrl+Alt+N denies." };
        list.Children.Add(tip);
        Root = new StackPanel { Width = 980, Padding = new Thickness(10, 6, 10, 10), Children = { Toolbar.Build(island, true), list } };
    }

    // A full-width row with a switch, like the Electron .setting button.
    Border Toggle(string label, string hint, bool on, Action<bool> change)
    {
        var knob = new Border { Width = 14, Height = 14, CornerRadius = new CornerRadius(7), Background = Ui.Text, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(on ? 16 : 2, 0, 0, 0) };
        var track = new Border { Width = 32, Height = 18, CornerRadius = new CornerRadius(9), Background = on ? Ui.Brush("#4f9b5f") : Ui.Brush("#2a2a30"), Child = knob, VerticalAlignment = VerticalAlignment.Center };
        var row = new Grid { ColumnSpacing = 12 };
        row.ColumnDefinitions.Add(new ColumnDefinition());
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(track, 1);
        row.Children.Add(Ui.Col(0, Ui.T(label, 13, bold: true), Ui.T(hint, 11, Ui.Muted)));
        row.Children.Add(track);
        var b = Ui.Clickable(row, () => { change(!on); island.Rebuild(); }, label, Ui.Tile, Ui.TileHi, 12, new Thickness(12, 10, 12, 10));
        b.BorderBrush = Ui.Line;
        b.BorderThickness = new Thickness(1);
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetHelpText(b, on ? "On" : "Off");
        return b;
    }

    static Border Link(string text, Action click) => Ui.Clickable(Ui.T(text, 12, Ui.Brush("#f6d6e3"), bold: true), click, text, Ui.Brush("#1f1f23"), Ui.Brush("#2a2a30"), 8, new Thickness(10, 4, 10, 4));

    void OpenFolder()
    {
        var dir = island.Prefs.Photos();
        Directory.CreateDirectory(dir);
        System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(dir) { UseShellExecute = true });
    }

    async void ChangeFolder()
    {
        var picker = new Windows.Storage.Pickers.FolderPicker { SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.PicturesLibrary };
        picker.FileTypeFilter.Add("*");
        WinRT.Interop.InitializeWithWindow.Initialize(picker, island.Handle);
        var folder = await picker.PickSingleFolderAsync();
        if (folder != null) { island.SavePrefs(p => p.PhotosDir = folder.Path); island.Rebuild(); }
    }

    public void Update() { }
}

static class Toolbar
{
    public static FrameworkElement Build(Island island, bool settings)
    {
        var g = new Grid { Height = 30, Padding = new Thickness(2, 0, 2, 4) };
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        g.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        FrameworkElement left = settings
            ? Ui.IconButton("back", "Back", () => { island.Page = "grid"; island.Render(); })
            : new TextBlock { Text = "Modak", FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = Ui.Muted, Margin = new Thickness(6, 0, 0, 0), VerticalAlignment = VerticalAlignment.Center };
        left.HorizontalAlignment = HorizontalAlignment.Left;
        var sound = Ui.IconButton(island.Prefs.Sound ? "sound" : "mute", island.Prefs.Sound ? "Mute" : "Unmute", () => { island.SavePrefs(p => p.Sound = !p.Sound); island.Rebuild(); });
        var gear = Ui.IconButton("gear", "Settings", () => { island.Page = settings ? "grid" : "settings"; island.Render(); });
        var right = Ui.Row(2, sound, gear);
        Grid.SetColumn(right, 1);
        g.Children.Add(left);
        g.Children.Add(right);
        return g;
    }
}
