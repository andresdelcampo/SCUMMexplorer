using SCUMMexplorer.Core.Rendering;

namespace SCUMMexplorer.Core.Formats;

/// <summary>
/// What the session knows about one actor (ScummVM <c>Actor</c>, small-header
/// v3 paths). A null field is unknown: an operand the preview could not read
/// touched it. <see cref="Estimate"/> says why known values may differ from
/// the game (a walk taken to end at once, or a value that follows a seeded pick).
/// </summary>
public sealed record LoomActorSnapshot(int Number, int? Room, int? X, int? Y, int? WalkBox, bool? Visible,
    bool? IgnoreBoxes, string? Unknown, string? Estimate)
{
    public bool PositionKnown => Room is not null && X is not null && Y is not null && WalkBox is not null && Visible is not null;

    /// <summary>How the actor looks: costume, elevation, facing, frame and limb state (M2b).</summary>
    public LoomActorLook Look { get; init; } = LoomActorLook.Initial;

    /// <summary>ScummVM <c>_moving</c>: nonzero while the actor walks or turns (M3a).</summary>
    public int Moving { get; init; }
}

/// <summary>
/// The actor's costume state (ScummVM <c>Actor</c> fields set by <c>actorOps</c>, <c>animateActor</c>,
/// <c>faceActor</c> and the walk): costume number, elevation, walk speed, the init/walk/stand/talk frames,
/// the 16-entry palette, the facing in degrees and the last started frame. <see cref="Animation"/> is a copy
/// of the limb state. A null value is unknown; <see cref="Estimate"/> says why a known value may differ.
/// </summary>
public sealed record LoomActorLook(int? Costume, int? Elevation, int? Facing, int? Frame,
    int InitFrame, int WalkFrame, int StandFrame, int TalkStartFrame, int TalkStopFrame,
    int WalkSpeedX, int WalkSpeedY, IReadOnlyList<byte> Palette, LoomCostumeAnimationState Animation, string? Estimate)
{
    /// <summary>Uncertainty in palette writes, independent of position, facing and movement.</summary>
    public string? PaletteEstimate { get; init; }

    public static LoomActorLook Initial { get; } = new(0, 0, 180, 0, 1, 2, 3, 4, 5, 8, 2, new byte[16],
        new LoomCostumeAnimationState(), null);

    /// <summary>ScummVM <c>newDirToOldDir</c> of the facing: 0 left, 1 right, 2 down, 3 up.</summary>
    public int? Direction => Facing is int facing ? LoomCostume.NewDirToOldDir(facing) : null;
}

/// <summary>
/// The actor table the executor and session share. Ports the ScummVM calls
/// that place actors (<c>initActor</c>, <c>putActor</c>, <c>showActor</c>,
/// <c>hideActor</c>, <c>adjustActorPos</c>, <c>startWalkActor</c>) over values
/// that may be unknown. Since M3a a walk in the current room is stepped frame by
/// frame (<c>walkActor</c>, see <see cref="StepWalks"/>); only a walk from an
/// unknown start is taken to end at its destination at once, labelled as an estimate.
/// </summary>
public sealed class LoomActorTable
{
    /// <summary>ScummVM <c>_numActors</c> for this release family; actor 0 is not valid for scripts.</summary>
    public const int Count = 13;
    /// <summary>ScummVM <c>kObjectClassPlayer</c>: <c>Actor::isPlayer</c> for v3.</summary>
    public const int PlayerClass = 23;

    private readonly Actor[] _actors = Enumerable.Range(0, Count).Select(number => new Actor(number)).ToArray();

    /// <summary>ScummVM <c>_egoPositioned</c>: set by <c>putActor</c> on the ego, cleared by <c>startScene</c>.</summary>
    public bool EgoPositioned { get; set; }

    /// <summary>
    /// How many times a script has put the ego at a point (<c>putActor</c>, <c>putActorAtObject</c>, a room entry),
    /// not counting <c>putActor()</c> re-placing it where it is. The app reads it to tell a change of scene.
    /// </summary>
    public int EgoPuts { get; private set; }

    public static bool IsValid(int number) => number is >= 1 and < Count;

    public IReadOnlyList<LoomActorSnapshot> Snapshot() => _actors.Skip(1).Select(actor => actor.ToSnapshot()).ToArray();

    public LoomActorSnapshot this[int number] => _actors[number].ToSnapshot();

    /// <summary>Every actor becomes unknown, for an operation whose actor number could not be read.</summary>
    public void ForgetAll(string reason)
    {
        foreach (var actor in _actors.Skip(1)) actor.Forget(reason);
    }

    public void Forget(int number, string reason) => _actors[number].Forget(reason);

    public void MarkEstimate(int number, string reason)
    {
        // A reason other than a walk's timing outlasts the walk (see WalkWaited).
        var actor = _actors[number];
        if (actor.Estimate is null || actor.EstimateIsWalk) actor.Estimate = reason;
        // A reason given after a walk started is about that walk, so its arrival keeps it.
        actor.ClearEstimateOnArrival = false;
    }

    /// <summary>Retains a possible palette write without changing placement or walk provenance.</summary>
    public void MarkPaletteEstimate(int number, string reason) => _actors[number].PaletteEstimate ??= reason;

    /// <summary><c>initActor(0)</c> (actorOps "init"): clears the last valid point and ignoreBoxes and resets the talk colour to 15; room and position stay.</summary>
    public void Init(int number)
    {
        var actor = _actors[number];
        actor.LastValidX = actor.LastValidY = 0;
        actor.IgnoreBoxes = false;
        actor.TalkColor = DefaultTalkColor;
        // Actor::initActor(0) for v3: elevation 0, walk speed (8, 2), frames 1-5; the facing and costume stay.
        actor.Elevation = 0;
        (actor.WalkSpeedX, actor.WalkSpeedY) = (8, 2);
        (actor.InitFrame, actor.WalkFrame, actor.StandFrame, actor.TalkStartFrame, actor.TalkStopFrame) = (1, 2, 3, 4, 5);
    }

