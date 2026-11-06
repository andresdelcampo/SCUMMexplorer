using SCUMMexplorer.Core.Discovery;
using Xunit.Abstractions;

namespace SCUMMexplorer.Tests;

/// <summary>
/// The user's Indy 3 tour of 6 November 2026, fixed as engine rules for every game: an actor walking to an actor by width stays in the room, a drawObject of no
/// object does nothing, the hero wears what a picked room's own code gives him, a scene is not played on a room pick, a room no loadRoomWithEgo enters still
/// shows the hero, and a room's loader can start its arrival scene. Record: <c>.verification/user-reports-20261106/README.md</c>.
/// </summary>
public sealed class UserReports20261106Tests(ITestOutputHelper output)
{
    private static GameLibrary? Indy3Vga()
    {
        var folder = Path.Combine(TestGames.Root, "indy3 vga en");
        return File.Exists(Path.Combine(folder, "00.LFL")) ? new GameFolderScanner().Scan(folder) : null;
    }

    private static GameResource Room(GameLibrary game, int id) => game.Rooms.First(r => r.ResourceId == id);

    private static int? HeroCostume(ScummSession session) => session.Actors.First(a => a.Number == 1).Look.Costume;

    [Fact]
    public void ThreeMenWalkToTheCarTogether()
    {
        // Room 6's entry starts Script 73: Script 56 walks each follower with walkActorToActor distance 0xFF (by width). Forgetting the walker took Indy
        // out of the room and left the man following him standing.
        if (Indy3Vga() is not { } game) return;
        var session = ScummGameSession.BootForPlay(game, out _);
        session.VisitRoom(Room(game, 6));
        int? lastFollowerX = null;
        var followerMoves = 0;
        for (var frame = 0; frame < 2000 && session.CurrentRoomId == 6; frame++)
        {
            session.StepFrame();
            var hero = session.Actors.First(a => a.Number == 1);
            Assert.True(hero.Room == 6 || session.CurrentRoomId != 6, $"frame {frame}: Indy left Room 6 while the scene is there (room {hero.Room}, {hero.Look.Estimate})");
            var follower = session.Actors.First(a => a.Number == 11);
            if (lastFollowerX is not null && follower.X != lastFollowerX) followerMoves++;
            lastFollowerX = follower.X;
        }
        output.WriteLine($"the follower moved in {followerMoves} frames; the session ended in Room {session.CurrentRoomId}");
        Assert.Equal(9, session.CurrentRoomId);
        Assert.True(followerMoves > 20, $"actor 11 moved in only {followerMoves} frames");
    }

    [Fact]
    public void ACastlePlanKeepsDrawingAsIndyWalks()
    {
        // Script 25 draws its second section, object 0 when Indy stands in one box, every frame: a drawObject of no object does nothing (o5_drawObject).
        if (Indy3Vga() is not { } game) return;
        var session = ScummGameSession.BootForPlay(game, out _);
        session.VisitRoom(Room(game, 32));
        for (var frame = 0; frame < 5; frame++) session.StepFrame();
        Assert.DoesNotContain(session.Snapshot.Uncertainties, line => line.Contains("drawObject targets", StringComparison.Ordinal));
    }

    [Fact]
    public void TheCastlePlansAreLive()
    {
        // Decision 7 (user, 6 November 2026): the guards' patrols (Script 58) watch for Indy; they do not make the room a still picture. In Room 32 a guard patrols
        // through the door of the assumed arrival, so another way in is taken.
        if (Indy3Vga() is not { } game) return;
        var boot = ScummGameSession.BootForPlay(game, out _);
        foreach (var plan in new[] { 20, 28, 32 })
        {
            var prepared = boot.PrepareManualRoom(Room(game, plan));
            output.WriteLine($"Room {plan}: {prepared.Reason ?? "live"}");
            Assert.True(prepared.Succeeded, prepared.Reason);
            Assert.Equal(plan, prepared.Session!.CurrentRoomId);
            Assert.Equal(0x14, HeroCostume(prepared.Session));
        }
    }

