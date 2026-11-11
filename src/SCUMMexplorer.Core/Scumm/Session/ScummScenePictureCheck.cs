namespace SCUMMexplorer.Core.Scumm;

/// <summary>One thing a picked scene's picture shows that the game probably never shows; <see cref="Actor"/> is null for a finding about the whole scene.</summary>
public sealed record ScummPictureSuspect(ScummPictureSuspectKind Kind, int? Actor, string Detail)
{
    public string Describe() => Actor is int actor ? $"{Kind}: actor {actor} {Detail}" : $"{Kind}: {Detail}";
}

public enum ScummPictureSuspectKind
{
    /// <summary>An actor the scene's setup put in the last room (no script of the game did) wears a costume no script set during the run, set while
    /// another room was shown, and the game dresses that actor in several costumes (one actor number plays different characters): it may have
    /// kept another room's look. An actor a script placed is the game's own doing (Indy 3's boot dresses Marcus and puts him in Room 1).</summary>
    CostumeFromBefore,
    /// <summary>An actor shown in the last room stands more than <see cref="ScummScenePictureCheck.BoxTolerance"/> pixels from every walk box of it,
    /// although it follows the boxes (it was put there, not walked).</summary>
    OutsideWalkBoxes,
    /// <summary>An actor moved for several frames while still on its costume's first (init) frame: it slides without its walk animation.</summary>
    MovesOnFirstFrame,
    /// <summary>Nothing changed from before the scene was started to the end of its first frames: no actor, no line, no object, no room, no camera move.</summary>
    NothingChanges,
}

/// <summary>
/// The picture checks of a picked scene (10 November 2026 audit, roadmap step 2): a scene was judged by "a cutscene began", so a setup that begins
/// another branch, or a scene started at its cutscene with the actors where and how the code before would never have left them, counted as good
/// (Indy 3's Grail knight, LOOM's sheep). These checks look at the session as the run leaves it and while it plays. They are estimates: each names
/// what was seen, and a game may do any of it on purpose (a script may stand an actor outside the boxes), so a finding is a suspect for a person to
/// look at, not a fault. <see cref="ScummCutsceneRun.Run"/> feeds one when it is given one; no room, actor or game is named here.
/// </summary>
public sealed class ScummScenePictureCheck
{
    /// <summary>Frames from the scene's start within which something must change on screen.</summary>
    public const int StillFrames = 50;

    /// <summary>Frames in a row an actor must move on its first frame before it is a suspect (a walk's own first frame is not one).</summary>
    public const int SlideFrames = 4;

    /// <summary>How far outside every walk box an actor may stand (a box's own edge is outside by a pixel in the slope test).</summary>
    public const int BoxTolerance = 4;

    private readonly IReadOnlySet<int>? _manyCostumes;

    /// <param name="manyCostumes">The actors the game's scripts dress in more than one costume (<see cref="ActorsWithManyCostumes"/>); null judges
    /// every actor.</param>
    public ScummScenePictureCheck(IReadOnlySet<int>? manyCostumes = null) => _manyCostumes = manyCostumes;

    private static readonly System.Runtime.CompilerServices.ConditionalWeakTable<SCUMMexplorer.Core.Domain.GameLibrary, IReadOnlySet<int>> ManyCostumes = new();
    private static readonly System.Text.RegularExpressions.Regex Dressed = new(@"^0x([0-9A-F]+), \[(?:[^\]]*; )?costume, 0x([0-9A-F]+)",
        System.Text.RegularExpressions.RegexOptions.CultureInvariant);

