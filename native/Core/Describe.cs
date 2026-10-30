using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Modak;

// What the agent tile's mini terminal shows: a diff, a command with its output, or the final answer.
public sealed record Preview(string Kind, string File = "", IReadOnlyList<(char Sign, string Text)>? Lines = null, string Command = "", IReadOnlyList<string>? Output = null, string Text = "")
{
    public IReadOnlyList<(char Sign, string Text)> Lines { get; init; } = Lines ?? [];
    public IReadOnlyList<string> Output { get; init; } = Output ?? [];
}

// Turns a tool call into the short lines and previews the island shows.
public static partial class Describe
{
    const int PreviewLines = 8;

    public static string Clip(string s, int n = 60) => s.Length > n ? s[..(n - 1)] + "…" : s;
    static string Base(string? p) => string.IsNullOrEmpty(p) ? "" : Path.GetFileName(p.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar));
    static string Str(JsonObject? o, string key) => o?[key] is JsonValue v && v.TryGetValue(out string? s) ? s : "";
    static string FirstLine(string s) => s.Split('\n')[0];
    static string[] Lines(string s) => s.Replace("\r", "").Split('\n');

    // Codex edits files with apply_patch; its tool_input.command is the patch text.
    static bool IsPatch(string tool) => tool == "apply_patch";
    [GeneratedRegex(@"^\*\*\* (?:Add|Update|Delete) File: (.+)$", RegexOptions.Multiline)] private static partial Regex PatchFileRx();
    static string PatchFile(JsonObject? input) { var m = PatchFileRx().Match(Str(input, "command")); return m.Success ? m.Groups[1].Value.Trim() : ""; }

    // One short line saying what a tool call does, for the collapsed pill.
    public static string Activity(string tool, JsonObject? input) => tool switch
    {
        "Edit" or "MultiEdit" or "Write" or "NotebookEdit" => $"Editing {Base(Str(input, "file_path") is { Length: > 0 } f ? f : Str(input, "notebook_path"))}",
        "apply_patch" => $"Editing {Base(PatchFile(input))}",
        "Read" => $"Reading {Base(Str(input, "file_path"))}",
        "Bash" or "PowerShell" => $"Running {Clip(FirstLine(Str(input, "command")), 48)}",
        "Grep" or "Glob" => $"Searching {Clip(Str(input, "pattern"), 40)}",
        "WebFetch" => $"Fetching {Clip(Str(input, "url"), 48)}",
        "WebSearch" => "Searching the web",
        "Task" or "Agent" => "Running an agent",
        _ => Clip(string.IsNullOrEmpty(tool) ? "Working" : tool),
    };

    // The detail on an approval card: the command, the file, or the raw input.
    public static string Detail(string tool, JsonObject? input)
    {
        if (IsPatch(tool)) return PatchFile(input);
        if (Str(input, "command") is { Length: > 0 } c) return Clip(c, 400);
        if (Str(input, "file_path") is { Length: > 0 } f) return f;
        if (Str(input, "url") is { Length: > 0 } u) return u;
        return Clip(input?.ToJsonString() ?? "{}", 400);
    }

    // Short question for the slim approval bar: "Allow: git push origin main?"
    public static string Ask(string tool, JsonObject? input)
    {
        if (IsPatch(tool)) return $"edit {Base(PatchFile(input))}";
        if (Str(input, "command") is { Length: > 0 } c) return Clip(FirstLine(c), 56);
        if (Str(input, "file_path") is { Length: > 0 } f) return $"{(tool == "Read" ? "read" : "edit")} {Base(f)}";
        if (Str(input, "url") is { Length: > 0 } u) return $"fetch {Clip(u, 48)}";
        return $"use {tool}";
    }

    // The preview for a tool call, or null to keep the previous one.
    public static Preview? PreviewFor(string tool, JsonObject? input)
    {
        switch (tool)
        {
            case "Edit":
                return new Preview("diff", Base(Str(input, "file_path")),
                    Lines(Str(input, "old_string")).Select(s => ('-', s)).Concat(Lines(Str(input, "new_string")).Select(s => ('+', s))).Take(PreviewLines).ToList());
            case "MultiEdit":
                var first = (input?["edits"] as JsonArray)?.FirstOrDefault() as JsonObject;
                var merged = first?.DeepClone().AsObject() ?? [];
                merged["file_path"] = Str(input, "file_path");
                return PreviewFor("Edit", merged);
            case "apply_patch":
                return new Preview("diff", Base(PatchFile(input)),
                    Lines(Str(input, "command")).Where(s => s.StartsWith('-') || s.StartsWith('+')).Select(s => (s[0], s[1..])).Take(PreviewLines).ToList());
            case "Write":
                return new Preview("diff", Base(Str(input, "file_path")), Lines(Str(input, "content")).Take(PreviewLines).Select(s => ('+', s)).ToList());
            case "Bash":
            case "PowerShell":
                return new Preview("term", Command: Clip(FirstLine(Str(input, "command")), 120));
            default:
                return null;
        }
    }

    // Last lines of a command's result; tool_response is an object with stdout/stderr or a plain string.
    public static IReadOnlyList<string> OutputTail(JsonNode? response)
    {
        var text = response is JsonValue v && v.TryGetValue(out string? s) ? s
            : string.Join("\n", new[] { Str(response as JsonObject, "stdout"), Str(response as JsonObject, "stderr") }.Where(x => x.Length > 0));
        var lines = Lines(text).Where(l => l.Trim().Length > 0).ToList();
        return lines.Skip(Math.Max(0, lines.Count - 6)).Select(l => Clip(l, 160)).ToList();
    }
}
