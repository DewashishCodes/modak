using System.Text.Json;
using System.Text.Json.Nodes;

namespace Modak;

// Installs and removes our hooks in ~/.claude/settings.json and ~/.codex/hooks.json. Never overwrites a file
// it cannot parse, keeps a one-time backup, and writes atomically.
public static class Hooks
{
    public static readonly string ClaudeSettings = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "settings.json");
    public static readonly string CodexHooks = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".codex", "hooks.json");
    public static readonly string[] Events = ["SessionStart", "SessionEnd", "UserPromptSubmit", "PreToolUse", "PostToolUse", "PermissionRequest", "Notification", "Stop"];
    public static readonly string[] CodexEvents = Events.Where(e => e != "Notification").ToArray(); // Codex has no Notification event

    // Every hook we add mentions one of these, so uninstall touches only ours.
    // "/peninsula/" is the app's old name; "modak-forward" is the Electron build's node forwarder.
    static readonly string[] Marks = ["/modak/", "/peninsula/", "modak-forward"];
    static bool Ours(JsonNode? h) =>
        h is JsonObject o && ((string?)o["type"]) is "http" or "command" &&
        Marks.Any(m => ((string?)o["url"] ?? (string?)o["command"] ?? "").Contains(m));

    static JsonObject Read(string file)
    {
        if (!File.Exists(file)) return [];
        var text = File.ReadAllText(file);
        if (string.IsNullOrWhiteSpace(text)) return [];
        return JsonNode.Parse(text) as JsonObject ?? throw new InvalidDataException($"{file} is not a JSON object"); // throws on a broken file
    }

    static void Write(string file, JsonObject settings)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        var backup = file + ".modak-backup";
        if (File.Exists(file) && !File.Exists(backup)) File.Copy(file, backup);
        var tmp = file + ".modak-tmp";
        File.WriteAllText(tmp, settings.ToJsonString(new JsonSerializerOptions { WriteIndented = true }) + "\n");
        File.Move(tmp, file, overwrite: true);
    }

    // Drops our handlers, then any matcher groups and events left empty by that.
    static JsonObject Strip(JsonObject settings)
    {
        if (settings["hooks"] is not JsonObject hooks) return settings;
        foreach (var ev in hooks.Select(kv => kv.Key).ToList())
        {
            if (hooks[ev] is not JsonArray groups) continue;
            foreach (var g in groups.OfType<JsonObject>())
                if (g["hooks"] is JsonArray hs) foreach (var h in hs.Where(Ours).ToList()) hs.Remove(h);
            foreach (var g in groups.OfType<JsonObject>().Where(g => g["hooks"] is JsonArray { Count: 0 }).ToList()) groups.Remove(g);
            if (groups.Count == 0) hooks.Remove(ev);
        }
        if (hooks.Count == 0) settings.Remove("hooks");
        return settings;
    }

    static IEnumerable<JsonObject> OurHooks(JsonObject settings) =>
        (settings["hooks"] as JsonObject)?.Select(kv => kv.Value).OfType<JsonArray>().SelectMany(g => g).OfType<JsonObject>()
            .SelectMany(g => (g["hooks"] as JsonArray)?.OfType<JsonObject>() ?? []).Where(Ours) ?? [];

    static void Add(string file, string[] events, Func<int, JsonObject> handler)
    {
        var settings = Strip(Read(file));
        var hooks = settings["hooks"] as JsonObject ?? [];
        settings["hooks"] = hooks;
        foreach (var ev in events)
        {
            // A held approval may wait for the user; everything else must answer fast or the agent moves on.
            var timeout = ev == "PermissionRequest" ? 600 : 5;
            if (hooks[ev] is not JsonArray groups) hooks[ev] = groups = [];
            groups.Add(new JsonObject { ["hooks"] = new JsonArray(handler(timeout)) });
        }
        Write(file, settings);
    }

    static bool Has(string file, string[] events, Func<JsonObject, bool> current)
    {
        try
        {
            var hooks = Read(file)["hooks"] as JsonObject;
            return events.All(e => (hooks?[e] as JsonArray)?.OfType<JsonObject>().Any(g => (g["hooks"] as JsonArray)?.Any(h => Ours(h) && current((JsonObject)h!)) == true) == true);
        }
        catch { return false; }
    }

    public static void InstallClaude(string url, string? file = null) =>
        Add(file ?? ClaudeSettings, Events, t => new JsonObject { ["type"] = "http", ["url"] = url, ["timeout"] = t });

    // Codex only runs command hooks: Modak.exe itself forwards the event (see Forwarder).
    public static string CodexCommand(string exe, string url) => $"\"{exe}\" --forward {url}";
    public static void InstallCodex(string exe, string url, string? file = null) =>
        Add(file ?? CodexHooks, CodexEvents, t => new JsonObject { ["type"] = "command", ["command"] = CodexCommand(exe, url), ["timeout"] = t });

    public static void Uninstall(string file) => Write(file, Strip(Read(file)));

    public static bool IsClaudeInstalled(string? file = null) => Has(file ?? ClaudeSettings, Events, _ => true);
    public static bool IsCodexInstalled(string? file = null) => Has(file ?? CodexHooks, CodexEvents, _ => true);

    // Installed but pointing somewhere else (old name, old port, old exe path), so it needs refreshing.
    public static bool IsStale(string file, Func<JsonObject, bool> current)
    {
        try { var all = OurHooks(Read(file)).ToList(); return all.Count > 0 && all.Any(h => !current(h)); }
        catch { return false; }
    }
    public static bool IsClaudeStale(string url, string? file = null) => IsStale(file ?? ClaudeSettings, h => (string?)h["url"] == url);
    public static bool IsCodexStale(string exe, string url, string? file = null) => IsStale(file ?? CodexHooks, h => (string?)h["command"] == CodexCommand(exe, url));
}