    /// <summary>
    /// The actor numbers the game's scripts dress in two or more costumes (<c>actorOps n, [costume, c]</c> with both written as numbers, over every
    /// global, room and object script): numbers that play different characters in different rooms (Indy 3's actor 4), unlike a game whose actors each
    /// keep one costume. An actor or costume given through a variable is not read, and versions 1 and 2 write the instruction another way, so there
    /// the set is empty and <see cref="ScummPictureSuspectKind.CostumeFromBefore"/> finds nothing.
    /// </summary>
    public static IReadOnlySet<int> ActorsWithManyCostumes(SCUMMexplorer.Core.Domain.GameLibrary game) => ManyCostumes.GetValue(game, static game =>
    {
        var data = ScummDataSources.For(game);
        var worn = new Dictionary<int, HashSet<int>>();
        void Read(Func<ScummScriptDisassembly?> read)
        {
            ScummScriptDisassembly? script;
            try { script = read(); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or ArgumentException or InvalidOperationException or KeyNotFoundException) { return; }
            foreach (var instruction in script?.Instructions ?? [])
                if (instruction.Operation == "actorOps" && Dressed.Match(instruction.Operands) is { Success: true } m)
                    (worn.TryGetValue(Convert.ToInt32(m.Groups[1].Value, 16), out var costumes) ? costumes : worn[Convert.ToInt32(m.Groups[1].Value, 16)] = [])
                        .Add(Convert.ToInt32(m.Groups[2].Value, 16));
        }
        foreach (var id in data.GlobalScriptIds()) Read(() => data.GlobalScript(id));
        foreach (var room in game.Rooms.Where(r => r.ResourceId is not null).Select(r => r.ResourceId!.Value).Distinct())
        {
            Read(() => data.RoomEntryScript(room));
            Read(() => data.RoomExitScript(room));
            try
            {
                foreach (var local in data.RoomLocalScriptIds(room)) Read(() => data.RoomLocalScript(room, local));
                var objects = data.ReadRoom(room).Objects;
                foreach (var (objectId, verbs) in data.ReadObjectVerbs(room, objects))
                    foreach (var verb in verbs) Read(() => data.ReadObjectVerb(room, objects.Last(o => o.Id == objectId), verb.VerbId, out _));
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or ArgumentException or InvalidOperationException or KeyNotFoundException) { }
        }
        return worn.Where(pair => pair.Value.Count > 1).Select(pair => pair.Key).ToHashSet();
    });

    private readonly Dictionary<int, int> _costumeSetsAtStart = [];
    private readonly Dictionary<int, (int? X, int? Y, int Slides)> _moves = [];
    private readonly Dictionary<int, int> _slid = [];
    private string? _firstPicture;
    private bool _changed;
    private int _frames;

    /// <summary>The run's session exists (booted, nothing of the scene done): what each actor's costume count is before any script of the run.</summary>
    public void RunStarts(ScummSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _costumeSetsAtStart.Clear();
        foreach (var actor in session.Actors) _costumeSetsAtStart[actor.Number] = actor.CostumeSets;
    }

    /// <summary>The scene is about to be started (before its room's entry plays again, or its verb is given): the picture the first frames are
    /// compared with. The last call before the frames counts.</summary>
    public void SceneStarts(ScummSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _firstPicture = Picture(session);
        _changed = false;
        _frames = 0;
        _moves.Clear();
        _slid.Clear();
        foreach (var actor in session.Actors) _moves[actor.Number] = (actor.X, actor.Y, 0);
    }

    /// <summary>After each frame of the scene.</summary>
    public void Frame(ScummSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        _frames++;
        if (!_changed && _frames <= StillFrames && Picture(session) != _firstPicture) _changed = true;
        foreach (var actor in session.Actors)
        {
            var before = _moves.GetValueOrDefault(actor.Number);
            var moved = (before.X, before.Y) != (actor.X, actor.Y) && before.X is not null && actor.X is not null;
            var sliding = moved && Shown(actor, session.CurrentRoomId) && actor.Moving != 0 && actor.Look.Frame == actor.Look.InitFrame;
            var slides = sliding ? before.Slides + 1 : 0;
            if (slides >= SlideFrames) _slid[actor.Number] = Math.Max(_slid.GetValueOrDefault(actor.Number), slides);
            _moves[actor.Number] = (actor.X, actor.Y, slides);
        }
    }

