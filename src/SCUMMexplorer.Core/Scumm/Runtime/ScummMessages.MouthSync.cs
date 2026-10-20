namespace SCUMMexplorer.Core.Scumm;

/// <summary>A change of the talking actor's mouth that a spoken line's mouth-sync times drove: the talk-start frame (open) or the talk-stop frame (closed).</summary>
public readonly record struct ScummMouthSyncEvent(long Jiffies, int Actor, bool Open);

/// <summary>
/// S10 talkie speech (19 October 2026): mouth sync. While a talkie's spoken line plays, ScummVM moves the talking actor's mouth from the sample's own
/// mouth-sync times instead of holding the talk-start frame for the whole text (sound.cpp on GitHub master, read 19 October 2026: <c>startTalkSound</c> reads
/// the <c>VCTL</c> words into <c>_mouthSyncTimes</c>, resets the speech timer and sets <c>_mouthSyncMode</c>; <c>speechTimerHandler</c> counts the timer
/// 60 times a second (a quarter of the 240 Hz PIT); <c>processSfxQueues</c> runs the talk-stop frame when the line has finished or the mouth goes off while
/// open, and the talk-start frame when it goes on while closed; <c>isMouthSyncOff</c> toggles at each time passed). Only for an actor below 0x80 with talk
/// animations on, and only after the boot (a line whose length is unknown keeps the plain talk animation, as before).
/// </summary>
public sealed partial class ScummMessageTable
{
    /// <summary>The spoken line's mouth-sync times from the speech bank (offset, VCTL size); null when they cannot be read. The session sets it after the boot.</summary>
    public Func<int, int, IReadOnlyList<int>?>? SpeechMouthSync { get; set; }

    private IReadOnlyList<int>? _mouthSync;
    private long _mouthSyncStart;
    private bool _mouthSyncMode;
    private readonly List<ScummMouthSyncEvent> _mouthSyncEvents = [];

    /// <summary>Every mouth change the mouth-sync times drove, in order (for the harnesses and tests).</summary>
    public IReadOnlyList<ScummMouthSyncEvent> MouthSyncEvents => _mouthSyncEvents;

    /// <summary>The mouth-sync times of the line now driving the mouth; null when none does (no talkie line, its length unknown, or the boot).</summary>
    public IReadOnlyList<int>? MouthSyncTimes => _mouthSync;

    private void StartMouthSync(int offset, int length, int? duration)
    {
        _mouthSync = duration is null || SpeechMouthSync is null ? null : SpeechMouthSync(offset, length);
        _mouthSyncStart = Jiffies;
        _mouthSyncMode = true;
    }

    private void EndMouthSync() => _mouthSync = null;

    /// <summary>ScummVM <c>isMouthSyncOff</c> for versions up to 6 (no delay before v6): true when the mouth is closed at <paramref name="position"/> ticks.</summary>
    public static bool IsMouthSyncOff(IReadOnlyList<int> times, long position)
    {
        var off = true;
        foreach (var time in times)
        {
            off = !off;
            if (position <= time) return off;
        }
        // The 0xFFFF end: one more toggle.
        return !off;
    }

    /// <summary>ScummVM <c>processSfxQueues</c>, the talking actor's part: after <c>displayDialog</c> each frame.</summary>
    private void UpdateMouthSync()
    {
        if (_mouthSync is not { } times || !_useTalkAnims || TalkingActor is <= 0 or >= 0x80) return;
        var finished = !SpeechPlaying;
        var off = IsMouthSyncOff(times, Jiffies - _mouthSyncStart);
        if (finished || off && _mouthSyncMode)
        {
            if (_mouthSyncMode) _mouthSyncEvents.Add(new ScummMouthSyncEvent(Jiffies, TalkingActor, false));
            TalkAnimation?.Invoke(TalkingActor, false);
            _mouthSyncMode = false;
        }
        else if (!off && !_mouthSyncMode)
        {
            _mouthSyncEvents.Add(new ScummMouthSyncEvent(Jiffies, TalkingActor, true));
            TalkAnimation?.Invoke(TalkingActor, true);
            _mouthSyncMode = true;
        }
    }
}
