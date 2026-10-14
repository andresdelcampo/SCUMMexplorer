using SCUMMexplorer.Core.Scumm;

namespace SCUMMexplorer.Tests;

/// <summary>Looping of a standalone v5 sound as ScummVM's iMUSE plays it: the loop sysex 0x7D 80, the always-taken jump hook, and the jump itself (<see cref="ScummImuseTimeline"/>).</summary>
public sealed class V5ImuseLoopTests
{
    private static byte[] Part(params byte[] track) =>
    [
        .. "MDhd"u8, 0, 0, 0, 8, 0, 0, 0x80, 0x7F, 0, 0, 0, 0x80,
        .. "MThd"u8, 0, 0, 0, 6, 0, 2, 0, 1, 0x01, 0xE0,
        .. "MTrk"u8, 0, 0, 0, (byte)track.Length, .. track,
    ];

    private static byte[] Nibbles(params byte[] bytes) => bytes.SelectMany(b => new[] { (byte)(b >> 4), (byte)(b & 15) }).ToArray();
    private static byte[] Words(params int[] words) => words.SelectMany(w => new[] { (byte)(w >> 8), (byte)w }).ToArray();

    /// <summary>A variable-length delta time.</summary>
    private static byte[] Delta(int ticks)
    {
        var bytes = new List<byte> { (byte)(ticks & 0x7F) };
        for (ticks >>= 7; ticks > 0; ticks >>= 7) bytes.Insert(0, (byte)(ticks & 0x7F | 0x80));
        return [.. bytes];
    }

    private static byte[] Sysex(int delta, params byte[] data) => [.. Delta(delta), 0xF0, (byte)(data.Length + 1), .. data, 0xF7];
    private static byte[] Event(int delta, params byte[] message) => [.. Delta(delta), .. message];
    private static byte[] Loop(int delta, int count, int toBeat, int toTick, int fromBeat, int fromTick) =>
        Sysex(delta, [0x7D, 80, 0, .. Nibbles(Words(count, toBeat, toTick, fromBeat, fromTick))]);
    private static byte[] Hook(int delta, int id, int beat, int tick) => Sysex(delta, [0x7D, 48, 0, .. Nibbles([(byte)id, 0, 0], Words(beat, tick))]);
    private static byte[] Nibbles(byte[] a, byte[] b) => Nibbles([.. a, .. b]);
    private static byte[] SetUp(int delta, int channel) => Sysex(delta, [0x7D, 0, (byte)channel, .. Nibbles(0x01, 0x00, 0x7F, 0x40, 0x00, 0x00, 0x02, 0x00)]);
    private static readonly byte[] End = [0x00, 0xFF, 0x2F, 0x00];

    /// <summary>Set-up at tick 0, a loop (count 3, from beat 5 back to beat 2), a note on at tick 960 and off at 1440, the track ends at tick 2400 (beat 6).</summary>
    private static ScummImuseSong CountedLoop() => ScummImuseSong.Parse(Part([
        .. SetUp(0, 0),
        .. Loop(0, 3, 2, 0, 5, 0),
        .. Event(960, 0x90, 60, 100),
        .. Event(480, 0x80, 60, 0),
        .. Event(960, 0xFF, 0x2F, 0x00)]));

