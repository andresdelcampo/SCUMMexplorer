namespace SCUMMexplorer.Core.Formats;

/// <summary>When a voice line of a batch starts, in seconds from the batch's first line, and the silence before it.</summary>
public readonly record struct LoomVoiceStart(LoomVoiceCue Cue, double StartSeconds, double GapSeconds);

/// <summary>
/// The timing of the voice lines one click issues. A click runs its whole interaction at once, so the lines arrive together; the game
/// issued them as its talk delays ran out, which the session clocks in jiffies (1/60 s). A line starts at the game time its print ran,
/// or when the line before it has finished (the disc plays one cue at a time and a line is not cut by an estimate), whichever is later.
/// The gap before a line is therefore the session's own model of the text delay, not a measurement of the game.
/// </summary>
public static class LoomVoiceSchedule
{
    public static IReadOnlyList<LoomVoiceStart> Plan(IReadOnlyList<LoomVoiceCue> cues, IReadOnlyList<long> jiffies)
    {
        var plan = new List<LoomVoiceStart>();
        long? origin = null;
        var previousEnd = 0.0;
        for (var i = 0; i < cues.Count && i < jiffies.Count; i++)
        {
            if (cues[i].IsStop) continue;
            origin ??= jiffies[i];
            var wanted = (jiffies[i] - origin.Value) / 60.0;
            var start = Math.Max(wanted, previousEnd);
            plan.Add(new LoomVoiceStart(cues[i], start, plan.Count == 0 ? 0 : start - previousEnd));
            previousEnd = start + cues[i].TrackFrameCount / 75.0;
        }
        return plan;
    }
}
