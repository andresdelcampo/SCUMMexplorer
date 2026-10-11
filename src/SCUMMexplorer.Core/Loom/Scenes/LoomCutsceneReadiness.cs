using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Loom;

/// <summary>What one headless run of a trigger did: whether a cutscene began, the lines it printed and the rooms it entered.</summary>
public sealed record LoomSceneOutcome(bool Begun, int Lines, int Rooms, string Signature)
{
    /// <summary>Beginning at all outweighs everything; then the rooms visited and the lines printed. A fuller scene is the more complete branch.</summary>
    public int Score => (Begun ? 10000 : 0) + Rooms * 20 + Lines;
}

/// <summary>
/// The story state a launched cutscene is brought to (R-8.26): the staff, the available notes and the story bits the scene's own code reads.
/// It only ever raises state, never lowers it.
/// </summary>
public sealed record LoomReadinessPlan(bool Staff, int Notes, IReadOnlyList<string> Bits, LoomSceneOutcome Base, LoomSceneOutcome Result, string Note)
{
    public static readonly LoomReadinessPlan None = new(false, 0, [], new(false, 0, 0, ""), new(false, 0, 0, ""), "");
    public bool HasEdits => Staff || Notes > 0 || Bits.Count > 0;
}

/// <summary>
/// Brings a cutscene launched from a fresh game to the story state it needs. The state is found by running the trigger headless in fresh
/// sessions: the staff, eight notes and each story bit the scene's own code reads (<see cref="LoomSceneStateScanner"/>) are raised one at a time, and a
/// raise is kept when the scene then begins, or enters more rooms, or prints more lines. The result is an estimate of the story point the scene belongs to
/// (a state-dependent scene has a fuller branch; a fresh game plays the shortest one), labelled as such. Applying a plan to a running session raises state
/// the session lacks and never lowers what it has.
/// </summary>
public sealed class LoomCutsceneReadiness
{
    private const int EndFrames = 6000;
    private const int TrialFrames = 600;
    private const int SettleFrames = 1500;
    private const int MaxBits = 8;

    private readonly GameLibrary _game;
    private readonly Func<LoomExplorationSession> _fresh;
    private readonly LoomSceneStateScanner _scanner;
    private readonly int _firstStoryBit;
    private readonly ConcurrentDictionary<(int, int, int), Lazy<LoomReadinessPlan>> _plans = [];

    public LoomCutsceneReadiness(GameLibrary game, Func<LoomExplorationSession> freshSession)
    {
        _game = game;
        _fresh = freshSession;
        _scanner = LoomSceneStateScanner.For(game);
        _firstStoryBit = LoomStoryEvents.FirstBitFor(game);
    }

    /// <summary>The script numbers in a scanned trigger's labels (<c>Script 91</c>).</summary>
    public static int[] ScriptIds(IEnumerable<string> labels) =>
        labels.Select(l => Regex.Match(l, @"\d+")).Where(m => m.Success).Select(m => int.Parse(m.Value)).Distinct().ToArray();

    /// <summary>True when the plan for the trigger has been worked out already (so the UI thread can ask without waiting).</summary>
    public bool IsReady(int room, int objectId, int verbId) => _plans.TryGetValue((room, objectId, verbId), out var lazy) && lazy.IsValueCreated;

    /// <summary>The plan for a trigger (an entry has object 0), worked out once and cached. Several seconds the first time.</summary>
    public LoomReadinessPlan Plan(int room, int objectId, int verbId, IReadOnlyCollection<int> scriptIds) =>
        _plans.GetOrAdd((room, objectId, verbId), _ => new(() => Search(room, objectId, verbId, scriptIds))).Value;

    private LoomReadinessPlan Search(int room, int objectId, int verbId, IReadOnlyCollection<int> scriptIds)
    {
        var scan = _scanner.Scan(room, scriptIds.Where(id => id >= LoomSceneStateScanner.SystemScriptLimit), objectId, verbId);
        var bits = scan.OwnTouches.Where(t => !t.Write && IsStoryBit(t.Variable)).Select(t => t.Variable).Distinct().Take(MaxBits).ToList();
        var readsStaff = scan.OwnTouches.Any(t => !t.Write && t.Variable == LoomSceneSetupPlanner.StaffBit);
        var baseline = Run(room, objectId, verbId, new(false, 0, []));
        if (!baseline.Ok) return LoomReadinessPlan.None;
        var best = baseline;
        var staff = false;
        var notes = 0;
        var kept = new List<string>();
        // The staff and notes first: they gate most draft-related scenes, and a bit may need them to show.
        if (readsStaff || bits.Count > 0)
        {
            var candidate = Run(room, objectId, verbId, new(true, 0, kept));
            if (candidate.Ok && candidate.Outcome.Score > best.Outcome.Score) { best = candidate; staff = true; }
            var withNotes = Run(room, objectId, verbId, new(true, 8, kept));
            if (withNotes.Ok && withNotes.Outcome.Score > best.Outcome.Score) { best = withNotes; staff = true; notes = 8; }
        }
        // Each bit, kept when the scene gets fuller; one pass in code order, then a second for bits that only matter after another.
        for (var pass = 0; pass < 2; pass++)
        {
            var changed = false;
            foreach (var bit in bits.Where(b => !kept.Contains(b)))
            {
                var trial = new List<string>(kept) { bit };
                var candidate = Run(room, objectId, verbId, new(staff, notes, trial));
                if (!candidate.Ok || candidate.Outcome.Score <= best.Outcome.Score) continue;
                best = candidate; kept = trial; changed = true;
            }
            if (!changed) break;
        }
        var plan = new LoomReadinessPlan(staff, notes, kept, baseline.Outcome, best.Outcome, "");
        return plan with { Note = Describe(plan) };
    }