    /// <summary>The run is over: the suspects, the scene-wide one first, then by actor.</summary>
    public IReadOnlyList<ScummPictureSuspect> Finish(ScummSession session)
    {
        ArgumentNullException.ThrowIfNull(session);
        var found = new List<ScummPictureSuspect>();
        // A scene over in fewer frames is judged by how it ends.
        if (!_changed && _frames < StillFrames && _firstPicture is not null && Picture(session) != _firstPicture) _changed = true;
        if (_firstPicture is not null && !_changed)
            found.Add(new(ScummPictureSuspectKind.NothingChanges, null, _frames == 0 ? "the scene was over at once and changed nothing on screen"
                : $"nothing on screen changed in the first {Math.Min(_frames, StillFrames)} frames"));
        var hero = session.HeroNumber;
        var boxes = session.CurrentWalkBoxes;
        foreach (var actor in session.Actors.Where(actor => Shown(actor, session.CurrentRoomId)).OrderBy(actor => actor.Number))
        {
            // The hero keeps his look from room to room; every other actor shown was dressed by the room or the scene, or it is a leftover.
            if (actor.Number != hero && actor.RoomBySetup && actor.CostumeSets == _costumeSetsAtStart.GetValueOrDefault(actor.Number) && actor.CostumeRoom != session.CurrentRoomId &&
                (_manyCostumes is null || _manyCostumes.Contains(actor.Number)))
                found.Add(new(ScummPictureSuspectKind.CostumeFromBefore, actor.Number,
                    $"wears costume {actor.Look.Costume}, set before the run {(actor.CostumeRoom is int set ? $"in Room {set}" : "by no script")}, at ({actor.X},{actor.Y})"));
            if (actor.IgnoreBoxes != true && boxes is { Count: > 0 } && actor is { X: int x, Y: int y } && !boxes.Boxes.Any(box => boxes.Contains(box.Index, x, y)))
            {
                var nearest = boxes.Boxes.Min(box => ScummWalkBoxSet.ClosestPointOnBox(box, x, y).Distance);
                if (nearest > BoxTolerance * BoxTolerance) found.Add(new(ScummPictureSuspectKind.OutsideWalkBoxes, actor.Number,
                    $"at ({x},{y}) is in none of the room's {boxes.Count} walk boxes (nearest: {Math.Sqrt(nearest):F0} pixels), elevation {actor.Look.Elevation ?? 0}"));
            }
        }
        foreach (var (number, frames) in _slid.OrderBy(pair => pair.Key))
            found.Add(new(ScummPictureSuspectKind.MovesOnFirstFrame, number, $"moved {frames} frames in a row on its costume's first frame"));
        return found;
    }

    /// <summary>In the room shown, visible and wearing a costume.</summary>
    private static bool Shown(ScummActorSnapshot actor, int? room) =>
        room is not null && actor.Room == room && actor.Visible == true && actor.Look.Costume is int costume && costume != 0;

    /// <summary>What decides the picture, as text: the room, the camera, the lines, the object states and each shown actor's place and frame.</summary>
    private static string Picture(ScummSession session)
    {
        var text = new System.Text.StringBuilder();
        text.Append(session.CurrentRoomId).Append('|').Append(session.CameraX).Append('|').Append(session.Messages.HaveMsg).Append('|')
            .Append(session.Messages.Log.Count).Append('|');
        foreach (var actor in session.Actors)
        {
            if (!Shown(actor, session.CurrentRoomId)) continue;
            text.Append(actor.Number).Append(':').Append(actor.X).Append(',').Append(actor.Y).Append(',').Append(actor.Look.Costume).Append(',')
                .Append(actor.Look.Facing).Append(',').Append(actor.Look.Frame).Append(',').Append(actor.Look.Elevation).Append(',');
            foreach (var position in actor.Look.Animation.CurrentPosition) text.Append(position).Append('.');
            text.Append(';');
        }
        text.Append('|');
        foreach (var (id, state) in session.Snapshot.ObjectStates.OrderBy(pair => pair.Key)) text.Append(id).Append('=').Append(state).Append(' ');
        return text.ToString();
    }
}
