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

sealed class SettingsView : IView
{
    public FrameworkElement Root { get; }
    public SettingsView(Island island) => Root = new StackPanel { Width = 980, Padding = new Thickness(10, 6, 10, 10), Children = { Toolbar.Build(island, true) } };
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
        var sound = Ui.IconButton(island.Prefs.Sound ? "sound" : "mute", island.Prefs.Sound ? "Mute" : "Unmute", () => { island.SavePrefs(p => p.Sound = !p.Sound); island.Rebuild(); });
        var gear = Ui.IconButton("gear", "Settings", () => { island.Page = settings ? "grid" : "settings"; island.Render(); });
        var right = Ui.Row(2, sound, gear);
        Grid.SetColumn(right, 1);
        g.Children.Add(left);
        g.Children.Add(right);
        return g;
    }
}