    /// <summary>ScummVM <c>initActor</c>: every mode sets <c>_talkColor</c> to 15.</summary>
    public const int DefaultTalkColor = 15;

    /// <summary>The colour of the actor's talk text (<c>actorOps talkColor</c>, ScummVM <c>_talkColor</c>).</summary>
    public int TalkColor(int number) => _actors[number].TalkColor;

    public void SetTalkColor(int number, int color) => _actors[number].TalkColor = color;

    /// <summary><c>_ignoreBoxes</c> from actorOps, or cleared by <c>setClass</c> 0 on an actor (small header).</summary>
    public void SetIgnoreBoxes(int number, bool value) => _actors[number].IgnoreBoxes = value;

    /// <summary>
    /// <c>Actor::putActor(x, y, room)</c>. In the current room the actor is shown
    /// (or stays shown) at its position adjusted into the walk boxes; elsewhere it
    /// is hidden and keeps the raw position. Putting the ego sets <see cref="EgoPositioned"/>.
    /// </summary>
    public void Put(int number, int? x, int? y, int? room, Placement place, int? ego) =>
        Put(number, x, y, room, place, ego, replace: false);

    private void Put(int number, int? x, int? y, int? room, Placement place, int? ego, bool replace)
    {
        var actor = _actors[number];
        actor.X = x;
        actor.Y = y;
        actor.Room = room;
        actor.Estimate = null;
        if (ego == number)
        {
            EgoPositioned = true;
            if (!replace) EgoPuts++;
        }
        if (room is null)
        {
            actor.Visible = null;
            actor.WalkBox = null;
            actor.Unknown ??= "its room is unknown";
            return;
        }
        if (room == place.CurrentRoom)
        {
            // putActor on a walking actor: stopActorMoving and the stand frame, then adjustActorPos.
            if (actor.Visible == true && actor.Moving != 0) StartAnimation(actor, actor.StandFrame, place);
            AdjustPosition(actor, place, show: true);
            ShowCostume(actor, place);
        }
        else
        {
            // hideActor stops a walk.
            actor.Moving = 0;
            actor.Visible = false;
        }
    }

    /// <summary><c>putActor(x, y)</c>: keeps the actor's room.</summary>
    public void Put(int number, int? x, int? y, Placement place, int? ego) =>
        Put(number, x, y, _actors[number].Room, place, ego);

    /// <summary><c>putActor()</c>: re-places the actor where it is (actorOps ignoreBoxes/followBoxes).</summary>
    public void Replace(int number, Placement place, int? ego) =>
        Put(number, _actors[number].X, _actors[number].Y, _actors[number].Room, place, ego, replace: true);

    /// <summary>
    /// <c>o5_putActorInRoom</c>: sets the room only. Room 0 also runs
    /// <c>putActor(0, 0, 0)</c>. Visibility is not changed here; the next room
    /// entry hides or shows the actor.
    /// </summary>
    public void PutInRoom(int number, int? room, Placement place, int? ego)
    {
        if (room == 0)
        {
            Put(number, 0, 0, 0, place, ego);
            return;
        }
        _actors[number].Room = room;
        if (room is null) _actors[number].Unknown ??= "its room is unknown";
    }

    /// <summary>
    /// <c>setActorCostume</c> for old costumes: the limb state is reset, the palette becomes 0-15 and the
    /// costume needs its init frame. A visible actor is hidden and shown again, which re-adjusts its position
    /// into the walk boxes and starts the init frame (<c>showActor</c>). A null costume is unknown.
    /// </summary>
    public void CostumeChanged(int number, int? costume, Placement place)
    {
        var actor = _actors[number];
        actor.CostumeNeedsInit = true;
        actor.Costume = costume;
        actor.Animation.Reset();
        actor.LookEstimate = costume is null ? "its costume is unknown" : null;
        for (var i = 0; i < 16; i++) actor.Palette[i] = (byte)i;
        if (actor.Visible == false) return;
        if (actor.Visible is null)
        {
            actor.WalkBox = null;
            actor.Unknown ??= "a costume change may have re-placed it while its visibility is unknown";
            return;
        }
        AdjustPosition(actor, place, show: true);
        ShowCostume(actor, place);
    }

    /// <summary>actorOps <c>elevation</c> (ScummVM <c>setElevation</c>); null is unknown.</summary>
    public void SetElevation(int number, int? elevation) => _actors[number].Elevation = elevation;

    /// <summary>actorOps <c>walkSpeed</c> (<c>setActorWalkSpeed</c>).</summary>
    public void SetWalkSpeed(int number, int x, int y) => (_actors[number].WalkSpeedX, _actors[number].WalkSpeedY) = (x, y);

    /// <summary>actorOps <c>palette i, c</c>: costume colour i draws EGA colour c.</summary>
    public void SetPalette(int number, int index, int color)
    {
        if (index is >= 0 and < 16) _actors[number].Palette[index] = (byte)color;
    }

    /// <summary>actorOps <c>initAnimation</c>, <c>walkAnimation</c>, <c>standAnimation</c> and <c>talkAnimation</c> (start, stop).</summary>
    public void SetFrame(int number, string kind, int frame)
    {
        var actor = _actors[number];
        switch (kind)
        {
            case "init": actor.InitFrame = frame; break;
            case "walk": actor.WalkFrame = frame; break;
            case "stand": actor.StandFrame = frame; break;
            case "talkStart": actor.TalkStartFrame = frame; break;
            case "talkStop": actor.TalkStopFrame = frame; break;
        }
    }

    /// <summary>Reads costume data for the limb state (the session supplies it; tests may leave it unset).</summary>
    public Func<int, LoomCostume?>? CostumeSource { get; set; }

    private readonly Dictionary<int, LoomCostume?> _costumes = [];

    /// <summary>The costume data for an id, read once (null when it cannot be read).</summary>
    public LoomCostume? CostumeData(int id)
    {
        if (_costumes.TryGetValue(id, out var costume)) return costume;
        try
        {
            costume = CostumeSource?.Invoke(id);
        }
        catch (Exception error) when (error is InvalidDataException or NotSupportedException or IOException)
        {
            costume = null;
        }
        return _costumes[id] = costume;
    }

