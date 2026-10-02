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
        var opening = data.Batches.ForRoom(56).Where(batch => batch.Priority == LoomSceneDrawBatches.OpeningFramePriority).ToArray();
        // Script 68 loads Room 56 twice; only its first load is an opening frame. Script 108's
        // cutscene body (after its beginOverride) opens the room on the same face and mouth.
        Assert.Equal(["opening frame of Room 56 in script 108 +0x0188", "opening frame of Room 56 in script 68 +0x0103"],
            opening.Select(batch => batch.Source).Order(StringComparer.Ordinal));
        Assert.All(opening, batch => Assert.Equal([779, 782], batch.Steps.Select(step => step.ObjectId)));

        var (selection, estimates, background) = data.FirstVisit(56);

        Assert.Equal([779, 782], estimates.Select(item => item.ObjectId));
        Assert.Equal([779, 782], selection.PaintOrder().Select(obj => obj.Id).TakeLast(2));
        Assert.Empty(selection.VisibleGaps());
        AssertTopImageIsVisible(selection, background, 782);
    }

    [Fact]
    public void Room58OpensOnScript86sCutsceneFrame()
    {
        if (Data.Value is not { } data) return;
        // Script 86's cutscene body lies after its beginOverride skip jump. Until the
        // disassembler followed beginOverride, no decoded code loaded Room 58.
        var opening = Assert.Single(data.Batches.ForRoom(58), batch => batch.Priority == LoomSceneDrawBatches.OpeningFramePriority);
        Assert.Equal("opening frame of Room 58 in script 86 +0x018A", opening.Source);

        var (selection, estimates, _) = data.FirstVisit(58);

        Assert.Equal([new RoomArtworkEstimate(792, opening.Source)], estimates);
        Assert.Empty(selection.VisibleGaps());
    }

    [Fact]
    public void DrawsTheSameScriptUndoesAreNotBatches()
    {
        if (Data.Value is not { } data) return;
        // Script 90 flashes 637 and 638 in Room 42 (drawObject, breakHere, setState 0).
        Assert.DoesNotContain(data.Batches.ForRoom(42), batch => batch.Source.StartsWith("script 90 ", StringComparison.Ordinal));
        // Script 99 later removes the scythe using these background repair images.
        // Its batch remains decoded, but first-visit fallback must retain the scythe.
        Assert.Contains(data.Batches.ForRoom(20), batch => batch.Source == "script 99 +0x02BB" &&
            batch.Steps.Select(step => step.ObjectId).Order().SequenceEqual(new[] {392, 393, 394, 395, 396}));
        var (selection, estimates, background) = data.FirstVisit(20);
        Assert.Empty(estimates);
        Assert.Empty(selection.PaintOrder());
        Assert.Equal(background.Pixels.ToArray(), selection.Compose(background));
        Assert.Empty(selection.VisibleGaps());
    }

    [Fact]
    public void VerbAreaImagesAreFoundAndKeptOffTheRoom()
    {
        if (Data.Value is not { } data) return;
        // Room 01's distaff pieces are literal verbOps images; object close-ups
        // reach verb 0x36 through var[0x068], which each object's verb 0x39 sets.
        Assert.All(Enumerable.Range(100, 52), id => Assert.StartsWith("verbOps image in ",
            data.Batches.InterfaceImages[id], StringComparison.Ordinal));
        Assert.Equal("var[0x068] set in Room 03 object 165 Verb 0x39 +0x0000, shown by verbOps image in script 08 +0x002D",
            data.Batches.InterfaceImages[166]);
        Assert.False(data.Batches.InterfaceImages.ContainsKey(152));

        // The boot's opening cutscene passes through Room 01, so 152's state is known: no estimate.
        var (title, titleEstimates, _) = data.FirstVisit(1);
        Assert.Equal([152], title.PaintOrder().Select(obj => obj.Id));
        Assert.Empty(titleEstimates);

        // Room 03 has no room artwork at all: its only image is object 165's close-up.
        var (lake, lakeEstimates, _) = data.FirstVisit(3);
        Assert.Empty(lake.PaintOrder());
        Assert.Empty(lakeEstimates);
        Assert.Empty(lake.VisibleGaps());
    }

    [Theory]
    [InlineData(18, 373, "Room 18 entry script +0x00DA sets it to 0 on every path")]
    [InlineData(42, 637, "Room 42 entry script +0x0000 sets it to 0 on every path")]
    public void EntryStatesSetOnEveryPathKeepTheirObjectsHidden(int roomId, int objectId, string source)
    {
        if (Data.Value is not { } data) return;
        Assert.Equal(new RoomStateEvidence(0, source), data.Batches.EntryStatesForRoom(roomId)[objectId]);

        var (selection, estimates, _) = data.FirstVisit(roomId);

        Assert.DoesNotContain(estimates, item => item.ObjectId == objectId);
        Assert.False(selection.IsActive(data.Images(roomId).Single(obj => obj.Id == objectId)));
        Assert.Empty(selection.VisibleGaps());
    }

    [Theory]
    [InlineData(2, 157, "opening frame of Room 02 in script 106 +0x03A0")]
    [InlineData(65, 830, "opening frame of Room 65 in script 72 +0x0029")]
    [InlineData(4, 178, "Room 04 local script 201 +0x0082, last frame from the table set at +0x000C")]
    public void NothingVisibleFallbackOpensOnTheStrongestScriptBatch(int roomId, int objectId, string source)
    {
        if (Data.Value is not { } data) return;
        var (selection, estimates, _) = data.FirstVisit(roomId);
        var images = data.Images(roomId);

        Assert.Contains(estimates, item => item.ObjectId == objectId && item.Reason == source);
        Assert.True(selection.IsActive(images.Single(obj => obj.Id == objectId)));
        // Every shown image is labelled, and the stored-image estimate is kept
        // only for positions that no decoded script draws.
        var drawn = data.Batches.ForRoom(roomId).SelectMany(batch => batch.Steps).Select(step => step.ObjectId).ToHashSet();
        foreach (var item in estimates.Where(item => item.Reason == RoomObjectSelection.FirstStoredImageReason))
        {
            var obj = images.Single(image => image.Id == item.ObjectId);
            Assert.DoesNotContain(images, other => other.X == obj.X && other.Y == obj.Y && other.Width == obj.Width &&
                                                   other.Height == obj.Height && drawn.Contains(other.Id));
        }
        Assert.Equal(images.Where(selection.IsActive).Select(obj => obj.Id).Order(),
            estimates.Select(item => item.ObjectId).Distinct().Order());
        Assert.Empty(selection.VisibleGaps());
    }

    [Fact]
    public void Room43ReachedClosedCageDoesNotBorrowLaterOpeningArtwork()
    {
        if (Data.Value is not { } data) return;
        var (selection, estimates, background) = data.FirstVisit(43);
        Assert.DoesNotContain(estimates, item => item.ObjectId is 645 or 646);
        Assert.Empty(selection.PaintOrder());
        Assert.Equal(background.Pixels.ToArray(), selection.Compose(background));
        Assert.Empty(selection.VisibleGaps());
    }

    // Once setClass and ifClassOfIs are modelled, these entries run past their
    // class tests and draw the image themselves; no estimate is needed.
    [Theory]
    [InlineData(30, 520)]
    // Room 38 draws 612 since the boot sets bit[0xD83] (script 001 +0x01B1).
    [InlineData(38, 612)]
    [InlineData(46, 687)]
    public void EntryScriptsThatTestClassesDrawTheirRoomsThemselves(int roomId, int objectId)
    {
        if (Data.Value is not { } data) return;
        var (selection, estimates, _) = data.FirstVisit(roomId);

        Assert.True(selection.IsActive(data.Images(roomId).Single(obj => obj.Id == objectId)));
        Assert.Contains(objectId, selection.DrawQueue);
        Assert.DoesNotContain(estimates, item => item.ObjectId == objectId);
        Assert.DoesNotContain(estimates, item => item.Reason == RoomObjectSelection.FirstStoredImageReason);
        Assert.Empty(selection.VisibleGaps());
    }

    [Theory]
    [InlineData(41)]
    [InlineData(69)]
    public void EntryScriptsRunPastSetVarRange(int roomId)
    {
        if (Data.Value is not { } data) return;
        var session = new LoomExplorationSession(data.Game, data.Index);
        session.VisitRoom(data.Room(roomId));

        var run = session.LastEntryRun!;
        Assert.DoesNotContain("setVarRange", run.UnresolvedReason ?? "", StringComparison.Ordinal);
        Assert.Contains(session.LastEntryScript!.Instructions, item => item.Operation == "setVarRange" &&
            item.Offset < (run.ResumeOffset ?? int.MaxValue));
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
                selection.ExcludeInterfaceImages(Batches.InterfaceImages.Keys);
                estimates = selection.FillGaps(Batches.ForRoom(id), seed: id);
                // As in MainWindow.CloseArtworkGaps: when nothing is visible, show the fallback estimate.
                if (!images.Any(selection.IsActive))
                    estimates = [.. estimates, .. selection.ShowUnresolvedArtwork(Batches.ForRoom(id), seed: id,
                        snapshot.StatesSetByCode, Batches.EntryStatesForRoom(id))];
            }
            return (selection, estimates, background);
        }
    }
}
