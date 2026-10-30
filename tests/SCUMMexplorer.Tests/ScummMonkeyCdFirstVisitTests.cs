using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Tests;

/// <summary>
/// S5 MonkeyCd (17 October 2026): the rooms of The Secret of Monkey Island CD show the first visit the generic session runs (boot, a visit that does not play
/// the route, frames until the room settles); three named rooms' settled object states are pinned. Record: <c>.verification/monkeycd-objects-20261017/README.md</c>.
/// Skips when the disc image is absent.
/// </summary>
public sealed class ScummMonkeyCdFirstVisitTests
{
    private static GameLibrary? Game()
    {
        var path = Path.Combine(TestGames.Root, "monkey1 vga en");
        return File.Exists(Path.Combine(path, "The_Secret_of_Monkey_Island.cue")) ? new GameFolderScanner().Scan(path) : null;
    }

    private static int[] ActiveImages(ScummRoomScene scene) => scene.Images.Where(scene.Selection.IsActive).Select(image => image.Id).Distinct().Order().ToArray();

    [Fact]
    public void TheProfileGivesTheFirstVisitForTheCdRelease()
    {
        if (Game() is not { } game) return;
        Assert.True(ScummProfile.For(game).HasFirstVisitScenes);
    }

    [Theory]
    [InlineData(30, new[] { 390, 398, 399 })]
    [InlineData(37, new[] { 479, 485 })]
    [InlineData(70, new[] { 773, 776, 778, 781 })]
    public void ARoomSettlesWithTheObjectsItsScriptsDraw(int room, int[] active)
    {
        if (Game() is not { } game) return;

        var scene = ScummRoomFirstVisit.Run(game, room);

        Assert.Equal(ScummSettleOutcome.Settled, scene.Outcome);
        Assert.Equal(active, ActiveImages(scene));
        Assert.Empty(scene.Gaps());
    }

    [Fact]
    public void TheScummBarRunsWithItsThirtyTwoColourPirates()
    {
        // Room 28's actors use 32-colour costumes (format 0x59); before S5 the room could not run.
        if (Game() is not { } game) return;

        var scene = ScummRoomFirstVisit.Run(game, 28);
        var layer = scene.ActorLayer();

        Assert.Empty(scene.Gaps());
        Assert.Contains(layer.Actors, actor => actor.Costume == 24);
        Assert.Empty(layer.Skipped);
    }

    [Theory]
    [InlineData(88)]
    [InlineData(89)]
    public void RoomsWithAnEmptyEntryOrAPaletteLoopRunWithNothingUnfollowed(int room)
    {
        // 30 October 2026 (.verification/monkeycd-rooms-20261030): Room 88's ENCD and EXCD blocks are empty (8 bytes, no code): no entry script, not an
        // undecoded byte. Room 89's local script 200 (palette flashes) decodes in full; its old saveRestoreVerbs gap was closed by later reader work.
        if (Game() is not { } game) return;
        Assert.Null(ScummDataSources.For(game).RoomEntryScript(88));

        var scene = ScummRoomFirstVisit.Run(game, room);

        Assert.Equal(ScummSettleOutcome.Settled, scene.Outcome);
        Assert.DoesNotContain(scene.Snapshot.Uncertainties, item => item.Contains("undecoded", StringComparison.Ordinal) || item.Contains("disassembly stops", StringComparison.Ordinal));
    }

    [Fact]
    public void TheInventoryIconRoomIsNotARoomTheGameEnters()
    {
        // Room 99 holds the inventory icons 955 to 1045 (Script 9 takes them by verbOps assignObject); the index has 1000 objects, so ScummVM's getState and
        // drawObject would refuse objects 1000 and up: the game never enters it, and its first visit refuses with that reason instead of inventing states.
        if (Game() is not { } game) return;
        var data = (ScummV5DataSource)ScummDataSources.For(game);
        Assert.Equal(999, data.ReadIndexedObjects().States.Keys.Max());

        var refused = Assert.Throws<InvalidDataException>(() => ScummRoomFirstVisit.Run(game, 99));
        Assert.Contains("never enters this room", refused.Message, StringComparison.Ordinal);
    }
}