    [Fact]
    public void ALoopReturnsToItsTargetAndPlaysTheSameEventsAgain()
    {
        var timeline = ScummImuseTimeline.Build(CountedLoop());
        // 960 ticks a second at the default tempo: the position reaches beat 5 (tick 1920) at 2.0 s, goes back to tick 480 and the pass takes 1.5 s.
        var ons = timeline.Items.Where(i => !i.Synthetic && i.Event.Command == 0x9).ToList();
        var offs = timeline.Items.Where(i => !i.Synthetic && i.Event.Command == 0x8).ToList();
        Assert.Equal(4, ons.Count); // the first pass and three more
        Assert.Equal(new[] { 1.0, 2.5, 4.0, 5.5 }, ons.Select(i => Math.Round(i.Seconds, 6)));
        Assert.Equal(new[] { 1.5, 3.0, 4.5, 6.0 }, offs.Select(i => Math.Round(i.Seconds, 6)));
        // The second pass starts at the loop point with the same events, byte for byte.
        Assert.All(ons, on => { Assert.Equal(960, on.Event.Tick); Assert.Equal(new byte[] { 60, 100 }, on.Event.Data); });
        // The events before the target are not played again: the set-up and the loop exclusive come once.
        Assert.Single(timeline.Items, i => i.Event.IsSysex && i.Event.Data[1] == 0);
        Assert.Single(timeline.Items, i => i.Event.IsSysex && i.Event.Data[1] == 80);
        Assert.Equal(3, timeline.JumpsBack);
        Assert.Equal(new[] { 2.0, 3.5, 5.0 }, timeline.JumpBackSeconds.Select(s => Math.Round(s, 6)));
        Assert.True(timeline.Loops);
        Assert.False(timeline.EndedOnLoop); // the count ran out and the song went on to its end
        Assert.Equal(7.0, timeline.EndSeconds, 6);
    }

    [Fact]
    public void AJumpStopsTheSoundingNotes()
    {
        var timeline = ScummImuseTimeline.Build(CountedLoop());
        var cut = timeline.Items.Where(i => i.Synthetic && i.Event.Channel == 0 && i.Event.Data[0] == 123).Select(i => Math.Round(i.Seconds, 6)).ToList();
        Assert.Equal(new[] { 2.0, 3.5, 5.0 }, cut);
        Assert.Contains(timeline.Items, i => i.Synthetic && i.Event.Data[0] == 64 && i.Event.Data[1] == 0);
    }

    [Fact]
    public void ALoopWithAHugeCountIsUnrolledToThreeMinutesAndEndsAtTheLoopPoint()
    {
        var song = ScummImuseSong.Parse(Part([
            .. SetUp(0, 0),
            .. Loop(0, 1000, 2, 0, 5, 0),
            .. Event(960, 0x90, 60, 100),
            .. Event(480, 0x80, 60, 0),
            .. Event(960, 0xFF, 0x2F, 0x00)]));
        var timeline = ScummImuseTimeline.Build(song);
        Assert.True(timeline.EndedOnLoop);
        Assert.InRange(timeline.EndSeconds, ScummImuseTimeline.MinLoopSeconds, ScummImuseTimeline.MinLoopSeconds + 1.5);
        Assert.Equal(timeline.JumpBackSeconds[^1], timeline.EndSeconds);
        Assert.True(timeline.JumpsBack > 100);
    }

    [Fact]
    public void ALoopTheDriverRefusesIsIgnored()
    {
        // to-beat 4, from-beat 5: to-beat + 1 >= from-beat, so setLoop returns false and the song plays through once.
        var song = ScummImuseSong.Parse(Part([
            .. SetUp(0, 0),
            .. Loop(0, 5, 4, 0, 5, 0),
            .. Event(960, 0x90, 60, 100),
            .. Event(480, 0x80, 60, 0),
            .. Event(960, 0xFF, 0x2F, 0x00)]));
        var timeline = ScummImuseTimeline.Build(song);
        Assert.False(timeline.Loops);
        Assert.Equal(2400 / 960.0, timeline.EndSeconds, 6);
    }

    [Fact]
    public void AClearedLoopDoesNotJump()
    {
        var song = ScummImuseSong.Parse(Part([
            .. SetUp(0, 0),
            .. Loop(0, 5, 2, 0, 5, 0),
            .. Sysex(480, 0x7D, 81, 0), // end loop
            .. Event(1440, 0xFF, 0x2F, 0x00)]));
        Assert.False(ScummImuseTimeline.Build(song).Loops);
    }

