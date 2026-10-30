using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;

namespace Modak;

// Collapsed: Modak's face only; with a focus timer running, a ring and the countdown next to it.
sealed class NotchView : IView
{
    readonly Island island;
    readonly Mascot mascot = new(30);
    readonly Ring ring = new(16, 2.5, "#ff9f43");
    readonly TextBlock left = Ui.T("", 12, Ui.Brush("#ffd2a6"), bold: true, mono: true);
    public FrameworkElement Root { get; }

    public NotchView(Island island)
    {
        this.island = island;
        var row = Ui.Row(10, mascot.Root);
        if (island.Focus.Running) row.Children.Add(Ui.Row(6, ring.Root, left));
        Root = new Grid { Width = island.Focus.Running ? 150 : 76, Height = 34, Children = { new Border { Child = row, HorizontalAlignment = HorizontalAlignment.Center } } };
        Update();
    }

    public void Update()
    {
        mascot.Set(island.Status, island.Primary?.Activity, island.Love);
        ring.Value = island.Focus.Progress;
        left.Text = FocusTimer.Mmss(island.Focus.Left);
    }
}

// "webshop: Allow git push origin main?" with Allow, Deny and answer in terminal. The text opens the details.
sealed class ApprovalBar : IView
{
    readonly Island island;
    readonly Approval card;
    readonly TextBlock more = Ui.T("", 11, Ui.Muted);
    readonly Border details;
    public FrameworkElement Root { get; }

    public ApprovalBar(Island island, Approval card)
    {
        this.island = island;
        this.card = card;
        var ask = new TextBlock { FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, FontFamily = Ui.Sans };
        ask.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = card.Folder + ": ", Foreground = Ui.Muted });
        ask.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = "Allow ", Foreground = Ui.Text });
        ask.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = card.Ask, FontFamily = Ui.Mono, FontSize = 12, FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = Ui.Text });
        ask.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = "?", Foreground = Ui.Text });

        details = new Border { Padding = new Thickness(12, 0, 12, 12), Visibility = Visibility.Collapsed, Child = PreviewBox.Build(card.Preview ?? new Preview("answer", Text: card.Detail), false, 150) };
        var askButton = Ui.Bare(ask, () => { details.Visibility = details.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible; island.Resize(); }, "Show details");

        var row = new Grid { Height = 40, Padding = new Thickness(16, 0, 10, 0), ColumnSpacing = 8 };
        foreach (var w in new[] { GridLength.Auto, new GridLength(1, GridUnitType.Star), GridLength.Auto, GridLength.Auto, GridLength.Auto, GridLength.Auto })
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = w });
        Place(row, 0, Ui.Dot(Ui.Asking));
        Place(row, 1, askButton);
        Place(row, 2, more);
        var allow = Ui.TextButton("Allow", Ui.Allow, Ui.Brush("#11210a"), () => island.Decide("allow"), Ui.Brush("#d6f8bd"));
        var deny = Ui.TextButton("Deny", Ui.Brush("#26262b"), Ui.Text, () => island.Decide("deny"), Ui.Brush("#303036"));
        Place(row, 3, allow);
        Place(row, 4, deny);
        Place(row, 5, Ui.IconButton("terminal", "Answer in terminal", () => island.Decide("terminal")));
        Root = new StackPanel { Width = 540, Children = { row, details } };
        Update();
    }

    static void Place(Grid g, int col, FrameworkElement e) { Grid.SetColumn(e, col); g.Children.Add(e); }

    public void Update()
    {
        var n = island.State.Approvals.Count - 1;
        more.Text = n > 0 ? $"+{n}" : "";
    }
}

// Chime, a green "Done in <folder>" with the start of the answer, and a happy Modak.
sealed class DoneBar : IView
{
    readonly Mascot mascot = new(30);
    public FrameworkElement Root { get; }

    public DoneBar(Island island, Session s)
    {
        var badge = new Border { Width = 20, Height = 20, CornerRadius = new CornerRadius(10), Background = Ui.Brush("#4ade80", 0.15), Child = Ui.Icon("check", 13, Ui.Done) };
        var text = new TextBlock { FontSize = 12.5, VerticalAlignment = VerticalAlignment.Center, TextTrimming = TextTrimming.CharacterEllipsis, FontFamily = Ui.Sans };
        text.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = "Done ", FontWeight = Microsoft.UI.Text.FontWeights.SemiBold, Foreground = Ui.Text });
        text.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = $"in {s.Folder}" + (s.LastAnswer.Length > 0 ? $" · {s.LastAnswer.ReplaceLineEndings(" ")}" : ""), Foreground = Ui.Muted });
        var row = new Grid { Width = 540, Height = 40, Padding = new Thickness(16, 0, 10, 0), ColumnSpacing = 8 };
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Grid.SetColumn(text, 1);
        Grid.SetColumn(mascot.Root, 2);
        row.Children.Add(badge);
        row.Children.Add(text);
        row.Children.Add(mascot.Root);
        mascot.Set("done", null, 0);
        Root = row;
    }

    public void Update() { }
}

// The dark mini terminal: a diff, a command with its output, or the final answer.
static class PreviewBox
{
    public static Border Build(Preview? p, bool working, double maxHeight = double.PositiveInfinity)
    {
        var lines = new StackPanel();
        Brush? background = null;
        if (p == null) lines.Children.Add(Line(working ? "▍" : "Waiting for Claude…", Ui.Dim));
        else if (p.Kind == "diff")
        {
            lines.Children.Add(Line(p.File, Ui.Brush("#7cb7ff")));
            foreach (var (sign, text) in p.Lines)
            {
                var del = sign == '-';
                lines.Children.Add(new Border
                {
                    Background = Ui.Brush(del ? "#f85149" : "#2ea043", del ? 0.14 : 0.16), Margin = new Thickness(-9, 0, -9, 0), Padding = new Thickness(9, 0, 9, 0),
                    Child = Line($"{sign} {text}", Ui.Brush(del ? "#ffb3ad" : "#a8f0b8")),
                });
            }
        }
        else if (p.Kind == "term")
        {
            var cmd = new TextBlock { FontFamily = Ui.Mono, FontSize = 11.5, TextTrimming = TextTrimming.CharacterEllipsis };
            cmd.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = "$ ", Foreground = Ui.Done });
            cmd.Inlines.Add(new Microsoft.UI.Xaml.Documents.Run { Text = p.Command, Foreground = Ui.Brush("#c9c9d1") });
            lines.Children.Add(cmd);
            foreach (var l in p.Output) lines.Children.Add(Line(l, Ui.Brush("#8f8f99")));
            if (p.Output.Count == 0 && working) lines.Children.Add(Line("▍", Ui.Brush("#c9c9d1")));
        }
        else
        {
            lines.Children.Add(new TextBlock { Text = p.Text, FontFamily = Ui.Mono, FontSize = 11.5, Foreground = Ui.Brush("#d6d6dc"), TextWrapping = TextWrapping.Wrap, MaxLines = 6, TextTrimming = TextTrimming.CharacterEllipsis });
        }
        return new Border
        {
            Background = background ?? Ui.Brush("#0c0c0e"), BorderBrush = Ui.Line, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(10),
            Padding = new Thickness(9, 7, 9, 7), MaxHeight = maxHeight, Child = lines,
        };
    }

    static TextBlock Line(string text, Brush color) => new()
    {
        Text = text.Length == 0 ? " " : text, FontFamily = Ui.Mono, FontSize = 11.5, LineHeight = 17, Foreground = color, TextTrimming = TextTrimming.CharacterEllipsis, TextWrapping = TextWrapping.NoWrap,
    };
}
