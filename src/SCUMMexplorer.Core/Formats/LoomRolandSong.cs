using System.Buffers.Binary;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Formats;

/// <summary>One MIDI message of a Roland song at an absolute tick (<see cref="LoomRolandSong.TicksPerQuarter"/> per quarter note).</summary>
public readonly record struct LoomRolandEvent(long Tick, byte Status, byte Data1, byte Data2)
{
    public byte Channel => (byte)(Status & 0x0F);
    public byte Command => (byte)(Status >> 4);
}

/// <summary>
/// A sound of the DOS LOOM Roland upgrade (sounds 33 to 64 of the EGA release with the upgrade installed; the scripts choose them when
/// <c>var[0x030]</c> is 4). The resource is a 16-bit length, two bytes the player skips, then a tagless MIDI-like stream
/// (ScummVM's <c>readSoundResourceSmallHeader</c> prepends "RO" to the rest and <c>MidiParser_RO</c> reads it): <c>F0 n</c> waits n ticks and
/// <c>F1 n</c> waits n + 256, <c>Ax</c> is a marker (no time), <c>8x/9x/Bx</c> take two data bytes, <c>Cx</c> one (a program number of the MT-32),
/// <c>00</c> ends the song and <c>FF</c> ends it with a loop. The tempo is fixed at 500000 microseconds per quarter note, 120 ticks per quarter.
/// </summary>
public sealed class LoomRolandSong
{
    public const int TicksPerQuarter = 120;
    public const int MicrosecondsPerQuarter = 500000;
    public static double SecondsPerTick => MicrosecondsPerQuarter / 1e6 / TicksPerQuarter;

    public IReadOnlyList<LoomRolandEvent> Events { get; }
    /// <summary>The tick of the end of the stream: its last wait, or its end marker.</summary>
    public long Ticks { get; }
    public bool Loops { get; }
    public bool HasEndMarker { get; }
    public TimeSpan Duration => TimeSpan.FromSeconds(Ticks * SecondsPerTick);

    private LoomRolandSong(IReadOnlyList<LoomRolandEvent> events, long ticks, bool loops, bool hasEnd)
    {
        Events = events;
        Ticks = ticks;
        Loops = loops;
        HasEndMarker = hasEnd;
    }

    /// <summary>True for an indexed sound that is a Roland stream rather than the PC speaker + AdLib bundle of the other sounds.</summary>
    public static bool IsRoland(GameResource resource)
    {
        try
        {
            var file = ReadFile(resource);
            var offset = resource.Offset!.Value;
            if (offset < 0 || offset > file.Length - 6) return false;
            var length = BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(offset, 2));
            if (length < 6 || length > file.Length - offset) return false;
            // The speaker + AdLib bundle has the speaker resource's header length (22) at byte 6.
            return length < 8 || BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(offset + 6, 2)) != 22;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { return false; }
    }

    public static LoomRolandSong Read(GameResource resource)
    {
        if (resource.Kind != ResourceKind.Sound || resource.Offset is null || resource.ContainerFileNumber is null)
            throw new NotSupportedException("This is not an indexed old-bundle LOOM sound.");
        var file = ReadFile(resource);
        var offset = resource.Offset.Value;
        if (offset < 0 || offset > file.Length - 4) throw new InvalidDataException("Invalid sound offset.");
        var length = BinaryPrimitives.ReadUInt16LittleEndian(file.AsSpan(offset, 2));
        if (length < 5 || length > file.Length - offset) throw new InvalidDataException("The Roland sound has an invalid length.");
        return Parse(file.AsSpan(offset + 4, length - 4));
    }

    /// <summary>Decodes a stream (the bytes after the 4-byte resource header).</summary>
    public static LoomRolandSong Parse(ReadOnlySpan<byte> data)
    {
        var events = new List<LoomRolandEvent>();
        long tick = 0;
        var loops = false;
        var ended = false;
        for (var p = 0; p < data.Length;)
        {
            var status = data[p++];
            if (status == 0) { ended = true; break; }
            if (status == 0xFF) { ended = true; loops = true; break; }
            if (status >> 4 == 0xA) continue;
            if (status is 0xF0 or 0xF1)
            {
                if (p >= data.Length) throw new InvalidDataException("A Roland wait has no length.");
                tick += data[p++] + (status == 0xF1 ? 256 : 0);
                continue;
            }
            if (status < 0x80) throw new InvalidDataException($"Unexpected data byte 0x{status:X2} at +{p - 1} in a Roland stream.");
            var count = (status >> 4) switch { 0xC => 1, 0x8 or 0x9 or 0xB => 2, _ => throw new InvalidDataException($"Unsupported Roland event 0x{status:X2} at +{p - 1}.") };
            if (p + count > data.Length) throw new InvalidDataException("A Roland event is cut off.");
            events.Add(new LoomRolandEvent(tick, status, data[p], count == 2 ? data[p + 1] : (byte)0));
            p += count;
        }
        return new LoomRolandSong(events, tick, loops, ended);
    }

    private static byte[] ReadFile(GameResource resource)
    {
        var file = File.ReadAllBytes(resource.FilePath);
        for (var i = 0; i < file.Length; i++) file[i] ^= 0xFF;
        return file;
    }
}
