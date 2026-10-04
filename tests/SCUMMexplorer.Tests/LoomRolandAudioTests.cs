using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

public sealed class LoomRolandAudioTests
{
    [Fact]
    public void ParsesWaitsMarkersAndEndsOfStream()
    {
        // program change, marker, wait 5, note on (velocity 0 is kept as read), wait 256+4 (F1), controller, loop
        byte[] data = [0xC5, 0x6C, 0xA0, 0xF0, 0x05, 0x95, 0x3C, 0x40, 0xF1, 0x04, 0xB5, 0x07, 0x7F, 0xFF];
        var song = LoomRolandSong.Parse(data);
        Assert.Equal(3, song.Events.Count);
        Assert.Equal(new LoomRolandEvent(0, 0xC5, 0x6C, 0), song.Events[0]);
        Assert.Equal(new LoomRolandEvent(5, 0x95, 0x3C, 0x40), song.Events[1]);
        Assert.Equal(new LoomRolandEvent(265, 0xB5, 0x07, 0x7F), song.Events[2]);
        Assert.Equal(265, song.Ticks);
        Assert.True(song.Loops);
        Assert.True(song.HasEndMarker);
        // 120 ticks a quarter note at 500000 microseconds: 4.1667 ms a tick.
        Assert.Equal(265 * 0.5 / 120, song.Duration.TotalSeconds, 6);
    }

    [Fact]
    public void ZeroEndsTheStreamWithoutLooping()
    {
        var song = LoomRolandSong.Parse([0xC1, 0x30, 0xF0, 0x0A, 0x00, 0xF0, 0x20]);
        Assert.Single(song.Events);
        Assert.Equal(10, song.Ticks);
        Assert.False(song.Loops);
        Assert.True(song.HasEndMarker);
    }

    [Theory]
    [InlineData(new byte[] { 0xF0 })]
    [InlineData(new byte[] { 0x40 })]
    [InlineData(new byte[] { 0x95, 0x3C })]
    [InlineData(new byte[] { 0xD1, 0x10 })]
    public void RefusesStreamsItCannotReadExactly(byte[] data) => Assert.Throws<InvalidDataException>(() => LoomRolandSong.Parse(data));

    [Fact]
    public void FindsRomsOnlyWhenBothImagesOfAModelArePresent()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"scumm-mt32-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        try
        {
            Assert.Null(Mt32RomSet.Find(folder));
            File.WriteAllBytes(Path.Combine(folder, "MT32_CONTROL.ROM"), [0]);
            Assert.Null(Mt32RomSet.Find(folder));
            File.WriteAllBytes(Path.Combine(folder, "CM32L_PCM.ROM"), [0]);
            File.WriteAllBytes(Path.Combine(folder, "CM32L_CONTROL.ROM"), [0]);
            Assert.Equal("CM-32L", Mt32RomSet.Find(folder)!.Model);
            File.WriteAllBytes(Path.Combine(folder, "MT32_PCM.ROM"), [0]);
            Assert.Equal("MT-32", Mt32RomSet.Find(folder)!.Model);
        }
        finally { Directory.Delete(folder, true); }
        Assert.Null(Mt32RomSet.Find(null));
    }

    [Fact]
    public void ReadsTheSuppliedRolandSoundsAndTellsThemFromTheSpeakerBundles()
    {
        var folder = Environment.GetEnvironmentVariable("SCUMM_SOUND_TEST_ROOT");
        if (string.IsNullOrWhiteSpace(folder)) return;
        var sounds = new GameFolderScanner().Scan(folder).Resources
            .Where(resource => resource.Kind == ResourceKind.Sound && resource.ResourceId.HasValue && resource.Offset.HasValue)
            .ToDictionary(resource => resource.ResourceId!.Value);
        // Only the edition with the Roland upgrade installed has files 82 to 85.
        if (!sounds.TryGetValue(33, out var first) || !Path.GetFileName(first.FilePath).Equals("82.LFL", StringComparison.OrdinalIgnoreCase)) return;
        foreach (var id in Enumerable.Range(1, 32).Where(id => sounds.ContainsKey(id)))
            Assert.False(LoomRolandSong.IsRoland(sounds[id]), $"Sound {id} is a speaker bundle.");
        foreach (var id in Enumerable.Range(33, 32))
        {
            Assert.True(LoomRolandSong.IsRoland(sounds[id]), $"Sound {id} should be Roland.");
            var song = LoomRolandSong.Read(sounds[id]);
            Assert.NotEmpty(song.Events);
            Assert.All(song.Events, item => Assert.True(item.Channel <= 9, $"Sound {id} uses channel {item.Channel}."));
        }
        // The overture is "a two-minute musical overture" (READ.ME): the longest piece of the upgrade bar the last music, 153 s here.
        Assert.InRange(LoomRolandSong.Read(sounds[56]).Duration.TotalSeconds, 140, 165);
    }

    [Fact]
    public void RendersARolandSoundThroughTheEmulatorWhenTheRomsAreAvailable()
    {
        var folder = Environment.GetEnvironmentVariable("SCUMM_SOUND_TEST_ROOT");
        var roms = Mt32RomSet.Find(Environment.GetEnvironmentVariable("SCUMM_MT32_ROM_ROOT"));
        if (string.IsNullOrWhiteSpace(folder) || roms is null) return;
        var sound = new GameFolderScanner().Scan(folder).Resources
            .FirstOrDefault(resource => resource.Kind == ResourceKind.Sound && resource.ResourceId == 45 && resource.Offset.HasValue);
        if (sound is null || !LoomRolandSong.IsRoland(sound)) return;
        var wave = new LoomMt32Audio(roms).RenderWave(sound);
        var rate = BitConverter.ToInt32(wave, 24);
        var peak = 0;
        for (var i = 44; i + 1 < wave.Length; i += 2) peak = Math.Max(peak, Math.Abs((int)BitConverter.ToInt16(wave, i)));
        Assert.True(peak > 1000, $"The rendered sound is nearly silent (peak {peak}).");
        // Song length (2.5 s) plus the release tail.
        Assert.InRange((wave.Length - 44) / 4.0 / rate, 2.5, 7);
    }

    [Fact]
    public void TheRolandSoundCardMakesTheScriptsChooseRolandSounds()
    {
        var folder = Environment.GetEnvironmentVariable("SCUMM_SOUND_TEST_ROOT");
        if (string.IsNullOrWhiteSpace(folder) || !File.Exists(Path.Combine(folder, "82.LFL"))) return;
        var game = new GameFolderScanner().Scan(folder);
        var snapshot = new LflObjectStateReader().ReadSnapshot(Path.Combine(folder, "00.LFL"));

        List<int> SoundIds(int card)
        {
            var session = new LoomExplorationSession(game, snapshot, soundCard: card);
            Assert.Equal(card, session.SoundCard);
            for (var frame = 0; frame < 1500; frame++) session.AdvanceFrame();
            return session.SoundEvents.Where(item => item.Event.Kind is LoomScriptEventKind.SoundStarted or LoomScriptEventKind.MusicStarted)
                .Select(item => item.Event.TargetId).OfType<int>().ToList();
        }

        var roland = SoundIds(LoomExplorationSession.RolandSoundCard);
        Assert.NotEmpty(roland);
        Assert.All(roland, id => Assert.InRange(id, 33, 64));
        var plain = SoundIds(0);
        Assert.NotEmpty(plain);
        Assert.All(plain, id => Assert.InRange(id, 1, 32));
    }
}