    private static bool InRoom(Actor actor, Placement place) => actor.Room is int room && room == place.CurrentRoom;

    /// <summary>
    /// ScummVM <c>Actor::startAnimActor</c> for v3: frames 56-60 redirect to the init, walk, stand, talk-start and
    /// talk-stop frames. Only an actor in the current room with a costume changes: the init frame resets the limbs,
    /// then <c>costumeDecodeData(frame)</c> starts the frame's animation for the actor's facing on every limb.
    /// </summary>
    public void StartAnimation(int number, int frame, Placement place) => StartAnimation(_actors[number], frame, place);

    private void StartAnimation(Actor actor, int frame, Placement place)
    {
        frame = frame switch
        {
            56 => actor.InitFrame,
            57 => actor.WalkFrame,
            58 => actor.StandFrame,
            59 => actor.TalkStartFrame,
            60 => actor.TalkStopFrame,
            _ => frame
        };
        if (!InRoom(actor, place) || actor.Costume == 0) return;
        if (actor.Costume is not int costumeId || actor.Facing is not int facing)
        {
            actor.Frame = frame;
            actor.LookEstimate ??= actor.Costume is null ? "its costume is unknown" : "its facing is unknown";
            return;
        }
        actor.Animation.AnimationCounter = 0;
        if (frame == actor.InitFrame) actor.Animation.Reset();
        CostumeData(costumeId)?.DecodeAnimation(actor.Animation, frame, LoomCostume.NewDirToOldDir(facing));
        actor.Frame = frame;
    }

    /// <summary>
    /// ScummVM <c>Actor::animateActor</c> for v3: <c>chore = 0x3F - (n &gt;&gt; 2) + 2</c>, <c>dir = n &amp; 3</c>.
    /// Chore 2 (0xFC-0xFF) starts the stand frame (in the current room); 3 (0xF8-0xFB) sets the direction;
    /// 4 (0xF4-0xF7) turns to it; any other value starts frame n. Returns what it did, for the trace.
    /// </summary>
    public string Animate(int number, int value, Placement place)
    {
        var actor = _actors[number];
        var chore = 0x3F - (value >> 2) + 2;
        var direction = LoomCostume.OldDirToNewDir(value & 3);
        switch (chore)
        {
            case 2:
                if (InRoom(actor, place))
                {
                    StartAnimation(actor, actor.StandFrame, place);
                    actor.Moving = 0;
                }
                return "stop: stand frame";
            case 3:
                if (InRoom(actor, place)) actor.Moving &= ~MoveTurn;
                SetDirection(actor, direction, place);
                return $"face {direction}";
            case 4:
                if (InRoom(actor, place)) TurnTo(actor, direction);
                return $"turn to {direction}";
            default:
                StartAnimation(actor, value, place);
                return $"frame {value}";
        }
    }

    /// <summary>
    /// ScummVM <c>Actor::setDirection</c>: the new facing, and for an actor in the current room with a costume,
    /// each limb whose animation's direction differs restarts that animation's frame for the new direction.
    /// </summary>
    public void SetDirection(int number, int facing, Placement place) => SetDirection(_actors[number], facing, place);

    private void SetDirection(Actor actor, int facing, Placement place)
    {
        facing = ((facing % 360) + 360) % 360;
        if (actor.Facing == facing) return;
        actor.Facing = facing;
        if (actor.Costume == 0 || !InRoom(actor, place)) return;
        if (actor.Costume is not int costumeId || CostumeData(costumeId) is not { } costume) return;
        var direction = LoomCostume.NewDirToOldDir(facing);
        for (var limb = 0; limb < 16; limb++)
        {
            var animation = actor.Animation.Animation[limb];
            if (animation == LoomCostumeAnimationState.None || (animation & 3) == direction) continue;
            costume.DecodeAnimation(actor.Animation, animation >> 2, direction, (uint)(0x8000 >> limb));
        }
    }

    /// <summary>
    /// ScummVM <c>turnToDirection</c> for v3: the target facing, and <c>_moving = MF_TURN</c> (which also ends a walk).
    /// <see cref="StepWalks"/> then turns the actor one step a frame (<c>updateActorDirection</c>). -1 does nothing.
    /// </summary>
    private static void TurnTo(Actor actor, int facing)
    {
        if (facing == -1) return;
        actor.TargetFacing = ((facing % 360) + 360) % 360;
        actor.Moving = MoveTurn;
    }

    /// <summary>ScummVM <c>kBoxXFlip</c>/<c>kBoxYFlip</c>, and the v3 class numbers of XFlip (19) and YFlip (18) after <c>putClass</c>'s translation.</summary>
    private const byte BoxXFlip = 0x08, BoxYFlip = 0x10;
    private const int ClassXFlip = 19, ClassYFlip = 18;
    /// <summary>ScummVM's <c>0x400</c> mark on <c>remapDirection</c>'s result: turn towards it one step at a time.</summary>
    private const int Interpolate = 0x400;

    /// <summary>
    /// ScummVM <c>Actor::remapDirection</c> for v3: the actor's box flags flip the direction (0x08 x, 0x10 y, or the
    /// actor's XFlip/YFlip class) and the low 3 bits lock it: 1 left or right, 2 up or down (walking: by the leg's
    /// sign, flipped with the direction; standing: 90 or 0 stays, anything else is 270 or 180), 3-6 a fixed 270, 90,
    /// 0, 180. A locked result is taken at once; any other carries <see cref="Interpolate"/>. v3 applies this with or
    /// without ignoreBoxes. <c>_extraBoxFlags</c> are not modelled (no decoded <c>setBoxFlags</c> in LOOM sets 0xC000
    /// values; see the script map).
    /// </summary>
    private static int RemapDirection(Actor actor, int direction, bool walking, Placement place)
    {
        var flags = actor.WalkBox is int box && place.Boxes is not null && place.BoxesUnknown is null ? place.Boxes.GetFlags(box) : (byte)0;
        var classes = place.Classes is not null && place.Classes.TryGetValue(actor.Number, out var bits) ? bits : 0u;
        var flipX = actor.DeltaXFactor > 0;
        var flipY = actor.DeltaYFactor > 0;
        if ((flags & BoxXFlip) != 0 || LoomObjectClasses.Has(classes, ClassXFlip))
        {
            direction = 360 - direction;
            flipX = !flipX;
        }
        if ((flags & BoxYFlip) != 0 || LoomObjectClasses.Has(classes, ClassYFlip))
        {
            direction = 180 - direction;
            flipY = !flipY;
        }
        switch (flags & 7)
        {
            case 1: return walking ? flipX ? 90 : 270 : direction == 90 ? 90 : 270;
            case 2: return walking ? flipY ? 180 : 0 : direction == 0 ? 0 : 180;
            case 3: return 270;
            case 4: return 90;
            case 5: return 0;
            case 6: return 180;
        }
        return (((direction % 360) + 360) % 360) | Interpolate;
    }

