using System.Text.Json.Nodes;
using Modak;
using Xunit;

// Ported from test/widgets.test.js.
public class WidgetsTests
{
    static string Msg(string id, DateTime ts, int output) => new JsonObject
    {
        ["type"] = "assistant",
        ["timestamp"] = ts.ToUniversalTime().ToString("o"),
        ["message"] = new JsonObject { ["id"] = id, ["usage"] = new JsonObject { ["input_tokens"] = 1, ["output_tokens"] = output, ["cache_read_input_tokens"] = 10, ["cache_creation_input_tokens"] = 100 } },
    }.ToJsonString();

    [Fact]
    public void Usage_sums_today_once_per_message_and_reads_appended_lines()
    {
        var root = Directory.CreateTempSubdirectory("modak-usage-").FullName;
        Directory.CreateDirectory(Path.Combine(root, "proj"));
        var f = Path.Combine(root, "proj", "s.jsonl");
        var now = DateTime.Now;
        File.WriteAllText(f, string.Join("\n", Msg("a", now, 5), Msg("a", now, 5), Msg("old", now.AddDays(-2), 999), """{"type":"user"}""", ""));
        var usage = new UsageWidget(root);
        var u = usage.Read(now);
        Assert.Equal(5, u.Output);
        Assert.Equal(1, u.Messages);
        File.AppendAllText(f, Msg("b", now, 7) + "\n");
        u = usage.Read(now);
        Assert.Equal(12, u.Output);
        Assert.Equal(2 * (1 + 10 + 100) + 12, u.Total);
        Assert.Equal(12, u.Hours[now.Hour]);
        Assert.Equal(12, u.Hours.Sum());
    }

    [Fact]
    public void Usage_keeps_a_character_split_across_chunks_whole()
    {
        var root = Directory.CreateTempSubdirectory("modak-usage-").FullName;
        var f = Path.Combine(root, "s.jsonl");
        // Pad so the two bytes of "é" sit on either side of the 1 MB chunk edge.
        const string prefix = """{"type":"user","text":" """;
        var pad = new string('x', 1024 * 1024 - prefix.TrimEnd().Length - 1);
        File.WriteAllText(f, prefix.TrimEnd() + pad + "é\"}\n" + Msg("a", DateTime.Now, 3) + "\n");
        Assert.Equal(3, new UsageWidget(root).Read().Output);
    }

    [Fact]
    public void Photos_lists_images_only()
    {
        var dir = Directory.CreateTempSubdirectory("modak-photos-").FullName;
        foreach (var n in new[] { "a.jpg", "b.PNG", "notes.txt" }) File.WriteAllText(Path.Combine(dir, n), "x");
        Assert.Equal(["a.jpg", "b.PNG"], PhotosWidget.List(dir).Select(Path.GetFileName).Order());
        Assert.Empty(PhotosWidget.List(Path.Combine(dir, "missing")));
    }

    [Fact]
    public void System_stats_are_sane_percentages()
    {
        SystemWidget.Read();
        Thread.Sleep(100);
        var s = SystemWidget.Read();
        Assert.InRange(s.Cpu, 0, 100);
        Assert.InRange(s.Ram, 1, 100);
        Assert.True(s.RamTotalGb > 0);
    }

    [Fact]
    public async Task Git_works_in_this_repo_and_returns_null_outside_one()
    {
        var repo = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        var g = await GitWidget.Read(repo);
        Assert.NotNull(g);
        Assert.False(string.IsNullOrEmpty(g!.Branch));
        Assert.Null(await GitWidget.Read(Path.Combine(Path.GetTempPath(), "modak-no-such-dir")));
    }
}
