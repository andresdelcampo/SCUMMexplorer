using System.Buffers.Binary;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>One event of a played iMUSE song at the second it sounds. <see cref="Synthetic"/> events are made by a jump (notes off and sustain off, as ScummVM's <c>allNotesOff</c> and <c>turn_off_pedals</c>), not read from the track.</summary>
public readonly record struct ScummImuseTimelineItem(double Seconds, ScummImuseEvent Event, bool Synthetic = false);

/// <summary>
/// A standalone iMUSE sound played the way ScummVM plays it with no script around: the song's events in playing order, with its jumps taken. Three things move the
/// position (ScummVM <c>imuse_player.cpp</c>, <c>sysex_scumm.cpp</c>, <c>audio/midiparser.cpp</c>, read 14 October 2026):
/// <list type="bullet">
/// <item>the hook 0x7D 48 with hook id 0 (<c>maybe_jump</c> with <c>cmd == 0</c>) always jumps to (beat - 1) * 480 + tick; hooks with another id wait for the script that sets that id and are never taken here;</item>
/// <item>the loop 0x7D 80 (<c>setLoop</c>: count, to-beat, to-tick, from-beat, from-tick; refused unless to-beat + 1 &lt; from-beat; to-beat 0 means 1) makes the player, once the position
/// reaches from-beat/from-tick, jump to to-beat/to-tick and count one down; a count of 1000 is "for ever"; 0x7D 81 clears it;</item>
/// <item>every jump (<c>jumpToTick</c>) skips the events before its target without sending them (controllers and set-ups are not replayed), then plays the events from the target tick on,
/// and first stops the sounding notes (<c>allNotesOff</c>) and sustain pedals.</item>
/// </list>
/// A song that jumps back is unrolled until it has looped once and played <see cref="MinLoopSeconds"/>, and never beyond <see cref="MaxSeconds"/>; the timeline then ends at a jump
/// (the point where the game would play the loop again) and <see cref="Loops"/> is true.
/// </summary>
public sealed class ScummImuseTimeline
{
    public const double MinLoopSeconds = 180;
    public const double MaxSeconds = 420;
    private const int TicksPerBeat = 480;

    public IReadOnlyList<ScummImuseTimelineItem> Items { get; }
    /// <summary>The second at which the last pass ends: the song's end, or the jump at which a looping song stops being unrolled.</summary>
    public double EndSeconds { get; }
    public bool Loops => JumpsBack > 0 || EndedOnLoop;
    /// <summary>How many times the position went back (a hook jump or a loop that went to an earlier or the same tick).</summary>
    public int JumpsBack { get; }
    /// <summary>True when the unrolling stopped at a jump back, false when the song ended or hit <see cref="MaxSeconds"/>.</summary>
    public bool EndedOnLoop { get; }
    /// <summary>The seconds, in playing time, at which each jump back happened; the pass that follows begins at its target.</summary>
    public IReadOnlyList<double> JumpBackSeconds { get; }

    private ScummImuseTimeline(List<ScummImuseTimelineItem> items, double end, int jumpsBack, bool endedOnLoop, List<double> jumps)
    {
        Items = items;
        EndSeconds = end;
        JumpsBack = jumpsBack;
        EndedOnLoop = endedOnLoop;
        JumpBackSeconds = jumps;
    }

    public static ScummImuseTimeline Build(ScummImuseSong song, double minLoopSeconds = MinLoopSeconds, double maxSeconds = MaxSeconds)
    {
        var events = song.Events;
        var items = new List<ScummImuseTimelineItem>(events.Count);
        var jumps = new List<double>();
        var index = 0;
        long position = 0;     // the song tick the player is at
        double playBase = 0;   // playing seconds at song tick `baseTick`
        long baseTick = 0;
        var jumpsBack = 0;
        var endedOnLoop = false;
        // The loop of 0x7D 80.
        var loopCounter = 0;
        long loopTo = 0, loopFrom = 0;

        double Playing(long tick) => playBase + song.SecondsAt(tick) - song.SecondsAt(baseTick);

        // Returns true when the unrolling should stop at this jump.
        bool Jump(long atTick, long target)
        {
            var seconds = Playing(atTick);
            var back = target <= atTick;
            for (var channel = 0; channel < 16; channel++)
            {
                items.Add(new ScummImuseTimelineItem(seconds, new ScummImuseEvent(atTick, (byte)(0xB0 | channel), [64, 0]), true));
                items.Add(new ScummImuseTimelineItem(seconds, new ScummImuseEvent(atTick, (byte)(0xB0 | channel), [123, 0]), true));
            }
            if (back)
            {
                jumpsBack++;
                jumps.Add(seconds);
                if (seconds >= minLoopSeconds) { endedOnLoop = true; return true; }
            }
            playBase = seconds;
            baseTick = target;
            position = target;
            while (index > 0 && events[index - 1].Tick >= target) index--;
            while (index < events.Count && events[index].Tick < target) index++;
            return false;
        }

        var guard = 0;
        while (guard++ < 2_000_000)
        {
            var nextTick = index < events.Count ? events[index].Tick : song.Ticks;
            // The loop's jump happens once the position reaches its from-point; events at the from-tick itself still play.
            if (loopCounter > 0 && (loopFrom < nextTick || index >= events.Count && loopFrom <= song.Ticks))
            {
                var at = Math.Max(loopFrom, position);
                loopCounter--;
                if (Jump(at, loopTo)) break;
                continue;
            }
            if (index >= events.Count) break;
            var e = events[index];
            if (Playing(e.Tick) > maxSeconds) break;
            index++;
            position = e.Tick;
            items.Add(new ScummImuseTimelineItem(Playing(e.Tick), e));
            if (!e.IsSysex || e.Data.Length < 2 || e.Data[0] != 0x7D) continue;
            switch (e.Data[1])
            {
                case 48 when e.Data.Length > 3:
                {
                    var buf = ScummImuseSong.Decode(e.Data.AsSpan(3), 9);
                    if (buf.Length < 7 || buf[0] != 0) break; // only the always-taken hook
                    var target = (long)(BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(3)) - 1) * TicksPerBeat + BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(5));
                    if (target > song.Ticks) break; // jumpToTick fails past the end of the track
                    if (Jump(e.Tick, target)) { index = events.Count; }
                    break;
                }
                case 80 when e.Data.Length > 3:
                {
                    var buf = ScummImuseSong.Decode(e.Data.AsSpan(3), 10);
                    if (buf.Length < 10) break;
                    int count = BinaryPrimitives.ReadUInt16BigEndian(buf), toBeat = BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(2)), toTick = BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(4));
                    int fromBeat = BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(6)), fromTick = BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(8));
                    if (toBeat + 1 >= fromBeat) break; // setLoop refuses
                    if (toBeat == 0) toBeat = 1;
                    loopTo = (long)(toBeat - 1) * TicksPerBeat + toTick;
                    loopFrom = (long)(fromBeat - 1) * TicksPerBeat + fromTick;
                    loopCounter = count;
                    break;
                }
                case 81: loopCounter = 0; break;
            }
            if (endedOnLoop) break;
        }
        // The jump that stopped the unrolling left its cut events in the list; the timeline ends there.
        double end;
        if (endedOnLoop) end = jumps[^1];
        else if (index >= events.Count && guard < 2_000_000) end = Playing(Math.Max(song.Ticks, position));
        else end = maxSeconds;
        return new ScummImuseTimeline(items, end, jumpsBack, endedOnLoop, jumps);
    }
}
