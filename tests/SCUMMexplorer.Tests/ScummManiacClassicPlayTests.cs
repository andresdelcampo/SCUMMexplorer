namespace SCUMMexplorer.Tests;

/// <summary>Classic English native inputs from boot. No catalog visits or story-variable writes.</summary>
public sealed class ScummManiacClassicPlayTests
{
    private static ScummGameSession? NewSession()
    {
        var folder = Path.Combine(TestGames.Root,"mmansion ega en");
        if (!File.Exists(Path.Combine(folder,"00.LFL"))) return null;
        var game = new GameFolderScanner().Scan(folder);
        var data = new ScummV1DataSource(game);
        return new(game,data.ReadIndexedObjects(),data:data);
    }

    private static void Frames(ScummGameSession session,int frames)
    {
        for(var frame=0;frame<frames;frame++) session.AdvanceFrame();
    }

    private static void Until(ScummGameSession session,Func<bool> ready,int limit=2500)
    {
        for(var frame=0;frame<limit && !ready();frame++) session.AdvanceFrame();
        Assert.True(ready(),string.Join("\n",session.SessionTrace.TakeLast(15)));
    }

    [Fact]
    public void NativePartyChoicesAndOverrideSkipReachPlayerControl()
    {
        if(NewSession() is not {} session) return;
        Assert.Equal(45,session.CurrentRoomId);
        foreach(var (x,y) in new[]{(76,104),(196,104),(292,60)})
        {
            session.Click(x,y);
            Frames(session,100);
        }
        Until(session,()=>session.CurrentRoomId==44 && session.CutsceneSkippable);
        Assert.Equal(1,session.Snapshot.KnownGlobalVariables["var[0x030]"]);
        Assert.Equal(5,session.Snapshot.KnownGlobalVariables["var[0x031]"]);
        Assert.True(session.InCutscene);
        session.PressKey(27);
        Until(session,()=>session.PlayerHasControl);
        Assert.False(session.InCutscene);
        Assert.Equal(44,session.CurrentRoomId);
        Assert.Contains(session.SessionTrace,line=>line.Contains("abortCutscene: Script 131") && line.Contains("+0x000A"));
        Assert.Equal(15,session.Verbs.Count(v=>v.Mode==1));
        Assert.Contains(session.Verbs,v=>v.VerbId==12 && v.Name=="Read");
        Assert.DoesNotContain(session.Verbs,v=>v.Name=="Talk to");
    }

    [Fact]
    public void NativeSentenceWalkAndExitRevealAndPickUpTheKey()
    {
        if(NewSession() is not {} session) return;
        void Click(int x,int y)
        {
            Assert.NotNull(session.ClickScreen(x,y,out var refused));
            Assert.Null(refused);
            Frames(session,1);
        }
        void Key(int key)
        {
            Assert.NotNull(session.PressPlayKey(key,out var refused));
            Assert.Null(refused);
            Frames(session,1);
        }
        foreach(var (x,y) in new[]{(76,104),(196,104),(292,60)})
        {
            Click(x,y+session.MainScreen.Top);
            Frames(session,100);
        }
        Until(session,()=>session.CurrentRoomId==44 && session.CutsceneSkippable);
        session.PressKey(27);
        Until(session,()=>session.PlayerHasControl);
        Click(8,120+session.MainScreen.Top);
        Until(session,()=>session.CurrentRoomId==1);
        Frames(session,120);
        Assert.Equal(960,session.ViewLeft);
        for(var step=0;step<4;step++) { Click(8,120+session.MainScreen.Top); Frames(session,200); }
        Assert.Equal(352,session.ViewLeft);
        Assert.Equal(512,session.Actors.Single(a=>a.Number==3).X);
        Key('d');
        Click(464-session.ViewLeft,92+session.MainScreen.Top);
        Assert.Equal(14,session.Snapshot.KnownGlobalVariables["var[0x01A]"]);
        Assert.Equal(54,session.Snapshot.KnownGlobalVariables["var[0x01B]"]);
        Assert.Equal("Pick up door mat",ScummTextDecoder.DecodeGlyphs(session.PlaySentenceGlyphs()));
        Assert.Contains((byte)13,session.PlayVerbArea().Pixels.Skip((152-ScummVerbArea.Top)*320).Take(320*8));
        Click(464-session.ViewLeft,92+session.MainScreen.Top);
        Until(session,()=>session.Snapshot.ObjectStates[54]==8);
        Key('d');
        Click(504-session.ViewLeft,92+session.MainScreen.Top);
        Click(504-session.ViewLeft,92+session.MainScreen.Top);
        Until(session,()=>session.Snapshot.ObjectOwners[55]==3);
        Assert.Contains(session.SessionTrace,l=>l.Contains("startObject runs object 54 verb 0x0E"));
        Assert.Contains(session.SessionTrace,l=>l.Contains("startObject runs object 55 verb 0x0E"));
        Assert.Contains(session.DeliveredInput,i=>i.Area==1 && i.Value==14);
        var inventory = session.PlayVerbArea();
        var painted = Enumerable.Range(184,8).Sum(y => inventory.Pixels.Skip((y-ScummVerbArea.Top)*320).Take(144).Count(p=>p!=0));
        Assert.Equal(63,painted);
        Click(8,188);
        Assert.Equal(3,session.DeliveredInput.Last().Area);
        Assert.Equal(55,session.DeliveredInput.Last().Value);
        Assert.Equal("Walk to key",ScummTextDecoder.DecodeGlyphs(session.PlaySentenceGlyphs()));
        Assert.DoesNotContain(session.Snapshot.Uncertainties,l=>l.Contains("unsupported v2"));
    }
}
