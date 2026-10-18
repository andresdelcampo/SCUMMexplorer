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

    /// <summary>
    /// For a song whose unrolling stopped at a jump back (<see cref="EndedOnLoop"/>): the second of the rendered song from which the game would play on after
    /// <see cref="EndSeconds"/>, so a player that goes back there when it reaches <see cref="EndSeconds"/> keeps the song looping as the game does until a
    /// script stops it (S6 Monkey2, 17 October 2026). It is the previous jump back when that one went to the same target (the pass after it is the one that
    /// follows), else the first time the position played the target. Null for a song that ends. An estimate: hooks and loops that scripts arm are not followed.
    /// </summary>
    public double? RestartSeconds { get; private init; }

    private ScummImuseTimeline(List<ScummImuseTimelineItem> items, double end, int jumpsBack, bool endedOnLoop, List<double> jumps)
    {
        Items = items;
        EndSeconds = end;
        JumpsBack = jumpsBack;
        EndedOnLoop = endedOnLoop;
        JumpBackSeconds = jumps;
    }

    public static ScummImuseTimeline Build(ScummImuseSong song, double minLoopSeconds = MinLoopSeconds, double maxSeconds = MaxSeconds) =>
        Build(song, minLoopSeconds, maxSeconds, 0, 0);

    /// <summary>
    /// The song with a jump hook a script armed while it played (S6 Monkey2, 17 October 2026; ScummVM master read 17 October 2026: <c>soundKludge</c>
    /// [0x10C, sound, 0, value] is <c>Player::setHook</c> class 0, <c>HookDatas::set</c> puts the value in <c>_jump[0]</c>; the sysex 0x7D 48 jumps when its id
    /// is 0 or equals <c>_jump[0]</c>, and an id below 0x80 is used up when taken, <c>Player::maybe_jump</c>). From <paramref name="armedAtSeconds"/> of playing
    /// time on, the hook markers with id <paramref name="hookValue"/> are taken too; the unrolling limits count from that second.
    /// </summary>
    /// <remarks>
    /// A taken hook that names another track of the file (<paramref name="track"/> gives it) ends the unrolling there: the song goes on in that track from the
    /// target and plays it on its default path; the timeline then holds the events up to the jump, <see cref="SwitchedToTrack"/> names the track, and
    /// <see cref="EndSeconds"/> adds that track's rest (a looping track makes the whole song loop: <see cref="EndedOnLoop"/>).
    /// </remarks>
    public static ScummImuseTimeline BuildWithHook(ScummImuseSong song, int hookValue, double armedAtSeconds, Func<int, ScummImuseSong?>? track = null) =>
        Build(song, armedAtSeconds + MinLoopSeconds, armedAtSeconds + MaxSeconds, hookValue, armedAtSeconds, track);

    /// <summary>The track a taken hook switched to (<see cref="BuildWithHook"/>), or null.</summary>
    public int? SwitchedToTrack { get; private init; }

    private static ScummImuseTimeline Build(ScummImuseSong song, double minLoopSeconds, double maxSeconds, int hookValue, double armedAtSeconds,
        Func<int, ScummImuseSong?>? trackOf = null)
    {
        var events = song.Events;
        var items = new List<ScummImuseTimelineItem>(events.Count);
        var jumps = new List<double>();
        var targets = new List<long>();
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
                targets.Add(target);
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
                    if (buf.Length < 7) break;
                    if (buf[0] != 0)
                    {
                        // A hook with an id jumps only when a script armed that id (BuildWithHook); one below 0x80 is used up by the jump.
                        if (hookValue == 0 || buf[0] != hookValue || Playing(e.Tick) < armedAtSeconds) break;
                        if (hookValue < 0x80) hookValue = 0;
                        var toTrack = BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(1));
                        if (toTrack != 0 && trackOf?.Invoke(toTrack) is { } other)
                        {
                            // Player::jump: setTrack, then jumpToTick in that track; the rest of the song is that track from the target.
                            var at = Playing(e.Tick);
                            var otherTarget = (long)(BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(3)) - 1) * TicksPerBeat + BinaryPrimitives.ReadUInt16BigEndian(buf.AsSpan(5));
                            var rest = Build(other);
                            var tail = Math.Max(0, rest.EndSeconds - other.SecondsAt(Math.Min(otherTarget, other.Ticks)));
                            jumps.Add(at);
                            return new ScummImuseTimeline(items, at + tail, jumpsBack, rest.EndedOnLoop, jumps) { SwitchedToTrack = toTrack };
                        }
                    }
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
        double? restart = null;
        if (endedOnLoop)
        {
            var target = targets[^1];
            if (jumps.Count >= 2 && targets[^2] == target) restart = jumps[^2];
            else if (items.FindIndex(item => !item.Synthetic && item.Event.Tick >= target) is var at and >= 0 && items[at] is var first)
                restart = Math.Max(0, first.Seconds - (song.SecondsAt(first.Event.Tick) - song.SecondsAt(target)));
            else restart = 0;
        }
        return new ScummImuseTimeline(items, end, jumpsBack, endedOnLoop, jumps) { RestartSeconds = restart };
    }
}
