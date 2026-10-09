// Adapts ScummVM 2.2.0 engines/scumm/actor.cpp and boxes.cpp.
// Copyright the ScummVM contributors; GPL-2.0-or-later. See THIRD_PARTY_NOTICES.md.
namespace SCUMMexplorer.Core.Scumm;

public sealed partial class ScummActorTable
{
    // ScummVM 2.2.0 Actor (v4), rather than Actor_v3. The old-v3 routines stay
    // separate because their gate selection, first step and counters differ.
    private void WalkActorV4(Actor actor, Placement place)
    {
        if ((actor.Moving & MoveNewLeg) == 0)
        {
            if ((actor.Moving & MoveInLeg) != 0 && ActorWalkStepV4(actor, place)) return;
            if ((actor.Moving & MoveLastLeg) != 0)
            {
                actor.Moving = 0;
                actor.WalkBox = actor.DestBox;
                if (actor.ClearEstimateOnArrival && actor.X == actor.DestX && actor.Y == actor.DestY) actor.Estimate = null;
                actor.ClearEstimateOnArrival = false;
                StartAnimation(actor, actor.StandFrame, place);
                if (actor.TargetFacing != actor.DestDir) TurnTo(actor, actor.DestDir);
                return;
            }
            if ((actor.Moving & MoveTurn) != 0)
            {
                var next = UpdateActorDirectionV4(actor, false, place);
                if (actor.Facing != next) SetDirection(actor, next, place);
                else actor.Moving = 0;
                return;
            }
            actor.WalkBox = actor.CurBox;
            actor.Moving &= MoveInLeg;
        }
        actor.Moving &= ~MoveNewLeg;
        while (true)
        {
            if (actor.WalkBox is not int box || box == ScummWalkBoxSet.InvalidBox)
            {
                actor.WalkBox = actor.CurBox = actor.DestBox ?? ScummWalkBoxSet.InvalidBox;
                break;
            }
            if (box == actor.DestBox) break;
            var nextBox = place.Boxes?.GetNextBox(box, actor.DestBox ?? ScummWalkBoxSet.InvalidBox) ?? -1;
            if (nextBox < 0)
            {
                actor.DestBox = box;
                actor.Moving |= MoveLastLeg;
                return;
            }
            actor.CurBox = nextBox;
            var (direct, gate) = FindPathTowardsV4(actor, box, nextBox, actor.DestBox!.Value, place);
            if (direct) break;
            if (CalcMovementFactorV4(actor, gate, place)) return;
            actor.WalkBox = actor.CurBox;
        }
        actor.Moving |= MoveLastLeg;
        CalcMovementFactorV4(actor, new ScummBoxPoint(actor.DestX, actor.DestY), place);
    }

    private static int UpdateActorDirectionV4(Actor actor, bool walking, Placement place)
    {
        var direction = RemapDirection(actor, actor.TargetFacing, walking, place);
        if ((direction & Interpolate) == 0) return direction;
        // Actor::updateActorDirection uses the clockwise four-direction ring.
        // Actor_v3 instead uses actorTurnInterpolateTable.
        var from = ((actor.Facing ?? 180) / 90) % 4;
        var to = (direction & 0x3FF) / 90;
        var diff = to - from;
        if (Math.Abs(diff) > 2) diff = -diff;
        return ((from + Math.Sign(diff) + 4) % 4) * 90;
    }

    private bool CalcMovementFactorV4(Actor actor, ScummBoxPoint next, Placement place)
    {
        var (x, y) = (actor.X!.Value, actor.Y!.Value);
        if (x == next.X && y == next.Y) return false;
        var dx = next.X - x;
        var dy = next.Y - y;
        var factorY = actor.WalkSpeedY << 16;
        if (dy < 0) factorY = -factorY;
        var factorX = factorY * dx;
        if (dy != 0) factorX /= dy;
        else factorY = 0;
        if ((uint)Math.Abs(factorX) > (actor.WalkSpeedX << 16))
        {
            factorX = actor.WalkSpeedX << 16;
            if (dx < 0) factorX = -factorX;
            factorY = factorX * dy;
            if (dx != 0) factorY /= dx;
            else factorX = 0;
        }
        actor.LegStart = new(x, y);
        actor.Next = next;
        actor.DeltaXFactor = factorX;
        actor.DeltaYFactor = factorY;
        actor.XFrac = actor.YFrac = 0;
        // getAngleFromPos(..., false): the angle follows the velocity factors.
        actor.TargetFacing = Math.Abs(factorY) * 2 < Math.Abs(factorX)
            ? factorX > 0 ? 90 : 270 : factorY > 0 ? 180 : 0;
        return ActorWalkStepV4(actor, place);
    }

