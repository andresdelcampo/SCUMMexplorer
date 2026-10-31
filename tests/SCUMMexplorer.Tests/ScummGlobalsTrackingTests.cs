using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Tests;

/// <summary>
/// Globals change tracking (20 October 2026): the runtime's globals are a <see cref="ScummGlobalVariables"/> store that enumerates as
/// <see cref="Dictionary{TKey,TValue}"/> does, and a run whose result is not kept reads the globals through it instead of copying them all
/// (<see cref="ScummRunVariables"/>). Played with full copies (the old way) or through the store, sessions end in the same state, variables in the
/// same order. Record: <c>.verification/globals-tracking-20261019/README.md</c>. The session cases skip a game without its data.
/// </summary>
public sealed class ScummGlobalsTrackingTests
{
    [Fact]
    public void TheStoreEnumeratesAsADictionary()
    {
        var random = new Random(20261020);
        var store = new ScummGlobalVariables();
        var reference = new Dictionary<string, ushort>(StringComparer.Ordinal);
        for (var step = 0; step < 20000; step++)
        {
            var key = $"var[0x{random.Next(64):X3}]";
            var value = (ushort)random.Next(1000);
            switch (random.Next(10))
            {
                case < 6: store[key] = value; reference[key] = value; break;
                case < 9: Assert.Equal(reference.Remove(key), store.Remove(key)); break;
                default:
                    if (random.Next(50) == 0) { store.Clear(); reference.Clear(); }
                    break;
            }
            if (step % 97 == 0) Assert.Equal(reference.ToArray(), store.ToArray());
        }
        Assert.Equal(reference.ToArray(), store.ToArray());
        Assert.Equal(reference.Keys.ToArray(), store.Keys.ToArray());
        Assert.Equal(new Dictionary<string, ushort>(reference).ToArray(), new Dictionary<string, ushort>(store).ToArray());
    }

    [Fact]
    public void AddingWhileEnumeratingThrowsAsADictionaryDoes()
    {
        var store = new ScummGlobalVariables { ["var[0x001]"] = 1, ["var[0x002]"] = 2 };
        Assert.Throws<InvalidOperationException>(() => { foreach (var _ in store) store["var[0x003]"] = 3; });
        foreach (var (key, value) in store) store[key] = (ushort)(value + 1);
        Assert.Equal(3, store["var[0x002]"]);
    }

    public static TheoryData<string, string> Games => new()
    {
        { "indy4 vga en", "ATLANTIS.cue" },
        { "mi2 talkie en", "MONKEY2.000" },
        { "monkey1 vga en", "The_Secret_of_Monkey_Island.cue" },
        { "zak ega en", "00.LFL" },
        { "mmansionE ega es", "00.LFL" },
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
            string.Join(",", snapshot.EstimatedObjectStates ?? []),
            string.Join(",", snapshot.Picks ?? []),
            string.Join("|", snapshot.ScreenMessages.Select(message => string.Join("/", message.LineGlyphs.Select(line => Convert.ToHexString(line))))),
            string.Join("\n", session.SessionTrace),
            string.Join("\n", snapshot.Uncertainties),
            string.Join(",", session.PendingScriptSummary));
    }

    [Theory]
    [MemberData(nameof(Games))]
    public void TrackedRunsPlayAsFullCopiesDo(string folder, string marker)
    {
        if (Game(folder, marker) is not { } game) return;
        var planner = new ScummCutscenePlanner(game);
        var triggers = planner.Groups.Where((_, i) => i % Math.Max(1, planner.Groups.Count / 4) == 0).Take(4).Select(group => group.First).ToList();
        string Play(bool fullCopies)
        {
            ScummRunVariables.FullCopiesOnThisThread = fullCopies;
            try
            {
                var boot = ScummBootSnapshot.Of(ScummGameSession.BootForPlay(game, out _));
                var states = new List<string> { State(boot.Start()) };
                foreach (var trigger in triggers)
                {
                    var session = boot.Start();
                    var room = game.Rooms.First(r => r.ResourceId == trigger.Room);
                    if (trigger.IsRoomEntry) session.EnterRoomAgain(room);
                    else { session.VisitRoom(room); session.ActivateObjectVerb(trigger.ObjectId, trigger.VerbId); }
                    for (var frame = 0; frame < 400; frame++) session.StepFrame();
                    states.Add(State(session));
                }
                return string.Join("\n----\n", states);
            }
            finally { ScummRunVariables.FullCopiesOnThisThread = false; }
        }
        Assert.Equal(Play(fullCopies: true), Play(fullCopies: false));
    }
}
