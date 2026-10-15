using SCUMMexplorer.Core.Discovery;

namespace SCUMMexplorer.Tests;

/// <summary>
/// S5 (14 October 2026): the first visit of a room from the generic session for Indiana Jones and the Last Crusade EGA (English, the family's fixture):
/// the settled object states of three named rooms are pinned, and the rules of the visit are checked. Record: <c>.verification/indy3-objects-20261014/README.md</c>.
/// The tests skip when the game folder is absent.
/// </summary>
public sealed class ScummRoomFirstVisitTests
{
    private static GameLibrary? Game()
    {
        var overridden = Environment.GetEnvironmentVariable("SCUMM_INDY3_EGA_TEST_ROOT");
        var folder = string.IsNullOrWhiteSpace(overridden) ? Path.Combine(TestGames.Root, "indy3 ega en") : overridden;
        return File.Exists(Path.Combine(folder, "00.LFL")) ? new GameFolderScanner().Scan(folder) : null;
    }

    private static int[] Active(ScummRoomScene scene) => scene.Images.Where(scene.Selection.IsActive).Select(image => image.Id).Order().ToArray();

    [Fact]
    public void TheFirstRoomIsTheBootsOwnSettledStateWithThePlayerInWetClothes()
    {
        if (Game() is not { } game) return;

        var scene = ScummRoomFirstVisit.Run(game, 73);

        Assert.True(scene.IsFirstRoomOfTheGame);
        Assert.Equal(ScummSettleOutcome.Settled, scene.Outcome);
        // The crowd and the boxers: the objects the room's entry and ambient scripts leave visible (the ambient animators are in one phase of their loop).
        Assert.Equal([826, 828, 830, 832], Active(scene));
        Assert.Empty(scene.Gaps());
        var ego = scene.State.Actors.Single(actor => actor.Number == 1);
        Assert.Equal(1, ego.Look.Costume);
        Assert.Equal(38, scene.State.Actors.Single(actor => actor.Number == 9).Look.Costume);
    }

    [Fact]
    public void ARoomEnteredWithoutItsRouteRunsItsOwnScriptsAndSettles()
    {
        if (Game() is not { } game) return;

        var stage = ScummRoomFirstVisit.Run(game, 45);
        var diary = ScummRoomFirstVisit.Run(game, 90);

        Assert.False(stage.IsFirstRoomOfTheGame);
        Assert.Equal(ScummSettleOutcome.Settled, stage.Outcome);
        Assert.Equal([615, 618, 621], Active(stage));
        Assert.Equal(ScummSettleOutcome.Settled, diary.Outcome);
        Assert.Equal([913, 915, 917, 920, 921, 922], Active(diary));
        Assert.Empty(stage.Gaps());
        Assert.Empty(diary.Gaps());
        // A state preview says so, and the run lists what it assumed.
        Assert.Contains(stage.Notes, note => note.Contains("without playing the route", StringComparison.Ordinal));
    }

    [Fact]
    public void EveryRoomOpensWithoutAGapAndHotspotsFollowTheGamesOwnObjectSearch()
    {
        if (Game() is not { } game) return;

        // A sample across the game: the opening rooms, a street, the castle, the catacombs and the zeppelin.
        foreach (var id in new[] { 1, 2, 15, 20, 28, 36, 52, 71, 86 })
        {
            var scene = ScummRoomFirstVisit.Run(game, id);
            Assert.Empty(scene.Gaps());
            foreach (var record in scene.Records.Where(scene.IsHotspot))
            {
                Assert.True(record.Width > 0 && record.Height > 0);
                Assert.False(scene.Snapshot.IsUntouchable(record.Id));
            }
        }
    }
}
