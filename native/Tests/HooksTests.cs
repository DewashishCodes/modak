using System.Text.Json.Nodes;
using Modak;
using Xunit;

// Ported from test/hooks.test.js, plus the Codex forwarder.
public class HooksTests
{
    const string Url = "http://127.0.0.1:47821/modak/hook";
    const string Exe = @"C:\Apps\Modak\Modak.exe";
    const string UserSettings = """{"model":"opus","hooks":{"Stop":[{"hooks":[{"type":"command","command":"echo mine"}]}],"PreToolUse":[{"matcher":"Bash","hooks":[{"type":"command","command":"lint"}]}]}}""";

    static string TmpFile(string? content = UserSettings)
    {
        var dir = Directory.CreateTempSubdirectory("modak-").FullName;
        var file = Path.Combine(dir, "settings.json");
        if (content != null) File.WriteAllText(file, content);
        return file;
    }
    static JsonObject Load(string f) => JsonNode.Parse(File.ReadAllText(f))!.AsObject();
    static bool Same(string a, string b) => JsonNode.DeepEquals(JsonNode.Parse(a), JsonNode.Parse(b));

    [Fact]
    public void Install_adds_every_event_keeps_user_settings_and_backs_up()
    {
        var f = TmpFile();
        Hooks.InstallClaude(Url, f);
        var s = Load(f);
        Assert.Equal("opus", (string?)s["model"]);
        Assert.Equal("echo mine", (string?)s["hooks"]!["Stop"]![0]!["hooks"]![0]!["command"]);
        Assert.Equal("Bash", (string?)s["hooks"]!["PreToolUse"]![0]!["matcher"]);
        Assert.True(Hooks.IsClaudeInstalled(f));
        Assert.Equal(600, (int)s["hooks"]!["PermissionRequest"]!.AsArray()[^1]!["hooks"]![0]!["timeout"]!);
        Assert.True(Same(File.ReadAllText(f + ".modak-backup"), UserSettings));
    }

    [Fact]
    public void Old_peninsula_hooks_are_stale_and_replaced()
    {
        var f = TmpFile();
        Hooks.InstallClaude("http://127.0.0.1:47821/peninsula/hook", f);
        Assert.True(Hooks.IsClaudeStale(Url, f));
        Hooks.InstallClaude(Url, f);
        Assert.False(Hooks.IsClaudeStale(Url, f));
        Assert.Equal(2, Load(f)["hooks"]!["Stop"]!.AsArray().Count); // the user's and ours
    }

    [Fact]
    public void Install_twice_does_not_duplicate_and_uninstall_restores_exactly()
    {
        var f = TmpFile();
        Hooks.InstallClaude(Url, f);
        Hooks.InstallClaude(Url, f);
        Assert.Equal(2, Load(f)["hooks"]!["Stop"]!.AsArray().Count);
        Hooks.Uninstall(f);
        Assert.True(Same(File.ReadAllText(f), UserSettings));
        Assert.False(Hooks.IsClaudeInstalled(f));
    }

    [Fact]
    public void Works_without_a_file_and_refuses_a_broken_one()
    {
        var f = TmpFile(null);
        Hooks.InstallClaude(Url, f);
        Assert.True(Hooks.IsClaudeInstalled(f));
        Hooks.Uninstall(f);
        Assert.True(Same(File.ReadAllText(f), "{}"));

        var broken = TmpFile("{ broken");
        Assert.ThrowsAny<Exception>(() => Hooks.InstallClaude(Url, broken));
        Assert.Equal("{ broken", File.ReadAllText(broken));
    }

    [Fact]
    public void Codex_gets_exe_forwarder_commands_and_replaces_the_node_ones()
    {
        var f = TmpFile();
        var node = new JsonObject { ["type"] = "command", ["command"] = "node \"C:/x/electron/modak-forward.js\" " + Url };
        var s = Load(f);
        s["hooks"]!["Stop"]!.AsArray().Add(new JsonObject { ["hooks"] = new JsonArray(node) });
        File.WriteAllText(f, s.ToJsonString());
        Assert.True(Hooks.IsCodexStale(Exe, Url, f));

        Hooks.InstallCodex(Exe, Url, f);
        Assert.True(Hooks.IsCodexInstalled(f));
        Assert.False(Hooks.IsCodexStale(Exe, Url, f));
        var stop = Load(f)["hooks"]!["Stop"]!.AsArray();
        Assert.Equal(2, stop.Count);
        Assert.Equal($"\"{Exe}\" --forward {Url}", (string?)stop[^1]!["hooks"]![0]!["command"]);
        Assert.Null(Load(f)["hooks"]!["Notification"]);
        Hooks.Uninstall(f);
        Assert.True(Same(File.ReadAllText(f), UserSettings));
    }
}

public sealed class ForwarderTests : IDisposable
{
    readonly Hub hub = new();
    readonly HookServer server;
    readonly string url;
    Snapshot last = new([], []);

    public ForwarderTests()
    {
        hub.Changed += s => last = s;
        server = new HookServer(hub);
        url = $"http://127.0.0.1:{server.Listen(0)}{Hub.HookPath}";
    }
    public void Dispose() => server.Dispose();

    static async Task<string> Forward(string url, string input)
    {
        var o = new StringWriter();
        await Forwarder.Run(url, new StringReader(input), o);
        return o.ToString();
    }

    [Fact]
    public async Task Tags_codex_prints_empty_object_and_relays_an_allow()
    {
        const string patch = """{"session_id":"c1","hook_event_name":"PreToolUse","tool_name":"apply_patch","tool_input":{"command":"*** Update File: src/app.py\n-x = 1\n+x = 2"}}""";
        Assert.Equal("{}", await Forward(url, patch));
        Assert.Equal("codex", last.Sessions[0].Agent);
        Assert.Equal("Editing app.py", last.Sessions[0].Activity);

        var held = Forward(url, patch.Replace("PreToolUse", "PermissionRequest"));
        for (int i = 0; i < 200 && last.Approvals.Count == 0; i++) await Task.Delay(10);
        hub.Decide(last.Approvals[0].Id, "allow");
        Assert.Equal("allow", (string?)JsonNode.Parse(await held)!["hookSpecificOutput"]!["decision"]!["behavior"]);
    }

    [Fact]
    public async Task Answers_empty_object_for_junk_or_when_modak_is_not_running()
    {
        Assert.Equal("{}", await Forward(url, "not json"));
        server.Dispose();
        Assert.Equal("{}", await Forward(url, """{"session_id":"c1","hook_event_name":"Stop"}"""));
    }
}
