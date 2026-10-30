using System.Text;
using System.Text.Json.Nodes;

namespace Modak;

// `Modak.exe --forward <hook url>`: Codex's command hook. Reads the event on stdin, posts it to the running
// Modak and prints the reply. Must never block Codex: if Modak is not running it answers "no decision" at once.
public static class Forwarder
{
    public static async Task<int> Run(string url, TextReader stdin, TextWriter stdout)
    {
        string reply = "";
        try
        {
            if (JsonNode.Parse(await stdin.ReadToEndAsync()) is JsonObject ev)
            {
                ev["modak_agent"] = "codex";
                using var http = new HttpClient { Timeout = Timeout.InfiniteTimeSpan }; // Codex's own hook timeout limits a held approval
                var r = await http.PostAsync(url, new StringContent(ev.ToJsonString(), Encoding.UTF8, "application/json"));
                if (r.IsSuccessStatusCode) reply = await r.Content.ReadAsStringAsync();
            }
        }
        catch { } // not running, bad input: fall through to "no decision"
        // Codex wants JSON on stdout (Stop rejects plain text); {} means "no decision", like Modak's empty reply.
        await stdout.WriteAsync(reply.Trim().Length > 0 ? reply : "{}");
        await stdout.FlushAsync();
        return 0;
    }
}
