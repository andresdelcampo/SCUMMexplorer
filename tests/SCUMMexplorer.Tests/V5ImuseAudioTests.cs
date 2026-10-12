using SCUMMexplorer.Core.Scumm;

namespace SCUMMexplorer.Tests;

public sealed class V5ImuseAudioTests
{
    /// <summary>MDhd (body size 8) + a one-track MIDI file: tempo meta, a set-up exclusive, a note on and off, end of track.</summary>
    private static byte[] Part(params byte[] track) =>
    [
        .. "MDhd"u8, 0, 0, 0, 8, 0, 0, 0x80, 0x7F, 0, 0, 0, 0x80,
        .. "MThd"u8, 0, 0, 0, 6, 0, 2, 0, 1, 0x01, 0xE0,
        .. "MTrk"u8, 0, 0, 0, (byte)track.Length, .. track,
    ];

    [Fact]
    public void ReadsTheMidiFileBehindTheMdhdBlock()
    {
        var song = ScummImuseSong.Parse(Part(
            0x00, 0xFF, 0x51, 0x03, 0x06, 0xD5, 0x11, // tempo 447761 us
            0x00, 0xF0, 0x04, 0x7D, 0x02, 0x00, 0xF7, // iMUSE: start of song
            0x00, 0x91, 0x3C, 0x40, // note on
            0x83, 0x60, 0x81, 0x3C, 0x00, // 480 ticks later: note off as velocity 0 on channel 1
            0x00, 0xFF, 0x2F, 0x00));
        Assert.Equal(480, song.TicksPerQuarter);
        Assert.Equal(447761, song.Tempos[0].Microseconds);
        Assert.Equal(3, song.Events.Count);
        Assert.True(song.Events[0].IsSysex);
        Assert.Equal(new byte[] { 0x7D, 0x02, 0x00 }, song.Events[0].Data);
        Assert.Equal(0x91, song.Events[1].Status);
        Assert.Equal(480, song.Events[2].Tick);
        Assert.Equal(447761 / 1e6, song.SecondsAt(480), 6);
    }

    [Fact]
    public void ATrackThatEndsInsideItsLastMessageStillParses()
    {
        var song = ScummImuseSong.Parse(Part(0x00, 0x91, 0x3C, 0x40, 0x10, 0xF0, 0x20, 0x7D));
        Assert.Single(song.Events);
    }

    [Fact]
    public void RendersTheMonkeyIsland2RolandMusicWhenTheDataIsGiven()
    {
        var folder = Environment.GetEnvironmentVariable("SCUMM_V5_MT32_TEST_ROOT");
        var roms = Mt32RomSet.Find(Environment.GetEnvironmentVariable("SCUMM_MT32_ROM_ROOT"));
        if (folder is null || roms is null || !Directory.Exists(folder)) return;
        var source = new FolderFileSource(folder);
        var archive = ScummV5Archive.Load(source, ScummV5Archive.FindGame(source)!);
        var sounds = archive.ReadSounds();
        Assert.True(sounds.Count(s => s.ImuseRoland is not null) > 100);
        foreach (var sound in sounds.Where(s => s.ImuseRoland is not null)) ScummImuseSong.Parse(sound.ImuseRoland!);
        var result = new ScummImuseMt32(roms).Render(ScummImuseSong.Parse(sounds.First(s => s.Id == 28).ImuseRoland!));
        Assert.True(result.Loops);
        var peak = 0;
        for (var i = 44; i + 1 < result.Wave.Length; i += 2) peak = Math.Max(peak, Math.Abs((int)BitConverter.ToInt16(result.Wave, i)));
        Assert.True(peak > 1000);
    }
}
