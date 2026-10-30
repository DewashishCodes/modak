using Microsoft.UI;
using Microsoft.UI.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Windows.UI;
using Shapes = Microsoft.UI.Xaml.Shapes;

namespace Modak;

// Design tokens and small builders, mirroring src/index.css so the native island looks the same.
static class Ui
{
    public static Color Hex(string hex, double alpha = 1)
    {
        var v = Convert.ToUInt32(hex.TrimStart('#'), 16);
        return Color.FromArgb((byte)Math.Round(alpha * 255), (byte)(v >> 16), (byte)(v >> 8), (byte)v);
    }
    public static SolidColorBrush Brush(string hex, double alpha = 1) => new(Hex(hex, alpha));

    public static readonly Color Bg = Hex("#0a0a0b");
    public static readonly SolidColorBrush Tile = Brush("#141416"), TileHi = Brush("#1a1a1d"), Line = Brush("#ffffff", 0.06),
        Text = Brush("#f2f2f3"), Muted = Brush("#8d8d96"), Dim = Brush("#5c5c64"), Working = Brush("#ff9f43"), Asking = Brush("#ffcf6e"),
        Done = Brush("#4ade80"), Allow = Brush("#c8f5a8"), Transparent = new(Colors.Transparent);
    public static readonly FontFamily Mono = new("Cascadia Code, Cascadia Mono, Consolas");
    public static readonly FontFamily Sans = new("Segoe UI Variable Text, Segoe UI");

    public static TextBlock T(string text, double size = 12, Brush? color = null, bool bold = false, bool mono = false) => new()
    {
        Text = text, FontSize = size, Foreground = color ?? Text, FontFamily = mono ? Mono : Sans,
        FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap,
        VerticalAlignment = VerticalAlignment.Center,
    };

    public static StackPanel Row(double gap, params UIElement[] children)
    {
        var s = new StackPanel { Orientation = Orientation.Horizontal, Spacing = gap, VerticalAlignment = VerticalAlignment.Center };
        foreach (var c in children) s.Children.Add(c);
        return s;
    }
    public static StackPanel Col(double gap, params UIElement[] children)
    {
        var s = new StackPanel { Spacing = gap, VerticalAlignment = VerticalAlignment.Center };
        foreach (var c in children) s.Children.Add(c);
        return s;
    }

    // A dashboard tile: dark rounded card with a hairline border.
    public static Border Card(UIElement child, double radius = 16, Thickness? padding = null) => new()
    {
        Background = Tile, BorderBrush = Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(radius),
        Padding = padding ?? new Thickness(12, 10, 12, 10), Child = child,
    };

    // A coloured rounded square holding a white icon, like an app icon.
    public static Border IconSquare(string color, string icon) => new()
    {
        Width = 30, Height = 30, CornerRadius = new CornerRadius(9), Background = Brush(color), Child = Icon(icon, 15, Brush("#ffffff")),
    };

    public static Border Pill(UIElement child, Brush bg, Thickness? padding = null) => new()
    {
        Background = bg, CornerRadius = new CornerRadius(999), Padding = padding ?? new Thickness(7, 1, 7, 1), Child = child, VerticalAlignment = VerticalAlignment.Center,
    };

    public static Shapes.Ellipse Dot(Brush color, double size = 7) => new() { Width = size, Height = size, Fill = color, VerticalAlignment = VerticalAlignment.Center };

