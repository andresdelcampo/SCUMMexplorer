using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;
using SCUMMexplorer.Core.Rendering;

namespace SCUMMexplorer.Tests;

/// <summary>
/// Actor costume state (M2b): <c>actorOps</c> costume, elevation, walk speed, frames and palette,
/// <c>animateActor</c> (ScummVM <c>Actor::animateActor</c> for v3), <c>setDirection</c> and the talk frames.
/// </summary>
public sealed class LoomActorLookTests
{
    private static readonly string Folder =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games/loom"));

    private static bool HasGame => File.Exists(Path.Combine(Folder, "00.LFL"));

    [Fact]
    public void AfterTheBootTheEgoStandsInRoom02WithCostume1()
    {
        if (!HasGame) return;
        var game = new GameFolderScanner().Scan(Folder);
        var session = new LoomExplorationSession(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(Folder, "00.LFL")), completeBoot: true);
        var ego = session.Actors[0];
        Assert.Equal((2, 828, 60, true), (ego.Room, ego.X, ego.Y, ego.Visible));

        // Script 017 +0x009B: costume var[0x077] = 1, walk speed (4, 2), elevation 0; bit[0xD61] is set after the boot,
        // so the frames are 8/9/10 and talk 14/15 (Bobbin without the distaff, see .verification/costume-anim).
        var look = ego.Look;
        Assert.Equal((1, 0), (look.Costume, look.Elevation));
        Assert.Equal((8, 9, 10, 14, 15), (look.InitFrame, look.WalkFrame, look.StandFrame, look.TalkStartFrame, look.TalkStopFrame));
        Assert.Equal((4, 2), (look.WalkSpeedX, look.WalkSpeedY));
        Assert.Equal(Enumerable.Range(0, 16).Select(i => (byte)i), look.Palette);
        // The boot's last talk ended with stopTalk: the talk-stop frame (15) restarted only the head (limb 1).
        Assert.Equal(15, look.Frame);
        Assert.NotNull(look.Direction);
        // The stand frame's limbs for the ego's direction: body and head (costume 1, animations 40..43).
        Assert.Equal(40 + look.Direction, look.Animation.Animation[0]);
        Assert.True(look.Animation.Positions[0] >= 0 && look.Animation.Positions[1] >= 0);
    }

    [Fact]
    public void AnimateActorMapsStopFaceTurnAndFrames()
    {
        if (!HasGame) return;
        var game = new GameFolderScanner().Scan(Folder);
        var table = new LoomActorTable { CostumeSource = id => LoomCostume.Load(game, id) };
        var place = new LoomActorTable.Placement(2, null, null, null);
        table.SetIgnoreBoxes(3, true);
        table.Put(3, 100, 80, 2, place, ego: 1);
        table.CostumeChanged(3, 1, place);

        // showActor after the costume change starts the init frame (1) facing 180 (down: animation 6).
        Assert.Equal((1, 180), (table[3].Look.Frame, table[3].Look.Facing));
        Assert.Equal(6, table[3].Look.Animation.Animation[0]);

        // 0xF9: chore 3, set direction 1 (90) at once; the limbs restart their frame for the new direction.
        Assert.Equal("face 90", table.Animate(3, 0xF9, place));
        Assert.Equal((90, 5), (table[3].Look.Facing, (int)table[3].Look.Animation.Animation[0]));

        // 0x0A: chore 0x3F, so frame 10 itself starts (costume 1 has animations up to 63: 10 * 4 + 1 = 41).
        Assert.Equal("frame 10", table.Animate(3, 0x0A, place));
        Assert.Equal((10, 41), (table[3].Look.Frame, (int)table[3].Look.Animation.Animation[0]));

        // 0xFE: chore 2, the stand frame (3).
        Assert.Equal("stop: stand frame", table.Animate(3, 0xFE, place));
        Assert.Equal(3, table[3].Look.Frame);
        Assert.Equal(13, table[3].Look.Animation.Animation[0]);

        // 0xF6: chore 4, turn to direction 2 (180): turnToDirection sets MF_TURN, and walkActor turns
        // one step a frame (actorTurnInterpolateTable: right to down is one step), then clears _moving.
        Assert.Equal("turn to 180", table.Animate(3, 0xF6, place));
        Assert.Equal((90, LoomActorTable.MoveTurn), (table[3].Look.Facing, table[3].Moving));
        table.StepWalks(place);
        Assert.Equal((180, LoomActorTable.MoveTurn), (table[3].Look.Facing, table[3].Moving));
        table.StepWalks(place);
        Assert.Equal((180, 0), (table[3].Look.Facing, table[3].Moving));
        // A turn to the opposite side goes through a side step: up (0) from down turns right first.
        table.Animate(3, 0xF7, place);
        table.StepWalks(place);
        Assert.Equal(90, table[3].Look.Facing);
        table.StepWalks(place);
        Assert.Equal(0, table[3].Look.Facing);
        table.Animate(3, 0xFA, place);

        // Frame 58 redirects to the stand frame; 57 to the walk frame.
        table.StartAnimation(3, 57, place);
        Assert.Equal(2, table[3].Look.Frame);
        var walk = new List<int>();
        for (var i = 0; i < 7; i++)
        {
            walk.Add(table[3].Look.Animation.Positions[0]);
            table.AnimateCostumes(place);
        }
        Assert.Equal([23, 24, 25, 26, 27, 28, 23], walk);
        table.StartAnimation(3, 58, place);
        Assert.Equal(3, table[3].Look.Frame);
    }

    [Fact]
    public void AnActorOutsideTheRoomKeepsItsFrame()
    {
        if (!HasGame) return;
        var game = new GameFolderScanner().Scan(Folder);
        var table = new LoomActorTable { CostumeSource = id => LoomCostume.Load(game, id) };
        var place = new LoomActorTable.Placement(2, null, null, null);
        table.PutInRoom(4, 5, place, ego: 1);
        table.CostumeChanged(4, 1, place);
        table.Animate(4, 0x0A, place);
        // startAnimActor needs the actor in the current room; setDirection still changes the facing.
        Assert.Equal(0, table[4].Look.Frame);
        table.Animate(4, 0xF8, place);
        Assert.Equal(270, table[4].Look.Facing);
        Assert.All(table[4].Look.Animation.Positions, position => Assert.Equal(-1, position));
    }

    [Fact]
    public void ActorOpsPaletteElevationAndInit()
    {
        var table = new LoomActorTable();
        table.SetPalette(1, 7, 15);
        table.SetElevation(1, 25);
        table.SetFrame(1, "stand", 10);
        Assert.Equal((byte)15, table[1].Look.Palette[7]);
        Assert.Equal(25, table[1].Look.Elevation);
        table.Init(1);
        // initActor(0): elevation 0 and the default frames; the palette stays until the next costume.
        Assert.Equal((0, 3), (table[1].Look.Elevation, table[1].Look.StandFrame));
        Assert.Equal((byte)15, table[1].Look.Palette[7]);
    }

    [Fact]
    public void ActorOpsOnAVariableActorAppliesItsFirstItem()
    {
        if (!HasGame) return;
        var game = new GameFolderScanner().Scan(Folder);
        var session = new LoomExplorationSession(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(Folder, "00.LFL")), completeBoot: true);
        // The trial (script 046) flies the Guild off through Room 11: +0x1153-0x1175 loops local[0] over actors 2-10 with
        // actorOps local[0x000], [costume, 0x50; ...]. The list's first item (the costume) must not be lost to local[0x000]'s bracket.
        session.VisitRoom(game.Rooms.Single(room => room.ResourceId == 8));
        session.ActivateObjectVerb(255, 0x38);
        for (var frame = 0; frame < 4000 && session.AdvanceFrame().RoomId != 11; frame++) { }
        var flight = session.Actors.Where(actor => actor.Number is >= 2 and <= 10).ToArray();
        Assert.Equal(9, flight.Length);
        Assert.All(flight, actor => Assert.Equal((11, 80), (actor.Room, actor.Look.Costume)));
    }
}
