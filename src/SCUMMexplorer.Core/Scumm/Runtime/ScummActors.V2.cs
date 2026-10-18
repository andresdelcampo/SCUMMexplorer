namespace SCUMMexplorer.Core.Scumm;

public sealed partial class ScummActorTable
{
    private bool _v2Animations;
    private bool _v1Costumes;
    // Actor_v2::walkActor/actorWalkStep and inherited calcMovementFactor (actor.cpp,
    // read 17 October 2026). Runtime positions stay pixels; native arithmetic uses 8x2 units.
    private void WalkActorV2(Actor actor, Placement place)
    {
        if ((actor.Moving & MoveTurn) != 0)
        {
            var direction = UpdateDirectionV2(actor, place);
            if (actor.TargetFacing == direction) actor.Moving &= ~MoveTurn;
            SetDirection(actor, direction, place);
            return;
        }
        if ((actor.Moving & MoveNewLeg) == 0) return;
        if ((actor.Moving & MoveInLeg) == 0) { StepV2(actor); return; }
        if ((actor.Moving & MoveLastLeg) != 0)
        {
            actor.Moving = MoveTurn;
            StartAnimation(actor, actor.StandFrame, place);
            if (actor.DestDir != -1) TurnTo(actor, actor.DestDir);
            return;
        }
        actor.WalkBox = actor.CurBox;
        ScummBoxPoint next;
        if (actor.WalkBox == actor.DestBox)
        {
            next = new(actor.DestX, actor.DestY);
            actor.Moving |= MoveLastLeg;
        }
        else
        {
            var box = place.Boxes?.GetNextBox(actor.WalkBox ?? ScummWalkBoxSet.InvalidBox, actor.DestBox ?? ScummWalkBoxSet.InvalidBox) ?? -1;
            if (box < 0) { actor.Moving |= MoveLastLeg; return; }
            if (place.Boxes is null || box >= place.Boxes.Count || actor.WalkBox is not int previousBox || previousBox < 0 || previousBox >= place.Boxes.Count)
            {
                // Native getBoxCoordinates asserts on a missing box. A bounded session
                // records the unsupported route instead of inventing a crossing.
                actor.Unknown = "v1/v2 walk route references an absent box";
                actor.Moving = 0;
                return;
            }
            var flags = place.Boxes!.GetFlags(box);
            var isPlayer = place.IsPlayer(actor.Number);
            if ((flags & 0x40) != 0 && !((flags & 0x20) != 0 && !isPlayer))
            { actor.Moving |= MoveLastLeg; actor.DestDir = -1; }
            actor.CurBox = box;
            var (_, tx, ty) = ScummWalkBoxSet.ClosestPointV2(place.Boxes.Boxes[box], actor.X!.Value, actor.Y!.Value);
            var (_, px, py) = ScummWalkBoxSet.ClosestPointV2(place.Boxes.Boxes[actor.WalkBox!.Value], tx, ty);
            next = new(px, py);
        }
        var dx = (next.X >> 3) - (actor.X!.Value >> 3);
        var dy = (next.Y >> 1) - (actor.Y!.Value >> 1);
        actor.Next = next;
        if (dx == 0 && dy == 0) { actor.Moving |= MoveInLeg; return; }
        actor.StepThreshold = Math.Max(Math.Abs(dx), Math.Abs(dy));
        actor.DeltaXFactor = dx < 0 ? -8 : 8;
        actor.DeltaYFactor = dy < 0 ? -2 : 2;
        actor.XFrac = actor.XAdd = (ushort)Math.Abs(dx);
        actor.YFrac = actor.YAdd = (ushort)Math.Abs(dy);
        actor.V2WalkFacing = dx >= 0 ? dy >= 0 ? 1 : 0 : dy >= 0 ? 2 : 3;
        actor.TargetFacing = ScummCostume.OldDirToNewDir(Math.Abs(dy) > Math.Abs(dx) ? dy >= 0 ? 2 : 3 : dx >= 0 ? 1 : 0);
        actor.Moving &= ~MoveInLeg;
        if (actor.Facing != actor.TargetFacing) actor.Moving |= MoveTurn;
        if (actor.Frame != actor.WalkFrame || actor.Facing != actor.TargetFacing) StartAnimation(actor, actor.WalkFrame, place);
        if ((actor.Moving & MoveTurn) == 0) StepV2(actor);
    }

    // Actor_v2::remapDirection mutates targetFacing, unlike the v3 method. Its
    // tables combine the requested direction, original leg direction and box flags.
    private static int UpdateDirectionV2(Actor actor, Placement place)
    {
        ReadOnlySpan<byte> table1 = [4,1,2,0,1,2,3,0, 6,1,3,0,1,2,3,0, 7,0,3,0,1,2,3,0, 5,0,2,0,1,2,3,0];
        ReadOnlySpan<byte> table2 = [0,1,3,2,3,0,2,0, 0,1,3,2,1,3,1,2, 0,1,3,2,1,0,2,2, 0,1,3,2,3,3,1,1];
        ReadOnlySpan<byte> table3 = [0,0,2,0,1,3,2,0, 1,1,2,0,1,3,2,0, 2,1,2,0,1,3,2,0, 3,0,3,0,1,3,2,0];
        var flags = (place.Boxes?.GetFlags(actor.WalkBox ?? ScummWalkBoxSet.InvalidBox) ?? 0) & 7;
        var requested = ScummCostume.NewDirToOldDir(actor.TargetFacing);
        var target = (actor.Moving & ~MoveTurn) != 0 ? table2[requested * 8 + table1[actor.V2WalkFacing * 8 + flags]] : table3[requested * 8 + flags];
        actor.TargetFacing = ScummCostume.OldDirToNewDir(target);
        return ScummCostume.OldDirToNewDir(TurnInterpolation[target | (ScummCostume.NewDirToOldDir(actor.Facing ?? 180) << 2)]);
    }

    private static void StepV2(Actor actor)
    {
        actor.XFrac = (ushort)(actor.XFrac + actor.XAdd);
        if (actor.XFrac >= actor.StepThreshold)
        {
            if (actor.X != actor.Next.X) actor.X += actor.DeltaXFactor;
            actor.XFrac = (ushort)(actor.XFrac - actor.StepThreshold);
        }
        actor.YFrac = (ushort)(actor.YFrac + actor.YAdd);
        if (actor.YFrac >= actor.StepThreshold)
        {
            if (actor.Y != actor.Next.Y) actor.Y += actor.DeltaYFactor;
            actor.YFrac = (ushort)(actor.YFrac - actor.StepThreshold);
        }
        if (actor.X == actor.Next.X && actor.Y == actor.Next.Y) actor.Moving |= MoveInLeg;
    }
    /// <summary>Native new-game actor state; generic preview tables keep their existing defaults.</summary>
    internal void InitializeV2Game(bool classic)
    {
        _v2Animations = true;
        _v1Costumes = classic;
        // Actor_v2::initActor (actor.cpp): native speeds 1,1; this runtime stores pixels (8,2).
        // resetScumm (scumm.cpp) additionally assigns each v1 actor its own costume number.
        foreach (var actor in _actors)
        {
            actor.InitFrame = 2;
            actor.WalkFrame = 0;
            actor.StandFrame = 1;
            actor.TalkStartFrame = 5;
            actor.TalkStopFrame = 4;
            actor.WalkSpeedX = 8;
            actor.WalkSpeedY = 2;
            if (!classic) continue;
            actor.Costume = actor.Number;
            actor.CostumeNeedsInit = true;
            actor.Animation.Reset();
            // setActorCostume returns before palette initialization for v1 (actor.cpp).
        }
    }
}
