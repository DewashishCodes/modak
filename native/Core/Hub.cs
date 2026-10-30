using System.Text.Json.Nodes;

namespace Modak;

public sealed class Session
{
    public required string Id { get; init; }
    public string Agent { get; set; } = "claude"; // or "codex"
    public string Cwd { get; set; } = "";
    public string Folder { get; set; } = "";
    public string Status { get; set; } = "idle"; // idle | working | asking | done
    public string Activity { get; set; } = "";
    public string LastAnswer { get; set; } = "";
    public Preview? Preview { get; set; }
    public long UpdatedAt { get; set; }
}

public sealed record Approval(string Id, string SessionId, string Folder, string Tool, string Detail, string Ask, Preview? Preview, string Activity, long CreatedAt);

public sealed record Snapshot(IReadOnlyList<Session> Sessions, IReadOnlyList<Approval> Approvals);

// Per-session state from Claude Code and Codex hook events. PermissionRequest replies are held until the
// user decides in the pill. Called from server threads; Changed and Done fire outside the lock.
public sealed class Hub
{
    public const string HookPath = "/modak/hook";
    readonly object gate = new();
    readonly Dictionary<string, Session> sessions = [];
    readonly Dictionary<string, (Approval Card, TaskCompletionSource<string?> Reply)> pending = [];
    long clock;

    public event Action<Snapshot>? Changed;
    public event Action<string>? Done; // session id that just finished

    static long Now() => DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

    public Snapshot State()
    {
        lock (gate) return Snap();
    }

    // Copies, so the UI thread never sees a session change under it.
    Snapshot Snap() => new(
        sessions.Values.OrderByDescending(s => s.UpdatedAt).Select(s => new Session { Id = s.Id, Agent = s.Agent, Cwd = s.Cwd, Folder = s.Folder, Status = s.Status, Activity = s.Activity, LastAnswer = s.LastAnswer, Preview = s.Preview, UpdatedAt = s.UpdatedAt }).ToList(),
        pending.Values.Select(p => p.Card).OrderBy(a => a.CreatedAt).ToList());

    // Null reply = "no decision", so the agent shows its normal terminal prompt.
    void Release(string id, string? body = null)
    {
        if (pending.Remove(id, out var p)) p.Reply.TrySetResult(body);
    }
    void ReleaseSession(string sessionId)
    {
        foreach (var p in pending.Values.Where(p => p.Card.SessionId == sessionId).ToList()) Release(p.Card.Id);
    }

    Session Touch(JsonObject ev, string id)
    {
        if (!sessions.TryGetValue(id, out var s))
        {
            s = new Session { Id = id, Agent = Str(ev, "modak_agent") == "codex" ? "codex" : "claude" };
            sessions[id] = s;
        }
        if (Str(ev, "cwd") is { Length: > 0 } cwd) { s.Cwd = cwd; s.Folder = Path.GetFileName(cwd.TrimEnd('\\', '/')); }
        s.UpdatedAt = ++clock; // only used for ordering, most recently active first
        return s;
    }

    static string Str(JsonObject ev, string key) => ev[key] is JsonValue v && v.TryGetValue(out string? s) ? s : "";

    // The reply for one hook event: completes at once (null) unless it is a held PermissionRequest.
    // approvalId is set for a held request, so the server can cancel it when the client hangs up.
    public Task<string?> Handle(JsonObject ev, out string? approvalId)
    {
        approvalId = null;
        var id = Str(ev, "session_id");
        if (id.Length == 0) return Task.FromResult<string?>(null);
        var name = Str(ev, "hook_event_name");
        var tool = Str(ev, "tool_name");
        var input = ev["tool_input"] as JsonObject;
        Task<string?> reply = Task.FromResult<string?>(null);
        bool done = false;

        lock (gate)
        {
            if (name == "SessionEnd")
            {
                ReleaseSession(id);
                sessions.Remove(id);
            }
            else
            {
                var s = Touch(ev, id);
                switch (name)
                {
                    case "SessionStart":
                        s.Status = "idle";
                        break;
                    case "UserPromptSubmit":
                        s.Status = "working";
                        s.Activity = "Thinking";
                        break;
                    case "PreToolUse":
                        s.Status = "working";
                        s.Activity = Describe.Activity(tool, input);
                        s.Preview = Describe.PreviewFor(tool, input) ?? s.Preview;
                        break;
                    case "PostToolUse":
                        ReleaseSession(s.Id); // a held card for this session was answered in the terminal
                        s.Status = "working";
                        if (s.Preview?.Kind == "term" && tool is "Bash" or "PowerShell") s.Preview = s.Preview with { Output = Describe.OutputTail(ev["tool_response"]) };
                        break;
                    case "PermissionRequest":
                        ReleaseSession(s.Id); // only one open card per session
                        var aid = Guid.NewGuid().ToString();
                        var tcs = new TaskCompletionSource<string?>(TaskCreationOptions.RunContinuationsAsynchronously);
                        pending[aid] = (new Approval(aid, s.Id, s.Folder, tool, Describe.Detail(tool, input), Describe.Ask(tool, input), Describe.PreviewFor(tool, input), Describe.Activity(tool, input), Now()), tcs);
                        s.Status = "asking";
                        s.Activity = $"Wants to use {tool}";
                        approvalId = aid;
                        reply = tcs.Task;
                        break;
                    case "Notification":
                        if (!pending.Values.Any(p => p.Card.SessionId == s.Id))
                        {
                            s.Status = "asking";
                            s.Activity = Describe.Clip(Str(ev, "message") is { Length: > 0 } m ? m : "Needs your input");
                        }
                        break;
                    case "Stop":
                        ReleaseSession(s.Id);
                        s.Status = "done";
                        s.Activity = "Done";
                        s.LastAnswer = Describe.Clip(Str(ev, "last_assistant_message"), 600);
                        s.Preview = new Preview("answer", Text: s.LastAnswer);
                        done = true;
                        break;
                    default:
                        return reply; // unknown event: nothing changed
                }
            }
        }
        Fire();
        if (done) Done?.Invoke(id);
        return reply;
    }

    void Fire()
    {
        Snapshot snap;
        lock (gate) snap = Snap();
        Changed?.Invoke(snap);
    }

    // The agent gave up or timed out on a held request.
    public void Abandon(string approvalId)
    {
        bool removed;
        lock (gate) removed = pending.Remove(approvalId);
        if (removed) Fire();
    }

    // behavior: allow | deny | terminal
    public void Decide(string approvalId, string behavior)
    {
        lock (gate)
        {
            if (!pending.TryGetValue(approvalId, out var p)) return;
            if (behavior == "terminal") Release(approvalId);
            else
            {
                var decision = behavior == "allow" ? new JsonObject { ["behavior"] = "allow" } : new JsonObject { ["behavior"] = "deny", ["message"] = "Denied from Modak" };
                Release(approvalId, new JsonObject { ["hookSpecificOutput"] = new JsonObject { ["hookEventName"] = "PermissionRequest", ["decision"] = decision } }.ToJsonString());
            }
            if (sessions.TryGetValue(p.Card.SessionId, out var s)) { s.Status = "working"; s.Activity = behavior == "deny" ? "Denied" : p.Card.Activity; }
        }
        Fire();
    }

    // On quit: every open request goes back to the terminal prompt.
    public void ReleaseAll()
    {
        lock (gate) foreach (var id in pending.Keys.ToList()) Release(id);
    }
}
