using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using Xunit.Abstractions;

namespace SCUMMexplorer.Tests;

/// <summary>
/// Cutscene snapshot (18 October 2026): a cutscene run from a copy of one boot (<see cref="ScummBootSnapshot"/>) does exactly what a run after its own
/// boot does: same result (begun, rooms entered, frames) and the same session trace, line for line, for a spread of entries of each v5 game (Monkey Island
/// CD, Monkey Island 2, Indiana Jones and the Fate of Atlantis) and of the Monkey Island floppy disk set; several copies of one snapshot run in turn and on
/// several threads stay independent. Record: <c>.verification/cutscene-snapshot-20261018/README.md</c>. Skips a game without its data.
/// </summary>
public sealed class ScummBootSnapshotTests(ITestOutputHelper output)
{
    public static TheoryData<string, string> Games => new()
    {
        { "monkey1 vga en", "The_Secret_of_Monkey_Island.cue" },
        { "mi2 talkie en", "MONKEY2.000" },
        { "indy4 vga en", "ATLANTIS.cue" },
        { "monkey1 ega en", "DISK01.LEC" },
    };

    private static GameLibrary? Game(string folder, string marker)
    {
        var path = Path.Combine(TestGames.Root, folder);
        return File.Exists(Path.Combine(path, marker)) ? new GameFolderScanner().Scan(path) : null;
    }

    private static (ScummCutsceneRunResult Result, IReadOnlyList<string> Trace, int Room) RunTraced(GameLibrary game, Func<ScummGameSession> start, ScummCutscene trigger)
    {
        ScummGameSession? session = null;
        var result = ScummCutsceneRun.Run(game, () => session = start(), trigger);
        return (result, session!.SessionTrace.ToArray(), session.CurrentRoomId ?? -1);
    }

    [Theory]
    [MemberData(nameof(Games))]
    public void ARunFromTheSnapshotEqualsARunAfterItsOwnBoot(string folder, string marker)
    {
        if (Game(folder, marker) is not { } game) return;
        var planner = new ScummCutscenePlanner(game);
        var snapshot = ScummBootSnapshot.Of(ScummGameSession.BootForPlay(game, out _));
        // A spread over the list.
        var groups = planner.Groups.Where((_, i) => i % Math.Max(1, planner.Groups.Count / 6) == 0).Take(6).ToList();
        foreach (var group in groups)
        {
            var booted = RunTraced(game, () => ScummGameSession.BootForPlay(game, out _), group.First);
            var copied = RunTraced(game, snapshot.Start, group.First);
            output.WriteLine($"{group.First.Room}/{group.First.ObjectId}/0x{group.First.VerbId:X2}: begun {booted.Result.Begun}, frames {booted.Result.Frames}, trace {booted.Trace.Count}");
            Assert.Equal(booted.Result.Begun, copied.Result.Begun);
            Assert.Equal(booted.Result.Frames, copied.Result.Frames);
            Assert.Equal(booted.Result.RoomsEntered, copied.Result.RoomsEntered);
            Assert.Equal(booted.Result.Failure, copied.Result.Failure);
            Assert.Equal(booted.Room, copied.Room);
            Assert.Equal(booted.Trace, copied.Trace);
        }
    }

    [Fact]
    public void APoolOfBootsGivesTheRunsOfABootEach()
    {
        // The App's way for a disk game (Monkey Island floppy): each boot reads its own copy of the game, at most one boot per run going on at once.
        if (Game("monkey1 ega en", "DISK01.LEC") is not { } game) return;
        var folder = Path.Combine(TestGames.Root, "monkey1 ega en");
        var pooled = new ScummCutscenePlanner(game, () => ScummGameSession.BootForPlay(new GameFolderScanner().Scan(folder), out _), start: ScummCutsceneStart.SnapshotPerWorker);
        var booting = new ScummCutscenePlanner(new GameFolderScanner().Scan(folder), start: ScummCutsceneStart.BootPerRun);
        var groups = pooled.Groups.Take(24).ToList();
        Parallel.ForEach(groups, new ParallelOptions { MaxDegreeOfParallelism = 4 }, group => pooled.Run(group.First));
        output.WriteLine($"{groups.Count} runs, {pooled.PoolBoots} boots");
        Assert.InRange(pooled.PoolBoots ?? 0, 1, 4);
        foreach (var group in groups)
        {
            var expected = booting.Run(booting.Groups.Single(g => g.First.Room == group.First.Room && g.First.ObjectId == group.First.ObjectId && g.First.VerbId == group.First.VerbId).First);
            var actual = pooled.Run(group.First);
            Assert.Equal((expected.Begun, expected.Frames, expected.Failure), (actual.Begun, actual.Frames, actual.Failure));
            Assert.Equal(expected.RoomsEntered, actual.RoomsEntered);
        }
    }

    [Theory]
    [MemberData(nameof(Games))]
    public void CopiesOfOneSnapshotStayIndependent(string folder, string marker)
    {
        if (Game(folder, marker) is not { } game) return;
        var boot = ScummGameSession.BootForPlay(game, out _);
        var trace = boot.SessionTrace.ToArray();
        var snapshot = ScummBootSnapshot.Of(boot);
        // The booted session goes on; the snapshot keeps the boot.
        for (var frame = 0; frame < 200; frame++) boot.AdvanceFrame();
        var copies = Enumerable.Range(0, 4).AsParallel().Select(_ =>
        {
            var copy = snapshot.Start();
            Assert.Equal(trace, copy.SessionTrace);
            for (var frame = 0; frame < 200; frame++) copy.AdvanceFrame();
            return copy.SessionTrace.ToArray();
        }).ToList();
        Assert.All(copies, copy => Assert.Equal(boot.SessionTrace, copy));
    }
}
