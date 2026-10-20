using System.Buffers.Binary;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using Xunit.Abstractions;

namespace SCUMMexplorer.Tests;

/// <summary>
/// Sound polish (19 October 2026), from ScummVM's source (GitHub master read 19 October 2026): Monkey Island 1's AD music on iMUSE's clock
/// (<c>convertADResource</c>, <c>MidiParser::onTimer</c>), Indy3's AdLib driver constants in the sound player (R-13.9), loops longer than the
/// three-minute rendering, and the PC speaker driver's timing for <c>isSoundRunning</c> (<c>Player_V2</c>). Record: <c>.verification/sound-polish-20261019/README.md</c>.
/// </summary>
public sealed class ScummSoundPolishTests(ITestOutputHelper output)
{
    // An AD music resource as the disks hold it (the bytes after the block header): 0x80 marks music, then the tempo byte, play-once, the loop
    // offset, the instruments and the event track at 0x93.
    private static byte[] AdSong(byte tempo, bool playOnce, byte[] track, bool instrument = false)
    {
        var data = new byte[0x93 + track.Length];
        data[2] = 0x80;
        data[3] = tempo;
        data[4] = (byte)(playOnce ? 1 : 0);
        if (instrument)
        {
            data[10] = 1;
            data[11] = 1; // channel 1 (id 0) plays instrument 0, at offset 19
            byte[] voice = [0x00, 0x00, 0x00, 0x01, 0x10, 0xF0, 0x77, 0x00, 0x01, 0x00, 0xF0, 0x77, 0x00, 0x00];
            voice.CopyTo(data, 19);
        }
        track.CopyTo(data, 0x93);
        return data;
    }

    // A note on, 480 ticks (the two-byte delay 0x83 0x60), and the end of the track.
    private static readonly byte[] OneBeat = [0x90, 0x3C, 0x7F, 0x83, 0x60, 0xFF, 0x2F, 0x00];

    [Fact]
    public void MonkeyIslandAdMusicRunsOnTheImuseClock()
    {
        // convertADResource: tempo 500000 * 256 / 100 = 1280000 µs a quarter, 480 ticks a quarter, so (1280000 + 120) / 480 = 2666 µs a tick
        // (MidiParser::setTempo). The end event is at tick 160 + 480 = 640: 1706240 µs, played in the 250 Hz step that ends at 427 × 4000 µs, which the
        // 472 Hz callbacks reach at ceil(427 × 4000 × 472 / 10^6) = 807.
        var adlib = new ScummAdlibAudio();
        var once = adlib.ReadDataLifetime(AdSong(100, true, OneBeat), ScummAdlibTiming.ImuseConversion);
        Assert.Equal(ScummSoundLifetimeKind.Finite, once.Kind);
        Assert.Equal(807, once.Callbacks);
        // Player_AD's constants give another length for the same bytes (the old half-tempo estimate did too).
        Assert.NotEqual(807, adlib.ReadDataLifetime(AdSong(100, true, OneBeat), ScummAdlibTiming.Standard).Callbacks);

        // A looping song jumps back to its first event (beat 1, tick 160: 426560 µs, step 107, callback 203) when its end comes (callback 807).
        var loop = adlib.ReadLoop(AdSong(100, false, OneBeat), ScummAdlibTiming.ImuseConversion);
        Assert.NotNull(loop);
        Assert.Equal(203.0 / 472, loop!.RestartSeconds, 6);
        Assert.Equal(807.0 / 472, loop.EndSeconds, 6);
        // iMUSE's music timer: 0 when no song plays, so it is not held; its values start again at the loop.
        var lifetime = adlib.ReadLifetime(AdSong(100, false, OneBeat), ScummAdlibTiming.ImuseConversion);
        Assert.Equal(ScummSoundLifetimeKind.Looping, lifetime.Kind);
        Assert.False(lifetime.KeepsMusicTimer);
        Assert.Equal(203, lifetime.LoopStartCallback);
        Assert.Equal(807, lifetime.LoopEndCallback);
    }

    [Theory]
    [InlineData("monkey1 ega en", true)]
    [InlineData("monkey1 vga en", true)]
    [InlineData("indy3 ega en", false)]
    [InlineData("indy3 vga en", false)]
    [InlineData("loom ega mt32 en", false)]
    [InlineData("loom vga en", false)]
    public void TheDriverClockFollowsScummVmsChoiceOfMusicPlayer(string folderName, bool imuse)
    {
        // setupMusic (scumm.cpp): Player_AD for LOOM and Indy3 DOS; the AD songs of every other game go to iMUSE (readSoundResourceSmallHeader).
        var folder = Path.Combine(TestGames.Root, folderName);
        if (!Directory.Exists(folder)) return;
        var game = new GameFolderScanner().Scan(folder);
        if (game.Version == ScummVersion.V5) return; // a CD copy under this name: iMUSE's own songs, not AD blocks
        var timing = ScummProfile.For(game).AdlibTiming;
        Assert.Equal(imuse, timing.ImuseClock);
        Assert.Equal(GameSupport.FamilyOf(game) is GameFamily.LoomEgaReference or GameFamily.LoomEga or GameFamily.LoomEgaDemo or GameFamily.LoomCd,
            timing == ScummAdlibTiming.Loom);
    }

