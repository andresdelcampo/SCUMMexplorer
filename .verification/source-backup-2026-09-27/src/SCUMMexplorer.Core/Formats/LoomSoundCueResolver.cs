namespace SCUMMexplorer.Core.Formats;

public sealed record LoomReachedSoundCue(LoomScriptEvent? Start, bool HasUnknownSoundTarget,
    int OtherActiveMusicCount);

/// <summary>Folds reached sound commands in route order without treating static paths as executed.</summary>
public static class LoomSoundCueResolver
{
    public static LoomReachedSoundCue Resolve(IEnumerable<LoomScriptEvent> events,
        Func<int, bool> isMusicTrack)
    {
        ArgumentNullException.ThrowIfNull(events);
        ArgumentNullException.ThrowIfNull(isMusicTrack);
        var active = new List<LoomScriptEvent>();
        var unknown = false;
        foreach (var item in events.Where(item => item.Certainty == LoomScriptEventCertainty.Reached))
        {
            switch (item.Kind)
            {
                case LoomScriptEventKind.MusicStarted:
                case LoomScriptEventKind.SoundStarted:
                    if (item.TargetId is not int startedId) { unknown = true; break; }
                    active.RemoveAll(previous => previous.TargetId == startedId);
                    active.Add(item);
                    break;
                case LoomScriptEventKind.SoundStopped:
                    if (item.TargetId is not int stoppedId) { unknown = true; break; }
                    active.RemoveAll(previous => previous.TargetId == stoppedId);
                    break;
                case LoomScriptEventKind.MusicStopped:
                    active.Clear();
                    unknown = false;
                    break;
            }
        }
        var music = active.Where(item => item.TargetId is int id && isMusicTrack(id)).ToArray();
        return new LoomReachedSoundCue(unknown ? null : music.LastOrDefault(), unknown,
            Math.Max(0, music.Length - 1));
    }
}
