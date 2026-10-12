using System.Buffers.Binary;
using System.Text;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>One event of an iMUSE song: a channel message (<see cref="Data"/> is its bytes after the status) or a system exclusive (<see cref="Data"/> is the bytes after <c>F0</c>, without the closing <c>F7</c>).</summary>
public readonly record struct ScummImuseEvent(long Tick, byte Status, byte[] Data)
{
    public bool IsSysex => Status == 0xF0;
    public byte Channel => (byte)(Status & 0x0F);
    public byte Command => (byte)(Status >> 4);
}

/// <summary>
/// A sound of the v5 games (Monkey Island 2, Indiana Jones and the Fate of Atlantis): the body of a <c>ROL </c>, <c>ADL </c> or <c>SPK </c> block,
/// that is an <c>MDhd</c> block and then a standard MIDI file (format 2, one track). The track carries iMUSE system exclusives (manufacturer 0x7D, see
/// <see cref="ScummImusePlayer"/>) and, for the MT-32, Roland ones (0x41). Reference: ScummVM <c>imuse_player.cpp</c>, <c>sysex_scumm.cpp</c>.
/// </summary>
public sealed class ScummImuseSong
{
    public int TicksPerQuarter { get; }
    public IReadOnlyList<ScummImuseEvent> Events { get; }
    /// <summary>Tempo changes as (tick, microseconds per quarter note), in order; the first is at or before tick 0 (the default 500000 when the track sets none).</summary>
    public IReadOnlyList<(long Tick, int Microseconds)> Tempos { get; }
    public long Ticks { get; }

    private ScummImuseSong(int division, List<ScummImuseEvent> events, List<(long, int)> tempos, long ticks)
    {
        TicksPerQuarter = division;
        Events = events;
        Tempos = tempos;
        Ticks = ticks;
    }

    /// <summary>Seconds at which a tick falls, through the tempo map.</summary>
    public double SecondsAt(long tick)
    {
        double seconds = 0;
        long last = 0;
        var micros = Tempos[0].Microseconds;
        for (var i = 1; i < Tempos.Count && Tempos[i].Tick < tick; i++)
        {
            seconds += (Tempos[i].Tick - last) * micros / 1e6 / TicksPerQuarter;
            last = Tempos[i].Tick;
            micros = Tempos[i].Microseconds;
        }
        return seconds + (tick - last) * micros / 1e6 / TicksPerQuarter;
    }

    public static ScummImuseSong Parse(ReadOnlySpan<byte> part)
    {
        var p = 0;
        // The MDhd block (tag, big-endian body size, 8 bytes) comes before the MIDI file.
        while (p + 8 <= part.Length && !part.Slice(p, 4).SequenceEqual("MThd"u8))
        {
            var size = (int)BinaryPrimitives.ReadUInt32BigEndian(part[(p + 4)..]);
            if (size < 0) throw new InvalidDataException("A sound block has an invalid size.");
            p += 8 + size; // the size of MDhd counts its body only
        }
        if (p + 14 > part.Length || !part.Slice(p, 4).SequenceEqual("MThd"u8)) throw new InvalidDataException("The sound holds no MIDI file.");
        var headerLength = BinaryPrimitives.ReadInt32BigEndian(part[(p + 4)..]);
        var division = BinaryPrimitives.ReadUInt16BigEndian(part[(p + 12)..]);
        if (division == 0 || (division & 0x8000) != 0) throw new InvalidDataException("The MIDI file's timing is not in ticks per quarter note.");
        p += 8 + headerLength;
        if (p + 8 > part.Length || !part.Slice(p, 4).SequenceEqual("MTrk"u8)) throw new InvalidDataException("The MIDI file has no track.");
        var end = Math.Min(part.Length, p + 8 + BinaryPrimitives.ReadInt32BigEndian(part[(p + 4)..]));
        p += 8;

        var events = new List<ScummImuseEvent>();
        var tempos = new List<(long, int)> { (0, 500000) };
        long tick = 0;
        byte running = 0;
        while (p < end)
        {
            tick += ReadVlq(part, ref p, end);
            if (p >= end) break;
            var status = part[p];
            if (status == 0xFF)
            {
                var type = part[p + 1];
                p += 2;
                var length = ReadVlq(part, ref p, end);
                if (type == 0x51 && length == 3 && p + 3 <= end)
                {
                    var micros = part[p] << 16 | part[p + 1] << 8 | part[p + 2];
                    if (tick == 0) tempos[0] = (0, micros); else tempos.Add((tick, micros));
                }
                p += length;
                if (type == 0x2F) break;
            }
            else if (status is 0xF0 or 0xF7)
            {
                p++;
                var length = ReadVlq(part, ref p, end);
                if (p + length > end) break; // a track may end inside its last message
                var data = part.Slice(p, length);
                if (data.Length > 0 && data[^1] == 0xF7) data = data[..^1];
                if (status == 0xF0) events.Add(new ScummImuseEvent(tick, 0xF0, data.ToArray()));
                p += length;
            }
            else
            {
                if (status >= 0x80) { running = status; p++; }
                else if (running == 0) throw new InvalidDataException("A MIDI event has no status.");
                var count = (running >> 4) is 0xC or 0xD ? 1 : 2;
                if (p + count > end) break;
                events.Add(new ScummImuseEvent(tick, running, part.Slice(p, count).ToArray()));
                p += count;
            }
        }
        return new ScummImuseSong(division, events, tempos, tick);
    }

    private static int ReadVlq(ReadOnlySpan<byte> data, ref int p, int end)
    {
        var value = 0;
        for (var i = 0; i < 4 && p < end; i++)
        {
            var b = data[p++];
            value = value << 7 | (b & 0x7F);
            if ((b & 0x80) == 0) break;
        }
        return value;
    }

    /// <summary>A short description of the sound's iMUSE system exclusives, for the sound's detail text: how many of each command it holds.</summary>
    public string DescribeSysex()
    {
        var counts = new SortedDictionary<int, int>();
        foreach (var e in Events.Where(e => e.IsSysex && e.Data.Length > 1 && e.Data[0] == 0x7D))
            counts[e.Data[1]] = counts.GetValueOrDefault(e.Data[1]) + 1;
        var text = new StringBuilder();
        foreach (var (command, count) in counts) text.Append($"{(text.Length > 0 ? ", " : "")}{command}×{count}");
        return text.ToString();
    }
}
