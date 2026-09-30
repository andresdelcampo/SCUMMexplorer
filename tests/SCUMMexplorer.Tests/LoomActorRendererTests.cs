using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;
using SCUMMexplorer.Core.Rendering;

namespace SCUMMexplorer.Tests;

/// <summary>Actors drawn in the room (M2c): ScummVM <c>processActors</c> order and <c>drawActorCostume</c> placement.</summary>
public sealed class LoomActorRendererTests
{
    private static readonly string Folder =
        Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games/loom"));

    private static bool HasGame => File.Exists(Path.Combine(Folder, "00.LFL"));

    [Fact]
    public void AfterTheBootTheEgoIsDrawnAtItsFeetInRoom02()
    {
        if (!HasGame) return;
        var game = new GameFolderScanner().Scan(Folder);
        var session = new LoomExplorationSession(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(Folder, "00.LFL")), completeBoot: true);
        var layer = LoomActorRenderer.Render(session.Actors, 2, 960, 144, id => LoomCostume.Load(game, id));

        // Room 02 after the boot: the ego, the last leaf (actor 5, costume 8, Room 02 local 200 +0x002B), the entry's
        // actor 4 (costume 141 at (433, 129), ignoring boxes after setClass class 22) and the swans (costume 5).
        Assert.Equal([1, 4, 5, 10, 11], layer.Actors.Select(actor => actor.Number).Order());
        var leaf = layer.Actors.Single(actor => actor.Number == 5);
        Assert.Equal((8, 807, 25), (leaf.Costume, leaf.X, leaf.Y));
        Assert.Equal((433, 129), (layer.Actors.Single(actor => actor.Number == 4).X, layer.Actors.Single(actor => actor.Number == 4).Y));
        var ego = layer.Actors.Single(actor => actor.Number == 1);
        Assert.Equal((1, 1, 828, 60, 0), (ego.Number, ego.Costume, ego.X, ego.Y, ego.Elevation));
        Assert.True(ego.Pixels > 100);
        // The costume's limbs stand on the actor's point: the drawn image ends at the feet and spans x = 828.
        var (left, top, right, bottom) = ego.Bounds!.Value;
        Assert.InRange(bottom, 55, 64);
        Assert.True(left < 828 && right > 828 && top < 40, $"bounds {left},{top} - {right},{bottom}");
        Assert.Contains(layer.Pixels, value => value != LoomActorRenderer.Transparent);
        // Nothing is drawn outside the drawn actors' bounds.
        var boxes = layer.Actors.Where(actor => actor.Bounds is not null).Select(actor => actor.Bounds!.Value).ToArray();
        for (var y = 0; y < 144; y++)
        for (var x = 0; x < 960; x++)
            if (layer.Pixels[y * 960 + x] != LoomActorRenderer.Transparent)
                Assert.Contains(boxes, b => x >= b.Left && x < b.Right && y >= b.Top && y < b.Bottom);
    }

    [Fact]
    public void AStandingActorOnADirectionLockedBoxTurnsLeftOrRight()
    {
        if (!HasGame) return;
        var game = new GameFolderScanner().Scan(Folder);
        var boxes = LoomWalkBoxReader.Read(File.ReadAllBytes(Path.Combine(Folder, "02.LFL")));
        Assert.Equal(1, boxes.GetFlags(1) & 7);
        var table = new LoomActorTable { CostumeSource = id => LoomCostume.Load(game, id) };
        var place = new LoomActorTable.Placement(2, boxes, null, null);
        table.CostumeChanged(3, 1, place);
        // adjustActorPos on box 1 (flags 0x01): turnToDirection(180) is remapped to 270 (remapDirection, standing).
        table.PutInRoom(3, 2, place, ego: 1);
        table.Put(3, 860, 70, place, ego: 1);
        Assert.Equal(LoomActorTable.MoveTurn, table[3].Moving);
        // The locked box's direction is not interpolated: the next frame's walkActor takes it at once.
        table.StepWalks(place);
        Assert.Equal((1, 270), (table[3].WalkBox, table[3].Look.Facing));
        // On box 0 (no flags) the facing is kept.
        table.Animate(3, 0xFA, place);
        table.Put(3, 820, 60, place, ego: 1);
        Assert.Equal((0, 180), (table[3].WalkBox, table[3].Look.Facing));
    }

    [Fact]
    public void ActorsAreSortedWithScummVmsExchangeSort()
    {
        static LoomActorSnapshot Actor(int number, int y) => new(number, 5, 10, y, 0, true, false, null, null);
        // For each j, each i: swap when y[j] < y[i]. It sorts by ascending y, so lower actors are drawn later (on top).
        var sorted = LoomActorRenderer.SortForDrawing([Actor(1, 90), Actor(2, 40), Actor(3, 60), Actor(4, 40)], 5);
        Assert.Equal([2, 4, 3, 1], sorted.Select(actor => actor.Number));
        // Actors in other rooms are left out.
        Assert.Empty(LoomActorRenderer.SortForDrawing([Actor(1, 90)], 6));
    }

    [Fact]
    public void AnActorInAMaskedWalkBoxIsHiddenWhereTheDepthMaskIsSet()
    {
        if (!HasGame) return;
        var game = new GameFolderScanner().Scan(Folder);
        var session = new LoomExplorationSession(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(Folder, "00.LFL")), completeBoot: true);
        var boxes = session.CurrentWalkBoxes!;
        // Room 02's boxes 3 and up have mask 1, box 0 (where the boot leaves the ego) has 0; no box (255) reads as 1.
        Assert.Equal((0, 1, 1), (boxes.GetMask(0), boxes.GetMask(4), boxes.GetMask(LoomWalkBoxSet.InvalidBox)));
        var ego = session.Actors.Single(actor => actor.Number == 1);
        var full = Enumerable.Repeat((byte)1, 960 * 144).ToArray();
        LoomDrawnActor Draw(LoomActorSnapshot actor, byte[]? mask) =>
            LoomActorRenderer.Render([actor], 2, 960, 144, id => LoomCostume.Load(game, id), mask, boxes).Actors.Single();
        var front = Draw(ego, full);
        Assert.Equal(0, front.ZPlane);
        Assert.True(front.Pixels > 100);
        var behind = Draw(ego with { WalkBox = 4 }, full);
        Assert.Equal((1, 0), (behind.ZPlane, behind.Pixels));
        // Without a mask nothing is hidden; an unknown box draws the actor in front and says so.
        Assert.Equal(front.Pixels, Draw(ego with { WalkBox = 4 }, null).Pixels);
        var unknown = Draw(ego with { WalkBox = null }, full);
        Assert.Equal(front.Pixels, unknown.Pixels);
        Assert.Contains("walk box unknown", unknown.Estimate);
    }

    [Fact]
    public void NeverClipActorsSuchAsRoom43sSheepAreDrawnInFrontOfTheDepthMask()
    {
        if (!HasGame) return;
        var game = new GameFolderScanner().Scan(Folder);
        var session = new LoomExplorationSession(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(Folder, "00.LFL")), completeBoot: true);
        session.VisitRoom(game.Rooms.Single(room => room.ResourceId == 43));
        // Room 43's entry gives the sheep setClass [0x0000, 0x0096, 0x0094]: ignore boxes (so box 255, whose mask
        // reads as 1) and class 20, NeverClip, which drawActorCostume honours before the box's mask.
        var sheep = session.Actors.Where(actor => actor.Room == 43 && actor.Visible == true && actor.IgnoreBoxes == true &&
            session.ObjectClasses.TryGetValue(actor.Number, out var bits) &&
            LoomObjectClasses.Has(bits, LoomActorRenderer.NeverClipClass)).ToArray();
        Assert.True(sheep.Length >= 2, $"{sheep.Length} never-clip actors in Room 43");
        const int width = 2048;
        var full = Enumerable.Repeat((byte)1, width * 144).ToArray();
        var classed = LoomActorRenderer.Render(sheep, 43, width, 144, id => LoomCostume.Load(game, id), full,
            session.CurrentWalkBoxes, session.ObjectClasses);
        Assert.All(classed.Actors, actor => Assert.Equal(0, actor.ZPlane));
        Assert.All(classed.Actors, actor => Assert.True(actor.Pixels > 0));
        // Without the classes, box 255 would hide them behind the whole mask.
        var unclassed = LoomActorRenderer.Render(sheep, 43, width, 144, id => LoomCostume.Load(game, id), full, session.CurrentWalkBoxes);
        Assert.All(unclassed.Actors, actor => Assert.Equal((1, 0), (actor.ZPlane, actor.Pixels)));
        // Class 21 (AlwaysClip) wins over NeverClip and over an unmasked box, as _forceClip does.
        var forced = new Dictionary<int, uint>(session.ObjectClasses) { [sheep[0].Number] = 1u << (LoomActorRenderer.AlwaysClipClass - 1) };
        var clipped = LoomActorRenderer.Render([sheep[0] with { WalkBox = 0 }], 43, width, 144, id => LoomCostume.Load(game, id), full,
            session.CurrentWalkBoxes, forced).Actors.Single();
        Assert.Equal((1, 0), (clipped.ZPlane, clipped.Pixels));
    }

    [Fact]
    public void HiddenActorsAndActorsWithoutACostumeAreNotDrawn()
    {
        var hidden = new LoomActorSnapshot(1, 5, 10, 20, 0, false, false, null, null) { Look = LoomActorLook.Initial with { Costume = 1 } };
        var bare = new LoomActorSnapshot(2, 5, 10, 20, 0, true, false, null, null);
        var layer = LoomActorRenderer.Render([hidden, bare], 5, 40, 40, _ => throw new InvalidOperationException("no costume is read"));
        Assert.Empty(layer.Actors);
        Assert.All(layer.Pixels, value => Assert.Equal(LoomActorRenderer.Transparent, value));
    }
}
