using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Modak;
using Xunit;

// Ported from test/server.test.js: the same hook events, answered over real HTTP.
public sealed class ServerTests : IDisposable
{
    readonly Hub hub = new();
    readonly HookServer server;
    readonly HttpClient http = new() { Timeout = TimeSpan.FromSeconds(10) };
    readonly int port;
    readonly List<string> done = [];
    Snapshot last = new([], []);

    public ServerTests()
    {
        hub.Changed += s => last = s;
        hub.Done += id => done.Add(id);
        server = new HookServer(hub);
        port = server.Listen(0);
    }

    public void Dispose() { server.Dispose(); http.Dispose(); }

    static string Ev(string name, string extra = "") => $$"""{"session_id":"s1","cwd":"C:\\code\\demo","hook_event_name":"{{name}}"{{(extra.Length > 0 ? "," + extra : "")}}}""";

    Task<HttpResponseMessage> Post(string body, string contentType = "application/json", string? host = null)
    {
        var req = new HttpRequestMessage(HttpMethod.Post, $"http://127.0.0.1:{port}{Hub.HookPath}") { Content = new StringContent(body, Encoding.UTF8) };
        req.Content.Headers.ContentType = new(contentType);
        if (host != null) req.Headers.Host = host;
        return http.SendAsync(req);
    }

    async Task WaitFor(Func<bool> ok) { for (int i = 0; i < 200 && !ok(); i++) await Task.Delay(10); }

    [Fact]
    public async Task Tool_events_update_activity_and_return_empty_200()
    {
        var r = await Post(Ev("PreToolUse", """ "tool_name":"Bash","tool_input":{"command":"npm test"} """));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("", await r.Content.ReadAsStringAsync());
        Assert.Equal("demo", last.Sessions[0].Folder);
        Assert.Equal("Running npm test", last.Sessions[0].Activity);
        Assert.Equal("working", last.Sessions[0].Status);
    }

    [Fact]
    public async Task Bash_preview_gets_the_output_tail_and_Stop_shows_the_answer()
    {
        await Post(Ev("PreToolUse", """ "tool_name":"Bash","tool_input":{"command":"npm test"} """));
        await Post(Ev("PostToolUse", """ "tool_name":"Bash","tool_response":{"stdout":"a\n\nb\nok 15 passing\n","stderr":""} """));
        Assert.Equal(["a", "b", "ok 15 passing"], last.Sessions[0].Preview!.Output);
        await Post(Ev("PreToolUse", """ "tool_name":"Read","tool_input":{"file_path":"x.ts"} """));
        Assert.Equal("term", last.Sessions[0].Preview!.Kind); // Read keeps the last preview
        await Post(Ev("Stop", """ "last_assistant_message":"Shipped." """));
        Assert.Equal("answer", last.Sessions[0].Preview!.Kind);
        Assert.Equal("Shipped.", last.Sessions[0].LastAnswer);
        Assert.Equal(["s1"], done);
    }

    [Fact]
    public async Task PermissionRequest_is_held_until_allow()
    {
        var p = Post(Ev("PermissionRequest", """ "tool_name":"Bash","tool_input":{"command":"rm -rf build"} """));
        await WaitFor(() => last.Approvals.Count == 1);
        Assert.Equal("rm -rf build", last.Approvals[0].Ask);
        Assert.Equal("asking", last.Sessions[0].Status);
        hub.Decide(last.Approvals[0].Id, "allow");
        var body = JsonNode.Parse(await (await p).Content.ReadAsStringAsync())!;
        Assert.Equal("allow", (string?)body["hookSpecificOutput"]!["decision"]!["behavior"]);
        Assert.Equal("PermissionRequest", (string?)body["hookSpecificOutput"]!["hookEventName"]);
        Assert.Empty(last.Approvals);
        Assert.Equal("Running rm -rf build", last.Sessions[0].Activity);
    }

    [Fact]
    public async Task Deny_has_a_message_and_terminal_is_empty()
    {
        var p = Post(Ev("PermissionRequest", """ "tool_name":"Write","tool_input":{"file_path":"a.txt"} """));
        await WaitFor(() => last.Approvals.Count == 1);
        hub.Decide(last.Approvals[0].Id, "deny");
        var body = JsonNode.Parse(await (await p).Content.ReadAsStringAsync())!;
        Assert.Equal("deny", (string?)body["hookSpecificOutput"]!["decision"]!["behavior"]);
        Assert.False(string.IsNullOrEmpty((string?)body["hookSpecificOutput"]!["decision"]!["message"]));

        p = Post(Ev("PermissionRequest", """ "tool_name":"Bash","tool_input":{"command":"ls"} """));
        await WaitFor(() => last.Approvals.Count == 1);
        hub.Decide(last.Approvals[0].Id, "terminal");
        Assert.Equal("", await (await p).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task PostToolUse_releases_a_card_answered_in_the_terminal()
    {
        var p = Post(Ev("PermissionRequest", """ "tool_name":"Bash","tool_input":{"command":"ls"} """));
        await WaitFor(() => last.Approvals.Count == 1);
        await Post(Ev("PostToolUse", """ "tool_name":"Bash" """));
        Assert.Equal("", await (await p).Content.ReadAsStringAsync());
        Assert.Empty(last.Approvals);
    }

    [Fact]
    public async Task Hanging_up_removes_the_card()
    {
        using var cts = new CancellationTokenSource();
        var p = http.PostAsync($"http://127.0.0.1:{port}{Hub.HookPath}", new StringContent(Ev("PermissionRequest", """ "tool_name":"Bash","tool_input":{"command":"ls"} """), Encoding.UTF8, "application/json"), cts.Token);
        await WaitFor(() => last.Approvals.Count == 1);
        cts.Cancel();
        await WaitFor(() => last.Approvals.Count == 0);
        Assert.Empty(last.Approvals);
    }

    [Fact]
    public async Task Dispose_hands_open_requests_back_to_the_terminal()
    {
        var p = Post(Ev("PermissionRequest", """ "tool_name":"Bash","tool_input":{"command":"ls"} """));
        await WaitFor(() => last.Approvals.Count == 1);
        server.Dispose();
        Assert.Equal("", await (await p).Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task SessionEnd_removes_the_session_and_Codex_is_tagged()
    {
        await Post(Ev("SessionStart", """ "modak_agent":"codex" """));
        Assert.Equal("codex", last.Sessions[0].Agent);
        await Post(Ev("SessionEnd"));
        Assert.Empty(last.Sessions);
    }

    [Fact]
    public async Task Bad_json_gets_an_empty_200()
    {
        var r = await Post("{nope");
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal("", await r.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Rejects_wrong_content_type_host_path_and_size()
    {
        Assert.Equal(HttpStatusCode.UnsupportedMediaType, (await Post(Ev("Stop"), "text/plain")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Post(Ev("Stop"), host: "evil.example")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await http.PostAsync($"http://127.0.0.1:{port}/other", new StringContent("{}"))).StatusCode);
        // Too big: the server answers 413 and hangs up mid-upload, so the client may see the reset instead.
        try { Assert.Equal(HttpStatusCode.RequestEntityTooLarge, (await Post(new string(' ', 1024 * 1024 + 10))).StatusCode); }
        catch (HttpRequestException) { }
        Assert.Empty(last.Sessions);
    }
}
