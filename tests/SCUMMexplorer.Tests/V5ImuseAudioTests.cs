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

    [Fact]
    public void RendersTheMonkeyIsland2AdlibMusicWhenTheDataIsGiven()
    {
        var folder = Environment.GetEnvironmentVariable("SCUMM_V5_MT32_TEST_ROOT");
        if (folder is null || !Directory.Exists(folder)) return;
        var source = new FolderFileSource(folder);
        var archive = ScummV5Archive.Load(source, ScummV5Archive.FindGame(source)!);
        var report = new System.Text.StringBuilder();
        var silent = 0;
        foreach (var sound in archive.ReadSounds().Where(s => s.ImuseAdlib is not null).Where((s, i) => i % 5 == 0 || s.Id is 4 or 16 or 34))
        {
            ScummImuseAdlib.Result result;
            try { result = new ScummImuseAdlib().Render(ScummImuseSong.Parse(sound.ImuseAdlib!)); }
            catch (InvalidDataException) { silent++; report.AppendLine($"{sound.Id}: silent"); continue; } // e.g. parts a script must switch on
            var peak = 0;
            for (var i = 44; i + 1 < result.Wave.Length; i += 2) peak = Math.Max(peak, Math.Abs((int)BitConverter.ToInt16(result.Wave, i)));
            report.AppendLine($"{sound.Id}: {result.Seconds:0.0}s loops {result.Loops} peak {peak}");
            if (sound.Id is 4 or 16 or 34) File.WriteAllBytes(Path.Combine(Path.GetTempPath(), $"v5-adl-{sound.Id}.wav"), result.Wave);
        }
        File.WriteAllText(Path.Combine(Path.GetTempPath(), "v5-adl-report.txt"), report.ToString());
        Assert.True(archive.ReadSounds().Count(s => s.ImuseAdlib is not null) > 150);
        Assert.True(silent < 10, $"{silent} silent AdLib sounds");
    }

    private static byte[] Nibbles(params byte[] bytes) => bytes.SelectMany(b => new[] { (byte)(b >> 4), (byte)(b & 15) }).ToArray();

    [Fact]
    public void AdlibNote69SoundsWhereTheDriversTablePutsIt()
    {
        // Part 0: on, full volume, bend range 2, one carrier-only (additive, modulator silenced) instrument, then note 69 for a second.
        byte[] instrument = new byte[30];
        // The driver writes the attack/decay and sustain/release bytes inverted and treats the output level as 63 = loudest.
        instrument[0] = 0x21; instrument[1] = 0x00; instrument[2] = 0x0F; instrument[3] = 0xFF;          // modulator, silent
        instrument[5] = 0x21; instrument[6] = 0x3F; instrument[7] = 0x0F; instrument[8] = 0xFF;          // carrier, loudest, fastest attack, full sustain
        instrument[10] = 0x00;
        byte[] setup = [0x7D, 0x00, 0x00, .. Nibbles(0x01, 0x00, 0x7F, 0x40, 0x00, 0x00, 0x02, 0x00)];
        byte[] define = [0x7D, 0x10, 0x00, 0x01, .. Nibbles(instrument)];
        var track = new List<byte> { 0x00, 0xF0, (byte)(setup.Length + 1) };
        track.AddRange(setup); track.Add(0xF7);
        track.AddRange([0x00, 0xF0, (byte)(define.Length + 1)]); track.AddRange(define); track.Add(0xF7);
        track.AddRange([0x00, 0x90, 69, 100, 0x87, 0x40, 0x80, 69, 0, 0x00, 0xFF, 0x2F, 0x00]); // 960 ticks = 1 s at 120 bpm... (tempo default 500000: 480 ticks a half second)
        var song = ScummImuseSong.Parse(Part([.. track]));
        var wave = new ScummImuseAdlib().Render(song).Wave;
        // Count rising zero crossings of the left channel in the second after the attack.
        const int rate = 49716;
        var crossings = 0;
        short previous = 0;
        for (var i = 44 + rate / 4 * 4; i < 44 + rate / 4 * 4 + rate / 4 * 4 * 2 && i + 3 < wave.Length; i += 4)
        {
            var sample = BitConverter.ToInt16(wave, i);
            if (previous < 0 && sample >= 0) crossings++;
            previous = sample;
        }
        var hz = crossings / 0.5;
        // MIDI note 69 -> note 65 -> block 5, F-number 143 (driver table): 143 * 49716 / 2^15 = 216.97 Hz, as in ScummVM's driver.
        Assert.InRange(hz, 214, 220);
    }

    [Fact]
    public void ReadsTheSampledEffectsWhenTheDataIsGiven()
    {
        var folder = Environment.GetEnvironmentVariable("SCUMM_V5_MT32_TEST_ROOT");
        if (folder is null || !Directory.Exists(folder)) return;
        var source = new FolderFileSource(folder);
        var archive = ScummV5Archive.Load(source, ScummV5Archive.FindGame(source)!);
        var digital = archive.ReadSounds().Where(s => s.ImuseDigital is not null).ToList();
        Assert.NotEmpty(digital);
        foreach (var sound in digital)
        {
            var wave = ScummV5Digital.RenderWave(sound.ImuseDigital!);
            Assert.True(wave.Length > 1000);
            Assert.InRange(BitConverter.ToInt32(wave, 24), 5000, 48000);
        }
    }
}
