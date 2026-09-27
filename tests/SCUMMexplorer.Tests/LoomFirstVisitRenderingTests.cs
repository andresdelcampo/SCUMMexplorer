using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;
using SCUMMexplorer.Core.Rendering;

namespace SCUMMexplorer.Tests;

/// <summary>
/// First-visit renders of real LOOM rooms, built the way the exploration view
/// builds them: a fresh session visits the room, then gaps are closed from
/// script draw batches when the reached state is not faithful.
/// </summary>
public sealed class LoomFirstVisitRenderingTests
{
    private static readonly Lazy<Fixture?> Data = new(Fixture.Load);

    [Fact]
    public void Room56OpeningFrameIsTheFaceThenItsMouth()
    {
        if (Data.Value is not { } data) return;
        var opening = data.Batches.ForRoom(56).Where(batch => batch.Priority == LoomSceneDrawBatches.OpeningFramePriority);
        // Script 68 loads Room 56 twice; only the first load is the opening frame.
        Assert.Equal([779, 782], Assert.Single(opening).Steps.Select(step => step.ObjectId));

        var (selection, estimates, background) = data.FirstVisit(56);

        Assert.Equal([779, 782], estimates.Select(item => item.ObjectId));
        Assert.Equal([779, 782], selection.PaintOrder().Select(obj => obj.Id).TakeLast(2));
        Assert.Empty(selection.VisibleGaps());
        AssertTopImageIsVisible(selection, background, 782);
    }

    [Fact]
    public void Room58WithoutScriptEvidenceUsesALabelledRandomPickAndLeavesNoGap()
    {
        if (Data.Value is not { } data) return;
        // No decoded script draws or loads Room 58 by literal, so no batch exists.
        Assert.Empty(data.Batches.ForRoom(58));

        var (selection, estimates, _) = data.FirstVisit(58);

        Assert.NotEmpty(estimates);
        Assert.All(estimates, item => Assert.StartsWith("random choice", item.Reason, StringComparison.Ordinal));
        Assert.Empty(selection.VisibleGaps());
    }

    [Fact]
    public void Room17HolesAreFilledByOneConsistentLocalScriptBatch()
    {
        if (Data.Value is not { } data) return;
        var (selection, _, _) = data.FirstVisit(17);
        var images = data.Images(17);
        var active = images.Where(selection.IsActive).Select(obj => obj.Id).ToHashSet();

        var batch = Assert.Single(data.Batches.ForRoom(17), item => item.Steps.Select(step => step.ObjectId).ToHashSet().SetEquals(active));
        Assert.Equal(4, active.Count);
        Assert.Contains("local script", batch.Source, StringComparison.Ordinal);
        Assert.Empty(selection.VisibleGaps());
    }

    [Fact]
    public void Room03DoorwaysIncludeExitsReachedThroughLocalScripts()
    {
        if (Data.Value is not { } data) return;
        var room = data.Room(3);
        var bytes = File.ReadAllBytes(room.FilePath);
        var records = new LoomRoomObjectDirectory().Decode(bytes);
        var verbReader = new LoomObjectVerbReader(ScummScriptDialects.For(data.Game.Id));
        var finder = new LoomDoorwayFinder(data.Game);
        var targets = new SortedSet<int>();
        var tables = verbReader.ReadAll(bytes, records);
        foreach (var record in records)
            if (tables.TryGetValue(record.Id, out var verbs))
                foreach (var verb in verbs.DistinctBy(item => item.ScriptOffset))
                    foreach (var target in finder.Find(room, verbReader.Decode(room, record, verb.VerbId)))
                        targets.Add(target.RoomId);

        Assert.Equal([2, 4, 5, 10], targets);
    }

    [Fact]
    public void Room86CyclingToTheOpenHoodFillsItsFaceCutout()
    {
        if (Data.Value is not { } data) return;
        var (selection, _, background) = data.FirstVisit(86);
        var images = data.Images(86);
        var hood = images.Single(obj => obj.Id == 925);

        var current = images.First(obj => obj.X == hood.X && obj.Y == hood.Y && obj.Width == hood.Width &&
                                          obj.Height == hood.Height && selection.IsActive(obj));
        for (var step = 0; step < 4 && current.Id != hood.Id; step++)
            current = selection.Next(current) ?? throw new InvalidOperationException("Cycling reached the background.");

        Assert.Equal(925, current.Id);
        var face = Assert.Single(images, obj => obj.Id is 926 or 927 or 928 && selection.IsActive(obj));
        Assert.Equal(face.Id, selection.PaintOrder()[^1].Id);
        Assert.Empty(selection.VisibleGaps());
        AssertTopImageIsVisible(selection, background, face.Id);
    }

    private static void AssertTopImageIsVisible(RoomObjectSelection selection, RoomImage background, int objectId)
    {
        var obj = selection.PaintOrder().Single(item => item.Id == objectId);
        var position = selection.GetPosition(obj);
        var pixels = selection.Compose(background);
        for (var y = 0; y < obj.Height; y++)
            for (var x = 0; x < obj.Width; x++)
                Assert.Equal(obj.Pixels[y * obj.Width + x], pixels[(position.Y + y) * background.Width + position.X + x]);
    }

    private sealed record Fixture(GameLibrary Game, LoomIndexedObjectSnapshot Index, LoomSceneDrawBatches Batches)
    {
        public static Fixture? Load()
        {
            var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games/loom"));
            if (!File.Exists(Path.Combine(folder, "00.LFL"))) return null;
            var game = new GameFolderScanner().Scan(folder);
            return new Fixture(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(folder, "00.LFL")),
                LoomSceneDrawBatches.Build(game));
        }

        public GameResource Room(int id) => Game.Rooms.Single(room => room.ResourceId == id);

        public IReadOnlyList<RoomObjectImage> Images(int id) =>
            new LoomRoomObjects().Decode(File.ReadAllBytes(Room(id).FilePath));

        public (RoomObjectSelection Selection, IReadOnlyList<RoomArtworkEstimate> Estimates, RoomImage Background) FirstVisit(int id)
        {
            var room = Room(id);
            var bytes = File.ReadAllBytes(room.FilePath);
            var background = new LoomEgaRoomDecoder().Decode(bytes);
            var snapshot = new LoomExplorationSession(Game, Index).VisitRoom(room);
            var images = new LoomRoomObjects().Decode(bytes);
            var selection = new RoomObjectSelection(images, snapshot.ObjectStates,
                snapshot.ObjectPositions, new LoomRoomObjectDirectory().Decode(bytes), background, Index.States,
                snapshot.DrawOrder);
            IReadOnlyList<RoomArtworkEstimate> estimates = [];
            if (!snapshot.IsFaithful)
            {
                estimates = selection.FillGaps(Batches.ForRoom(id), seed: id);
                // As in MainWindow.CloseArtworkGaps: when nothing is visible, show the fallback estimate.
                if (!images.Any(selection.IsActive)) selection.ShowUnresolvedArtwork();
            }
            return (selection, estimates, background);
        }
    }
}
