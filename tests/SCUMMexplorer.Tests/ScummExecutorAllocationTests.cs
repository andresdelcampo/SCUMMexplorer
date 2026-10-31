using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Tests;

/// <summary>
/// Executor allocation speed-up (19 October 2026): the frame loop no longer copies the session's state for results and snapshots nobody reads
/// (<see cref="ScummGameSession.StepFrame"/>, <c>snapshotState: false</c>, a pool of variable dictionaries, reused hit-test buffers). A session played
/// with <see cref="ScummGameSession.StepFrame"/> ends exactly where one played with <see cref="ScummSession.AdvanceFrame"/> does, and a run the session
/// keeps (<see cref="ScummSession.LastAction"/>) still holds the states as they were when it ended. Record: <c>.verification/executor-alloc-20261019/README.md</c>.
/// Skips a game without its data.
/// </summary>
public sealed class ScummExecutorAllocationTests
{
    public static TheoryData<string, string> Games => new()
    {
        { "indy4 vga en", "ATLANTIS.cue" },
        { "mi2 talkie en", "MONKEY2.000" },
        { "zak ega en", "00.LFL" },
        { "indy3 ega en", "00.LFL" },
    };

    private static GameLibrary? Game(string folder, string marker)
    {
        var path = Path.Combine(TestGames.Root, folder);
        return File.Exists(Path.Combine(path, marker)) ? new GameFolderScanner().Scan(path) : null;
    }

    private static string State(ScummSession session)
    {
        var snapshot = session.Snapshot;
        return string.Join("\n",
            $"room {session.CurrentRoomId}",
            string.Join(",", snapshot.ObjectStates.Select(pair => $"{pair.Key}:{pair.Value}")),
            string.Join(",", snapshot.ObjectPositions.Select(pair => $"{pair.Key}:{pair.Value.X},{pair.Value.Y}")),
            string.Join(",", snapshot.KnownGlobalVariables.Select(pair => $"{pair.Key}={pair.Value}")),
            string.Join("\n", session.SessionTrace),
            string.Join("\n", snapshot.Uncertainties),
            string.Join(",", session.PendingScriptSummary));
    }

    [Theory]
    [MemberData(nameof(Games))]
    public void StepFramePlaysTheSameFramesAsAdvanceFrame(string folder, string marker)
    {
        if (Game(folder, marker) is not { } game) return;
        var boot = ScummBootSnapshot.Of(ScummGameSession.BootForPlay(game, out _));
        var planner = new ScummCutscenePlanner(game);
        foreach (var group in planner.Groups.Where((_, i) => i % Math.Max(1, planner.Groups.Count / 3) == 0).Take(3))
        {
            var trigger = group.First;
            var room = game.Rooms.First(r => r.ResourceId == trigger.Room);
            var advanced = boot.Start();
            var stepped = boot.Start();
            foreach (var session in new[] { advanced, stepped })
            {
                if (trigger.IsRoomEntry) session.EnterRoomAgain(room);
                else { session.VisitRoom(room); session.ActivateObjectVerb(trigger.ObjectId, trigger.VerbId); }
            }
            for (var frame = 0; frame < 300; frame++)
            {
                advanced.AdvanceFrame();
                stepped.StepFrame();
            }
            Assert.Equal(State(advanced), State(stepped));
        }
    }

    [Theory]
    [MemberData(nameof(Games))]
    public void AKeptRunHoldsTheStatesItEndedWith(string folder, string marker)
    {
        if (Game(folder, marker) is not { } game) return;
        var planner = new ScummCutscenePlanner(game);
        var trigger = planner.Groups.Select(group => group.First).FirstOrDefault(item => !item.IsRoomEntry);
        if (trigger is null) return;
        var session = ScummBootSnapshot.Of(ScummGameSession.BootForPlay(game, out _)).Start();
        session.VisitRoom(game.Rooms.First(r => r.ResourceId == trigger.Room));
        session.ActivateObjectVerb(trigger.ObjectId, trigger.VerbId);
        if (session.LastAction?.Run is not { } run) return;
        var states = run.ObjectStates.ToArray();
        var positions = run.ObjectPositions.ToArray();
        for (var frame = 0; frame < 200; frame++) session.StepFrame();
        // A copy, not the live runtime: the frames after the action do not change it.
        Assert.Equal(states, run.ObjectStates.ToArray());
        Assert.Equal(positions, run.ObjectPositions.ToArray());
    }
}