    /// <summary>ScummVM <c>actorTurnInterpolateTable</c>: the next old direction, indexed by target | (current &lt;&lt; 2).</summary>
    private static readonly int[] TurnInterpolation = [0, 2, 2, 3, 2, 1, 2, 3, 0, 1, 2, 1, 0, 1, 0, 3];

    /// <summary>ScummVM <c>Actor::updateActorDirection</c>: the next facing on the way to the target facing.</summary>
    private static int UpdateActorDirection(Actor actor, bool walking, Placement place)
    {
        var direction = RemapDirection(actor, actor.TargetFacing, walking, place);
        if ((direction & Interpolate) == 0) return direction;
        var facing = actor.Facing ?? 180;
        return LoomCostume.OldDirToNewDir(TurnInterpolation[LoomCostume.NewDirToOldDir(direction & 0x3FF) |
                                                            (LoomCostume.NewDirToOldDir(facing) << 2)]);
    }

    /// <summary>
    /// ScummVM <c>Actor::faceToObject</c> for v3: right or left of the target, or down or up when the x distance
    /// is under half the target's width; only for an actor in the current room.
    /// </summary>
    public void FaceTo(int number, int x, int y, int width, Placement place)
    {
        var actor = _actors[number];
        if (!InRoom(actor, place) || actor.X is not int ax || actor.Y is not int ay) return;
        var direction = ax < x ? 1 : 0;
        if (Math.Abs(ax - x) < width / 2) direction = ay > y ? 3 : 2;
        TurnTo(actor, LoomCostume.OldDirToNewDir(direction));
    }

    /// <summary>
    /// <c>runActorTalkScript</c> without a talk script: the talk-start frame when a message starts for the actor,
    /// the talk-stop frame at <c>stopTalk</c>; only in the current room and when the frame differs.
    /// </summary>
    public void TalkAnimation(int number, bool start, Placement place)
    {
        if (!IsValid(number)) return;
        var actor = _actors[number];
        var frame = start ? actor.TalkStartFrame : actor.TalkStopFrame;
        if (!InRoom(actor, place) || actor.Frame == frame) return;
        StartAnimation(actor, frame, place);
    }

    /// <summary>
    /// The end of <c>processActors</c> for one frame: <c>animateCostume</c> (animation speed 0) steps the limbs of
    /// every actor in the current room with a costume. Returns the actors whose drawn image changed.
    /// </summary>
    public IReadOnlyList<int> AnimateCostumes(Placement place)
    {
        var changed = new List<int>();
        foreach (var actor in _actors.Skip(1))
        {
            if (!InRoom(actor, place) || actor.Costume is not int costumeId || costumeId == 0) continue;
            if (CostumeData(costumeId)?.IncreaseAnimations(actor.Animation) == true) changed.Add(actor.Number);
        }
        return changed;
    }

    /// <summary><c>showActor</c> (v3): a costume that needs init starts its init frame.</summary>
    private void ShowCostume(Actor actor, Placement place)
    {
        if (!actor.CostumeNeedsInit || actor.Costume == 0) return;
        StartAnimation(actor, actor.InitFrame, place);
        actor.CostumeNeedsInit = false;
    }

    /// <summary>
    /// ScummVM <c>Actor::startWalkActor</c> for v3. Outside the current room the actor is put at the destination
    /// exactly (and faces <paramref name="direction"/> unless it is -1). In the current room the destination is kept
    /// in its destination box or adjusted into the nearest box, and the walk starts: <see cref="StepWalks"/> moves the
    /// actor along the box path frame by frame (<c>walkActor</c>). An actor already there only turns to the
    /// direction. When the start point, box or facing is unknown, the walk cannot be stepped, and it is taken to end
    /// at once (an estimate, as before M3a). <paramref name="atOnce"/> asks for that too: a catalog visit's assumed
    /// arrival, which shows the state after the arrival rather than playing it.
    /// </summary>
    public void Walk(int number, int? x, int? y, Placement place, string label, int direction = -1, bool atOnce = false)
    {
        var actor = _actors[number];
        if (actor.Room is null || x is null || y is null)
        {
            actor.Forget(x is null || y is null ? $"{label} has an unknown destination" : "its room is unknown");
            return;
        }
        if (actor.Room != place.CurrentRoom)
        {
            actor.X = x;
            actor.Y = y;
            if (direction != -1) actor.Facing = ((direction % 360) + 360) % 360;
            return;
        }
        if (actor.IgnoreBoxes is null)
        {
            actor.Forget($"{label}: whether it ignores walk boxes is unknown");
            return;
        }
        if (atOnce || actor.X is not int startX || actor.Y is not int startY || actor.Facing is null ||
            actor.IgnoreBoxes == false && (actor.WalkBox is null || place.Boxes is null || place.BoxesUnknown is not null))
        {
            WalkAtOnce(actor, x.Value, y.Value, place, label);
            return;
        }
        int endX = x.Value, endY = y.Value, endBox;
        if (actor.IgnoreBoxes == true)
        {
            endBox = LoomWalkBoxSet.InvalidBox;
            actor.WalkBox = LoomWalkBoxSet.InvalidBox;
        }
        else
        {
            if (actor.DestBox is int destination && place.Boxes!.Contains(destination, endX, endY))
                endBox = destination;
            else
            {
                var adjusted = AdjustXY(actor, endX, endY, place);
                if (adjusted is null)
                {
                    actor.Forget($"{label}: the walk's end could not be placed in a walk box");
                    return;
                }
                (endX, endY, endBox) = (adjusted.Value.X, adjusted.Value.Y, adjusted.Value.Box);
            }
            if (actor.Moving != 0 && actor.DestDir == direction && actor.DestX == endX && actor.DestY == endY) return;
        }
        var fromAnywhere = actor.IgnoreBoxes == true || place.Boxes is { } boxes &&
            Enumerable.Range(0, boxes.Count).All(box => box == endBox || boxes.GetNextBox(box, endBox) >= 0);
        if (startX == endX && startY == endY)
        {
            // Already there: from any other start the walk would have ended here too.
            if (fromAnywhere && !actor.EstimateIsWalk) actor.Estimate = null;
            if (direction != actor.Facing) TurnTo(actor, direction);
            return;
        }
        (actor.DestX, actor.DestY, actor.DestBox, actor.DestDir) = (endX, endY, endBox, direction);
        actor.Point3 = null;
        actor.CurBox = actor.WalkBox ?? LoomWalkBoxSet.InvalidBox;
        actor.Moving = (actor.Moving & MoveInLeg) | MoveNewLeg;
        // When every box of the room routes to the end box (or the actor ignores boxes), the
        // walk ends there wherever it started, so on arrival an estimate about the start
        // (an assumed arrival, a seeded pick) no longer applies to the position.
        actor.ClearEstimateOnArrival = actor.Estimate is not null && fromAnywhere;
    }

