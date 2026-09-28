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
}

/// <summary>
/// The actor table the executor and session share. Ports the ScummVM calls
/// that place actors (<c>initActor</c>, <c>putActor</c>, <c>showActor</c>,
/// <c>hideActor</c>, <c>adjustActorPos</c>, <c>startWalkActor</c>) over values
/// that may be unknown. Walks are not stepped: a walk in the current room is
/// taken to end at its destination at once, labelled as an estimate.
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
    }

    /// <summary><c>initActor(0)</c> (actorOps "init"): clears the last valid point and ignoreBoxes and resets the talk colour to 15; room and position stay.</summary>
    public void Init(int number)
    {
        var actor = _actors[number];
        actor.LastValidX = actor.LastValidY = 0;
        actor.IgnoreBoxes = false;
        actor.TalkColor = DefaultTalkColor;
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
    public void Put(int number, int? x, int? y, int? room, Placement place, int? ego)
    {
        var actor = _actors[number];
        actor.X = x;
        actor.Y = y;
        actor.Room = room;
        actor.Estimate = null;
        if (ego == number) EgoPositioned = true;
        if (room is null)
        {
            actor.Visible = null;
            actor.WalkBox = null;
            actor.Unknown ??= "its room is unknown";
            return;
        }
        if (room == place.CurrentRoom) AdjustPosition(actor, place, show: true);
        else actor.Visible = false;
    }

    /// <summary><c>putActor(x, y)</c>: keeps the actor's room.</summary>
    public void Put(int number, int? x, int? y, Placement place, int? ego) =>
        Put(number, x, y, _actors[number].Room, place, ego);

    /// <summary><c>putActor()</c>: re-places the actor where it is (actorOps ignoreBoxes/followBoxes).</summary>
    public void Replace(int number, Placement place, int? ego) =>
        Put(number, _actors[number].X, _actors[number].Y, _actors[number].Room, place, ego);

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
    /// <c>setActorCostume</c> for old costumes: a visible actor is hidden and
    /// shown again, which re-adjusts its position into the walk boxes.
    /// </summary>
    public void CostumeChanged(int number, Placement place)
    {
        var actor = _actors[number];
        if (actor.Visible == false) return;
        if (actor.Visible is null)
        {
            actor.WalkBox = null;
            actor.Unknown ??= "a costume change may have re-placed it while its visibility is unknown";
            return;
        }
        AdjustPosition(actor, place, show: true);
    }

    /// <summary>
    /// <c>Actor::startWalkActor</c> for v3. Outside the current room the actor is
    /// put at the destination exactly. In the current room the destination is
    /// kept in its destination box or adjusted into the nearest box, and the walk
    /// is taken to end there at once (an estimate: timing and the path are not
    /// simulated). When the box matrix has no route, the end point is unknown.
    /// </summary>
    public void Walk(int number, int? x, int? y, Placement place, string label)
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
            return;
        }
        if (actor.IgnoreBoxes is null)
        {
            actor.Forget($"{label}: whether it ignores walk boxes is unknown");
            return;
        }
        int endX = x.Value, endY = y.Value, endBox;
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
        actor.X = endX;
        actor.Y = endY;
        actor.WalkBox = endBox;
        actor.DestBox = endBox;
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
        foreach (var actor in _actors.Skip(1)) actor.Visible = false;
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
            if (actor.Room == place.CurrentRoom && actor.Visible != true) AdjustPosition(actor, place, show: true);
        }
        EgoPositioned = false;
    }

    /// <summary><c>adjustActorPos</c> (via <c>showActor</c>): position and box from <c>adjustXYToBeInBox</c>.</summary>
    private static void AdjustPosition(Actor actor, Placement place, bool show)
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
            Unknown = reason;
            Estimate = null;
        }

        public LoomActorSnapshot ToSnapshot() =>
            new(Number, Room, X, Y, WalkBox, Visible, IgnoreBoxes,
                Room is null || X is null || Y is null || WalkBox is null || Visible is null ? Unknown ?? "not known" : null,
                Estimate);
    }
}
