using System.Collections.Concurrent;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>Where a cutscene run's fresh start comes from (<see cref="ScummCutscenePlanner"/>).</summary>
public enum ScummCutsceneStart
{
    /// <summary>A copy of one boot, shared by every thread (the game's readers may be read from several threads).</summary>
    SharedSnapshot,
    /// <summary>
    /// A copy of one of several boots, one per run going on at the same time (<see cref="ScummBootSnapshotPool"/>): for a disk or archive game whose
    /// session factory reads its own copy of the game at each call, so no reader is used by two runs at once.
    /// </summary>
    SnapshotPerWorker,
    /// <summary>A boot for every run.</summary>
    BootPerRun,
}

/// <summary>
/// The cutscene list of a game played through the generic session (S7, 17 October 2026; first games: Indiana Jones and the Last Crusade, EGA and
/// 256 colours): the triggers the engine's scan finds in the game's scripts (<see cref="ScummCutsceneIndex"/>), titled with the game's own verb names
/// (read from its verbs after the boot, as its scripts create them), grouped as LOOM's are, and for each group a headless run from a fresh start
/// (<see cref="ScummCutsceneRun"/>) that says whether that start begins the cutscene and which rooms it plays out in. Thread-safe for one derivation
/// at a time per trigger; each run gets its own session, a copy of one boot (<see cref="ScummBootSnapshot"/>).
/// </summary>
public sealed class ScummCutscenePlanner
{
    // Cutscene snapshot (18 October 2026): the runs start from copies of one boot, not from a boot each (Monkey Island 2: 260 boots of about 0.9 s).

    private readonly GameLibrary _game;
    private readonly Func<ScummGameSession> _newSession;
    private readonly ScummBootSnapshotPool? _pool;
    private readonly ConcurrentDictionary<(int, int, int), ScummCutsceneRunResult> _runs = [];

    /// <param name="newSession">A fresh session booted to the first playable room; by default <see cref="ScummGameSession.BootForPlay"/>.</param>
    /// <param name="start">
    /// How each run gets its fresh start: by default a copy of one boot (<see cref="ScummBootSnapshot"/>; the boot is <paramref name="newSession"/>'s
    /// first call); <see cref="ScummCutsceneStart.SnapshotPerWorker"/> when each call of <paramref name="newSession"/> reads its own copy of the game;
    /// <see cref="ScummCutsceneStart.BootPerRun"/> calls it for every run, as before 18 October 2026.
    /// </param>
    public ScummCutscenePlanner(GameLibrary game, Func<ScummGameSession>? newSession = null, string? preparationNote = null,
        Func<string, string>? readableText = null, ScummCutsceneStart start = ScummCutsceneStart.SharedSnapshot)
    {
        ArgumentNullException.ThrowIfNull(game);
        _game = game;
        PreparationNote = preparationNote;
        var boot = newSession ?? (() => ScummGameSession.BootForPlay(game, out _));
        _newSession = start switch
        {
            ScummCutsceneStart.SharedSnapshot => ScummBootSnapshot.Shared(boot),
            _ => boot,
        };
        if (start == ScummCutsceneStart.SnapshotPerWorker) _pool = new ScummBootSnapshotPool(boot);
        var booted = _pool is null ? _newSession() : _pool.Use(fresh => fresh());
        VerbSource = UntilAVerbIsOn(booted);
        Verbs = VerbsOf(booted, readableText);
        Triggers = ScummCutsceneIndex.Scan(ScummDataSources.For(game), game.Rooms.Where(r => r.ResourceId is not null).Select(r => r.ResourceId!.Value).ToList(), null, game, Verbs);
        if (readableText is not null)
            Triggers = Triggers.Select(t => t with { ObjectName = readableText(t.ObjectName), FirstText = readableText(t.FirstText) }).ToArray();
        Groups = ScummCutsceneIndex.Group(Triggers);
    }

    /// <summary>The game's verbs as the titles name them: every text verb the boot left, by the name its script gave it (<c>verbOps name</c>).</summary>
    public ScummCutsceneVerbs Verbs { get; }

    /// <summary>
    /// Where the verb names came from: null when the boot itself shows the game's verbs; otherwise what the game's own skips did to reach them
    /// (<see cref="UntilAVerbIsOn"/>).
    /// </summary>
    public string? VerbSource { get; }

    /// <summary>The frames the planner lets the game run after the boot to reach its verbs (Indy 4: 110).</summary>
    public const int VerbFrameLimit = 6000;

