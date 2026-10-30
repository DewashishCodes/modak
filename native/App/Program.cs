using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace Modak;

public static class Program
{
    [STAThread]
    static int Main(string[] args)
    {
        // Codex runs `Modak.exe --forward <url>` for every hook event: answer and exit before any UI starts.
        if (args is ["--forward", var url, ..]) return Forwarder.Run(url, Console.In, Console.Out).GetAwaiter().GetResult();

        WinRT.ComWrappersSupport.InitializeComWrappers();
        Application.Start(_ =>
        {
            System.Threading.SynchronizationContext.SetSynchronizationContext(new DispatcherQueueSynchronizationContext(DispatcherQueue.GetForCurrentThread()));
            // --second and --port keep test runs off the main screen and away from real sessions on the default port;
            // --open pins the dashboard open so it can be checked without moving the mouse.
            int port = Array.IndexOf(args, "--port") is var i and >= 0 && i + 1 < args.Length && int.TryParse(args[i + 1], out var n) ? n : Island.Port;
            new App(args.Contains("--second"), port, args.Contains("--open"));
        });
        return 0;
    }
}

public class App : Application
{
    readonly bool second;
    readonly int port;
    readonly bool open;
    Island? island;

    public App(bool second, int port, bool open)
    {
        this.open = open;
        this.second = second;
        this.port = port;
        RequestedTheme = ApplicationTheme.Dark;
    }

    protected override void OnLaunched(LaunchActivatedEventArgs e)
    {
        UnhandledException += (_, x) => CrashLog.Write(x.Exception);
        try
        {
            island = new Island(second, port, open);
        }
        catch (Exception x) { CrashLog.Write(x); throw; }
    }
}

// A WinExe has no console, so startup failures would vanish without this.
static class CrashLog
{
    public static readonly string File = Path.Combine(Path.GetTempPath(), "modak-crash.log");
    static readonly bool Tracing = Environment.GetEnvironmentVariable("MODAK_TRACE") == "1";
    // Opt-in trace for debugging: set MODAK_TRACE=1, then read %TEMP%\modak-trace.log.
    public static void Trace(string line) { if (Tracing) try { System.IO.File.AppendAllText(Path.Combine(Path.GetTempPath(), "modak-trace.log"), $"{DateTime.Now:HH:mm:ss.fff} {line}{Environment.NewLine}"); } catch { } }
    public static void Write(Exception x) { try { System.IO.File.AppendAllText(File, $"{DateTime.Now:s} {x}{Environment.NewLine}{Environment.NewLine}"); } catch { } }
}