    /// <summary>ScummVM <c>_moving</c> flags.</summary>
    public const int MoveNewLeg = 1, MoveInLeg = 2, MoveTurn = 4, MoveLastLeg = 8;

    /// <summary>
    /// ScummVM <c>walkActors</c> for one frame: <c>Actor_v3::walkActor</c> for every actor in the current room, in
    /// actor order. A walk goes box by box (<c>getNextBox</c>, <c>findPathTowardsOld</c>'s gates), each leg with
    /// <c>calcMovementFactor</c> and <c>actorWalkStep</c> at the actor's walk speed; the walk frame starts at each
    /// leg, the stand frame at the end, then the actor turns to the walk's direction. A turn goes one step a frame.
    /// Returns the actors that walked or turned.
    /// </summary>
    public IReadOnlyList<int> StepWalks(Placement place)
    {
        var moved = new List<int>();
        foreach (var actor in _actors.Skip(1))
        {
            if (!InRoom(actor, place) || actor.Moving == 0) continue;
            if (actor.X is null || actor.Y is null)
            {
                actor.Moving = 0;
                continue;
            }
            WalkActor(actor, place);
            moved.Add(actor.Number);
        }
        return moved;
    }

    private void WalkActor(Actor actor, Placement place)
    {
        if ((actor.Moving & MoveNewLeg) == 0)
        {
            if ((actor.Moving & MoveInLeg) != 0 && ActorWalkStep(actor, place)) return;
            if ((actor.Moving & MoveLastLeg) != 0)
            {
                actor.Moving = 0;
                if (actor.ClearEstimateOnArrival && actor.X == actor.DestX && actor.Y == actor.DestY) actor.Estimate = null;
                actor.ClearEstimateOnArrival = false;
                StartAnimation(actor, actor.StandFrame, place);
                if (actor.TargetFacing != actor.DestDir) TurnTo(actor, actor.DestDir);
                return;
            }
            if ((actor.Moving & MoveTurn) != 0)
            {
                var next = UpdateActorDirection(actor, false, place);
                if (actor.Facing != next) SetDirection(actor, next, place);
                else actor.Moving = 0;
                return;
            }
            if (actor.Point3 is { } point3)
            {
                if (CalcMovementFactor(actor, point3, place))
                {
                    actor.Point3 = null;
                    return;
                }
                actor.Point3 = null;
            }
            actor.WalkBox = actor.CurBox;
            actor.Moving &= MoveInLeg;
        }
        actor.Moving &= ~MoveNewLeg;
        while (true)
        {
            if (actor.WalkBox is not int box || box == LoomWalkBoxSet.InvalidBox)
            {
                actor.WalkBox = actor.CurBox = actor.DestBox ?? LoomWalkBoxSet.InvalidBox;
                break;
            }
            if (box == actor.DestBox) break;
            var nextBox = place.Boxes?.GetNextBox(box, actor.DestBox ?? LoomWalkBoxSet.InvalidBox) ?? -1;
            if (nextBox < 0)
            {
                actor.Moving |= MoveLastLeg;
                return;
            }
            actor.CurBox = nextBox;
            var (p2, p3) = FindPathTowardsOld(actor, box, nextBox, actor.DestBox ?? LoomWalkBoxSet.InvalidBox, place);
            if (p2 is null && p3 is null) break;
            if (p2 is { } gate2 && CalcMovementFactor(actor, gate2, place))
            {
                actor.Point3 = p3;
                return;
            }
            if (p3 is { } gate3 && CalcMovementFactor(actor, gate3, place)) return;
            actor.WalkBox = actor.CurBox;
        }
        actor.Moving |= MoveLastLeg;
        CalcMovementFactor(actor, new LoomBoxPoint(actor.DestX, actor.DestY), place);
    }

