using Modak;
using Xunit;

public class PrefsTests
{
    [Fact]
    public void Prefs_round_trip_and_survive_a_broken_file()
    {
        var file = Prefs.File(Directory.CreateTempSubdirectory("modak-prefs-").FullName);
        Assert.True(Prefs.Load(file).Sound);
        new Prefs { Sound = false, Declined = true, PhotosDir = @"D:\pics" }.Save(file);
        var p = Prefs.Load(file);
        Assert.False(p.Sound);
        Assert.True(p.Declined);
        Assert.Equal(@"D:\pics", p.Photos());
        File.WriteAllText(file, "{ broken");
        Assert.True(Prefs.Load(file).Sound);
    }

    [Fact]
    public void Focus_counts_down_pauses_resumes_and_finishes_once()
    {
        var t = new FocusTimer();
        var now = new DateTime(2026, 10, 30, 12, 0, 0);
        t.Toggle(now);
        Assert.False(t.Tick(now.AddSeconds(60)));
        Assert.Equal(FocusTimer.Seconds - 60, t.Left);
        t.Toggle(now.AddSeconds(60)); // pause
        Assert.False(t.Tick(now.AddSeconds(600)));
        Assert.Equal(FocusTimer.Seconds - 60, t.Left);
        t.Toggle(now.AddSeconds(600)); // resume where it was
        Assert.True(t.Tick(now.AddSeconds(600 + FocusTimer.Seconds)));
        Assert.False(t.Tick(now.AddSeconds(601 + FocusTimer.Seconds)));
        Assert.Equal(0, t.Left);
        t.Reset();
        Assert.Equal("25:00", FocusTimer.Mmss(t.Left));
        Assert.Equal(0, t.Progress);
    }
}