    [Fact]
    public void IndyThreeEgaMusicRendersWithItsOwnDriverConstants()
    {
        // R-13.9: the sound player's AdLib rendition of Indy3 uses the non-LOOM constants (limit 256, tempo × 1, delays >> 1), the ones its session's
        // clock already used: a song that ends renders to the length the session gives it (plus at most two seconds of release).
        var folder = Environment.GetEnvironmentVariable("SCUMM_INDY3_EGA_TEST_ROOT");
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return;
        var game = new GameFolderScanner().Scan(folder);
        var timing = ScummProfile.For(game).AdlibTiming;
        Assert.Equal(ScummAdlibTiming.Standard, timing);
        var adlib = new ScummAdlibAudio();
        var checkedSongs = 0;
        foreach (var sound in game.Resources.Where(r => r.Kind == ResourceKind.Sound && adlib.HasMusic(r)))
        {
            var lifetime = adlib.ReadLifetime(sound, timing);
            if (lifetime is not { Kind: ScummSoundLifetimeKind.Finite, Callbacks: int callbacks } || callbacks > 472 * 60) continue;
            var seconds = (adlib.RenderWave(sound, timing).Length - 44) / 4.0 / 49716;
            var loomSeconds = (adlib.RenderWave(sound).Length - 44) / 4.0 / 49716;
            output.WriteLine($"sound {sound.ResourceId}: session {callbacks / 472.0:0.00} s, render {seconds:0.00} s, with LOOM's constants {loomSeconds:0.00} s");
            Assert.InRange(seconds, callbacks / 472.0 - 0.01, callbacks / 472.0 + 2.01);
            Assert.True(Math.Abs(seconds - loomSeconds) > 0.05, $"Sound {sound.ResourceId} renders the same with LOOM's constants.");
            if (++checkedSongs == 3) break;
        }
        Assert.True(checkedSongs > 0);
    }

    [Fact]
    public void ALoopLongerThanThreeMinutesRendersThroughItsRestart()
    {
        // Player_AD (Indy3's constants), tempo 1: an engine tick every 256 callbacks; the 40 ticks before the first event and two delays of 300
        // (>> 1: 150 ticks each) make a first pass of 340 ticks, 87040 callbacks (184.4 s), after a restart point at 40 ticks (21.7 s). The rendition
        // goes on through the restart and ten seconds more (194.4 s), where it used to stop at 180 s, before the song had come round.
        byte[] track = [0x90, 0x3C, 0x7F, 0x82, 0x2C, 0x80, 0x3C, 0x00, 0x82, 0x2C, 0xFF, 0x2F, 0x00];
        var song = AdSong(1, false, track, instrument: true);
        var adlib = new ScummAdlibAudio();
        var loop = adlib.ReadLoop(song, ScummAdlibTiming.Standard);
        Assert.NotNull(loop);
        Assert.Equal(40 * 256 / 472.0, loop!.RestartSeconds, 2);
        Assert.Equal(340 * 256 / 472.0, loop.EndSeconds, 2);
        var seconds = (adlib.RenderWave(song, timing: ScummAdlibTiming.Standard).Length - 44) / 4.0 / 49716;
        Assert.InRange(seconds, loop.EndSeconds + 9.99, loop.EndSeconds + 10.01);
    }

    [Fact]
    public void ADriverTimerThatSurvivesAStopIsHeld()
    {
        // Player_V2Base (version 3): _music_timer is the player's, so stopSound (which clears the channels) leaves it; the version 2 channel timer is cleared.
        ScummSoundTimerPoint[] values = [new(0, 0), new(236, 1), new(472, 2)];
        var v3 = new ScummSoundLifetime(ScummSoundLifetimeKind.Looping, null, false, TimerValues: values, KeepsMusicTimer: true, TimerSurvivesStop: true);
        var v2 = v3 with { TimerSurvivesStop = false };
        var clock = new ScummSoundClock(id => id == 1 ? v3 : v2);
        clock.Start(1, false);
        clock.Advance(60);
        Assert.Equal(2, clock.MusicTimer());
        clock.Stop(1);
        Assert.Equal(2, clock.MusicTimer());
        clock.Start(2, false);
        clock.Advance(60);
        clock.Stop(2);
        Assert.Equal(0, clock.MusicTimer());
    }