    // The stroke icons from src/icons.jsx (16x16 viewBox), drawn as XAML paths.
    static readonly Dictionary<string, (string Data, bool Fill)> Icons = new()
    {
        ["play"] = ("M5 3.5v9l7-4.5z", true),
        ["pause"] = ("M4 4.5a1 1 0 0 1 1-1h.6a1 1 0 0 1 1 1v7a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1zM9.4 4.5a1 1 0 0 1 1-1h.6a1 1 0 0 1 1 1v7a1 1 0 0 1-1 1h-.6a1 1 0 0 1-1-1z", true),
        ["gear"] = ("M10 8a2 2 0 1 1-4 0 2 2 0 0 1 4 0zM6.9 1.8h2.2l.4 1.7 1.3.7 1.6-.6 1.1 1.9-1.3 1.2v1.5l1.3 1.2-1.1 1.9-1.6-.6-1.3.7-.4 1.7H6.9l-.4-1.7-1.3-.7-1.6.6-1.1-1.9 1.3-1.2V7.2L2.5 6l1.1-1.9 1.6.6 1.3-.7z", false),
        ["sound"] = ("M3 6.2h2.2L8.5 3.5v9L5.2 9.8H3zM11 5.5a3.5 3.5 0 0 1 0 5M12.8 3.8a6 6 0 0 1 0 8.4", false),
        ["mute"] = ("M3 6.2h2.2L8.5 3.5v9L5.2 9.8H3zM11 6l3.5 4M14.5 6L11 10", false),
        ["branch"] = ("M6 3.5a1.5 1.5 0 1 1-3 0 1.5 1.5 0 0 1 3 0zM6 12.5a1.5 1.5 0 1 1-3 0 1.5 1.5 0 0 1 3 0zM13 5.5a1.5 1.5 0 1 1-3 0 1.5 1.5 0 0 1 3 0zM4.5 5v6M11.5 7c0 3-7 2-7 4", false),
        ["check"] = ("M3.5 8.5l3 3 6-7", false),
        ["back"] = ("M10 3.5L5.5 8l4.5 4.5", false),
        ["terminal"] = ("M3.8 2.8h8.4a2 2 0 0 1 2 2v6.4a2 2 0 0 1-2 2H3.8a2 2 0 0 1-2-2V4.8a2 2 0 0 1 2-2zM4.5 6.5l2 1.5-2 1.5M8.5 10h3", false),
        ["chip"] = ("M5.5 4h5a1.5 1.5 0 0 1 1.5 1.5v5a1.5 1.5 0 0 1-1.5 1.5h-5A1.5 1.5 0 0 1 4 10.5v-5A1.5 1.5 0 0 1 5.5 4zM6 1.8V4M10 1.8V4M6 12v2.2M10 12v2.2M1.8 6H4M1.8 10H4M12 6h2.2M12 10h2.2", false),
        ["spark"] = ("M8 1.5l1.6 4.9 4.9 1.6-4.9 1.6L8 14.5l-1.6-4.9L1.5 8l4.9-1.6z", false),
        ["image"] = ("M4 3h8a2 2 0 0 1 2 2v6a2 2 0 0 1-2 2H4a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2zM7.2 6.5a1.2 1.2 0 1 1-2.4 0 1.2 1.2 0 0 1 2.4 0zM2.5 11.5l3.5-3 2.5 2 2-1.5 3 2.5", false),
        ["reset"] = ("M3 8a5 5 0 1 0 5-5 5.2 5.2 0 0 0-3.6 1.5L3 6M3 2.8V6h3.2", false),
    };

    public static FrameworkElement Icon(string name, double size = 16, Brush? color = null)
    {
        var (data, fill) = Icons[name];
        var stroke = fill ? "" : "Stroke='#FFFFFFFF' StrokeThickness='1.6' StrokeLineJoin='Round' StrokeStartLineCap='Round' StrokeEndLineCap='Round'";
        var path = (Shapes.Path)XamlReader.Load($"<Path xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' Data='{data}' {stroke} {(fill ? "Fill='#FFFFFFFF'" : "")}/>");
        if (fill) path.Fill = color ?? Text; else path.Stroke = color ?? Text;
        return new Viewbox { Width = size, Height = size, Child = new Canvas { Width = 16, Height = 16, Children = { path } }, VerticalAlignment = VerticalAlignment.Center };
    }

    // Clickable area built from a plain Border: templated controls (Button, ToolTip) need XamlControlsResources,
    // which cannot load in an app without App.xaml. Hover lightens the background, press dims it a little.
    public static Border Clickable(UIElement content, Action click, string name, Brush? bg = null, Brush? hover = null, double radius = 8, Thickness? padding = null)
    {
        bg ??= Transparent;
        var b = new Border { Child = content, Background = bg, CornerRadius = new CornerRadius(radius), Padding = padding ?? new Thickness(0), VerticalAlignment = VerticalAlignment.Center };
        Microsoft.UI.Xaml.Automation.AutomationProperties.SetName(b, name);
        b.PointerEntered += (_, _) => b.Background = hover ?? Brush("#1f1f23");
        b.PointerExited += (_, _) => { b.Background = bg; b.Opacity = 1; };
        b.PointerPressed += (_, _) => b.Opacity = 0.8;
        b.PointerReleased += (_, _) => b.Opacity = 1;
        b.Tapped += (_, e) => { e.Handled = true; click(); };
        return b;
    }

    // Like .icon-btn: a 26px square with a muted icon.
    public static Border IconButton(string icon, string name, Action click, double size = 26) =>
        Clickable(new Grid { Width = size, Height = size, Children = { Icon(icon, 14, Muted) } }, click, name);

    // Like .pill-btn: a rounded label button.
    public static Border TextButton(string text, Brush bg, Brush fg, Action click, Brush? hover = null) =>
        Clickable(T(text, 12, fg, bold: true), click, text, bg, hover ?? bg, 999, new Thickness(12, 4, 12, 4));

    // A clickable area with no visible chrome.
    public static Border Bare(UIElement content, Action click, string name) => Clickable(content, click, name, hover: Transparent, radius: 0);

    public static string Tokens(long n) => n >= 1_000_000 ? $"{n / 1e6:0.0}M" : n >= 1000 ? $"{Math.Round(n / 1e3)}K" : n.ToString();
}
