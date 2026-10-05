namespace SCUMMexplorer.Core.Formats;

/// <summary>Which OPL chip emulation renders the AdLib music and effects.</summary>
public enum AdlibEmulation
{
    /// <summary>Nuked OPL3 (NukedOPL3Sharp): the YM3812 as the hardware behaves.</summary>
    Hardware,
    /// <summary>ScummVM's MAME OPL core (<see cref="MameOpl2"/>): how ScummVM's default AdLib sounds.</summary>
    ScummVm
}

/// <summary>The part of an OPL emulation the renderers use.</summary>
public interface IOplChip
{
    void WriteRegister(ushort register, byte value);

    /// <summary>Fills an interleaved stereo buffer with the next samples.</summary>
    void GenerateStream(short[] stereo);
}

/// <summary>The Nuked OPL3 emulation (NukedOPL3Sharp), in OPL2 use.</summary>
public sealed class NukedOplChip : IOplChip
{
    private readonly NukedOPL3Sharp.Opl3Chip _chip = new();

    public NukedOplChip(int sampleRate) => _chip.Reset((uint)sampleRate);

    /// <summary>
    /// Buffered, as Nuked provides for it: each write takes effect two samples after the previous one, like the real
    /// chip's write latency. The driver sets up a note's registers between its key-off and key-on; written all at
    /// one instant, the key-off never registers and the note never re-attacks (the envelope just keeps decaying).
    /// </summary>
    public void WriteRegister(ushort register, byte value) => _chip.WriteRegisterBuffered(register, value);

    public void GenerateStream(short[] stereo) => _chip.GenerateStream(stereo);

    public static IOplChip Create(AdlibEmulation emulation, int sampleRate) =>
        emulation == AdlibEmulation.ScummVm ? new ScummVmVolumeChip(new MameOpl2(sampleRate)) : new NukedOplChip(sampleRate);
}

/// <summary>
/// ScummVM's AdLib volume handling (<c>Player_AD::writeReg</c>): the output level (bits 0-5 of registers 0x40-0x55) of
/// each carrier, or of both operators when the channel is additive, is scaled by the music/sound volume. ScummVM's
/// default is 192 of 255 (also in the bundled scummvm.ini), so the AdLib it plays is quieter than the raw instrument
/// data. The driver's own register state is untouched; only what reaches the chip is scaled.
/// </summary>
public sealed class ScummVmVolumeChip(IOplChip chip, int volume = ScummVmVolumeChip.DefaultVolume) : IOplChip
{
    public const int DefaultVolume = 192;
    private static readonly int[] OperatorToChannel =
        [0, 1, 2, 0, 1, 2, -1, -1, 3, 4, 5, 3, 4, 5, -1, -1, 6, 7, 8, 6, 7, 8];
    private static readonly int[] OperatorOffsets = [0, 3, 1, 4, 2, 5, 8, 11, 9, 12, 10, 13, 16, 19, 17, 20, 18, 21];
    private readonly byte[] _registers = new byte[256];

    public void WriteRegister(ushort register, byte value)
    {
        _registers[register & 0xff] = value;
        if (register is >= 0x40 and <= 0x55)
        {
            var offset = register - 0x40;
            var channel = OperatorToChannel[offset];
            if (channel != -1)
            {
                var additive = (_registers[0xC0 + channel] & 1) != 0;
                var scale = 255;
                if (additive || offset == OperatorOffsets[channel * 2 + 1]) scale = volume;
                var level = 0x3F - (value & 0x3F);
                level = level * scale / 255;
                value = (byte)((value & 0xC0) | (0x3F - level));
            }
        }
        chip.WriteRegister(register, value);
    }

    public void GenerateStream(short[] stereo) => chip.GenerateStream(stereo);
}