    [Fact]
    public void TheAlwaysTakenJumpHookLoopsAndAHookWaitingForAScriptDoesNot()
    {
        // The end of the song jumps back to beat 2 (hook id 0); a hook with id 3 in the middle waits for a script and is never taken.
        var song = ScummImuseSong.Parse(Part([
            .. SetUp(0, 0),
            .. Event(960, 0x90, 60, 100),
            .. Event(240, 0x80, 60, 0),
            .. Hook(0, 3, 1, 0),
            .. Hook(720, 0, 2, 0),
            .. Event(0, 0xFF, 0x2F, 0x00)]));
        var timeline = ScummImuseTimeline.Build(song);
        Assert.True(timeline.EndedOnLoop);
        Assert.Equal(new[] { 1.0, 2.5 }, timeline.Items.Where(i => !i.Synthetic && i.Event.Command == 0x9).Take(2).Select(i => Math.Round(i.Seconds, 6)));
        // Hook id 3 is played over, but did not jump: the first jump is the one at tick 1920.
        Assert.Equal(2.0, timeline.JumpBackSeconds[0], 6);
    }

    [Fact]
    public void AForwardHookSkipsTheStretchAndIsNotALoop()
    {
        var song = ScummImuseSong.Parse(Part([
            .. SetUp(0, 0),
            .. Hook(480, 0, 3, 0),           // at tick 480 jump to tick 960
            .. Event(240, 0x90, 62, 100),    // tick 720: skipped
            .. Event(720, 0x90, 64, 100),    // tick 1440: played, half a second after the jump
            .. Event(480, 0xFF, 0x2F, 0x00)]));
        var timeline = ScummImuseTimeline.Build(song);
        Assert.False(timeline.Loops);
        var ons = timeline.Items.Where(i => !i.Synthetic && i.Event.Command == 0x9).ToList();
        Assert.Single(ons);
        Assert.Equal(64, ons[0].Event.Data[0]);
        Assert.Equal(1.0, ons[0].Seconds, 6);
    }

    /// <summary>One note on the carrier-only test instrument; the loop plays it a second time.</summary>
    [Fact]
    public void TheAdlibWaveSoundsTheLoopedNoteAgainAtTheLoopPoint()
    {
        var instrument = new byte[30];
        instrument[0] = 0x21; instrument[2] = 0x0F; instrument[3] = 0xFF;
        instrument[5] = 0x21; instrument[6] = 0x3F; instrument[7] = 0x0F; instrument[8] = 0xF0; // written inverted: sustain level loudest, fastest release
        byte[] define = [0x7D, 0x10, 0x00, 0x01, .. Nibbles(instrument)];
        // Loop once (count 1) from beat 4 (tick 1440) back to beat 2 (tick 480); the note is on at 960 to 1440 and again in the second pass.
        var song = ScummImuseSong.Parse(Part([
            .. SetUp(0, 0),
            .. Sysex(0, define),
            .. Loop(0, 1, 2, 0, 4, 0),
            .. Event(960, 0x90, 69, 100),
            .. Event(480, 0x80, 69, 0),
            .. Event(480, 0xFF, 0x2F, 0x00)]));
        var result = new ScummImuseAdlib().Render(song);
        Assert.True(result.Loops);
        var wave = result.Wave;
        const int rate = 49716;
        double Energy(double from, double to)
        {
            double sum = 0;
            for (var i = 44 + (int)(from * rate) * 4; i + 3 < wave.Length && i < 44 + (int)(to * rate) * 4; i += 4)
            {
                double s = BitConverter.ToInt16(wave, i);
                sum += s * s;
            }
            return sum;
        }
        // First note: 1.0 s to 1.5 s. The jump at 1.5 s goes back to tick 480, so the second note-on is 0.5 s later, at 2.0 s.
        var first = Energy(1.1, 1.4);
        var second = Energy(2.1, 2.4);
        var between = Energy(1.7, 1.95);
        var before = Energy(0.2, 0.9);
        Assert.True(first > 1e6, $"first {first}");
        Assert.InRange(second / first, 0.8, 1.25);
        Assert.True(before < first * 0.001, $"before {before}");
        Assert.True(between < first * 0.05, $"between {between} first {first}");
        Assert.InRange(result.Seconds, 2.4, 6.5);
    }

