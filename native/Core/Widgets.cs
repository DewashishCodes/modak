using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;

namespace Modak;

public sealed record SystemStats(int Cpu, int Ram, double RamUsedGb, int RamTotalGb);

// CPU % since the previous call, plus RAM in use.
public static class SystemWidget
{
    [StructLayout(LayoutKind.Sequential)] struct FileTime { public uint Low, High; public readonly ulong Value => ((ulong)High << 32) | Low; }
    [StructLayout(LayoutKind.Sequential)] struct MemoryStatus { public uint Length, Load; public ulong TotalPhys, AvailPhys, TotalPage, AvailPage, TotalVirtual, AvailVirtual, AvailExtended; }
    [DllImport("kernel32.dll")] static extern bool GetSystemTimes(out FileTime idle, out FileTime kernel, out FileTime user);
    [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx(ref MemoryStatus m);

    static ulong prevIdle, prevAll;

    public static SystemStats Read()
    {
        GetSystemTimes(out var idle, out var kernel, out var user);
        ulong all = kernel.Value + user.Value; // kernel time includes idle time
        ulong dAll = all - prevAll, dIdle = idle.Value - prevIdle;
        int cpu = prevAll > 0 && dAll > 0 ? (int)Math.Round(100 * (1 - (double)dIdle / dAll)) : 0;
        (prevAll, prevIdle) = (all, idle.Value);
        var m = new MemoryStatus { Length = (uint)Marshal.SizeOf<MemoryStatus>() };
        GlobalMemoryStatusEx(ref m);
        double used = m.TotalPhys - m.AvailPhys;
        return new SystemStats(cpu, (int)Math.Round(100 * used / m.TotalPhys), Math.Round(used / (1L << 30), 1), (int)Math.Round((double)m.TotalPhys / (1L << 30)));
    }
}

public sealed record GitFile(string Kind, string Path); // kind: add | mod | del
public sealed record GitInfo(string Branch, int Changed, int Added, int Modified, int Deleted, IReadOnlyList<GitFile> Files, int? Ahead, int? Behind, string Subject, string When, string Author);

public static class GitWidget
{
    static async Task<string?> Git(string cwd, params string[] args)
    {
        var psi = new ProcessStartInfo("git") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false, CreateNoWindow = true, StandardOutputEncoding = Encoding.UTF8 };
        psi.ArgumentList.Add("-C");
        psi.ArgumentList.Add(cwd);
        foreach (var a in args) psi.ArgumentList.Add(a);
        try
        {
            using var p = Process.Start(psi)!;
            var output = p.StandardOutput.ReadToEndAsync();
            using var timeout = new CancellationTokenSource(2000);
            await p.WaitForExitAsync(timeout.Token);
            return p.ExitCode == 0 ? (await output).TrimEnd() : null; // keep leading spaces: porcelain status codes start with one
        }
        catch { return null; }
    }

    public static async Task<GitInfo?> Read(string? cwd)
    {
        if (string.IsNullOrEmpty(cwd)) return null;
        var branch = await Git(cwd, "rev-parse", "--abbrev-ref", "HEAD");
        if (branch == null) return null;
        var status = Git(cwd, "status", "--porcelain");
        var log = Git(cwd, "log", "-1", "--format=%s%x1f%cr%x1f%an");
        var counts = Git(cwd, "rev-list", "--left-right", "--count", "@{upstream}...HEAD"); // null when there is no upstream
        var files = (await status) is { Length: > 0 } st ? st.Split('\n') : [];
        var parts = ((await log) ?? "").Split('\x1f');
        int Count(Func<string, bool> f) => files.Count(l => f(l[..2]));
        static string Kind(string xy) => xy.Contains('D') ? "del" : xy.Contains("??") || xy.Contains('A') ? "add" : "mod";
        int? ahead = null, behind = null;
        if (await counts is { } c && c.Split((char[])['\t', ' '], StringSplitOptions.RemoveEmptyEntries) is [var b, var a]) { behind = int.Parse(b); ahead = int.Parse(a); }
        return new GitInfo(branch, files.Length,
            Count(xy => xy.Contains("??") || xy.Contains('A')), Count(xy => xy.IndexOfAny(['M', 'R', 'C', 'U']) >= 0), Count(xy => xy.Contains('D')),
            files.Take(4).Select(l => new GitFile(Kind(l[..2]), l[3..].Trim('"'))).ToList(),
            ahead, behind, parts.ElementAtOrDefault(0) ?? "", parts.ElementAtOrDefault(1) ?? "", parts.ElementAtOrDefault(2) ?? "");
    }
}

