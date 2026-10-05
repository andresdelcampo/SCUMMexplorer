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