    /// <summary>
    /// A boot that ends before the game shows any verb (Indiana Jones and the Fate of Atlantis: the playable credits, Script 78 as the input script, no
    /// verb on; Script 19 builds the full interface only when the credits end) leaves the titles without the game's verb names. Such a session is run on,
    /// pressing Esc each time the game itself allows a skip (an override is set: <see cref="ScummSession.CutsceneSkippable"/>), until a verb is on, the
    /// player has control and no cutscene is open, within <see cref="VerbFrameLimit"/> frames. The session is the planner's own (it serves only for the
    /// names); the scenes' runs still start from the boot. A boot that shows a verb is left as it is (null). S7 Indy4, 18 October 2026.
    /// </summary>
    public static string? UntilAVerbIsOn(ScummGameSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        if (session.Verbs.Any(verb => verb.Mode == 1)) return null;
        var from = session.CurrentRoomId;
        int frame = 0, skips = 0;
        for (; frame < VerbFrameLimit && !(session.Verbs.Any(verb => verb.Mode == 1) && session.UserInputOn && !session.InCutscene); frame++)
        {
            if (session.CutsceneSkippable) { session.PressKey(0x1B); skips++; }
            session.AdvanceFrame();
        }
        return session.Verbs.Any(verb => verb.Mode == 1)
            ? $"the boot (Room {from:00}) shows no verb; the verb names are those of Room {session.CurrentRoomId:00} after {skips} of the game's own skips (Esc) and {frame} frames"
            : $"the boot (Room {from:00}) shows no verb, and none came within {frame} frames and {skips} skips; titles keep verb numbers";
    }

    public string? PreparationNote { get; }

    public IReadOnlyList<ScummCutscene> Triggers { get; }

    public IReadOnlyList<ScummCutsceneGroup> Groups { get; }

    /// <summary>
    /// The verb words a booted session's verbs give: a text verb's name, decoded, when it is printable text (a name built from escapes, such as the
    /// sentence line, is left out). No verb gets a role: a verb-based game's look, walk-to and use are its scripts' business, not the list's.
    /// </summary>
    public static ScummCutsceneVerbs VerbsOf(ScummSession session, Func<string, string>? readableText = null)
    {
        ArgumentNullException.ThrowIfNull(session);
        var words = new Dictionary<int, string>();
        foreach (var verb in session.Verbs)
        {
            if (verb.Name is null || verb.ImageObject is not null || words.ContainsKey(verb.VerbId)) continue;
            var bytes = ScummTextDecoder.ParseEscaped(verb.Name);
            if (bytes.Length == 0 || bytes.Any(b => b < 0x20 || b > 0x7E)) continue;
            var word = System.Text.Encoding.Latin1.GetString(bytes).Trim();
            if (word.Length > 0) words[verb.VerbId] = readableText?.Invoke(word) ?? word;
        }
        return ScummCutsceneVerbs.None with { Word = verb => words.GetValueOrDefault(verb), NamesSharedCode = true };
    }

    private static (int, int, int) Key(ScummCutscene t) => (t.Room, t.ObjectId, t.VerbId);

    /// <summary>Runs the trigger from a fresh start (once; cached).</summary>
    public ScummCutsceneRunResult Run(ScummCutscene trigger) =>
        _runs.GetOrAdd(Key(trigger), _ => _pool is null ? ScummCutsceneRun.Run(_game, _newSession, trigger)
            : _pool.Use(fresh => ScummCutsceneRun.Run(_game, fresh, trigger)));

    /// <summary>With <see cref="ScummCutsceneStart.SnapshotPerWorker"/>, the boots the pool made so far (at most one per run going on at once); else null.</summary>
    public int? PoolBoots => _pool?.Boots;

    /// <summary>The run when it is done already (so a caller on the UI thread can ask without waiting).</summary>
    public bool TryGetRun(ScummCutscene trigger, out ScummCutsceneRunResult run) => _runs.TryGetValue(Key(trigger), out run!);

    /// <summary>Whether a run of the scene was in this room at some point (its own room always counts).</summary>
    public bool PlaysIn(ScummCutsceneGroup group, int room) =>
        group.First.Room == room || group.Others.Any(o => o.Room == room) || TryGetRun(group.First, out var run) && run.RoomsEntered.Contains(room);

    /// <summary>The start sentence of a scene's note: what its fresh-start run showed, or that it is still being checked.</summary>
    public string StartNote(ScummCutscene trigger) =>
        (PreparationNote is { Length: > 0 } note ? note + " " : "") + RunNote(trigger);

    private string RunNote(ScummCutscene trigger) =>
        !TryGetRun(trigger, out var run) ? "Whether a fresh start begins it is still being checked (open the list again in a few seconds)."
        : run.Begun ? "A fresh start begins its cutscene (checked by a headless run" + (run.RoomsEntered.Count > 0 ? $"; it went on to {RoomList(run.RoomsEntered)})." : ").")
        : "A fresh start did not begin its cutscene in a headless run" + (run.Failure is { } failure ? $" ({failure})" : "") +
          ": the state its code tests is not reached by the boot, and no start setup is derived for this game yet.";

    /// <summary>The rooms each run entered, keyed "room,object,verb", for saving between runs.</summary>
    public Dictionary<string, int[]> ExportRoomLists() =>
        _runs.ToDictionary(pair => $"{pair.Key.Item1},{pair.Key.Item2},{pair.Key.Item3}", pair => pair.Value.RoomsEntered.ToArray());

    private static string RoomList(IEnumerable<int> rooms)
    {
        var list = rooms.Distinct().Order().Select(room => room.ToString("00")).ToList();
        return (list.Count == 1 ? "Room " : "Rooms ") + string.Join(", ", list);
    }
}