public sealed record UsageToday(long Input, long Output, long CacheRead, long CacheWrite, int Messages, long[] Hours)
{
    public long Total => Input + Output + CacheRead + CacheWrite;
}

// Today's token use summed from Claude Code transcripts. Files are read incrementally (only new bytes) and in
// chunks, because the active transcript grows on every turn and a day of them can be hundreds of MB.
public sealed class UsageWidget(string? root = null)
{
    const int Chunk = 1024 * 1024;
    readonly string root = root ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".claude", "projects");
    DateTime day;
    HashSet<string> seen = []; // one assistant message is logged once per content block, all with the same usage
    Dictionary<string, long> offsets = [];
    Dictionary<string, string> partial = [];
    long input, output, cacheRead, cacheWrite;
    int messages;
    long[] hours = new long[24];

    void Reset(DateTime d)
    {
        day = d; seen = []; offsets = []; partial = [];
        input = output = cacheRead = cacheWrite = 0; messages = 0; hours = new long[24];
    }

    void AddLine(string line, DateTime start)
    {
        if (!line.Contains("\"usage\"")) return;
        try
        {
            using var doc = JsonDocument.Parse(line);
            var j = doc.RootElement;
            if (!j.TryGetProperty("type", out var type) || type.GetString() != "assistant") return;
            if (!j.TryGetProperty("message", out var msg) || !msg.TryGetProperty("usage", out var u)) return;
            if (!j.TryGetProperty("timestamp", out var ts) || !DateTime.TryParse(ts.GetString(), out var at) || at.ToLocalTime() < start) return;
            var key = msg.TryGetProperty("id", out var id) ? id.GetString() : j.TryGetProperty("requestId", out var r) ? r.GetString() : j.TryGetProperty("uuid", out var uu) ? uu.GetString() : null;
            if (key == null || !seen.Add(key)) return;
            long N(string k) => u.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetInt64() : 0;
            input += N("input_tokens");
            output += N("output_tokens");
            cacheRead += N("cache_read_input_tokens");
            cacheWrite += N("cache_creation_input_tokens");
            messages++;
            hours[at.ToLocalTime().Hour] += N("output_tokens");
        }
        catch (JsonException) { }
    }

    public UsageToday Read(DateTime? now = null)
    {
        var start = (now ?? DateTime.Now).Date;
        if (day != start) Reset(start);
        IEnumerable<string> files;
        try { files = Directory.EnumerateFiles(root, "*.jsonl", SearchOption.AllDirectories).ToList(); } catch { files = []; }
        var buf = new byte[Chunk];
        foreach (var f in files)
        {
            try
            {
                var info = new FileInfo(f);
                if (info.LastWriteTime < start) continue;
                long from = offsets.GetValueOrDefault(f);
                if (info.Length <= from) continue;
                using var fs = new FileStream(f, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
                fs.Position = from;
                var dec = Encoding.UTF8.GetDecoder(); // keeps characters split across chunks whole
                var chars = new char[Encoding.UTF8.GetMaxCharCount(Chunk)];
                long pos = from;
                int n;
                while (pos < info.Length && (n = fs.Read(buf, 0, (int)Math.Min(Chunk, info.Length - pos))) > 0)
                {
                    pos += n;
                    var text = partial.GetValueOrDefault(f, "") + new string(chars, 0, dec.GetChars(buf, 0, n, chars, 0));
                    var lines = text.Split('\n');
                    partial[f] = lines[^1]; // the last line may still be being written
                    foreach (var l in lines[..^1]) AddLine(l, start);
                }
                offsets[f] = pos;
            }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
        return new UsageToday(input, output, cacheRead, cacheWrite, messages, (long[])hours.Clone());
    }
}

public static class PhotosWidget
{
    static readonly string[] Extensions = [".jpg", ".jpeg", ".png", ".gif", ".webp", ".avif", ".bmp"];
    const int MaxPhotos = 200;

    // Image files in the folder (not recursive), newest first.
    public static IReadOnlyList<string> List(string dir)
    {
        try
        {
            return new DirectoryInfo(dir).EnumerateFiles().Where(f => Extensions.Contains(f.Extension.ToLowerInvariant()))
                .OrderByDescending(f => f.LastWriteTimeUtc).Take(MaxPhotos).Select(f => f.FullName).ToList();
        }
        catch { return []; }
    }
}
