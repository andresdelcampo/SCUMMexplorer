namespace SCUMMexplorer.Core.Scumm;

public sealed partial class ScummActorTable
{
    /// <summary>Native new-game actor state; generic preview tables keep their existing defaults.</summary>
    internal void InitializeV2Game(bool classic)
    {
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
