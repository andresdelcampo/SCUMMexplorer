namespace SCUMMexplorer.Tests;

/// <summary>S6 genuine v2 inputs from the boot. No scene visits, forced variables or direct sentences.</summary>
public sealed class ScummZakEnhancedPlayTests
{
    private static ScummGameSession? Boot()
    {
        var folder = Path.Combine(TestGames.Root, "zakE ega en");
        return File.Exists(Path.Combine(folder, "00.LFL"))
            ? ScummGameSession.BootForPlay(new GameFolderScanner().Scan(folder), out _) : null;
    }

    private static void Run(ScummGameSession session, int count)
    {
        for (var frame = 0; frame < count; frame++) session.AdvanceFrame();
    }

    private static void Key(ScummGameSession session, char key)
    {
        Assert.NotNull(session.PressPlayKey(key, out var refused));
        Assert.Null(refused);
        Run(session, 2);
    }

    private static void Click(ScummGameSession session, int x, int y)
    {
        Assert.NotNull(session.ClickScreen(x, y, out var refused));
        Assert.Null(refused);
        Run(session, 2);
    }

    [Fact]
    public void NativeSentenceLineExecutesOpenDrawerThenPickUpPhoneBill()
    {
        if (Boot() is not { } session) return;
        Run(session, 150);
        Key(session, 'w'); // game's Open shortcut
        Click(session, 50, 81); // drawer 92, room y65 + top16
        Assert.Equal(1, session.Snapshot.KnownGlobalVariables["var[0x01A]"]);
        Assert.Equal(92, session.Snapshot.KnownGlobalVariables["var[0x01B]"]);
        Assert.Equal("Open dresser drawer", ScummTextDecoder.DecodeGlyphs(session.PlaySentenceGlyphs()));
        Assert.Equal(0, session.Snapshot.ObjectStates.GetValueOrDefault(92) & 8);
        Click(session, 10, 145); // engine's sentence area, executes composed sentence
        Run(session, 180);
        Assert.Equal(8, session.Snapshot.ObjectStates.GetValueOrDefault(92) & 8);
        Key(session, 'd'); // game's Pick up shortcut
        Click(session, 55, 78); // now exposed phone bill 15
        Assert.Equal(15, session.Snapshot.KnownGlobalVariables["var[0x01B]"]);
        Click(session, 10, 145);
        Run(session, 180);
        Assert.Equal(1, session.Snapshot.ObjectOwners[15]);
        var area = session.PlayVerbArea();
        Assert.Contains(Enumerable.Range(0, 8).SelectMany(y => Enumerable.Range(176, 144).Select(x => area.Pixels[(176 - ScummVerbArea.Top + y) * 320 + x])), pixel => pixel != 0);
        Assert.Equal(15, session.ClickScreen(180, 177, out _)!.Value);
        Assert.Contains(session.SessionTrace, line => line.StartsWith("sentence script 2 runs verb 0x01 with objects 92 and 0"));
        Assert.Contains(session.SessionTrace, line => line.StartsWith("sentence script 2 runs verb 0x0E with objects 15 and 0"));
        Assert.Contains(session.DeliveredInput, input => input.Area == 5);
    }

    [Fact]
    public void NativeBedroomDoorWalksToLivingRoomAndReachedAlienCutsceneCanBeSkipped()
    {
        if (Boot() is not { } session) return;
        Run(session, 150);
        Key(session, 'w');
        Click(session, 307, 81);
        Click(session, 10, 145);
        Run(session, 180);
        Assert.Equal(8, session.Snapshot.ObjectStates.GetValueOrDefault(84) & 8);
        Key(session, 'e');
        Click(session, 307, 81);
        Run(session, 240);
        Assert.Equal(2, session.Actors.Single(actor => actor.Number == 1).Room);
        Assert.True(session.CutsceneSkippable);
        session.PressKey(27);
        Run(session, 120);
        Assert.Equal(2, session.Snapshot.KnownGlobalVariables[ScummVars.Room]);
        Assert.True(session.UserInputOn);
        Assert.False(session.InCutscene);
        Assert.Contains(session.SessionTrace, line => line.Contains("Script 126 continues at its skip jump"));
        Assert.Contains(session.SessionTrace, line => line.Contains("Object 84") && line.Contains("0x0066"));
    }
}
