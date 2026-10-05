using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

/// <summary>The CD game's cutscenes, derived from its scripts, and Escape during them. Without <c>Games/loom vga en</c> these do nothing.</summary>
public sealed class LoomCdCutsceneTests
{
    private static GameLibrary? Game()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games", "loom vga en"));
        return Directory.Exists(folder) && SCUMMexplorer.Core.Storage.DiscImage.FindImageIn(folder) is not null ? new GameFolderScanner().Scan(folder) : null;
    }

    private static IReadOnlyList<LoomCdCutsceneGroup> Groups(GameLibrary game) =>
        LoomCdCutsceneIndex.Group(LoomCdCutsceneIndex.Scan(ScummDataSources.For(game), game.Rooms.Select(r => r.ResourceId!.Value)));

    [Fact]
    public void TheCutscenesAreDerivedFromTheScriptsAndSharedHelpersAreLeftOut()
    {
        if (Game() is not { } game) return;
        var triggers = LoomCdCutsceneIndex.Scan(ScummDataSources.For(game), game.Rooms.Select(r => r.ResourceId!.Value));
        var groups = LoomCdCutsceneIndex.Group(triggers);
        Assert.True(triggers.Count > 300, $"{triggers.Count} triggers");
        Assert.InRange(groups.Count, 100, 260);
        // The generic reaction (global 41) is reached from about a hundred objects: a helper, not a scene.
        Assert.DoesNotContain(groups, g => g.First.Scripts.SequenceEqual(["global script 41"]));
        // Four owl holes run the same script: one scene with three other triggers.
        var holes = groups.Single(g => g.First.Room == 4 && g.First.ObjectName == "hole" && g.First.VerbId == 0x36);
        Assert.Equal(3, holes.Others.Count(o => o.ObjectName == "hole" && o.VerbId == 0x36));
        Assert.Equal("There's an owl in there!", holes.First.FirstText);
        // The Elders' tribunal of room 8 is a scene (room loads and many lines); a lone look line is a reaction.
        Assert.Contains(groups, g => g.First.Room == 8 && g.IsScene && g.First.RoomLoads >= 5 && g.First.Texts > 20);
        Assert.Contains(groups, g => g.First.Room == 6 && g.First.ObjectName == "book" && !g.IsScene);
    }

    [Fact]
    public void AnEscapeAtTheFirstSkipPointOnlySkipsTheOpeningNarrationAndTheIntroPlaysOn()
    {
        if (Game() is not { } game) return;
        var session = new LoomExplorationSession(game, game.Vga!.ReadIndexedObjects(), completeBoot: false, playCdIntro: true);
        var frames = 0;
        // Script 001's own beginOverride (+0x0337) covers only the narration over the skill screen's answer, so Escape there
        // jumps to +0x0354 and script 044's opening still plays: the first skip point is not the whole intro.
        while (!session.CutsceneSkippable && frames++ < 200) session.AdvanceFrame();
        Assert.True(session.CutsceneSkippable);
        session.PressKey(0x1B);
        var after = 0;
        while (session.BootRunning && after++ < 1500) session.AdvanceFrame();
        Assert.Equal(2, session.Snapshot.RoomId);
        Assert.Contains(session.Messages.Log, m => m.Text == "LucasArts Entertainment Company" && m.Charset == 4);
        Assert.True(frames + after > 800, $"{frames} + {after} frames");
    }

    [Theory]
    [InlineData(150)]
    [InlineData(450)]
    [InlineData(-1)]
    public void AnEscapeDuringTheCdIntroSkipsToTheGame(int framesBeforeEscape)
    {
        if (Game() is not { } game) return;
        var session = new LoomExplorationSession(game, game.Vga!.ReadIndexedObjects(), completeBoot: false, playCdIntro: true);
        var frames = 0;
        var presses = 0;
        // -1: Escape at every frame that has a skip point (each cutscene's own override), as a player holding the key would.
        // Otherwise one Escape at the first skip point from that frame on (script 044's override comes with the overture).
        while (session.BootRunning && frames < 1500)
        {
            if (session.CutsceneSkippable && (framesBeforeEscape < 0 || frames >= framesBeforeEscape && presses == 0)) { session.PressKey(0x1B); presses++; }
            session.AdvanceFrame();
            frames++;
        }
        Assert.False(session.BootRunning);
        Assert.True(presses > 0);
        Assert.Equal(2, session.Snapshot.RoomId);
        Assert.True(frames < 600, $"{presses} Escape presses, {frames} frames; the whole intro takes 978");
    }

    private static LoomExplorationSession Fresh(GameLibrary game) => new(game, game.Vga!.ReadIndexedObjects(), completeBoot: false);

    private static ushort Known(LoomExplorationSession session, string key) =>
        session.Snapshot.KnownGlobalVariables.TryGetValue(key, out var value) ? value : (ushort)0;

    [Fact]
    public void TheCdBitsAreKeptApartFromTheVariableWordsAndTheStartUpLockIsClear()
    {
        if (Game() is not { } game) return;
        var session = Fresh(game);
        // ScummVM aliases a bit onto a variable word only up to version 3; the camera (var[0x002], 176 here) must not set bit[0x024] or bit[0x025].
        session.VisitRoom(game.Rooms.First(r => r.ResourceId == 4));
        Assert.NotEqual(0, Known(session, "var[0x002]"));
        Assert.Equal(0, Known(session, "bit[0x025]"));
        // Script 001's tail clears the start-up lock the intro sets, which the entry scripts of many rooms test before they start their monitors.
        Assert.Equal(0, Known(session, "bit[0x00E]"));
    }

    [Fact]
    public void TheMusicTimerCountsInASessionThatSkippedTheIntro()
    {
        if (Game() is not { } game) return;
        var session = Fresh(game);
        for (var i = 0; i < 30; i++) session.AdvanceFrame();
        Assert.True(Known(session, "var[0x00E]") >= 30, $"timer {Known(session, "var[0x00E]")}");
        Assert.Contains(session.Snapshot.Uncertainties, u => u.Contains("music timer", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(9, 199, 0x38)]   // the feather: script 105 waits for the timer
    [InlineData(24, 359, 0x3E)]  // the sheep: script 70 waits for the timer
    public void ScenesThatWaitForTheMusicTimerEnd(int room, int obj, int verb)
    {
        if (Game() is not { } game) return;
        var session = Fresh(game);
        session.VisitRoom(game.Rooms.First(r => r.ResourceId == room));
        session.ActivateObjectVerb(obj, verb);
        var begun = false;
        for (var frames = 0; frames < 3000; frames++)
        {
            begun |= session.InCutscene;
            if (session.CutsceneSkippable) session.PressKey(0x1B);
            session.AdvanceFrame();
        }
        Assert.True(begun);
        Assert.False(session.InCutscene);
    }

    [Fact]
    public void TheGuardsOfAWalkBoxMonitorNameTheBoxesAndTheEgo()
    {
        if (Game() is not { } game) return;
        var data = ScummDataSources.For(game);
        var paths = LoomCdSceneGuards.Find(data, 15, data.RoomEntryScript(15)!, "root");
        var boxes = paths.SelectMany(p => p.Guards).Where(g => g.WalkBoxOfActor == 1).Select(g => g.Constant).ToHashSet();
        Assert.Contains(3, boxes);
        Assert.Contains(5, boxes);
    }

    [Fact]
    public void EveryCdSceneEitherPlaysFromADerivedSetupOrSaysWhyNot()
    {
        if (Game() is not { } game) return;
        var data = ScummDataSources.For(game);
        var triggers = LoomCdCutsceneIndex.Scan(data, game.Rooms.Select(r => r.ResourceId!.Value).ToList());
        var planner = new LoomCdSceneSetupPlanner(game, triggers, data);
        var groups = LoomCdCutsceneIndex.Group(triggers);
        var setups = groups.Select(g => (g, s: planner.Derive(g.First))).Where(x => x.s != LoomCdStartSetup.None).ToList();
        Assert.InRange(setups.Count, 12, 30);
        Assert.All(setups, x => Assert.NotEmpty(x.s.Notes));
        Assert.All(setups.Where(x => !x.s.Plays), x => Assert.Contains("tests it fails", x.s.Notes[0]));
        Assert.True(setups.Count(x => x.s.Plays) >= 14, $"{setups.Count(x => x.s.Plays)} of {setups.Count} play");
        // A monitor of the ego's walk box: the ego goes into the box after the room's monitors have started.
        Assert.Contains(planner.Derive(groups.First(g => g.First.Room == 2 && g.First.IsRoomEntry).First).After, s => s.Kind == "box");
        // A story state another scene's script sets: that scene runs first, unseen.
        Assert.Contains(planner.Derive(groups.First(g => g.First.Room == 25 && g.First.ObjectId == 373).First).Before, s => s.Kind == "scene");
        // A scene whose only writers are its own code or code nothing starts says so.
        var blocked = planner.Derive(groups.First(g => g.First.Room == 34 && g.First.ObjectId == 485).First);
        Assert.False(blocked.Plays);
        Assert.Contains("this scene's own script", blocked.Notes[0]);
    }}
