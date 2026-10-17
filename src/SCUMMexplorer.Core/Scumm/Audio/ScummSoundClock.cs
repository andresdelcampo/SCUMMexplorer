using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

public enum ScummSoundLifetimeKind { Finite, Looping, Unknown }

/// <summary>A sound-script timer value at a 472 Hz clock position (v2's 236 Hz tick is two callbacks).</summary>
public sealed record ScummSoundTimerPoint(int Callback, ushort Value);

/// <summary>472 Hz AdLib driver callbacks until native completion, never WAV duration.</summary>
public sealed record ScummSoundLifetime(ScummSoundLifetimeKind Kind, int? Callbacks, bool Music, string? Reason = null, long[]? MarkerTicks = null,
    bool KeepsMusicTimer = false, ScummSoundTimerPoint[]? TimerValues = null, int? LoopStartCallback = null, int? LoopEndCallback = null);

/// <summary>
/// Resource clocks for independent native AdLib voices. Output mode/mute do not alter game state.
/// Hardware channel contention remains outside this explorer model.
/// </summary>
public sealed class ScummSoundClock(Func<int, ScummSoundLifetime> resolve)
{
    private sealed record Voice(ScummSoundLifetime Lifetime, long Started, bool Estimated);
    private readonly Dictionary<int, Voice> _voices = [];
    private readonly HashSet<int> _estimatedStops = [];
    private long _jiffies;

    // The music timer a song of the AdLib player left behind: that player's timer keeps its last value after the song stops, until the next song starts.
    private int _heldTimer;
    private bool _heldTimerIsV2;

    public void Start(int id, bool estimated)
    {
        var lifetime = resolve(id);
        if (lifetime.KeepsMusicTimer)
        {
            _heldTimer = 0;
            _heldTimerIsV2 = lifetime.TimerValues is not null;
        }
        _voices[id] = new(lifetime, _jiffies, estimated);
        _estimatedStops.Remove(id);
    }

    /// <summary>The sound is music for the AdLib driver (its resource's third byte is 0x80, as <c>Player_AD</c> and iMuse's <c>convertADResource</c> test).</summary>
    public bool IsMusic(int id) => resolve(id).Music;

    /// <summary>The sounds the clock has playing now, in no particular order.</summary>
    public IReadOnlyList<int> Playing => _voices.Keys.ToArray();

    public void Stop(int id, bool estimated = false) => StopVoice(id, estimated, naturalEnd: false);

    private void StopVoice(int id, bool estimated, bool naturalEnd)
    {
        if (_voices.TryGetValue(id, out var voice) && voice.Lifetime.KeepsMusicTimer)
            // Player_V2 stopSound clears every channel word; natural completion preserves the channel timer.
            _heldTimer = voice.Lifetime.TimerValues is not null && !naturalEnd ? 0 : MarkersPassed(voice);
        _voices.Remove(id);
        if (estimated) _estimatedStops.Add(id); else _estimatedStops.Remove(id);
    }

    public void StopAll()
    {
        if (_heldTimerIsV2) _heldTimer = 0;
        foreach (var voice in _voices.Values)
            if (voice.Lifetime.KeepsMusicTimer && voice.Lifetime.TimerValues is null) _heldTimer = Math.Max(_heldTimer, MarkersPassed(voice));
        _voices.Clear();
        _estimatedStops.Clear();
    }

    public IReadOnlyList<int> Advance(int jiffies)
    {
        if (jiffies < 0) throw new ArgumentOutOfRangeException(nameof(jiffies));
        _jiffies += jiffies;
        var ended = _voices.Where(pair => pair.Value.Lifetime is { Kind: ScummSoundLifetimeKind.Finite, Callbacks: int callbacks } &&
            (_jiffies - pair.Value.Started) * 472 >= callbacks * 60L).Select(pair => pair.Key).ToArray();
        // A song that ends within this advance has counted every marker it has by then.
        foreach (var id in ended) StopVoice(id, _voices[id].Estimated, naturalEnd: true);
        return ended;
    }

    /// <summary>
    /// The game's music timer (<c>VAR_MUSIC_TIMER</c>, <c>var[0x00E]</c>): ScummVM's iMuse reports the markers the playing song has passed
    /// (<c>MidiParser_RO::getTick</c> counts them), the largest over the playing songs, and 0 when none plays. Only songs that carry
    /// markers (the Roland ones) have it; the clock runs at 60 jiffies a second and a Roland tick lasts 1/240 s.
    /// </summary>
    public int MusicTimer()
    {
        var best = 0;
        foreach (var voice in _voices.Values)
            best = Math.Max(best, MarkersPassed(voice));
        return best > 0 || _voices.Values.Any(voice => voice.Lifetime.KeepsMusicTimer) ? best : _heldTimer;
    }

    private int MarkersPassed(Voice voice)
    {
        if (voice.Lifetime.TimerValues is { } values)
        {
            var callback = (_jiffies - voice.Started) * 472 / 60;
            if (voice.Lifetime.LoopStartCallback is int start && voice.Lifetime.LoopEndCallback is int end && end > start && callback >= end)
                callback = start + (callback - start) % (end - start);
            int lo = 0, hi = values.Length;
            while (lo < hi)
            {
                var mid = (lo + hi) / 2;
                if (values[mid].Callback <= callback) lo = mid + 1; else hi = mid;
            }
            return lo == 0 ? 0 : values[lo - 1].Value;
        }
        if (voice.Lifetime.MarkerTicks is not { } markers) return 0;
        // A song that has run out has passed every marker it has (its clock is bounded by its own length).
        var elapsed = _jiffies - voice.Started;
        if (voice.Lifetime is { Kind: ScummSoundLifetimeKind.Finite, Callbacks: int callbacks })
            elapsed = Math.Min(elapsed, (callbacks * 60L + 471) / 472);
        var ticks = elapsed * 4;
        int low = 0, high = markers.Length;
        while (low < high)
        {
            var mid = (low + high) / 2;
            if (markers[mid] <= ticks) low = mid + 1; else high = mid;
        }
        return low;
    }

    public (bool Running, string? Estimate) Status(int id)
    {
        if (id == 0) return (false, null);
        if (!_voices.TryGetValue(id, out var voice))
            return (false, _estimatedStops.Contains(id) ? "the stop followed a seeded route" : null);
        return (true, voice.Estimated ? "the start followed a seeded route" :
            voice.Lifetime.Kind == ScummSoundLifetimeKind.Unknown ? voice.Lifetime.Reason ?? "native completion is unresolved"
            // A finite length that carries a reason is an estimate (the v5 iMUSE default path, S4b Indy4); the others are the resource's own length.
            : voice.Lifetime.Kind == ScummSoundLifetimeKind.Finite ? voice.Lifetime.Reason : null);
    }
}