    [Fact]
    public void ThePcSpeakerDriverTimesIndyThreesSoundsWhenItIsTheDevice()
    {
        // Player_V2 (the PC speaker of a version 3 game): a sound runs while its channels have time left, at 236 ticks a second (two clock callbacks a tick).
        var folder = Environment.GetEnvironmentVariable("SCUMM_INDY3_EGA_TEST_ROOT");
        if (string.IsNullOrWhiteSpace(folder) || !Directory.Exists(folder)) return;
        var game = new GameFolderScanner().Scan(folder);
        var speaker = new ScummPcSpeakerAudio();
        var differing = 0;
        foreach (var sound in game.Resources.Where(r => r.Kind == ResourceKind.Sound && r.ResourceId is > 0).Take(80))
        {
            byte[] data;
            try { data = ScummPcSpeakerAudio.ReadOldBundle(sound); }
            catch (Exception ex) when (ex is NotSupportedException or InvalidDataException) { continue; }
            var lifetime = speaker.ReadV3Lifetime(data, 4);
            if (lifetime is not { Kind: ScummSoundLifetimeKind.Finite, Callbacks: int callbacks }) continue;
            Assert.True(lifetime.KeepsMusicTimer && lifetime.TimerSurvivesStop);
            var adlib = new ScummAdlibAudio().ReadLifetime(sound, ScummAdlibTiming.Standard);
            if (adlib.Callbacks != callbacks) differing++;
            // The rendition plays the same ticks (22050 / 236 samples each), then lets the speaker's filter settle.
            double seconds;
            try { seconds = (speaker.RenderWave(data, 4).Length - 44) / 2.0 / 22050; }
            catch (NotSupportedException) { continue; } // no note sounds
            Assert.InRange(seconds, callbacks / 472.0 - 0.01, callbacks / 472.0 + 1.01);
        }
        output.WriteLine($"{differing} sounds last otherwise on the PC speaker than on AdLib");
        Assert.True(differing > 0);

        var session = ScummGameSession.BootForPlay(game, out _);
        Assert.False(session.PcSpeakerTiming);
        session.PcSpeakerTiming = true;
        Assert.True(session.PcSpeakerTiming);
    }

    [Fact]
    public void AVersionFiveGameKeepsItsImuseClockForEveryDevice()
    {
        // A v5 game's songs are iMUSE's for every card (the speaker driver is iMUSE's too), so the session has no separate PC speaker timing.
        var folder = Path.Combine(TestGames.Root, "mi2 mt32 en");
        if (!Directory.Exists(folder)) return;
        var game = new GameFolderScanner().Scan(folder);
        if (game.Version != ScummVersion.V5) return;
        var session = new ScummGameSession(game, game.V4Disks!.Disks.OfType<ScummV5Archive>().First().ReadIndexedObjects(), boot: false);
        session.PcSpeakerTiming = true;
        Assert.False(session.PcSpeakerTiming);
    }

    [Fact]
    public void LongestLoopsOfTheOldGames()
    {
        // A survey for the record (not an assertion): the longest AdLib and PC speaker loops in the v3/v4 copies, against the three-minute rendering.
        foreach (var name in new[] { "loom ega mt32 en", "indy3 ega en", "indy3 vga en", "monkey1 ega en", "monkey1 vga en" })
        {
            var folder = Path.Combine(TestGames.Root, name);
            if (!Directory.Exists(folder)) continue;
            var game = new GameFolderScanner().Scan(folder);
            if (game.Version is not (ScummVersion.V3 or ScummVersion.V4)) continue;
            var timing = ScummProfile.For(game).AdlibTiming;
            var adlib = new ScummAdlibAudio();
            var speaker = new ScummPcSpeakerAudio();
            var loops = new List<string>();
            foreach (var sound in game.Resources.Where(r => r.Kind == ResourceKind.Sound && r.ResourceId is int))
            {
                ScummImuseLoop? a = null, s = null;
                if (game.V4ArchiveFor(sound) is ScummV4Archive archive)
                {
                    var part = archive.ReadSounds().FirstOrDefault(item => item.Id == sound.ResourceId);
                    if (part?.Adlib is { } ad) a = adlib.ReadLoop(ad, timing);
                    if (part?.Speaker is { } wa) s = speaker.ReadLoop(wa, 6);
                }
                else if (game.V4Disks is null)
                {
                    a = adlib.ReadLoop(sound, timing);
                    try { s = speaker.ReadLoop(ScummPcSpeakerAudio.ReadOldBundle(sound), 4); } catch (Exception ex) when (ex is NotSupportedException or InvalidDataException) { }
                }
                if (a is not null) loops.Add($"{sound.ResourceId} AdLib {a.RestartSeconds:0.0}-{a.EndSeconds:0.0} s");
                if (s is not null) loops.Add($"{sound.ResourceId} speaker {s.RestartSeconds:0.0}-{s.EndSeconds:0.0} s");
            }
            output.WriteLine($"{name} ({timing}): {string.Join("; ", loops)}");
        }
    }
}
