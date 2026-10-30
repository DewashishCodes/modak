using System.Text.Json;

namespace Modak;

// Small user preferences in %APPDATA%\Modak\prefs.json. A missing or broken file means defaults.
public sealed class Prefs
{
    public bool Declined { get; set; }
    public bool Sound { get; set; } = true;
    public string? PhotosDir { get; set; }

    static readonly JsonSerializerOptions Json = new() { WriteIndented = true };
    public static string File(string? dir = null) => Path.Combine(dir ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Modak"), "prefs.json");

    public static Prefs Load(string? file = null)
    {
        try { return JsonSerializer.Deserialize<Prefs>(System.IO.File.ReadAllText(file ?? File())) ?? new(); }
        catch { return new(); }
    }

    public void Save(string? file = null)
    {
        file ??= File();
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        System.IO.File.WriteAllText(file, JsonSerializer.Serialize(this, Json));
    }

    public string Photos() => PhotosDir ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyPictures), "Modak");
}

// The 25 minute focus timer. Lives outside the dashboard so closing it does not reset the timer.
public sealed class FocusTimer
{
    public const int Seconds = 25 * 60;
    DateTime end;
    public int Left { get; private set; } = Seconds;
    public bool Running { get; private set; }
    public double Progress => 1 - (double)Left / Seconds;

    public void Toggle(DateTime now)
    {
        if (Running) { Running = false; return; }
        if (Left == 0) Left = Seconds;
        end = now.AddSeconds(Left);
        Running = true;
    }

    public void Reset() { Running = false; Left = Seconds; }

    // Returns true once, when the timer reaches zero.
    public bool Tick(DateTime now)
    {
        if (!Running) return false;
        Left = Math.Max(0, (int)Math.Ceiling((end - now).TotalSeconds));
        if (Left > 0) return false;
        Running = false;
        return true;
    }

    public static string Mmss(int s) => $"{s / 60:00}:{s % 60:00}";
}
