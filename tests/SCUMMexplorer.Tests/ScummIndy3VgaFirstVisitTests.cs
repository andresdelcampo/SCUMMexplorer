using SCUMMexplorer.Core.Discovery;

namespace SCUMMexplorer.Tests;

/// <summary>
/// S5 Indy3Vga (16 October 2026): the first visit of a room from the generic session for the 256-colour Indiana Jones and the Last Crusade (one file per
/// room, version 3 scripts). Three named rooms are pinned: the college gym (Room 73, the boot's own room), Room 45 and Room 90 (entered without their route).
/// Their settled object states are the ones the EGA release reaches (<see cref="ScummRoomFirstVisitTests"/>): the same game, read from its own data.
/// Record: <c>.verification/indy3vga-objects-20261015/README.md</c>. Skips when the game folder is absent.
/// </summary>
public sealed class ScummIndy3VgaFirstVisitTests
{
    private static GameLibrary? Game() => ScummIndy3VgaSessionTests.Folder() is { } folder ? new GameFolderScanner().Scan(folder) : null;

    private static int[] Active(ScummRoomScene scene) => scene.Images.Where(scene.Selection.IsActive).Select(image => image.Id).Distinct().Order().ToArray();

    [Fact]
    public void TheGymIsTheBootsOwnSettledStateWithTheEgoAndTheBoxer()
    {
        if (Game() is not { } game) return;

        var scene = ScummRoomFirstVisit.Run(game, 73);

        Assert.True(scene.IsFirstRoomOfTheGame);
        Assert.Equal(ScummSettleOutcome.Settled, scene.Outcome);
        Assert.Equal([826, 828, 830, 832], Active(scene));
        var ego = scene.State.Actors.Single(actor => actor.Number == 1);
        Assert.Equal(1, ego.Look.Costume);
        Assert.Contains(scene.State.Actors, actor => actor is { Number: 9, Room: 73 } && actor.Look.Costume == 38);
        Assert.Empty(scene.Gaps());
        Assert.Equal(2, scene.ActorLayer().Actors.Count);
    }

    [Fact]
    public void RoomsEnteredWithoutTheirRouteRunTheirOwnScriptsAndSettle()
    {
        if (Game() is not { } game) return;

        var stage = ScummRoomFirstVisit.Run(game, 45);
        var diary = ScummRoomFirstVisit.Run(game, 90);

        Assert.False(stage.IsFirstRoomOfTheGame);
        Assert.Equal(ScummSettleOutcome.Settled, stage.Outcome);
        Assert.Equal([615, 618, 621], Active(stage));
        Assert.Equal(ScummSettleOutcome.Settled, diary.Outcome);
        Assert.Equal([913, 915, 917, 920, 921, 922], Active(diary));
        Assert.Contains(stage.Notes, note => note.Contains("without playing the route", StringComparison.Ordinal));
        Assert.Empty(stage.Gaps());
        Assert.Empty(diary.Gaps());
    }

    [Fact]
    public void ScriptHeavyRoomsOpenWithoutAGapAndHotspotsFollowTheGamesOwnObjectSearch()
    {
        if (Game() is not { } game) return;
        Assert.True(ScummProfile.For(game).HasFirstVisitScenes);

        // The most script-heavy rooms of the record's audit (Room 82 is left for Room 94 by its own script; 15 and 79 never rest).
        foreach (var id in new[] { 82, 70, 35, 23, 14, 25, 79, 15, 48 })
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
