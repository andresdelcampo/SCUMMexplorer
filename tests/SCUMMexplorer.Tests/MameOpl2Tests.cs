using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

public sealed class MameOpl2Tests
{
    // One carrier note on channel 0 (the modulator is silent): additive connection, full level, sustained.
    private static short[] Note(int kslBits, int fnumHigh = 0x2B, int fnumLow = 0x41, double seconds = 0.5)
    {
        var chip = new MameOpl2(49716);
        void W(int r, int v) => chip.WriteRegister((ushort)r, (byte)v);
        W(0x01, 0x20);
        W(0xC0, 0x01);
        W(0x20, 0x21); W(0x40, 0x3F); W(0x60, 0xF0); W(0x80, 0x07);
        W(0x23, 0x21); W(0x43, kslBits << 6); W(0x63, 0xF0); W(0x83, 0x07);
        W(0xA0, fnumLow); W(0xB0, 0x20 | fnumHigh);
        var stereo = new short[(int)(49716 * seconds) * 2];
        chip.GenerateStream(stereo);
        return stereo;
    }

    private static double Rms(short[] stereo)
    {
        double sum = 0;
        var from = stereo.Length / 8;
        for (var i = from; i < stereo.Length; i += 2) sum += (double)stereo[i] * stereo[i];
        return Math.Sqrt(sum / ((stereo.Length - from) / 2));
    }

    [Fact]
    public void ASustainedNoteIsAudibleAndMono()
    {
        var samples = Note(0);
        Assert.InRange(Rms(samples), 1000, 20000);
        for (var i = 0; i < samples.Length; i += 2) Assert.Equal(samples[i], samples[i + 1]);
    }

    [Fact]
    public void PitchFollowsTheFNumber()
    {
        // Block 4, F-number 0x141 (octave 4): crossings per second give the frequency of the OPL (49716 Hz) formula.
        var samples = Note(0, fnumHigh: 0x10 | 0x01, fnumLow: 0x41, seconds: 1);
        var crossings = 0;
        for (var i = 2; i < samples.Length; i += 2) if (samples[i - 2] < 0 && samples[i] >= 0) crossings++;
        var expected = 0x141 * 49716.0 / (1 << 20) * (1 << 4); // fnum * rate / 2^20 * 2^block
        Assert.InRange(crossings, expected * 0.97, expected * 1.03);
    }

    [Fact]
    public void KeyScaleLevelOneAttenuatesLessThanTwoLikeScummVmsMameCore()
    {
        // ScummVM reads the register value 1 as 1.5 dB/octave and 2 as 3 dB/octave (the chip documents the opposite);
        // LOOM's AdLib music is meant to be heard as ScummVM plays it.
        var none = Rms(Note(0, fnumHigh: 0x1B, fnumLow: 0x41));
        var one = Rms(Note(1, fnumHigh: 0x1B, fnumLow: 0x41));
        var two = Rms(Note(2, fnumHigh: 0x1B, fnumLow: 0x41));
        var three = Rms(Note(3, fnumHigh: 0x1B, fnumLow: 0x41));
        Assert.True(none > one && one > two && two > three, $"{none:F0} {one:F0} {two:F0} {three:F0}");
    }
}

public sealed class ScummVmVolumeChipTests
{
    private sealed class Recorder : IOplChip
    {
        public readonly Dictionary<int, byte> Written = [];
        public void WriteRegister(ushort register, byte value) => Written[register] = value;
        public void GenerateStream(short[] stereo) { }
    }

    [Fact]
    public void ScalesTheCarrierOfAnFmChannelButNotItsModulator()
    {
        var recorder = new Recorder();
        var chip = new ScummVmVolumeChip(recorder, 192);
        chip.WriteRegister(0xC0, 0x00);      // FM: only the carrier (operator offset 3 of channel 0) is scaled
        chip.WriteRegister(0x40, 0x05);      // modulator, kept
        chip.WriteRegister(0x43, 0x80 | 0x00); // carrier: key scale bits kept, level 63 -> 63 * 192 / 255 = 47
        Assert.Equal(0x05, recorder.Written[0x40]);
        Assert.Equal(0x80 | (0x3F - 47), recorder.Written[0x43]);
    }

    [Fact]
    public void ScalesBothOperatorsOfAnAdditiveChannel()
    {
        var recorder = new Recorder();
        var chip = new ScummVmVolumeChip(recorder, 192);
        chip.WriteRegister(0xC0, 0x01);
        chip.WriteRegister(0x40, 0x00);
        Assert.Equal(0x3F - 47, recorder.Written[0x40]);
    }

    [Fact]
    public void ChoosesTheChipByEmulation()
    {
        Assert.IsType<NukedOplChip>(NukedOplChip.Create(AdlibEmulation.Hardware, 49716));
        Assert.IsType<ScummVmVolumeChip>(NukedOplChip.Create(AdlibEmulation.ScummVm, 49716));
    }
}

public sealed class NukedRetriggerTests
{
    // The driver rewrites a note's registers between its key-off and key-on. On the real chip each write takes time, so
    // the note re-attacks; the hardware option must not collapse that into one instant (the notes then fade away).
    [Theory]
    [InlineData(AdlibEmulation.Hardware)]
    [InlineData(AdlibEmulation.ScummVm)]
    public void ANoteReAttacksAfterKeyOffAndKeyOnInTheSameInstant(AdlibEmulation emulation)
    {
        var chip = NukedOplChip.Create(emulation, 49716);
        void W(int r, int v) => chip.WriteRegister((ushort)r, (byte)v);
        W(0x01, 0x20); W(0xC0, 0x01);
        W(0x20, 0x21); W(0x40, 0x3F); W(0x60, 0xF0); W(0x80, 0x07);
        W(0x23, 0x21); W(0x43, 0x00); W(0x63, 0xD3); W(0x83, 0x89); // fast attack, slow decay to a low sustain level
        W(0xA0, 0x41); W(0xB0, 0x2B);
        double Level(int frames)
        {
            var buffer = new short[frames * 2];
            chip.GenerateStream(buffer);
            double sum = 0;
            for (var i = 0; i < buffer.Length; i += 2) sum += (double)buffer[i] * buffer[i];
            return Math.Sqrt(sum / frames);
        }
        Level(49716 / 20);                 // attack
        Level(49716 * 2);                  // two seconds of decay towards the sustain level
        var decayed = Level(49716 / 5);    // the settled level
        W(0xB0, 0x0B);                     // key off ...
        for (var i = 0; i < 12; i++) W(0x23, 0x21); // ... the registers of the next note ...
        W(0xB0, 0x2B);                     // ... key on, with no samples generated in between by the driver
        var retriggered = Level(49716 / 10);
        Assert.True(retriggered > decayed * 3, $"{emulation}: {decayed:F0} before, {retriggered:F0} after the retrigger");
    }
}
