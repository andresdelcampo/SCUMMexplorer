using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Formats;

/// <summary>
/// The start setup derived for one scene: leading replay steps to run unseen (real earlier scenes, a visit of the
/// start room) and the labelled assumptions that go with it. <see cref="Steps"/> is empty when the fresh catalog
/// visit is already the least labelled start.
/// </summary>
public sealed record LoomStartSetup(IReadOnlyList<LoomReplayStep> Steps, IReadOnlyList<string> Notes)
{
    public static readonly LoomStartSetup None = new([], []);
    public bool HasSteps => Steps.Count > 0;

    /// <summary>The setup as one paragraph for the picker note and the status tooltip (empty when there is nothing to say).</summary>
    public string Describe() => Notes.Count == 0 ? "" : "Derived start setup (estimates): " + string.Join(" ", Notes);
}

/// <summary>
/// Derives a scene's start setup from what its code reads (see <see cref="LoomSceneStateScanner"/>), not from a list of
/// scenes. A state the scene needs and a fresh catalog visit lacks is established, where the game has a way to, by a
/// real earlier scene that assigns it (found by <see cref="LoomSceneStateScanner.FindConstantWriters"/>) run unseen, then
/// a catalog visit of the scene's room. Only when no real scene sets the state does a labelled edit remain, and no
/// story flag is written by the explorer. Everything the derivation assumed is returned as notes.
/// </summary>
public sealed class LoomSceneSetupPlanner
{
    public const string StaffBit = "bit[0xD61]";
    private const int FirstStoryBit = 0xD72;
    /// <summary>The first room beyond the LOOM island (an estimate of the user's "outside the island").</summary>
    public const int FirstRoomOffTheIsland = 13;

    private readonly LoomSceneStateScanner _scanner;
    private IReadOnlyList<LoomStateSource>? _staffHeldWriters;

    public LoomSceneSetupPlanner(GameLibrary game) => _scanner = new LoomSceneStateScanner(game);

    /// <summary>The scan behind a scene (the scripts it names and what they call), for the audit and the tests.</summary>
    public LoomSceneScan Scan(int startRoom, IEnumerable<int> scriptIds) => _scanner.Scan(startRoom, scriptIds);

    /// <summary>Real places that set the staff bit to held, object verbs first and in room order.</summary>
    public IReadOnlyList<LoomStateSource> StaffHeldWriters =>
        _staffHeldWriters ??= _scanner.FindConstantWriters(StaffBit, 1)
            .OrderBy(w => w.ObjectId is null).ThenBy(w => w.Room).ThenBy(w => w.ObjectId).ToArray();

