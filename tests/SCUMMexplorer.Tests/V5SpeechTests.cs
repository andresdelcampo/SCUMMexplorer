using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Loom;
using SCUMMexplorer.Core.Scumm;

namespace SCUMMexplorer.Tests;

public sealed class V5SpeechTests
{
    [Theory]
    [InlineData("SCUMM_V5_TALKIE_TEST_ROOT")]
    [InlineData("SCUMM_V5_TALKIE2_TEST_ROOT")]
    [InlineData("SCUMM_V5_DISC_TEST_ROOT")]
    public void SpokenLinesPlayTheirSampleFromTheSpeechFile(string variable)
    {
        var folder = Environment.GetEnvironmentVariable(variable);
        if (folder is null || !(Directory.Exists(folder) || File.Exists(folder))) return;
        var game = new GameFolderScanner().Scan(folder);
        Assert.NotNull(game.Speech);
        var extraction = new LoomTextExtractor().Extract(game);
        var spoken = extraction.Entries.Where(e => e.SpeechOffset is not null).ToList();
        Assert.True(spoken.Count > 100, $"{spoken.Count} lines with speech");
        var decoded = 0;
        foreach (var entry in spoken.Take(60))
        {
            var wave = game.Speech!.ReadWave(entry.SpeechOffset!.Value, entry.SpeechLength!.Value);
            Assert.True(wave.Length > 2000);
            decoded++;
        }
        Assert.Equal(Math.Min(60, spoken.Count), decoded);
    }

    [Fact]
    public void ReadsTheSpeechCodes()
    {
        var text = "[text control 0A: 34 12][text control 0A: 78 56][text control 0A: 10 00][text control 0A: 00 00]Hello";
        Assert.Equal((0x56781234, 0x10), LoomTextExtractor.ReadSpeechCodes(text));
        Assert.Equal((null, null), LoomTextExtractor.ReadSpeechCodes("Hello"));
    }

    [Fact]
    public void TheMonkeyIsland1CdTracksSayWhereTheyPlay()
    {
        var cue = Environment.GetEnvironmentVariable("SCUMM_MI1CD");
        if (cue is null || !File.Exists(cue)) return;
        var game = new GameFolderScanner().Scan(cue);
        var tracks = game.Resources.Where(r => r.Name.StartsWith("CD track")).ToList();
        Assert.True(tracks.Count >= 20);
        File.WriteAllLines(Path.Combine(Path.GetTempPath(), "mi1-tracks.txt"), tracks.Select(t => t.Name + " | " + t.Description));
        Assert.True(tracks.Count(t => t.Name.Contains(" - ")) >= 10);
    }
}
