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
    public void HiddenActorsAndActorsWithoutACostumeAreNotDrawn()
    {
        var hidden = new LoomActorSnapshot(1, 5, 10, 20, 0, false, false, null, null) { Look = LoomActorLook.Initial with { Costume = 1 } };
        var bare = new LoomActorSnapshot(2, 5, 10, 20, 0, true, false, null, null);
        var layer = LoomActorRenderer.Render([hidden, bare], 5, 40, 40, _ => throw new InvalidOperationException("no costume is read"));
        Assert.Empty(layer.Actors);
        Assert.All(layer.Pixels, value => Assert.Equal(LoomActorRenderer.Transparent, value));
    }
}
