
namespace SCUMMexplorer.Tests;

public sealed class NukedOplChipTests
{
    // The driver rewrites a note's registers between its key-off and key-on. On the real chip each write takes time, so
    // the note re-attacks; the chip must not collapse that into one instant (the notes then fade away).
    [Fact]
    public void ANoteReAttacksAfterKeyOffAndKeyOnInTheSameInstant()
    {
        var chip = new NukedOplChip(49716);
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
        Assert.True(retriggered > decayed * 3, $"{decayed:F0} before, {retriggered:F0} after the retrigger");
    }

    [Fact]
    public void ASustainedNoteIsAudible()
    {
        var chip = new NukedOplChip(49716);
        void W(int r, int v) => chip.WriteRegister((ushort)r, (byte)v);
        W(0x01, 0x20); W(0xC0, 0x01);
        W(0x20, 0x21); W(0x40, 0x3F); W(0x60, 0xF0); W(0x80, 0x07);
        W(0x23, 0x21); W(0x43, 0x00); W(0x63, 0xF0); W(0x83, 0x07);
        W(0xA0, 0x41); W(0xB0, 0x2B);
        var buffer = new short[49716 * 2];
        chip.GenerateStream(buffer);
        Assert.Contains(buffer, sample => Math.Abs((int)sample) > 500);
    }
}