    /// <summary>
    /// ScummVM <c>Actor_v3::findPathTowardsOld</c>: the gate between the actor's box and the next one. Into the final
    /// box, no gate point is needed when the straight line to the destination passes between both gate lines.
    /// </summary>
    private static (LoomBoxPoint? P2, LoomBoxPoint? P3) FindPathTowardsOld(Actor actor, int box1, int box2, int finalBox, Placement place)
    {
        if (place.Boxes?.Coordinates(box1) is not { } first || place.Boxes.Coordinates(box2) is not { } second) return (null, null);
        var (a0, a1, b0, b1) = LoomWalkBoxSet.GetGates(first, second);
        var pos = new LoomBoxPoint(actor.X!.Value, actor.Y!.Value);
        var dest = new LoomBoxPoint(actor.DestX, actor.DestY);
        if (box2 == finalBox &&
            LoomWalkBoxSet.CompareSlope(pos, dest, a0) != LoomWalkBoxSet.CompareSlope(pos, dest, b0) &&
            LoomWalkBoxSet.CompareSlope(pos, dest, a1) != LoomWalkBoxSet.CompareSlope(pos, dest, b1))
            return (null, null);
        var p3 = LoomWalkBoxSet.ClosestPointOnLine(a1, b1, pos);
        LoomBoxPoint? p2 = null;
        if (LoomWalkBoxSet.CompareSlope(pos, p3, a0) == LoomWalkBoxSet.CompareSlope(pos, p3, b0))
            p2 = LoomWalkBoxSet.ClosestPointOnLine(a0, b0, pos);
        return (p2, p3);
    }

    /// <summary>
    /// ScummVM <c>Actor_v3::calcMovementFactor</c> (v3): a leg to <paramref name="next"/>, unless it is within one step
    /// and not the last leg. Along x the step is the x speed, or y speed + 1 on a leg more than twice as steep (in
    /// steps); the leg's facing is up or down when |dy| * 3 &gt; |dx| (<c>_facingXYratio</c>). Takes the first step.
    /// </summary>
    private bool CalcMovementFactor(Actor actor, LoomBoxPoint next, Placement place)
    {
        var (x, y) = (actor.X!.Value, actor.Y!.Value);
        if (x == next.X && y == next.Y) return false;
        var diffX = next.X - x;
        var diffY = next.Y - y;
        var speedX = Math.Max(1, actor.WalkSpeedX);
        var speedY = Math.Max(1, actor.WalkSpeedY);
        if ((actor.Moving & MoveLastLeg) == 0 && speedX > Math.Abs(diffX) && speedY > Math.Abs(diffY)) return false;
        actor.StepX = ((Math.Abs(diffY) / speedY) >> 1) > Math.Abs(diffX) / speedX ? speedY + 1 : speedX;
        actor.StepThreshold = Math.Max(Math.Abs(diffY) / speedY, Math.Abs(diffX) / actor.StepX);
        actor.DeltaXFactor = diffX < 0 ? -actor.StepX : actor.StepX;
        actor.DeltaYFactor = diffY < 0 ? -speedY : speedY;
        actor.XFrac = actor.XAdd = (ushort)(diffX / actor.DeltaXFactor);
        actor.YFrac = actor.YAdd = (ushort)(diffY / actor.DeltaYFactor);
        actor.Next = next;
        actor.TargetFacing = LoomCostume.OldDirToNewDir(Math.Abs(diffY) * 3 > Math.Abs(diffX)
            ? 3 - (diffY >= 0 ? 1 : 0)
            : diffX >= 0 ? 1 : 0);
        return ActorWalkStep(actor, place);
    }

    /// <summary>
    /// ScummVM <c>Actor_v3::actorWalkStep</c>: a leg's first step (or a change of facing) only turns the actor and
    /// starts the walk frame; later steps snap to the leg's end within one step, change box when inside the next
    /// one, and add the x and y steps by Bresenham counters (<c>xfrac</c>/<c>yfrac</c> against the step threshold).
    /// False once the leg's end is reached.
    /// </summary>
    private bool ActorWalkStep(Actor actor, Placement place)
    {
        var nextFacing = UpdateActorDirection(actor, true, place);
        if ((actor.Moving & MoveInLeg) == 0 || actor.Facing != nextFacing)
        {
            if (actor.WalkFrame != actor.Frame || actor.Facing != nextFacing)
            {
                SetDirection(actor, nextFacing, place);
                StartAnimation(actor, actor.WalkFrame, place);
            }
            actor.Moving |= MoveInLeg;
            return true;
        }
        var (x, y) = (actor.X!.Value, actor.Y!.Value);
        var next = actor.Next;
        var speedY = Math.Max(1, actor.WalkSpeedY);
        if (next.X - actor.StepX <= x && next.X + actor.StepX >= x) x = next.X;
        if (next.Y - speedY <= y && next.Y + speedY >= y) y = next.Y;
        (actor.X, actor.Y) = (x, y);
        if (actor.WalkBox != actor.CurBox && place.Boxes?.Contains(actor.CurBox, x, y) == true) actor.WalkBox = actor.CurBox;
        if (x == next.X && y == next.Y)
        {
            actor.Moving &= ~MoveInLeg;
            return false;
        }
        actor.XFrac = (ushort)(actor.XFrac + actor.XAdd);
        if (actor.XFrac >= actor.StepThreshold)
        {
            if (x != next.X) actor.X = x + actor.DeltaXFactor;
            actor.XFrac = (ushort)(actor.XFrac - actor.StepThreshold);
        }
        actor.YFrac = (ushort)(actor.YFrac + actor.YAdd);
        if (actor.YFrac >= actor.StepThreshold)
        {
            if (y != next.Y) actor.Y = y + actor.DeltaYFactor;
            actor.YFrac = (ushort)(actor.YFrac - actor.StepThreshold);
        }
        return true;
    }

    /// <summary>ScummVM <c>_moving</c> of an actor (<c>o5_getActorMoving</c>, <c>o5_wait</c> for an actor).</summary>
    public int Moving(int number) => _actors[number].Moving;

