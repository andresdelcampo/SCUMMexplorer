using SCUMMexplorer.Core.Discovery;

namespace SCUMMexplorer.Tests;

/// <summary>
/// Indy 3 EGA first visits (30 October 2026, <c>.verification/indy3-rooms-20261030/README.md</c>): <c>getActorWidth</c> (ScummVM <c>o5_getActorWidth</c>,
/// <c>_width</c> 24 from <c>initActor</c>, changed by <c>actorOps width</c>) is answered, and <c>actorFollowCamera</c> on an actor in another room starts
/// that room's scene in version 3 as in every version (ScummVM <c>setCameraFollows</c>). Skips without the data.
/// </summary>
public sealed class ScummIndy3FirstVisitTests
{
    private static string? Folder()
    {
        var overridden = Environment.GetEnvironmentVariable("SCUMM_INDY3_EGA_TEST_ROOT");
        var folder = string.IsNullOrWhiteSpace(overridden) ? Path.Combine(TestGames.Root, "indy3 ega en") : overridden;
        return File.Exists(Path.Combine(folder, "00.LFL")) ? folder : null;
    }

    [Fact]
    public void TheCollegeOutsideFollowsIndyIntoTheNextRoomWithNothingUnfollowed()
    {
        // Room 6: Script 56 +0x0000 reads getActorWidth (nested in an expression and plain at +0x000E); Script 73 +0x00F4 follows actor 1, who is in
        // Room 9, so the game starts Room 9's scene.
        if (Folder() is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        Assert.True(ScummProfile.For(game).CameraFollowStartsScene);

        var scene = ScummRoomFirstVisit.Run(game, 6);

        Assert.Equal(ScummSettleOutcome.RoomLeft, scene.Outcome);
        Assert.Equal(9, scene.LeftTo);
        Assert.DoesNotContain(scene.Snapshot.Uncertainties, item => item.Contains("getActorWidth", StringComparison.Ordinal));
        Assert.DoesNotContain(scene.Snapshot.Uncertainties, item => item.Contains("which would load that room", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(13)]
    public void AStartedScriptReceivesItsIndexedArguments(int room)
    {
        // Room 3's entry +0x006F passes var[0x064+local[0x000]] to local script 200 (twelve times: the students' images); Room 13's Script 131 +0x0038
        // passes local[0x003+0x000] .. local[0x003+0x004] to Script 132. The executor now resolves an indexed argument when the script starts.
        if (Folder() is not { } folder) return;
        var scene = ScummRoomFirstVisit.Run(new GameFolderScanner().Scan(folder), room);

        Assert.DoesNotContain(scene.Snapshot.Uncertainties, item => item.Contains("arguments depending on unknown variables", StringComparison.Ordinal));
        if (room == 3) Assert.True(scene.Images.Where(scene.Selection.IsActive).Select(image => image.Id).Distinct().Count() >= 12);
    }

    [Fact]
    public void AnActorsWidthStartsAtTwentyFourAndFollowsActorOps()
    {
        var actors = new ScummActorTable();
        Assert.Equal(ScummActorTable.DefaultWidth, actors.Width(1));
        actors.SetWidth(1, 6);
        Assert.Equal(6, actors.Width(1));
        actors.Init(1);
        Assert.Equal(24, actors.Width(1));
    }
}
