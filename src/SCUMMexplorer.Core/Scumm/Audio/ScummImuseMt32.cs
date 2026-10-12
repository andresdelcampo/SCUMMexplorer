using System.Buffers.Binary;
using Mt32emu;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// Plays the <c>ROL </c> part of a v5 sound (<see cref="ScummImuseSong"/>) on Munt's MT-32 and renders it to a stereo wave. It follows ScummVM's iMUSE player
/// for the messages that decide how a song sounds: the part set-up exclusive (0x7D 0: channel, on/off, reverb, volume, pan, transpose, pitch-bend range, program),
/// the Roland custom timbre (0x41), the controllers and program changes of the track, and the unconditional hooks (hook id 0). As in ScummVM the MT-32 driver
/// ignores a part's transpose. A song is rendered up to the first
/// jump back or loop end (<see cref="Result.Loops"/> tells the game plays it again from there); hooks that wait for a script's id are never taken here.
/// </summary>
public sealed class ScummImuseMt32
{
    private const double ReleaseSeconds = 3;
    private const double MaxSeconds = 420;
    private const int RhythmChannel = 9;

    private readonly Mt32RomSet _roms;

    public ScummImuseMt32(Mt32RomSet roms) => _roms = roms;

    public sealed record Result(byte[] Wave, bool Loops, double Seconds);

    public byte[] RenderWave(byte[] part, CancellationToken cancellationToken = default) => Render(ScummImuseSong.Parse(part), cancellationToken).Wave;

    public Result Render(ScummImuseSong song, CancellationToken cancellationToken = default)
    {
        using var synth = new Mt32Context(null!);
        synth.AnalogOutputMode = AnalogOutputMode.Accurate;
        synth.AddRom(_roms.ControlPath);
        synth.AddRom(_roms.PcmPath);
        synth.OpenSynth();
        var rate = (int)Math.Round(synth.ActualStereoOutputSampleRate);

        var plan = Plan(song);
        var endSeconds = Math.Min(MaxSeconds, song.SecondsAt(plan.EndTick) + ReleaseSeconds);
        var totalFrames = (long)Math.Ceiling(endSeconds * rate);
        var pcm = new short[totalFrames * 2];
        long frame = 0;

        void RenderUntil(long target)
        {
            target = Math.Min(target, totalFrames);
            const int chunk = 4096;
            while (frame < target)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var count = (int)Math.Min(chunk, target - frame);
                synth.Render(pcm.AsSpan((int)(frame * 2), count * 2));
                frame += count;
            }
        }

