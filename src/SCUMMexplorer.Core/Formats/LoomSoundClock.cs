using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Formats;

public enum LoomSoundLifetimeKind { Finite, Looping, Unknown }

/// <summary>472 Hz AdLib driver callbacks until native completion, never WAV duration.</summary>
public sealed record LoomSoundLifetime(LoomSoundLifetimeKind Kind, int? Callbacks, bool Music, string? Reason = null, long[]? MarkerTicks = null);

/// <summary>
/// Resource clocks for independent native AdLib voices. Output mode/mute do not alter game state.
/// Hardware channel contention remains outside this explorer model.
/// </summary>
public sealed class LoomSoundClock(Func<int, LoomSoundLifetime> resolve)
{
    private sealed record Voice(LoomSoundLifetime Lifetime, long Started, bool Estimated);
    private readonly Dictionary<int, Voice> _voices = [];
    private readonly HashSet<int> _estimatedStops = [];
    private long _jiffies;

    public void Start(int id, bool estimated)
    {
        _voices[id] = new(resolve(id), _jiffies, estimated);
        _estimatedStops.Remove(id);
    }

    public void Stop(int id, bool estimated = false)
    {
        _voices.Remove(id);
        if (estimated) _estimatedStops.Add(id); else _estimatedStops.Remove(id);
    }

    public void StopAll()
    {
        _voices.Clear();
        _estimatedStops.Clear();
    }

    public IReadOnlyList<int> Advance(int jiffies)
    {
        if (jiffies < 0) throw new ArgumentOutOfRangeException(nameof(jiffies));
        _jiffies += jiffies;
        var ended = _voices.Where(pair => pair.Value.Lifetime is { Kind: LoomSoundLifetimeKind.Finite, Callbacks: int callbacks } &&
            (_jiffies - pair.Value.Started) * 472 >= callbacks * 60L).Select(pair => pair.Key).ToArray();
        foreach (var id in ended) Stop(id, _voices[id].Estimated);
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
        {
            if (voice.Lifetime.MarkerTicks is not { } markers) continue;
            var ticks = (_jiffies - voice.Started) * 4;
            int low = 0, high = markers.Length;
            while (low < high)
            {
                var mid = (low + high) / 2;
                if (markers[mid] <= ticks) low = mid + 1; else high = mid;
            }
            best = Math.Max(best, low);
        }
        return best;
    }

    public (bool Running, string? Estimate) Status(int id)
    {
        if (id == 0) return (false, null);
        if (!_voices.TryGetValue(id, out var voice))
            return (false, _estimatedStops.Contains(id) ? "the stop followed a seeded route" : null);
        return (true, voice.Estimated ? "the start followed a seeded route" :
            voice.Lifetime.Kind == LoomSoundLifetimeKind.Unknown ? voice.Lifetime.Reason ?? "native completion is unresolved" : null);
    }
}