    private static ScummImuseSong NoteWithPartHook(int hookId, int on)
    {
        var instrument = new byte[30];
        instrument[0] = 0x21; instrument[2] = 0x0F; instrument[3] = 0xFF;
        instrument[5] = 0x21; instrument[6] = 0x3F; instrument[7] = 0x0F; instrument[8] = 0xF0;
        byte[] define = [0x7D, 0x10, 0x00, 0x01, .. Nibbles(instrument)];
        return ScummImuseSong.Parse(Part([
            .. SetUp(0, 0),
            .. Sysex(0, define),
            .. Sysex(0, [0x7D, 50, 0, .. Nibbles((byte)hookId, (byte)on)]), // part on/off hook
            .. Event(240, 0x90, 69, 100),
            .. Event(480, 0x80, 69, 0),
            .. Event(240, 0xFF, 0x2F, 0x00)]));
    }

    [Fact]
    public void APartOffHookWithIdZeroSilencesThePartAndOneWithAnotherIdDoesNot()
    {
        // Hook 50 with id 0 acts at once (ScummVM maybe_part_onoff); with id 2 it waits for a script, so the part stays on.
        Assert.Throws<InvalidDataException>(() => new ScummImuseAdlib().Render(NoteWithPartHook(0, 0)));
        var waiting = new ScummImuseAdlib().Render(NoteWithPartHook(2, 0));
        Assert.Contains(waiting.Wave.Skip(44).Chunk(2), pair => BitConverter.ToInt16([.. pair]) != 0);
    }

    [Fact]
    public void ALoopingRolandSongPlaysPastItsFirstLoopPointWhenTheDataIsGiven()
    {
        var folder = Environment.GetEnvironmentVariable("SCUMM_V5_MT32_TEST_ROOT");
        var roms = Mt32RomSet.Find(Environment.GetEnvironmentVariable("SCUMM_MT32_ROM_ROOT"));
        if (folder is null || roms is null || !Directory.Exists(folder)) return;
        var source = new FolderFileSource(folder);
        var archive = ScummV5Archive.Load(source, ScummV5Archive.FindGame(source)!);
        var song = ScummImuseSong.Parse(archive.ReadSounds().First(s => s.Id == 28).ImuseRoland!);
        var timeline = ScummImuseTimeline.Build(song);
        Assert.True(timeline.EndedOnLoop);
        var started = DateTime.UtcNow;
        var result = new ScummImuseMt32(roms).Render(song);
        var wave = result.Wave;
        Assert.True(result.Loops);
        Assert.InRange(result.Seconds, ScummImuseTimeline.MinLoopSeconds, ScummImuseTimeline.MaxSeconds + 1);
        // The wave holds the whole unrolled song plus the release tail.
        Assert.InRange(BitConverter.ToInt32(wave, 40) / 4.0 / BitConverter.ToInt32(wave, 24), result.Seconds, result.Seconds + 4);
        Assert.True(DateTime.UtcNow - started < TimeSpan.FromMinutes(5));
    }

    [Fact]
    public void RealLoopedSoundsUnrollWhenTheDataIsGiven()
    {
        var folder = Environment.GetEnvironmentVariable("SCUMM_V5_MT32_TEST_ROOT");
        if (folder is null || !Directory.Exists(folder)) return;
        var source = new FolderFileSource(folder);
        var archive = ScummV5Archive.Load(source, ScummV5Archive.FindGame(source)!);
        var looping = 0;
        foreach (var sound in archive.ReadSounds().Where(s => s.ImuseAdlib is not null))
        {
            var timeline = ScummImuseTimeline.Build(ScummImuseSong.Parse(sound.ImuseAdlib!));
            if (!timeline.Loops) continue;
            looping++;
            Assert.True(timeline.JumpsBack >= 1);
            Assert.True(timeline.EndSeconds <= ScummImuseTimeline.MaxSeconds + 1e-6);
        }
        Assert.True(looping > 20, $"{looping} looping AdLib sounds");
    }
}