    /// <summary>
    /// The setup for the scene that starts in <paramref name="startRoom"/> with the named scripts and, when
    /// <paramref name="objectId"/> is not 0, that object's verb <paramref name="verbId"/>.
    /// </summary>
    public LoomStartSetup Derive(int startRoom, IReadOnlyCollection<int> scriptIds, int objectId = 0, int verbId = 0)
    {
        var scriptsOf = scriptIds.Where(id => id >= LoomSceneStateScanner.SystemScriptLimit).ToArray();
        if (scriptsOf.Length == 0 && objectId == 0) return LoomStartSetup.None;
        var scan = _scanner.Scan(startRoom, scriptsOf, objectId, verbId);
        var steps = new List<LoomReplayStep>();
        var notes = new List<string>();
        var own = scan.OwnTouches.ToList();

        // Bit D61 (the distaff). A scene whose own code clears it takes the distaff from Bobbin, so it presumes he
        // holds it; a fresh game does not. Unless the scene itself sets the bit first, a real earlier scene that
        // assigns 1 (the first object verb in room order) is run unseen.
        // Only the scene's own global scripts count: the entry and local scripts of rooms it passes through reset
        // the look for their own arrival (Room 10 local 203) or restore a saved copy (Room 10 local 200), which is not a presumption.
        var clears = own.Where(t => t.Write && t.Variable == StaffBit && t.Where.StartsWith("Script ", StringComparison.Ordinal) &&
                                    t.Instruction.EndsWith("= 0x0000", StringComparison.Ordinal)).ToList();
        var sets = own.Where(t => t.Write && t.Variable == StaffBit && t.Instruction.EndsWith("= 0x0001", StringComparison.Ordinal)).ToList();
        // The user's decision (3 October): outside the LOOM island Bobbin holds the distaff, so a scene that reads the bit
        // in a room from FirstRoomOffTheIsland on starts with it held too. On the island (the opening rooms) the fresh state stays.
        var reads = own.Where(t => !t.Write && t.Variable == StaffBit).ToList();
        var presumesHeld = clears.Count > 0 || reads.Count > 0 && startRoom >= FirstRoomOffTheIsland;
        if (presumesHeld && sets.Count == 0)
        {
            var writer = StaffHeldWriters.FirstOrDefault(w => w.ObjectId is not null && w.VerbId is not null);
            var why = clears.Count > 0
                ? $"The scene clears bit D61 ({string.Join(", ", clears.Select(t => t.Where).Distinct().Take(3))}), so it presumes Bobbin holds the distaff."
                : $"The scene's own code reads bit D61 ({string.Join(", ", reads.Select(t => t.Where).Distinct().Take(3))}) and Room {startRoom:00} is outside the island, where Bobbin holds the distaff (decided with the user, 3 October).";
            if (writer is { ObjectId: int obj, VerbId: int verb })
            {
                steps.Add(new("visit", writer.Room));
                steps.Add(new("verb", obj, verb));
                steps.Add(new("frames", Frames: 8));
                notes.Add($"{why} A fresh game does not, so the game's own {writer.Where.Split(" +")[0]} (it sets D61 to 1) runs first, unseen, as an exploration action. " +
                          $"Other places that set it: {string.Join(", ", StaffHeldWriters.Where(w => w != writer).Select(w => w.Where.Split(" +")[0]).Distinct().Take(4))}.");
            }
            else notes.Add($"{why} No object verb sets it; the Game Status edit \"staff held\" would be needed (not applied).");
        }
        else if (reads.Count > 0)
            notes.Add($"The scene's own code reads bit D61 ({string.Join(", ", reads.Select(t => t.Where).Distinct().Take(3))}); Room {startRoom:00} is on the island, so the fresh state (distaff not held) is kept and its without-distaff branch is the one shown.");

        if (steps.Count > 0 && steps[0].ObjectId != startRoom) steps.Add(new("visit", startRoom));

        // Speakers. A line printed for an actor who is not in the room shows nothing (ScummVM's actorTalk), so a scene whose
        // own code prints for actor N without putting him in a room first needs him there. The game's own placement of him into
        // this room (putActorInRoom then putActor / putActorAtObject) is taken as the labelled estimate; the first in the
        // start room's own code, else the first in code order, and the others are listed.
        foreach (var actor in scan.PrintedActors.Except(scan.PlacedActors))
        {
            var placers = ActorPlacers(actor, startRoom);
            if (placers.Count == 0)
            {
                notes.Add($"The scene prints lines for actor {actor}, who no code of the game puts into Room {startRoom:00}; if he is not there his lines show nothing (not applied).");
                continue;
            }
            var chosen = placers.FirstOrDefault(p => p.Where.StartsWith($"Room {startRoom:00} ", StringComparison.Ordinal)) ?? placers[0];
            steps.Add(new("actor", actor, chosen.X ?? 0, chosen.Y ?? 0, chosen.AtObject ?? 0, EndBoundary: chosen.Where));
            notes.Add($"The scene prints lines for actor {actor}, and a line for an actor outside the room shows nothing. The game puts him into Room {startRoom:00} at {chosen.Where}" +
                      $"{(chosen.AtObject is int o ? $" (at object {o}'s walk point)" : $" ({chosen.X}, {chosen.Y})")}; that placement is applied if he is not there already (estimate: which earlier scene left him there was not executed)." +
                      (placers.Count > 1 ? $" Other placements: {string.Join(", ", placers.Where(p => p != chosen).Select(p => p.Where).Take(4))}." : ""));
        }

        // The ego's costume variable: a scene that tests it is reached with the fresh value, the first-visit branch.
        var costumeReads = own.Where(t => !t.Write && t.Variable == "var[0x07D]").Select(t => t.Where).Distinct().ToList();
        if (costumeReads.Count > 0)
            notes.Add($"The scene's own code tests the ego's costume variable var[0x07D] ({string.Join(", ", costumeReads.Take(2))}); the fresh value (Bobbin's own look) is kept.");

        // Story bits: every read is first-visit (0) unless a setup step above changed it.
        var storyReads = own.Where(t => !t.Write && IsStoryBit(t.Variable)).Select(t => t.Variable).Distinct().Order().ToList();
        if (storyReads.Count > 0)
            notes.Add($"Story bits read by the scene's own code ({string.Join(" ", storyReads)}) keep their first-visit value 0; no flag is set by the explorer.");

        return notes.Count == 0 ? LoomStartSetup.None : new LoomStartSetup(steps, notes);
    }

    private readonly Dictionary<(int, int), IReadOnlyList<LoomActorPlacer>> _placers = [];

    private IReadOnlyList<LoomActorPlacer> ActorPlacers(int actor, int room) =>
        _placers.TryGetValue((actor, room), out var cached) ? cached : _placers[(actor, room)] = _scanner.FindActorPlacers(actor, room);

    private static bool IsStoryBit(string variable) =>
        variable.StartsWith("bit[0x", StringComparison.Ordinal) &&
        int.TryParse(variable.AsSpan(6, variable.Length - 7), System.Globalization.NumberStyles.HexNumber, null, out var bit) && bit >= FirstStoryBit;
}