    [Fact]
    public void APickedRoomDressesTheHeroItsOwnWay()
    {
        if (Indy3Vga() is not { } game) return;
        // Room 12's entry +0x001E: costume 0x2A, the small Indy of the castle front (it was overwritten after the entry).
        var session = ScummGameSession.BootForPlay(game, out _);
        session.VisitRoomAsPicked(Room(game, 12));
        Assert.Equal(0x2A, HeroCostume(session));
        // What Room 12 dressed him in does not follow him into a room that says nothing of it.
        session.VisitRoomAsPicked(Room(game, 83));
        Assert.Equal(2, HeroCostume(session));
        // Rooms 28 and 32, Script 25 +0x001F: the top-down costume by what he wears (a test the visit cannot answer): the first its code gives.
        foreach (var plan in new[] { 28, 32 })
        {
            var fresh = ScummGameSession.BootForPlay(game, out _);
            fresh.VisitRoomAsPicked(Room(game, plan));
            Assert.Equal(0x14, HeroCostume(fresh));
        }
    }

    [Fact]
    public void ARoomPickDoesNotPlayAScene()
    {
        // Script 90 is a page: no time passes, no actor acts (Room 74, the plane's manual). Script 55 is a scene with walks and lines (Room 3): on demand only.
        if (Indy3Vga() is not { } game) return;
        var session = ScummGameSession.BootForPlay(game, out _);
        Assert.Equal("global script 90", session.ShownOnlyBy(74));
        Assert.Null(session.ShownOnlyBy(3));
    }

    [Fact]
    public void AScriptThatStandsTheHeroInARoomIsAnArrival()
    {
        // No loadRoomWithEgo enters Room 9; Script 73 +0x00FC stands him there by hand.
        if (Indy3Vga() is not { } game) return;
        var index = ScummEgoArrivalIndex.For(game);
        Assert.Empty(index.Into(9));
        Assert.NotEmpty(index.PlacedInto(9));
        var session = ScummGameSession.BootForPlay(game, out _);
        session.VisitRoomAsPicked(Room(game, 9));
        Assert.Equal(9, session.Actors.First(a => a.Number == 1).Room);
    }

    [Fact]
    public void AnAmbientPreviewKeepsTheActorsOfThePicture()
    {
        // Room 15's fountains are ambient: its still preview's frames lost the Indy its first-visit picture showed.
        if (Indy3Vga() is not { } game) return;
        var picture = ScummRoomFirstVisit.Run(game, 15);
        Assert.Contains(picture.State.Actors, a => a.Number == 1 && a.Room == 15);
        var preview = ScummGameSession.BootForPlay(game, out _).PrepareAmbientRoom(Room(game, 15), picture: picture.State);
        Assert.NotNull(preview.Session);
        var hero = preview.Session!.Actors.First(a => a.Number == 1);
        Assert.Equal(15, hero.Room);
        Assert.Equal(picture.State.Actors.First(a => a.Number == 1).Look.Costume, hero.Look.Costume);
    }

    [Fact]
    public void ARoomsLoaderCanStartItsArrivalScene()
    {
        // Script 91 +0x00AC loads Room 15 and starts Script 98: Elsa meets Indy. It belongs to Room 15, and begins from a fresh start.
        if (Indy3Vga() is not { } game) return;
        var planner = new ScummCutscenePlanner(game);
        var trigger = planner.Triggers.First(t => t.Room == 15 && t.IsArrivalScript && t.VerbId == 98);
        var run = planner.Run(trigger);
        output.WriteLine($"begun {run.Begun}, way {run.Way}, lines {run.Lines}, {run.Frames} frames");
        Assert.True(run.Begun);
        Assert.Equal(ScummPickedStart.AtCutscene, run.Way);
        Assert.True(run.Lines > 3);
    }
}