        foreach (var item in plan.Messages)
        {
            RenderUntil((long)Math.Round(song.SecondsAt(item.Tick) * rate));
            if (frame >= totalFrames) break;
            if (item.Sysex is { } sysex) synth.PlaySysex(sysex);
            else synth.PlayMessage(item.Short);
        }
        RenderUntil(totalFrames);
        return new Result(ScummMt32Audio.ToWave(pcm, rate), plan.Loops, song.SecondsAt(plan.EndTick));
    }

    private readonly record struct Message(long Tick, uint Short, byte[]? Sysex);

    private sealed record Planned(List<Message> Messages, long EndTick, bool Loops);

    private sealed class Part
    {
        public bool On;
        public bool Rhythm;
        public int Channel = -1; // the MT-32 channel this part plays on
    }

    /// <summary>Walks the song and turns it into timed messages for the MT-32.</summary>
    /// <summary>iMUSE's part volume as the driver gets it: (volume + 1) times the player's effective volume (127 at full), shifted down 7.</summary>
    private static int EffectiveVolume(int volume) => (volume + 1) * 127 >> 7;

    private static Planned Plan(ScummImuseSong song)
    {
        var parts = new Part?[16];
        var messages = new List<Message>();
        var nextChannel = 1; // MT-32 parts 1 to 8 answer on channels 2 to 9 (index 1 to 8)
        var loops = false;
        var endTick = song.Ticks;
        long shift = 0, skipUntil = 0; // an unconditional jump forward cuts the stretch it skips out of the timeline

        void Short(long tick, int status, int d1, int d2 = 0) => messages.Add(new Message(tick - shift, (uint)(status | d1 << 8 | d2 << 16), null));
        void Roland(long tick, int device, byte[] body) => messages.Add(new Message(tick - shift, 0, BuildSysex(device, body)));

        foreach (var e in song.Events)
        {
            if (loops && e.Tick - shift > endTick) break;
            if (e.Tick < skipUntil) continue;
            if (e.IsSysex)
            {
                var d = e.Data;
                if (d.Length < 2) continue;
                if (d[0] == 0x41 && d.Length > 8)
                {
                    // Roland custom timbre: 41 <part> 16 12 04 00 00 <timbre ... checksum>. It goes to the timbre of the channel's part.
                    if (parts[d[1] & 0x0F] is { Channel: >= 0, Rhythm: false } p)
                        Roland(e.Tick, p.Channel, [0x02, 0x00, 0x00, .. d.AsSpan(7, d.Length - 8)]);
                    continue;
                }
                if (d[0] != 0x7D) continue;
                switch (d[1])
                {
                    case 0: // set up a part
                    {
                        if (d.Length < 3) break;
                        var chan = d[2] & 0x0F;
                        // ScummVM's buf[1..8]: on/reverb, priority, volume, pan, transpose (bit 7: percussion), detune, bend range, program.
                        var buf = ScummImuseSong.Decode(d.AsSpan(3), 8).Prepend((byte)chan).ToArray();
                        if (buf.Length < 9) break;
                        var part = parts[chan] ??= new Part();
                        part.On = (buf[1] & 1) != 0;
                        part.Rhythm = (buf[5] & 0x80) != 0;
                        if (part.Channel < 0) part.Channel = part.Rhythm ? RhythmChannel : nextChannel < 9 ? nextChannel++ : -1;
                        if (part.Channel < 0) break;
                        if (part.Rhythm) { Short(e.Tick, 0xB0 | part.Channel, 7, EffectiveVolume(buf[3])); break; }
                        var ch = part.Channel;
                        var range = buf[7] <= 12 ? buf[7] : 12;
                        Roland(e.Tick, ch, [0x00, 0x00, 0x04, (byte)range]);
                        Short(e.Tick, 0xB0 | ch, 7, EffectiveVolume(buf[3]));
                        Short(e.Tick, 0xB0 | ch, 10, 127 - Math.Clamp((sbyte)buf[4] + 0x40, 0, 127)); // the original MT-32 driver reverses the pan
                        Short(e.Tick, 0xC0 | ch, buf[8] & 0x7F);
                        break;
                    }
                    case 1: // shut a part down
                        if (d.Length > 2 && parts[d[2] & 0x0F] is { } off) off.On = false;
                        break;
                    case 48 when d.Length > 3: // hook: jump (id 0: always taken)
                    {
                        var buf = ScummImuseSong.Decode(d.AsSpan(3), 9);
                        // buf: hook id, track, beat, tick (480 ticks to a beat, counted from beat 1); a jump back ends the pass.
                        if (buf.Length < 7 || buf[0] != 0) break;
                        var target = (long)(BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(3)) - 1) * 480 + BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(5));
                        if (target <= e.Tick) { loops = true; endTick = Math.Min(endTick, e.Tick - shift); }
                        else { shift += target - e.Tick; skipUntil = target; }
                        break;
                    }
                    case 80: // loop: the song would play on from the loop start here
                        loops = true;
                        endTick = Math.Min(endTick, e.Tick - shift);
                        break;
                }
                continue;
            }

            var part2 = parts[e.Channel];
            if (part2 is not { Channel: >= 0 } pp) continue;
            var ch2 = pp.Channel;
            switch (e.Command)
            {
                case 0x9 or 0x8:
                {
                    if (!pp.On && e.Command == 0x9) break;
                    var note = e.Data[0];
                    var velocity = e.Command == 0x8 || e.Data[1] == 0 ? 0 : e.Data[1];
                    if (velocity == 0) Short(e.Tick, 0x80 | ch2, note, 0); else Short(e.Tick, 0x90 | ch2, note, velocity);
                    break;
                }
                case 0xB: // controller: volume, pan, sustain, modulation and the effect levels go on (the part's volume is scaled as in iMUSE)
                    Short(e.Tick, 0xB0 | ch2, e.Data[0], e.Data[0] == 7 ? EffectiveVolume(e.Data[1]) : e.Data[0] == 10 ? 127 - e.Data[1] : e.Data[1]);
                    break;
                case 0xC:
                    Short(e.Tick, 0xC0 | ch2, e.Data[0]);
                    break;
                case 0xE:
                    Short(e.Tick, 0xE0 | ch2, e.Data[0], e.Data[1]);
                    break;
            }
        }
        return new Planned(messages, loops ? endTick : song.Ticks - shift, loops);
    }

    /// <summary>A Roland data-set message for the device number (here a channel) with the address and data in <paramref name="body"/> and the checksum added.</summary>
    private static byte[] BuildSysex(int device, byte[] body)
    {
        var sum = 0;
        foreach (var b in body) sum += b;
        var message = new byte[body.Length + 7];
        message[0] = 0xF0;
        message[1] = 0x41;
        message[2] = (byte)device;
        message[3] = 0x16;
        message[4] = 0x12;
        body.CopyTo(message, 5);
        message[^2] = (byte)(-sum & 0x7F);
        message[^1] = 0xF7;
        return message;
    }
}
