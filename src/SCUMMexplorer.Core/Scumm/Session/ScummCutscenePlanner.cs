using System.Collections.Concurrent;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// The cutscene list of a game played through the generic session (S7, 17 October 2026; first games: Indiana Jones and the Last Crusade, EGA and
/// 256 colours): the triggers the engine's scan finds in the game's scripts (<see cref="ScummCutsceneIndex"/>), titled with the game's own verb names
/// (read from its verbs after the boot, as its scripts create them), grouped as LOOM's are, and for each group a headless run from a fresh start
/// (<see cref="ScummCutsceneRun"/>) that says whether that start begins the cutscene and which rooms it plays out in. Thread-safe for one derivation
/// at a time per trigger; the runs build their own sessions.
/// </summary>
public sealed class ScummCutscenePlanner
{
    private readonly GameLibrary _game;
    private readonly Func<ScummGameSession> _newSession;
    private readonly ConcurrentDictionary<(int, int, int), ScummCutsceneRunResult> _runs = [];

    /// <param name="newSession">A fresh session booted to the first playable room; by default <see cref="ScummGameSession.BootForPlay"/>.</param>
    public ScummCutscenePlanner(GameLibrary game, Func<ScummGameSession>? newSession = null)
    {
        ArgumentNullException.ThrowIfNull(game);
        _game = game;
        _newSession = newSession ?? (() => ScummGameSession.BootForPlay(game, out _));
        var booted = _newSession();
        Verbs = VerbsOf(booted);
        Triggers = ScummCutsceneIndex.Scan(ScummDataSources.For(game), game.Rooms.Where(r => r.ResourceId is not null).Select(r => r.ResourceId!.Value).ToList(), null, game, Verbs);
        Groups = ScummCutsceneIndex.Group(Triggers);
    }

    /// <summary>The game's verbs as the titles name them: every text verb the boot left, by the name its script gave it (<c>verbOps name</c>).</summary>
    public ScummCutsceneVerbs Verbs { get; }

    public IReadOnlyList<ScummCutscene> Triggers { get; }

    public IReadOnlyList<ScummCutsceneGroup> Groups { get; }

    /// <summary>
    /// The verb words a booted session's verbs give: a text verb's name, decoded, when it is printable text (a name built from escapes, such as the
    /// sentence line, is left out). No verb gets a role: a verb-based game's look, walk-to and use are its scripts' business, not the list's.
    /// </summary>
    public static ScummCutsceneVerbs VerbsOf(ScummSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var words = new Dictionary<int, string>();
        foreach (var verb in session.Verbs)
        {
            if (verb.Name is null || verb.ImageObject is not null || words.ContainsKey(verb.VerbId)) continue;
            var bytes = ScummTextDecoder.ParseEscaped(verb.Name);
            if (bytes.Length == 0 || bytes.Any(b => b < 0x20 || b > 0x7E)) continue;
            var word = System.Text.Encoding.Latin1.GetString(bytes).Trim();
            if (word.Length > 0) words[verb.VerbId] = word;
        }
        return ScummCutsceneVerbs.None with { Word = verb => words.GetValueOrDefault(verb), NamesSharedCode = true };
    }

    private static (int, int, int) Key(ScummCutscene t) => (t.Room, t.ObjectId, t.VerbId);

    /// <summary>Runs the trigger from a fresh start (once; cached).</summary>
    public ScummCutsceneRunResult Run(ScummCutscene trigger) =>
        _runs.GetOrAdd(Key(trigger), _ => ScummCutsceneRun.Run(_game, _newSession, trigger));

    /// <summary>The run when it is done already (so a caller on the UI thread can ask without waiting).</summary>
    public bool TryGetRun(ScummCutscene trigger, out ScummCutsceneRunResult run) => _runs.TryGetValue(Key(trigger), out run!);

    /// <summary>Whether a run of the scene was in this room at some point (its own room always counts).</summary>
    public bool PlaysIn(ScummCutsceneGroup group, int room) =>
        group.First.Room == room || group.Others.Any(o => o.Room == room) || TryGetRun(group.First, out var run) && run.RoomsEntered.Contains(room);

    /// <summary>The start sentence of a scene's note: what its fresh-start run showed, or that it is still being checked.</summary>
    public string StartNote(ScummCutscene trigger) =>
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
