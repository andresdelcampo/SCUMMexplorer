using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Scumm;

namespace SCUMMexplorer.Tests;

/// <summary>The previously verified native S6 route, shared by S8 tests and its one harness.</summary>
public static class ManiacClassicStateRoute
{
    public static ScummGameSession AcquireKey(string folder)
    {
        var game = new GameFolderScanner().Scan(folder);
        var session = ScummGameSession.BootForPlay(game, out _);
        void Frames(int count) { for(var frame=0; frame<count; frame++) session.AdvanceFrame(); }
        void Until(Func<bool> condition)
        {
            for(var frame=0; frame<2500 && !condition(); frame++) session.AdvanceFrame();
            if(!condition()) throw new InvalidOperationException("Native classic key route exceeded its frame bound.");
        }
        void Click(int x,int y)
        {
            if(session.ClickScreen(x,y,out var refused) is null) throw new InvalidOperationException(refused);
            Frames(1);
        }
        void PickUpKey()
        {
            if(session.PressPlayKey('d',out var refused) is null) throw new InvalidOperationException(refused);
            Frames(1);
        }
        foreach(var (x,y) in new[]{(76,104),(196,104),(292,60)})
        {
            Click(x,y+session.MainScreen.Top);
            Frames(100);
        }
        Until(()=>session.CurrentRoomId==44 && session.CutsceneSkippable);
        session.PressKey(27);
        Until(()=>session.PlayerHasControl);
        Click(8,120+session.MainScreen.Top);
        Until(()=>session.CurrentRoomId==1);
        Frames(120);
        for(var step=0;step<4;step++) { Click(8,120+session.MainScreen.Top); Frames(200); }
        PickUpKey();
        Click(464-session.ViewLeft,92+session.MainScreen.Top);
        Click(464-session.ViewLeft,92+session.MainScreen.Top);
        Until(()=>session.Snapshot.ObjectStates[54]==8);
        PickUpKey();
        Click(504-session.ViewLeft,92+session.MainScreen.Top);
        Click(504-session.ViewLeft,92+session.MainScreen.Top);
        Until(()=>session.Snapshot.ObjectOwners[55]==3);
        return session;
    }
}