    private bool ActorWalkStepV4(Actor actor, Placement place)
    {
        var facing = UpdateActorDirectionV4(actor, true, place);
        if ((actor.Moving & MoveInLeg) == 0 || actor.Facing != facing)
        {
            if (actor.WalkFrame != actor.Frame || actor.Facing != facing)
            {
                SetDirection(actor, facing, place);
                StartAnimation(actor, actor.WalkFrame, place);
            }
            actor.Moving |= MoveInLeg;
        }
        var (x, y) = (actor.X!.Value, actor.Y!.Value);
        if (actor.WalkBox != actor.CurBox && place.Boxes?.Contains(actor.CurBox, x, y) == true)
            actor.WalkBox = actor.CurBox;
        var distX = Math.Abs(actor.Next.X - actor.LegStart.X);
        var distY = Math.Abs(actor.Next.Y - actor.LegStart.Y);
        if (Math.Abs(x - actor.LegStart.X) >= distX && Math.Abs(y - actor.LegStart.Y) >= distY)
        {
            actor.Moving &= ~MoveInLeg;
            return false;
        }
        var scale = actor.IgnoreBoxes == true ? actor.Scale :
            place.Boxes?.BoxScale(actor.WalkBox ?? ScummWalkBoxSet.InvalidBox, y) ?? 255;
        var tmpX = (x << 16) + actor.XFrac + (actor.DeltaXFactor / 256) * scale;
        var tmpY = (y << 16) + actor.YFrac + (actor.DeltaYFactor / 256) * scale;
        actor.XFrac = unchecked((ushort)tmpX);
        actor.YFrac = unchecked((ushort)tmpY);
        x = tmpX / 65536;
        y = tmpY / 65536;
        if (Math.Abs(x - actor.LegStart.X) > distX) x = actor.Next.X;
        if (Math.Abs(y - actor.LegStart.Y) > distY) y = actor.Next.Y;
        (actor.X, actor.Y) = (x, y);
        if (x != actor.Next.X || y != actor.Next.Y) return true;
        actor.Moving &= ~MoveInLeg;
        return false;
    }

    private static (bool Direct, ScummBoxPoint Gate) FindPathTowardsV4(Actor actor, int box1, int box2, int finalBox, Placement place)
    {
        if (place.Boxes?.Coordinates(box1) is not { } first || place.Boxes.Coordinates(box2) is not { } second)
            return (false, default);
        var a = first.Corners.ToArray();
        var b = second.Corners.ToArray();
        for (var i = 0; i < 4; i++)
        {
            for (var j = 0; j < 4; j++)
            {
                if (a[0].X == a[1].X && a[0].X == b[0].X && a[0].X == b[1].X)
                {
                    var loA = Math.Min(a[0].Y, a[1].Y); var hiA = Math.Max(a[0].Y, a[1].Y);
                    var loB = Math.Min(b[0].Y, b[1].Y); var hiB = Math.Max(b[0].Y, b[1].Y);
                    if (Overlap(loA, hiA, loB, hiB))
                    {
                        var pos = actor.Y!.Value;
                        if (box2 == finalBox)
                        {
                            var dx = actor.DestX - actor.X!.Value;
                            var dy = actor.DestY - pos;
                            if (dx != 0)
                            {
                                dy *= a[0].X - actor.X.Value;
                                var t = dy / dx;
                                if (t == 0 && (dy <= 0 || dx <= 0) && (dy >= 0 || dx >= 0)) t = -1;
                                pos += t;
                            }
                        }
                        var q = Math.Clamp(Math.Clamp(pos, loB, hiB), loA, hiA);
                        return (q == pos && box2 == finalBox, new(a[0].X, q));
                    }
                }
                if (a[0].Y == a[1].Y && a[0].Y == b[0].Y && a[0].Y == b[1].Y)
                {
                    var loA = Math.Min(a[0].X, a[1].X); var hiA = Math.Max(a[0].X, a[1].X);
                    var loB = Math.Min(b[0].X, b[1].X); var hiB = Math.Max(b[0].X, b[1].X);
                    if (Overlap(loA, hiA, loB, hiB))
                    {
                        var pos = actor.X!.Value;
                        if (box2 == finalBox)
                        {
                            var dy = actor.DestY - actor.Y!.Value;
                            if (dy != 0) pos += (actor.DestX - pos) * (a[0].Y - actor.Y.Value) / dy;
                        }
                        var q = Math.Clamp(Math.Clamp(pos, loB, hiB), loA, hiA);
                        return (q == pos && box2 == finalBox, new(q, a[0].Y));
                    }
                }
                Rotate(a);
            }
            Rotate(b);
        }
        return (false, default);

        static bool Overlap(int loA, int hiA, int loB, int hiB) =>
            loA <= hiB && loB <= hiA && !((hiA == loB || hiB == loA) && loA != hiA && loB != hiB);
        static void Rotate(ScummBoxPoint[] points) =>
            (points[0], points[1], points[2], points[3]) = (points[1], points[2], points[3], points[0]);
    }
}