    /// <summary>
    /// The pre-M3a estimate for a walk that cannot be stepped (an unknown start point, box or facing): the
    /// destination is kept in its destination box or adjusted into the nearest box, and the walk is taken to end
    /// there at once. When the box matrix has no route, the end point is unknown.
    /// </summary>
    private void WalkAtOnce(Actor actor, int x, int y, Placement place, string label)
    {
        if (actor.IgnoreBoxes is null)
        {
            actor.Forget($"{label}: whether it ignores walk boxes is unknown");
            return;
        }
        int endX = x, endY = y, endBox;
        if (actor.IgnoreBoxes == true)
            endBox = LoomWalkBoxSet.InvalidBox;
        else
        {
            if (place.Boxes is null || place.BoxesUnknown is not null)
            {
                actor.Forget($"{label}: the room's walk boxes are {(place.Boxes is null ? "not known" : place.BoxesUnknown)}");
                return;
            }
            if (actor.DestBox is int destination && place.Boxes.Contains(destination, endX, endY))
                endBox = destination;
            else
            {
                var adjusted = AdjustXY(actor, endX, endY, place);
                if (adjusted is null)
                {
                    actor.Forget($"{label}: the walk's end could not be placed in a walk box");
                    return;
                }
                (endX, endY, endBox) = (adjusted.Value.X, adjusted.Value.Y, adjusted.Value.Box);
            }
            if (actor.WalkBox is not int from)
            {
                actor.Forget($"{label} starts from an unknown walk box, so its route cannot be checked");
                return;
            }
            if (from != endBox && place.Boxes.GetNextBox(from, endBox) < 0)
            {
                actor.Forget($"{label}: the box matrix has no route from box {from} to box {endBox}, so where the walk stops is not modelled");
                return;
            }
        }
        // When every box of the room has a route to the end box (or the actor ignores
        // boxes), the walk ends there wherever it started, so an estimate about the
        // start (an assumed arrival, a seeded pick) no longer applies to the end.
        var fromAnywhere = actor.IgnoreBoxes == true || (place.Boxes is { } boxes &&
            Enumerable.Range(0, boxes.Count).All(box => box == endBox || boxes.GetNextBox(box, endBox) >= 0));
        if (actor.X == endX && actor.Y == endY && !(fromAnywhere && actor.Estimate is not null && !actor.EstimateIsWalk)) return;
        if (actor.X is int startX && actor.Y is int startY && (startX != endX || startY != endY))
        {
            // The walk's last step faces along its last leg (calcMovementFactor, facing ratio 1), then
            // walkActor starts the stand frame. The preview takes the whole walk as one leg.
            var (dx, dy) = (endX - startX, endY - startY);
            var direction = Math.Abs(dy) > Math.Abs(dx) ? dy >= 0 ? 2 : 3 : dx >= 0 ? 1 : 0;
            SetDirection(actor, LoomCostume.OldDirToNewDir(direction), place);
            StartAnimation(actor, actor.StandFrame, place);
            actor.LookEstimate = $"after {label}, it faces {LoomCostume.OldDirToNewDir(direction)} along the whole walk (the path's last leg is not simulated)";
        }
        actor.X = endX;
        actor.Y = endY;
        actor.WalkBox = endBox;
        actor.DestBox = endBox;
        actor.Moving = 0;
        if (fromAnywhere && !actor.EstimateIsWalk) actor.Estimate = null;
        if (actor.Estimate is null)
        {
            actor.Estimate = $"{label} is taken to end at ({endX}, {endY}), box {BoxName(endBox)}, at once (walk timing and path are not simulated)";
            actor.EstimateIsWalk = true;
        }
    }

    /// <summary>
    /// <c>o5_wait</c> for an actor: the script goes on only once the actor has
    /// stopped walking, so the walk has reached the end <see cref="Walk"/> gave
    /// it. When that walk was the only reason the position was an estimate, it
    /// is no longer one; only how long the walk took is not simulated. Returns
    /// true when an estimate was cleared.
    /// </summary>
    public bool WalkWaited(int number)
    {
        var actor = _actors[number];
        if (!actor.EstimateIsWalk || actor.X is null) return false;
        actor.Estimate = null;
        actor.EstimateIsWalk = false;
        return true;
    }

    /// <summary>
    /// <c>Actor::adjustXYToBeInBox</c> for a point, as <c>getObjActToObjActDist</c>
    /// does for an object's position; null when the actor's box mode, the room's
    /// boxes or the actor's last valid point are unknown.
    /// </summary>
    public (int X, int Y)? AdjustPoint(int number, int x, int y, Placement place)
    {
        var actor = _actors[number];
        if (actor.IgnoreBoxes is null) return null;
        if (actor.IgnoreBoxes == true) return (x, y);
        if (place.Boxes is null || place.BoxesUnknown is not null) return null;
        var adjusted = AdjustXY(actor, x, y, place);
        return adjusted is null ? null : (adjusted.Value.X, adjusted.Value.Y);
    }

    /// <summary><c>startScene</c>: hides every actor and clears <see cref="EgoPositioned"/>.</summary>
    public void HideAll()
    {
        foreach (var actor in _actors.Skip(1))
        {
            // hideActor stops a walk (its stand frame is started in the room being left).
            actor.Moving = 0;
            actor.Visible = false;
        }
    }

    /// <summary><c>showActors</c>: shows every actor whose room is the new room, adjusted into its boxes.</summary>
    public void ShowActorsIn(Placement place)
    {
        foreach (var actor in _actors.Skip(1))
        {
            if (actor.Room is null)
            {
                actor.Visible = null;
                actor.WalkBox = null;
                continue;
            }
            if (actor.Room == place.CurrentRoom && actor.Visible != true)
            {
                AdjustPosition(actor, place, show: true);
                ShowCostume(actor, place);
            }
        }
        EgoPositioned = false;
    }

    /// <summary>
    /// <c>adjustActorPos</c> (via <c>showActor</c>): position and box from <c>adjustXYToBeInBox</c>; on a box whose flags
    /// lock the direction (<c>flags &amp; 7</c>), the actor turns to its facing as <see cref="RemapDirection"/> gives it.
    /// </summary>
    private void AdjustPosition(Actor actor, Placement place, bool show)
    {
        AdjustXYAndBox(actor, place, show);
        // adjustActorPos: stopActorMoving before the turn below.
        actor.Moving = 0;
        if (actor.WalkBox is int box && box != LoomWalkBoxSet.InvalidBox && place.Boxes is { } boxes &&
            place.BoxesUnknown is null && (boxes.GetFlags(box) & 7) != 0 && actor.Facing is int facing && InRoom(actor, place))
            TurnTo(actor, facing);
    }