    private static string Describe(LoomReadinessPlan plan)
    {
        if (!plan.HasEdits)
            return plan.Base.Begun
                ? "Story state: the scene plays the same with or without more story state, so none was added."
                : "Story state: no staff, notes or story bits the scene's own code reads make it begin; none was added.";
        var parts = new List<string>();
        if (plan.Staff) parts.Add("the staff held");
        if (plan.Notes > 0) parts.Add($"{plan.Notes} notes");
        if (plan.Bits.Count > 0) parts.Add("story bits " + string.Join(" ", plan.Bits));
        var effect = !plan.Base.Begun ? "without them the scene does not begin"
            : $"fresh it prints {plan.Base.Lines} lines in {plan.Base.Rooms} other room(s), with them {plan.Result.Lines} in {plan.Result.Rooms}";
        return $"Story state added (estimate): {string.Join(", ", parts)}; a headless search kept what made the scene fuller ({effect}). " +
               "The game sets these in earlier scenes that were not played; state the session already has is kept, never lowered.";
    }

    private bool IsStoryBit(string variable) =>
        variable.StartsWith("bit[0x", StringComparison.Ordinal) &&
        int.TryParse(variable.AsSpan(6, variable.Length - 7), System.Globalization.NumberStyles.HexNumber, null, out var bit) && bit >= _firstStoryBit;

    private readonly record struct Edit(bool Staff, int Notes, IReadOnlyList<string> Bits);

    private readonly record struct Trial(bool Ok, LoomSceneOutcome Outcome);

    private Trial Run(int room, int objectId, int verbId, Edit edit)
    {
        try
        {
            var session = _fresh();
            var resource = _game.Rooms.First(r => r.ResourceId == room);
            session.VisitRoom(resource);
            if (!Settle(session)) return new(false, new(false, 0, 0, ""));
            Apply(session, new(edit.Staff, edit.Notes, edit.Bits, new(false, 0, 0, ""), new(false, 0, 0, ""), ""), resource);
            return new(true, Play(session, objectId, verbId));
        }
        catch (Exception ex) when (ex is InvalidOperationException or InvalidDataException or IOException or ArgumentException) { return new(false, new(false, 0, 0, "")); }
    }

    /// <summary>Plays the trigger on the session's own frames until the cutscene and its lines end, and summarises it.</summary>
    public static LoomSceneOutcome Play(LoomExplorationSession session, int objectId, int verbId)
    {
        var earlier = session.CutscenesBegun;
        var roomMark = session.RoomsEntered.Count;
        var logMark = session.Messages.Log.Count;
        var here = session.Snapshot.RoomId;
        if (objectId != 0) session.ActivateObjectVerb(objectId, verbId);
        var begun = false;
        for (var frame = 0; frame < EndFrames; frame++)
        {
            if (session.InCutscene || session.CutscenesBegun > earlier) begun = true;
            if (begun && !session.InCutscene && session.Messages.HaveMsg == 0) break;
            if (!begun && frame >= TrialFrames) break;
            session.AdvanceFrame();
        }
        var lines = session.Messages.Log.Skip(logMark).Select(m => m.Text.Replace("\n", " ")).ToArray();
        var rooms = session.RoomsEntered.Skip(roomMark).Where(r => r != here).Distinct().ToArray();
        return new(begun, lines.Length, rooms.Length, $"[{string.Join(",", rooms)}] {string.Join("|", lines)}");
    }

    /// <summary>Runs frames until Game Status can be edited (the room's entry scene is over); false when it never can.</summary>
    public static bool Settle(LoomExplorationSession session)
    {
        for (var i = 0; i < SettleFrames && !session.GameStatus.CanEdit; i++) session.AdvanceFrame();
        return session.GameStatus.CanEdit;
    }

    /// <summary>
    /// Raises the session to the plan. Each part is skipped when the session has it already, so a running game keeps its later state. Returns the notes
    /// of what was added; empty when nothing was. When something was added and <paramref name="reenter"/> is given, the room is entered again so that what
    /// its entry script derives from the story bits (portals, objects, who stands where) follows the raised state.
    /// </summary>
    public static IReadOnlyList<string> Apply(LoomExplorationSession session, LoomReadinessPlan plan, GameResource? reenter = null)
    {
        var added = new List<string>();
        if (!plan.HasEdits || !Settle(session)) return added;
        if (plan.Staff && session.GameStatus.StaffHeld != true) { session.UpdateStaffPossession(true); added.Add("staff held"); }
        if (plan.Notes > 0 && (session.GameStatus.AvailableNoteCount ?? 0) < plan.Notes) { session.UpdateAvailableNotes(plan.Notes); added.Add($"{plan.Notes} notes"); }
        foreach (var bit in plan.Bits)
            if (session.StoryBit(bit) != true) { session.UpdateStoryBit(bit, true); added.Add(bit); }
        if (added.Count > 0 && reenter is not null) session.VisitRoom(reenter);
        return added;
    }
}
