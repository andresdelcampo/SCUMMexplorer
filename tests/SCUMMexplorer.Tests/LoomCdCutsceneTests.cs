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
}