    private static void AdjustXYAndBox(Actor actor, Placement place, bool show)
    {
        if (show) actor.Visible = true;
        if (actor.X is not int x || actor.Y is not int y || actor.IgnoreBoxes is null)
        {
            actor.WalkBox = null;
            actor.DestBox = null;
            actor.Unknown ??= "its position is unknown";
            return;
        }
        if (actor.IgnoreBoxes == true)
        {
            actor.WalkBox = actor.DestBox = LoomWalkBoxSet.InvalidBox;
            return;
        }
        if (place.Boxes is null || place.BoxesUnknown is not null)
        {
            actor.WalkBox = actor.DestBox = null;
            actor.Unknown ??= $"the room's walk boxes are {(place.Boxes is null ? "not known" : place.BoxesUnknown)}";
            return;
        }
        var adjusted = AdjustXY(actor, x, y, place);
        if (adjusted is null)
        {
            actor.WalkBox = actor.DestBox = null;
            actor.Unknown ??= "it could not be placed in a walk box";
            return;
        }
        actor.X = adjusted.Value.X;
        actor.Y = adjusted.Value.Y;
        actor.WalkBox = actor.DestBox = adjusted.Value.Box;
    }

    /// <summary><c>Actor::adjustXYToBeInBox</c> with the actor's last valid point (null when that point matters and is unknown).</summary>
    private static BoxAdjustment? AdjustXY(Actor actor, int x, int y, Placement place)
    {
        var isPlayer = place.Classes is not null && place.Classes.TryGetValue(actor.Number, out var bits) &&
                       LoomObjectClasses.Has(bits, PlayerClass);
        var result = place.Boxes!.Adjust(x, y, isPlayer, actor.LastValidX ?? 0, actor.LastValidY ?? 0);
        // The last valid point comes back only when no box was within reach.
        if (result.Box == LoomWalkBoxSet.InvalidBox && actor.LastValidX is null) return null;
        actor.LastValidX = result.LastValidX;
        actor.LastValidY = result.LastValidY;
        return result;
    }

    private static string BoxName(int box) => box == LoomWalkBoxSet.InvalidBox ? "none (255)" : box.ToString();

    /// <summary>The room the actor calls are made in: its number, its walk boxes, and class bits for <c>isPlayer</c>.</summary>
    public readonly record struct Placement(int? CurrentRoom, LoomWalkBoxSet? Boxes, string? BoxesUnknown,
        IReadOnlyDictionary<int, uint>? Classes);

    private sealed class Actor(int number)
    {
        public int Number { get; } = number;
        // initActor(-1): room 0, position (0,0), walk box 0, invisible, last valid point (0,0).
        public int? Room { get; set; } = 0;
        public int? X { get; set; } = 0;
        public int? Y { get; set; } = 0;
        public int? WalkBox { get; set; } = 0;
        public int? DestBox { get; set; } = 0;
        public bool? Visible { get; set; } = false;
        public bool? IgnoreBoxes { get; set; } = false;
        public int TalkColor { get; set; } = DefaultTalkColor;
        public int? LastValidX { get; set; } = 0;
        public int? LastValidY { get; set; } = 0;
        public string? Unknown { get; set; }
        // Costume state (initActor(-1), Actor::initActor and setActorCostume for v3).
        public int? Costume { get; set; } = 0;
        public int? Elevation { get; set; } = 0;
        public int? Facing { get; set; } = 180;
        public int? Frame { get; set; } = 0;
        public int InitFrame { get; set; } = 1;
        public int WalkFrame { get; set; } = 2;
        public int StandFrame { get; set; } = 3;
        public int TalkStartFrame { get; set; } = 4;
        public int TalkStopFrame { get; set; } = 5;
        public int WalkSpeedX { get; set; } = 8;
        public int WalkSpeedY { get; set; } = 2;
        // ScummVM _moving and _walkdata (Actor_v3): destination, boxes, the current leg and its step counters.
        public int Moving { get; set; }
        public int DestX { get; set; }
        public int DestY { get; set; }
        public int DestDir { get; set; }
        public int CurBox { get; set; }
        public LoomBoxPoint Next { get; set; }
        public LoomBoxPoint? Point3 { get; set; }
        public int StepX { get; set; } = 1;
        public int StepThreshold { get; set; }
        public int DeltaXFactor { get; set; }
        public int DeltaYFactor { get; set; }
        public ushort XFrac { get; set; }
        public ushort YFrac { get; set; }
        public ushort XAdd { get; set; }
        public ushort YAdd { get; set; }
        public int TargetFacing { get; set; }
        /// <summary>True when the walk's arrival makes the position independent of an estimated start (see <see cref="Walk"/>).</summary>
        public bool ClearEstimateOnArrival { get; set; }
        public byte[] Palette { get; } = new byte[16];
        public bool CostumeNeedsInit { get; set; }
        public LoomCostumeAnimationState Animation { get; } = new();
        public string? LookEstimate { get; set; }
        public string? PaletteEstimate { get; set; }
        public string? Estimate
        {
            get => _estimate;
            set
            {
                _estimate = value;
                EstimateIsWalk = false;
            }
        }
        private string? _estimate;
        /// <summary>True when <see cref="Estimate"/> says only that a walk was taken to end at once.</summary>
        public bool EstimateIsWalk { get; set; }

        public void Forget(string reason)
        {
            Room = X = Y = WalkBox = DestBox = LastValidX = LastValidY = null;
            Visible = null;
            Moving = 0;
            Unknown = reason;
            Estimate = null;
        }

        public LoomActorSnapshot ToSnapshot() =>
            new(Number, Room, X, Y, WalkBox, Visible, IgnoreBoxes,
                Room is null || X is null || Y is null || WalkBox is null || Visible is null ? Unknown ?? "not known" : null,
                Estimate)
            {
                Moving = Moving,
                Look = new LoomActorLook(Costume, Elevation, Facing, Frame, InitFrame, WalkFrame, StandFrame,
                    TalkStartFrame, TalkStopFrame, WalkSpeedX, WalkSpeedY, Palette.ToArray(), Animation.Clone(), LookEstimate)
                { PaletteEstimate = PaletteEstimate }
            };
    }
}
