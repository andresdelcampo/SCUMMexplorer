using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

public sealed class LoomSoundCueTests
{
    [Fact]
    public void ReachedStartSoundResolvesKnownScriptLocal()
    {
        var script = new LoomScriptDisassembly(39, 1, 0, 3, 3,
            [new LoomScriptInstruction(0, 0x9C, "startSound", "local[0x000]", "9C 00"),
                new LoomScriptInstruction(2, 0, "stopObjectCode", "", "00")],
            null, null, [], []);
        var run = new LoomBoundedScriptExecutor().Execute(script,
            new Dictionary<int, byte>(), [],
            initialLocals: new Dictionary<string, ushort> { ["local[0x000]"] = 31 });

        Assert.Equal(31, Assert.Single(run.Events).TargetId);
    }

    [Fact]
    public void ReachedStopRemovesInheritedCue()
    {
        var start = Event(LoomScriptEventKind.SoundStarted, 31);
        var effect = Event(LoomScriptEventKind.SoundStarted, 3);
        var stop = Event(LoomScriptEventKind.SoundStopped, 31);

        Assert.Equal(31, LoomSoundCueResolver.Resolve([start, effect], id => id == 31).Start?.TargetId);
        Assert.Null(LoomSoundCueResolver.Resolve([start, effect, stop], id => id == 31).Start);
        Assert.Null(LoomSoundCueResolver.Resolve([start, Event(LoomScriptEventKind.MusicStopped, null)],
            id => id == 31).Start);
    }

    [Fact]
    public void UnknownStopPreventsClaimingCurrentCue()
    {
        var resolved = LoomSoundCueResolver.Resolve(
            [Event(LoomScriptEventKind.SoundStarted, 31), Event(LoomScriptEventKind.SoundStopped, null)],
            id => id == 31);

        Assert.Null(resolved.Start);
        Assert.True(resolved.HasUnknownSoundTarget);
    }

    [Fact]
    public void RoomCueKeepsOnlyCuesStartedSinceEntryOrByTheRoomItself()
    {
        // Sound 25 started in Room 02 by a global chain (the intro); sound 30 by Room 16's code.
        var intro = new LoomSessionSoundEvent(Event(LoomScriptEventKind.SoundStarted, 25), 0, 2, null);
        var room16 = new LoomSessionSoundEvent(Event(LoomScriptEventKind.SoundStarted, 30), 5, 16, 16);

        Assert.Null(LoomSoundCueResolver.ResolveForRoom([intro], 2, 3, _ => true).Start);
        Assert.Equal(25, LoomSoundCueResolver.ResolveForRoom([intro], 2, 0, _ => true).Start?.TargetId);

        var sinceEntry = LoomSoundCueResolver.ResolveForRoom([intro, room16], 16, 4, _ => true);
        Assert.Equal(30, sinceEntry.Start?.TargetId);
        Assert.False(sinceEntry.StartedBeforeEntry);
        Assert.Equal(0, sinceEntry.OtherActiveMusicCount);

        // Back in Room 16 later: its own cue still counts, as started on an earlier visit.
        var revisit = LoomSoundCueResolver.ResolveForRoom([intro, room16], 16, 9, _ => true);
        Assert.Equal(30, revisit.Start?.TargetId);
        Assert.True(revisit.StartedBeforeEntry);
        Assert.Null(LoomSoundCueResolver.ResolveForRoom([intro, room16], 17, 9, _ => true).Start);

        // An unresolved stop of a cue that is not shown does not make the room's cue uncertain.
        var unknownStop = new LoomSessionSoundEvent(Event(LoomScriptEventKind.SoundStopped, null), 1, 2, null);
        Assert.False(LoomSoundCueResolver.ResolveForRoom([intro, unknownStop, room16], 16, 4, _ => true)
            .HasUnknownSoundTarget);
        Assert.True(LoomSoundCueResolver.ResolveForRoom([intro, room16, unknownStop with { Sequence = 6 }], 16, 9,
            _ => true).HasUnknownSoundTarget);
    }

    private static readonly string Folder =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games/loom"));

    [Fact]
    public void IntroSoundIsNotRoomMusicButRoomSixteensOwnCueIs()
    {
        if (!File.Exists(Path.Combine(Folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(Folder);
        var session = new LoomExplorationSession(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(Folder, "00.LFL")));
        LoomReachedSoundCue Cue() => LoomSoundCueResolver.ResolveForRoom(session.SoundEvents,
            session.Snapshot.RoomId!.Value, session.RoomEntrySequence, _ => true);

        // Script 044 +0x02C3 starts script 039 with sound 25 in Room 02, before the intro's last Room 02 entry.
        var intro = Assert.Single(session.SoundEvents);
        Assert.Equal(25, intro.Event.TargetId);
        Assert.Null(intro.OriginRoomId);
        Assert.Equal(25, LoomSoundCueResolver.Resolve(session.ReachedSoundEvents, _ => true).Start?.TargetId);
        Assert.Null(Cue().Start);

        session.VisitRoom(game.Rooms.Single(room => room.ResourceId == 3));
        Assert.Null(Cue().Start);

        // Room 16 entry +0x009A starts script 039 with sound 30; it starts the sound after one breakHere.
        session.VisitRoom(game.Rooms.Single(room => room.ResourceId == 16));
        session.AdvanceFrame();
        session.AdvanceFrame();
        var cue = Cue();
        Assert.Equal(30, cue.Start?.TargetId);
        Assert.False(cue.StartedBeforeEntry);
        Assert.Equal(16, session.SoundEvents.Last(item => item.Event.TargetId == 30).OriginRoomId);

        session.VisitRoom(game.Rooms.Single(room => room.ResourceId == 17));
        Assert.Null(Cue().Start);
    }

    private static LoomScriptEvent Event(LoomScriptEventKind kind, int? id) =>
        new(39, "Script 39", 0, kind, kind.ToString(), "", null, id,
            LoomScriptEventCertainty.Reached);
}
