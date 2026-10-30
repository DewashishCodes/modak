using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json.Nodes;

namespace Modak;

// Minimal HTTP/1.1 server for hook POSTs on 127.0.0.1. HttpListener cannot bind 127.0.0.1 without admin,
// and one JSON endpoint does not justify ASP.NET. One request per connection, then close.
public sealed class HookServer(Hub hub) : IDisposable
{
    const int MaxBody = 1024 * 1024, MaxHeader = 16 * 1024;
    TcpListener? listener;
    string[] allowedHosts = [];
    readonly CancellationTokenSource stop = new();

    public int Port { get; private set; }
    public Action<Exception>? OnError { get; set; } // our bugs: logged, never passed on to the agent

    public int Listen(int port)
    {
        listener = new TcpListener(IPAddress.Loopback, port);
        listener.Start();
        Port = ((IPEndPoint)listener.LocalEndpoint).Port;
        allowedHosts = [$"127.0.0.1:{Port}", $"localhost:{Port}"];
        _ = AcceptLoop();
        return Port;
    }

    async Task AcceptLoop()
    {
        while (!stop.IsCancellationRequested)
        {
            TcpClient client;
            try { client = await listener!.AcceptTcpClientAsync(stop.Token); } catch { return; }
            _ = Serve(client);
        }
    }

    async Task Serve(TcpClient client)
    {
        using var _ = client;
        var stream = client.GetStream();
        try
        {
            var (head, body) = await ReadRequest(stream);
            if (head == null) return;
            var lines = head.Split("\r\n");
            var parts = lines[0].Split(' ');
            var headers = lines.Skip(1).Select(l => l.Split(':', 2)).Where(p => p.Length == 2).GroupBy(p => p[0].Trim().ToLowerInvariant()).ToDictionary(g => g.Key, g => g.First()[1].Trim());
            if (parts.Length < 2 || parts[0] != "POST" || parts[1] != Hub.HookPath) { await Send(stream, 404); return; }
            // Browsers cannot send these cross-site without a CORS preflight we never answer; the Host check stops DNS rebinding.
            if (!allowedHosts.Contains(headers.GetValueOrDefault("host"))) { await Send(stream, 403); return; }
            if (!headers.GetValueOrDefault("content-type", "").StartsWith("application/json")) { await Send(stream, 415); return; }
            if (body == null) { await Send(stream, 413); return; }

            JsonObject? ev;
            try { ev = JsonNode.Parse(body) as JsonObject; } catch { ev = null; }
            if (ev == null) { await Send(stream, 200); return; } // bad JSON: empty answer, never block the agent

            Task<string?> reply;
            string? approvalId;
            try { reply = hub.Handle(ev, out approvalId); }
            catch (Exception x) { OnError?.Invoke(x); await Send(stream, 200); return; } // our bug must never block the agent

            if (approvalId != null)
            {
                // Held: if the agent hangs up (timeout, Ctrl+C) the card goes away.
                var hangup = stream.ReadAsync(new byte[1]).AsTask();
                if (await Task.WhenAny(reply, hangup) != reply) { hub.Abandon(approvalId); return; }
            }
            await Send(stream, 200, await reply);
        }
        catch (IOException) { }
        catch (ObjectDisposedException) { }
    }

    // Returns (head, body); body is null when it exceeds MaxBody. Handles Content-Length and chunked bodies.
    static async Task<(string? Head, string? Body)> ReadRequest(NetworkStream stream)
    {
        var buf = new MemoryStream();
        var chunk = new byte[8192];
        int headEnd = -1;
        while (headEnd < 0)
        {
            int n = await stream.ReadAsync(chunk);
            if (n == 0) return (null, null);
            buf.Write(chunk, 0, n);
            headEnd = IndexOf(buf.GetBuffer(), (int)buf.Length, "\r\n\r\n"u8);
            if (headEnd < 0 && buf.Length > MaxHeader) return (null, null);
        }
        var all = buf.ToArray();
        var head = Encoding.ASCII.GetString(all, 0, headEnd);
        var rest = new MemoryStream();
        rest.Write(all, headEnd + 4, all.Length - headEnd - 4);
        var lower = head.ToLowerInvariant();

        if (lower.Contains("transfer-encoding: chunked"))
        {
            while (IndexOf(rest.GetBuffer(), (int)rest.Length, "0\r\n\r\n"u8) < 0)
            {
                if (rest.Length > MaxBody + 64 * 1024) return (head, null);
                int n = await stream.ReadAsync(chunk);
                if (n == 0) break;
                rest.Write(chunk, 0, n);
            }
            return (head, Dechunk(rest.ToArray()));
        }

        var m = System.Text.RegularExpressions.Regex.Match(lower, @"content-length:\s*(\d+)");
        long length = m.Success ? long.Parse(m.Groups[1].Value) : 0;
        if (length > MaxBody) return (head, null);
        while (rest.Length < length)
        {
            int n = await stream.ReadAsync(chunk);
            if (n == 0) break;
            rest.Write(chunk, 0, n);
        }
        return (head, Encoding.UTF8.GetString(rest.GetBuffer(), 0, (int)Math.Min(rest.Length, length)));
    }

    static string? Dechunk(byte[] data)
    {
        var body = new MemoryStream();
        int i = 0;
        while (i < data.Length)
        {
            int lineEnd = IndexOf(data[i..], data.Length - i, "\r\n"u8);
            if (lineEnd < 0) break;
            var size = Convert.ToInt32(Encoding.ASCII.GetString(data, i, lineEnd).Split(';')[0].Trim(), 16);
            i += lineEnd + 2;
            if (size == 0) break;
            if (body.Length + size > MaxBody || i + size > data.Length) return null;
            body.Write(data, i, size);
            i += size + 2;
        }
        return Encoding.UTF8.GetString(body.ToArray());
    }

    static int IndexOf(byte[] hay, int length, ReadOnlySpan<byte> needle) => hay.AsSpan(0, length).IndexOf(needle);

    static async Task Send(NetworkStream stream, int status, string? body = null)
    {
        var bytes = Encoding.UTF8.GetBytes(body ?? "");
        var reason = status switch { 200 => "OK", 403 => "Forbidden", 404 => "Not Found", 413 => "Payload Too Large", _ => "Unsupported Media Type" };
        var head = $"HTTP/1.1 {status} {reason}\r\nContent-Type: application/json\r\nContent-Length: {bytes.Length}\r\nConnection: close\r\n\r\n";
        await stream.WriteAsync(Encoding.ASCII.GetBytes(head));
        await stream.WriteAsync(bytes);
    }

    public void Dispose()
    {
        hub.ReleaseAll(); // open approvals go back to the terminal prompt
        stop.Cancel();
        listener?.Stop();
    }
}
