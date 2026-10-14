namespace SCUMMexplorer.Core.Loom;

/// <param name="StartedBeforeEntry">
/// The cue began before the current room was last entered; <see cref="LoomSoundCueResolver.ResolveForRoom"/>
/// keeps such a cue only when the room's own code started it.
/// </param>
public sealed record LoomReachedSoundCue(ScummScriptEvent? Start, bool HasUnknownSoundTarget,
    int OtherActiveMusicCount, bool StartedBeforeEntry = false);

/// <summary>Folds reached sound commands in route order without treating static paths as executed.</summary>
public static class LoomSoundCueResolver
{
    public static LoomReachedSoundCue Resolve(IEnumerable<ScummScriptEvent> events,
        Func<int, bool> isMusicTrack)
    {
        ArgumentNullException.ThrowIfNull(events);
        return Fold(events.Select((item, index) => new ScummSessionSoundEvent(item, index, null, null)),
            _ => true, isMusicTrack, 0);
    }

    /// <summary>
    /// The cue for a room view: music started since the room was entered, or still
    /// active after the room's own code started it on an earlier visit. A cue that
    /// only carries over from another room (such as the intro's) is left out, since
    /// the session does not simulate when a track ends.
    /// </summary>
    public static LoomReachedSoundCue ResolveForRoom(IEnumerable<ScummSessionSoundEvent> events, int roomId,
        int entrySequence, Func<int, bool> isMusicTrack)
    {
        ArgumentNullException.ThrowIfNull(events);
        return Fold(events, item => item.Sequence >= entrySequence || item.OriginRoomId == roomId,
            isMusicTrack, entrySequence);
    }

    private static LoomReachedSoundCue Fold(IEnumerable<ScummSessionSoundEvent> events,
        Func<ScummSessionSoundEvent, bool> isRelevant, Func<int, bool> isMusicTrack, int entrySequence)
    {
        ArgumentNullException.ThrowIfNull(isMusicTrack);
        var active = new List<ScummSessionSoundEvent>();
        // Cues an unresolved stop may have ended.
        var possiblyStopped = new HashSet<ScummSessionSoundEvent>(ReferenceEqualityComparer.Instance);
        var unknownStart = false;
        foreach (var item in events.Where(item => item.Event.Certainty == ScummScriptEventCertainty.Reached))
        {
            switch (item.Event.Kind)
            {
                case ScummScriptEventKind.MusicStarted:
                case ScummScriptEventKind.SoundStarted:
                    if (item.Event.TargetId is not int startedId)
                    {
                        if (isRelevant(item)) unknownStart = true;
                        break;
                    }
                    active.RemoveAll(previous => previous.Event.TargetId == startedId);
                    active.Add(item);
                    break;
                case ScummScriptEventKind.SoundStopped:
                    if (item.Event.TargetId is not int stoppedId)
                    {
                        possiblyStopped.UnionWith(active);
                        break;
                    }
                    active.RemoveAll(previous => previous.Event.TargetId == stoppedId);
                    break;
                case ScummScriptEventKind.MusicStopped:
                    active.Clear();
                    possiblyStopped.Clear();
                    unknownStart = false;
                    break;
            }
        }
        var music = active.Where(item => isRelevant(item) && item.Event.TargetId is int id && isMusicTrack(id))
            .ToArray();
        var unknown = unknownStart || music.Any(possiblyStopped.Contains);
        var start = unknown ? null : music.LastOrDefault();
        return new LoomReachedSoundCue(start?.Event, unknown, Math.Max(0, music.Length - 1),
            start is not null && start.Sequence < entrySequence);
    }
}
