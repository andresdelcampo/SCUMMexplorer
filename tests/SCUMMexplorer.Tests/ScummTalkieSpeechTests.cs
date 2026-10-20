using SCUMMexplorer.Core.Discovery;
using Xunit.Abstractions;

namespace SCUMMexplorer.Tests;

/// <summary>
/// S10 talkie speech (19 October 2026): the speech banks of the talkie games and mouth sync. Monkey Island 2's fan "Ultimate Talkie Edition" patch
/// (<c>mi2 talkie en</c>/<c>es</c>) stores its speech as Indiana Jones 4 does: <c>monster.sou</c> is the "SOU " header and back-to-back <c>VCTL</c> blocks, each
/// followed by a Creative Voice File, and its texts' code 10 gives the block's offset and size. Each of its 6808 blocks is 10 bytes long and holds one mouth-sync
/// word, 4095; Indy 4's 7029 blocks hold 0 to 37 words. Record: <c>.verification/talkie-speech-20261019/README.md</c>. Skips without the data.
/// </summary>
public sealed class ScummTalkieSpeechTests(ITestOutputHelper output)
{
    private static string? Folder(string copy)
    {
        var path = Path.Combine(TestGames.Root, copy);
        return Directory.Exists(path) ? path : null;
    }

    [Theory]
    [InlineData("mi2 talkie en", 6808, 10)]
    [InlineData("mi2 talkie es", 6808, 10)]
    [InlineData("indy4 vga en", 7029, 0)]
    [InlineData("indy4 talkie vga es", 7029, 0)]
    public void EachSpeechBankHoldsVoiceFilesWithTheirMouthSyncTimes(string copy, int count, int onlySize)
    {
        if (Folder(copy) is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        Assert.NotNull(game.Speech);
        var samples = game.Speech!.ListSamples();
        Assert.Equal(count, samples.Count);
        var times = samples.Select(sample => game.Speech.ReadMouthSync(sample.Offset, sample.Length)).ToList();
        output.WriteLine($"{copy}: VCTL sizes " + string.Join(" ", samples.GroupBy(s => s.Length).OrderBy(g => g.Key).Select(g => $"{g.Key}x{g.Count()}")));
        if (onlySize != 0)
        {
            // The patch writes one word, 4095 ticks (68 s), in every block: the mouth stays open until the line ends (the plain talk animation).
            Assert.All(samples, sample => Assert.Equal(onlySize, sample.Length));
            Assert.All(times, list => Assert.Equal([4095], list));
        }
        else
        {
            // Indy 4's first sample (offset 8): five times, open to 13, closed to 15, open to 23, closed to 26, open to 33, then closed.
            Assert.Equal([13, 15, 23, 26, 33], times[0]);
            Assert.Contains(times, list => list.Count > 10);
            Assert.Equal([false, false, false, false, false, true, false, false, true, false, false, false, true],
                Enumerable.Range(0, 13).Select(i => ScummMessageTable.IsMouthSyncOff(times[0], i * 3)).ToArray());
        }
    }

    /// <summary>
    /// Mouth sync for one Indy 4 line: after the boot (Esc skips the prologue), the first spoken line with several mouth-sync times moves the talking actor's
    /// mouth at the frames <c>isMouthSyncOff</c> gives for the line's own times, and closes it when the sample ends.
    /// </summary>
    [Theory]
    [InlineData("indy4 vga en")]
    [InlineData("indy4 talkie vga es")]
    public void AnIndy4LinesMouthFollowsItsSamplesMouthSyncTimes(string copy)
    {
        if (Folder(copy) is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        var session = ScummGameSession.BootForPlay(game, out _);
        Assert.NotNull(session.Messages.SpeechMouthSync);
        Assert.Empty(session.Messages.MouthSyncEvents);
        session.PressKey(0x1B);
        var frames = new List<(long Jiffies, int Actor, int Frame)>();
        ScummSpeechEvent? line = null;
        IReadOnlyList<int>? times = null;
        for (var i = 0; i < 3000; i++)
        {
            session.AdvanceFrame();
            if (line is null && session.Messages.MouthSyncTimes is { Count: >= 3 } found && session.Messages.SpeechPlaying)
            {
                line = session.Messages.SpeechEvents.Last(e => !e.IsStop);
                times = found;
            }
            if (line is { } current && session.Messages.SpeechEvents[^1] != current) break;
        }
        Assert.NotNull(line);
        var spoken = line!.Value;
        output.WriteLine($"{copy}: line 0x{spoken.Offset:X8}/{spoken.Length} from jiffy {spoken.Jiffies}, {spoken.DurationJiffies} jiffies, times [{string.Join(" ", times!)}]");
        Assert.Equal(times, game.Speech!.ReadMouthSync(spoken.Offset, spoken.Length));
        var events = session.Messages.MouthSyncEvents.Where(e => e.Jiffies >= spoken.Jiffies && e.Jiffies <= spoken.Jiffies + spoken.DurationJiffies!.Value + 15).ToList();
        foreach (var e in events) output.WriteLine($"mouth {(e.Open ? "open" : "closed")} at +{e.Jiffies - spoken.Jiffies} (actor {e.Actor})");
        Assert.True(events.Count >= 2, "the mouth moves with the line");
        // The events alternate, starting with a close (the line starts with the mouth open), and each one is where the times say it is.
        for (var i = 0; i < events.Count; i++)
        {
            Assert.Equal(i % 2 == 1, events[i].Open);
            var position = events[i].Jiffies - spoken.Jiffies;
            var ended = position >= spoken.DurationJiffies;
            if (!ended) Assert.Equal(!events[i].Open, ScummMessageTable.IsMouthSyncOff(times!, position));
        }
    }
}
