using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Modak;

// The hover dashboard: toolbar, then the tile grid. Tiles arrive one commit at a time.
sealed class Dashboard : IView
{
    readonly Island island;
    public FrameworkElement Root { get; }

    public Dashboard(Island island)
    {
        this.island = island;
        var grid = new Grid { Height = 346, ColumnSpacing = 8, RowSpacing = 8 };
        Root = new StackPanel { Width = 980, Padding = new Thickness(10, 6, 10, 10), Children = { Toolbar.Build(island, false), grid } };
    }

    public void Update() { }
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
        var sound = Ui.IconButton(island.Prefs.Sound ? "sound" : "mute", island.Prefs.Sound ? "Mute" : "Unmute", () => island.SavePrefs(p => p.Sound = !p.Sound));
        var gear = Ui.IconButton("gear", "Settings", () => { island.Page = settings ? "grid" : "settings"; island.Render(); });
        var right = Ui.Row(2, sound, gear);
        Grid.SetColumn(right, 1);
        g.Children.Add(left);
        g.Children.Add(right);
        return g;
    }
}
