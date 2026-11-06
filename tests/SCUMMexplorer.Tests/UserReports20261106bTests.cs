using SCUMMexplorer.Core.Discovery;
using Xunit.Abstractions;

namespace SCUMMexplorer.Tests;

/// <summary>
/// The user's second Indy 3 list of 6 November 2026, fixed as rules for every game: a doorway is an object you walk through (R-6.13b), a verb run directly
/// knows its object as the sentence script would tell it, and a still room is a session like any other. Record: <c>.verification/user-reports-20261106b/README.md</c>.
/// </summary>
public sealed class UserReports20261106bTests(ITestOutputHelper output)
{
    private static GameLibrary? Game(string folder)
    {
        var path = Path.Combine(TestGames.Root, folder);
        return Directory.Exists(path) ? new GameFolderScanner().Scan(path) : null;
    }

    private static GameResource Room(GameLibrary game, int id) => game.Rooms.First(r => r.ResourceId == id);

    /// <summary>The rooms an object is a doorway to, by the shared definition, as the app reads them.</summary>
    private static int[] DoorwayTargets(GameLibrary game, int roomId, int objectId)
    {
        var data = ScummDataSources.For(game);
        var records = data.ReadRoom(roomId).Objects;
        var record = records.First(r => r.Id == objectId);
        var finder = new ScummDoorwayFinder(game, data);
        var room = Room(game, roomId);
        return finder.DoorVerbs(data.ReadObjectVerbs(roomId, records).GetValueOrDefault(objectId) ?? [])
            .SelectMany(verb => finder.FindDoorway(room, data.ReadObjectVerb(roomId, record, verb.VerbId, out _), verb))
            .Select(target => target.RoomId).Distinct().Order().ToArray();
    }

    [Theory]
    // Indy 3 Room 48: the tunnel and the door are doorways; the statues only spring a trap (local script 200's cutscene, Room 53).
    [InlineData("indy3 vga en", 48, 640, new[] { 47 })]
    [InlineData("indy3 vga en", 48, 641, new[] { 60 })]
    [InlineData("indy3 vga en", 48, 657, new int[0])]
    [InlineData("indy3 ega en", 48, 658, new int[0])]
    // Room 93: the way back is a doorway; drinking from the Grail (verb 0x08) leads there too and is not one.
    [InlineData("indy3 vga en", 93, 944, new[] { 82 })]
    [InlineData("indy3 vga en", 93, 945, new int[0])]
    // Room 86: the Grails' default code plays Script 122's cutscene first.
    [InlineData("indy3 vga en", 86, 888, new int[0])]
    // Room 4's doors, whose walk verb tests that they are open.
    [InlineData("indy3 vga en", 4, 115, new[] { 1 })]
    // Monkey Island 2's exits often have only default code; they stay doorways.
    [InlineData("mi2 mt32 en", 4, 22, new[] { 2 })]
    [InlineData("mi2 mt32 en", 7, 47, new[] { 15 })]
    // Room 53's door plays a short cutscene of its own, then loads the room.
    [InlineData("mi2 mt32 en", 53, 597, new[] { 54 })]
    // Monkey Island's walk verb differs between copies of one game.
    [InlineData("monkey1 vga es", 7, 91, new int[0])]
    // Maniac Mansion and LOOM keep theirs: LOOM Room 3's path through a local script, its look at the sky in Room 4 not.
    [InlineData("loom ega mt32 en", 3, 161, new[] { 5 })]
    [InlineData("loom ega mt32 en", 4, 177, new int[0])]
    public void ADoorwayIsAnObjectYouWalkThrough(string folder, int room, int objectId, int[] targets)
    {
        if (Game(folder) is not { } game) return;
        Assert.Equal(targets, DoorwayTargets(game, room, objectId));
    }

    [Fact]
    public void AVerbRunDirectlyActsOnItsOwnObject()
    {
        // Room 4's door: verb 0x01 starts Script 26 with var[0x007], which only the sentence script sets; Script 26 then opened the object last used.
        if (Game("indy3 vga en") is not { } game) return;
        var session = ScummGameSession.BootForPlay(game, out _);
        ScummCutscenePlanner.IntoControl(session);
        session.VisitRoomAsPicked(Room(game, 4));
        Assert.Equal(0, session.Snapshot.ObjectStates.GetValueOrDefault(115));
        session.ActivateObjectVerb(115, 0x01);
        for (var frame = 0; frame < 30; frame++) session.StepFrame();
        Assert.Equal(1, session.Snapshot.ObjectStates.GetValueOrDefault(115));
        Assert.Equal(0, session.Snapshot.ObjectStates.GetValueOrDefault(116));
        session.ActivateObjectVerb(115, 0x02);
        for (var frame = 0; frame < 30; frame++) session.StepFrame();
        Assert.Equal(0, session.Snapshot.ObjectStates.GetValueOrDefault(115));
    }

    [Theory]
    // Room 62 has no ambient script and two doors; Room 6 is wider than the screen.
    [InlineData(62)]
    [InlineData(6)]
    public void AStillRoomIsASessionThatShowsItsPicture(int roomId)
    {
        if (Game("indy3 vga en") is not { } game) return;
        var boot = ScummGameSession.BootForPlay(game, out _);
        ScummCutscenePlanner.IntoControl(boot);
        Assert.False(boot.PrepareManualRoom(Room(game, roomId)).Succeeded);
        var picture = ScummRoomFirstVisit.Run(game, roomId);
        var prepared = boot.PrepareAmbientRoom(Room(game, roomId), requireAmbient: false, picture: picture.State);
        var session = Assert.IsType<ScummGameSession>(prepared.Session);
        Assert.Equal(roomId, session.CurrentRoomId);
        Assert.Equal(picture.State.CameraX, session.CameraX);
        var (top, bottom) = picture.MainScreen ?? (16, 144);
        session.UseScreen((top, bottom));
        var want = ScummPlayScreen.Still(picture, top, bottom, wide: false);
        var got = new ScummPlayScreen(game, session, picture.IndexedStates).Compose(0);
        var different = 0;
        for (var y = top; y < bottom; y++)
            for (var x = 0; x < ScummPlayFrame.Width; x++)
                if (want.Pixels[y * ScummPlayFrame.Width + x] != got.Pixels[y * ScummPlayFrame.Width + x]) different++;
        output.WriteLine($"Room {roomId}: {different} pixels differ");
        Assert.Equal(0, different);
    }

    [Fact]
    public void AWideStillRoomScrolls()
    {
        if (Game("indy3 vga en") is not { } game) return;
        var boot = ScummGameSession.BootForPlay(game, out _);
        ScummCutscenePlanner.IntoControl(boot);
        var picture = ScummRoomFirstVisit.Run(game, 6);
        var session = Assert.IsType<ScummGameSession>(boot.PrepareAmbientRoom(Room(game, 6), requireAmbient: false, picture: picture.State).Session);
        var start = session.CameraX;
        session.PanCameraBy(-64);
        for (var frame = 0; frame < 12; frame++) session.StepFrame();
        Assert.True(session.CameraX < start, $"the camera stayed at {session.CameraX}");
    }
}
