namespace SCUMMexplorer.Tests;

/// <summary>S6 genuine v1 inputs from the boot. No scene visits, forced variables or direct sentences.</summary>
public sealed class ScummZakClassicPlayTests
{
    private static ScummGameSession? Boot()
    {
        var folder = Path.Combine(TestGames.Root, "zak ega en");
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
        Assert.Contains(session.SessionTrace, line => line.StartsWith("sentence script 2 runs verb 0x01 with objects 92 and 0"));
        Assert.Contains(session.SessionTrace, line => line.StartsWith("sentence script 2 runs verb 0x0E with objects 15 and 0"));
        Assert.Contains(session.DeliveredInput, input => input.Area == 5);
    }
}

