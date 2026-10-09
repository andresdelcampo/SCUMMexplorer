namespace SCUMMexplorer.Core.Scumm;

/// <summary>The Nuked OPL3 emulation (NukedOPL3Sharp), in OPL2 use: the AdLib chip as the hardware behaves.</summary>
public sealed class NukedOplChip
{
    private readonly NukedOPL3Sharp.Opl3Chip _chip = new();

    public NukedOplChip(int sampleRate) => _chip.Reset((uint)sampleRate);

    /// <summary>
    /// Buffered, as Nuked provides for it: each write takes effect two samples after the previous one, like the real
    /// chip's write latency. The driver sets up a note's registers between its key-off and key-on; written all at
    /// one instant, the key-off never registers and the note never re-attacks (the envelope just keeps decaying,
    /// which made notes fade away: the spell effect, many title-music instruments).
    /// </summary>
    public void WriteRegister(ushort register, byte value) => _chip.WriteRegisterBuffered(register, value);

    /// <summary>Fills an interleaved stereo buffer with the next samples.</summary>
    public void GenerateStream(short[] stereo) => _chip.GenerateStream(stereo);
}
