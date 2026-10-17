namespace SCUMMexplorer.Core.Scumm;

public sealed partial class ScummActorTable
{
    private bool _v2Animations;
    // Actor_v2::walkActor/actorWalkStep and inherited calcMovementFactor (actor.cpp,
    // read 17 October 2026). Runtime positions stay pixels; native arithmetic uses 8x2 units.
    private void WalkActorV2(Actor actor, Placement place)
    {
        if ((actor.Moving & MoveTurn) != 0)
        {
            var direction = UpdateActorDirection(actor, false, place);
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
            var flags = place.Boxes!.GetFlags(box);
            var isPlayer = place.Classes is not null && place.Classes.TryGetValue(actor.Number, out var classes) && ScummObjectClasses.Has(classes, PlayerClass);
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
        actor.TargetFacing = ScummCostume.OldDirToNewDir(Math.Abs(dy) > Math.Abs(dx) ? dy >= 0 ? 2 : 3 : dx >= 0 ? 1 : 0);
        actor.Moving &= ~MoveInLeg;
        if (actor.Facing != actor.TargetFacing) actor.Moving |= MoveTurn;
        if (actor.Frame != actor.WalkFrame || actor.Facing != actor.TargetFacing) StartAnimation(actor, actor.WalkFrame, place);
        if ((actor.Moving & MoveTurn) == 0) StepV2(actor);
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
