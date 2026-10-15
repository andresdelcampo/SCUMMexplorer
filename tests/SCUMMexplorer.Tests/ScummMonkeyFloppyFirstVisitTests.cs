using SCUMMexplorer.Core.Discovery;

namespace SCUMMexplorer.Tests;

/// <summary>
/// S5 MonkeyFloppy (15 October 2026): the first visit of a room from the generic session for The Secret of Monkey Island (floppy, SCUMM v4, four disks,
/// the family's fixture). The settled object states of three named rooms are pinned: the dock (Room 33, the boot's own room), the shop (Room 30,
/// entered without its route; its state needs the v4 bit variables kept apart from the word variables) and the forest (Room 58, a room the game only
/// loads under pseudo room numbers). Record: <c>.verification/monkey1-objects-20261015/README.md</c>. Skips when the game folder is absent.
/// </summary>
public sealed class ScummMonkeyFloppyFirstVisitTests
{
    private static GameLibrary? Game()
    {
        var path = Path.Combine(TestGames.Root, "monkey1 ega en");
        return File.Exists(Path.Combine(path, "DISK01.LEC")) ? new GameFolderScanner().Scan(path) : null;
    }

    private static int[] Active(ScummRoomScene scene) => scene.Images.Where(scene.Selection.IsActive).Select(image => image.Id).Distinct().Order().ToArray();

    private static int RoomVariable(ScummRoomScene scene) => scene.Snapshot.KnownGlobalVariables.GetValueOrDefault(ScummVars.Room);

    [Fact]
    public void TheDockIsTheBootsOwnSettledStateWithGuybrushOnTheQuay()
    {
        if (Game() is not { } game) return;

        var scene = ScummRoomFirstVisit.Run(game, 33);

        Assert.True(scene.IsFirstRoomOfTheGame);
        Assert.Equal(ScummSettleOutcome.Settled, scene.Outcome);
        Assert.Empty(Active(scene));
        Assert.Equal(33, RoomVariable(scene));
        var ego = scene.State.Actors.Single(actor => actor.Number == 1);
        Assert.Equal(1, ego.Look.Costume);
        Assert.Equal((346, 133), (ego.X, ego.Y));
        Assert.Empty(scene.Gaps());
    }

    [Fact]
    public void TheShopEnteredWithoutItsRouteKeepsItsRoomVariableAndItsEntryState()
    {
        if (Game() is not { } game) return;

        var scene = ScummRoomFirstVisit.Run(game, 30);

        Assert.False(scene.IsFirstRoomOfTheGame);
        Assert.Equal(ScummSettleOutcome.Settled, scene.Outcome);
        // From version 4 the bit variables are not bits of the word variables (readVar): bits 0x040 to 0x04F no longer overwrite VAR_ROOM.
        Assert.Equal(30, RoomVariable(scene));
        Assert.Equal([404, 412, 932], Active(scene));
        Assert.Contains(scene.State.Actors, actor => actor.Number == 11 && actor.Room == 30 && actor.Look.Costume == 28);
        Assert.Contains(scene.Notes, note => note.Contains("without playing the route", StringComparison.Ordinal));
        Assert.Empty(scene.Gaps());
    }

    [Fact]
    public void AForestLoadedOnlyAsPseudoRoomsIsEnteredUnderTheNumberItsArrivalUses()
    {
        if (Game() is not { } game) return;

        var forest = ScummRoomFirstVisit.Run(game, 58);
        var beach = ScummRoomFirstVisit.Run(game, 1);

        // Room 85 (the map) Object 866 loads pseudo room 218 with the ego; script 1's pseudoRoom maps 201 to 220 to Room 58. The entry draws that cell's paths.
        Assert.Equal(218, RoomVariable(forest));
        Assert.Equal([649, 650, 651, 652, 653, 654, 665, 666, 667, 668], Active(forest));
        Assert.Contains(forest.State.Actors, actor => actor.Number == 1 && actor.Room == 58);
        // Room 1 has no literal arrival; it is loaded only as 130 to 132: the first of them, labelled as an assumed pseudo room.
        Assert.Equal(130, RoomVariable(beach));
        Assert.Equal([21, 22, 23, 296], Active(beach));
        Assert.Contains(beach.Snapshot.Uncertainties, note => note.Contains("Assumed pseudo room", StringComparison.Ordinal));
    }

    [Fact]
    public void ScriptHeavyRoomsOpenWithoutAGapAndHotspotsFollowTheGamesOwnObjectSearch()
    {
        if (Game() is not { } game) return;

        // Script-heavy rooms of the record's sample (the Scumm Bar 28 and the circus 51 never rest: their state at the frame limit).
        foreach (var id in new[] { 28, 29, 35, 41, 51, 53, 39, 70 })
        {
            var scene = ScummRoomFirstVisit.Run(game, id);
            Assert.Empty(scene.Gaps());
            Assert.Equal(id, RoomVariable(scene));
            foreach (var record in scene.Records.Where(scene.IsHotspot))
            {
                Assert.True(record.Width > 0 && record.Height > 0);
                Assert.False(scene.Snapshot.IsUntouchable(record.Id));
            }
        }
    }
}
