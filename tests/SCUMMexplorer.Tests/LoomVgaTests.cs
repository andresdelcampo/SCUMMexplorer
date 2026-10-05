using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;
using SCUMMexplorer.Core.Storage;

namespace SCUMMexplorer.Tests;

/// <summary>
/// LOOM on CD (SCUMM v4, VGA), read from the disc image in <c>Games/loom vga en</c> (and the other dump in <c>loom vga en kixx</c>).
/// Without those folders these tests do nothing. Probe record: .verification/loom-vga/README.md.
/// </summary>
public sealed class LoomVgaTests
{
    private static string? Folder(string name)
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games", name));
        return Directory.Exists(folder) && DiscImage.FindImageIn(folder) is not null ? folder : null;
    }

    [Theory]
    [InlineData("loom vga en")]
    [InlineData("loom vga en kixx")]
    public void TheDiscImageOpensFromItsFolderAndListsTheGameFiles(string name)
    {
        if (Folder(name) is not { } folder) return;
        var source = new DiscFileSource(folder);
        Assert.Contains("DISK01.LEC", source.FileNames);
        Assert.Contains("000.LFL", source.FileNames);
        Assert.True(LoomVgaArchive.IsVga(source));
        Assert.True(source.Image.AudioStartSector > 20000);
    }

    [Fact]
    public void TheScannerIdentifiesTheCdGameFromAFolderACueOrABin()
    {
        if (Folder("loom vga en") is not { } folder) return;
        foreach (var path in new[] { folder, Path.Combine(folder, "Loom.cue") })
        {
            var game = new GameFolderScanner().Scan(path);
            Assert.Equal(GameId.LoomVga, game.Id);
            Assert.Equal(ScummVersion.V4, game.Version);
            Assert.Equal(78, game.Rooms.Count());
            Assert.Equal(78, game.Scenes.Count());
        }
    }

    [Fact]
    public void TheTwoDumpsCarryTheSameRooms()
    {
        if (Folder("loom vga en") is not { } a || Folder("loom vga en kixx") is not { } b) return;
        var first = new GameFolderScanner().Scan(a).Vga!;
        var second = new GameFolderScanner().Scan(b).Vga!;
        Assert.Equal(first.Rooms, second.Rooms);
    }

    [Fact]
    public void EveryRoomBackgroundDecodesWithItsPaletteAndSize()
    {
        if (Folder("loom vga en") is not { } folder) return;
        var archive = new GameFolderScanner().Scan(folder).Vga!;
        foreach (var room in archive.Rooms)
        {
            var image = archive.DecodeRoom(room.Number);
            Assert.True(image.IsValid, $"Room {room.Number}");
            Assert.Equal(256, image.Palette.Count);
            Assert.True(image.Height is 144 or 200, $"Room {room.Number} height {image.Height}");
        }
        var logo = archive.Rooms.Single(r => r.Number == 1);
        Assert.Equal("logo", logo.Name);
        Assert.Equal((320, 200), (archive.DecodeRoom(1).Width, archive.DecodeRoom(1).Height));
        Assert.Equal(960, archive.DecodeRoom(2).Width);
    }

    [Fact]
    public void EveryScriptOfTheCdGameDecodesWithTheV4Dialect()
    {
        if (Folder("loom vga en") is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        var reader = new LoomScriptReader(ScummScriptDialects.For(game.Id));
        var count = 0;
        foreach (var room in game.Vga!.Rooms)
            foreach (var script in game.Vga.ReadScripts(room.Number))
            {
                var decoded = reader.DecodeCode(script.Code, script.Number);
                Assert.True(decoded.Issues is not { Count: > 0 } && decoded.DecodeNote is null,
                    $"{script.Kind} {script.Number} in room {room.Number}: {decoded.Issues?.FirstOrDefault()?.Reason ?? decoded.DecodeNote}");
                count++;
            }
        Assert.Equal(793, count);
    }

    [Theory]
    [InlineData("33 02 05 00 0C 00", "roomOps", "roomColor, 0x0005, 0x000C")]
    [InlineData("33 01 A0 00 40 01", "roomOps", "roomScroll, 0x00A0, 0x0140")]
    [InlineData("30 01 02 03", "matrixOps", "setBoxFlags, 0x02, 0x03")]
    [InlineData("4C 01 05 00 FF", "soundKludge", "[0x0005]")]
    public void TheV4DialectReadsItsOwnOperandForms(string hex, string operation, string startsWith)
    {
        var code = Convert.FromHexString(hex.Replace(" ", ""));
        var decoded = new LoomScriptReader(ScummScriptDialect.LoomCd).DecodeCode(code);
        var first = decoded.Instructions.First();
        Assert.Equal(operation, first.Operation);
        Assert.StartsWith(startsWith, first.Operands, StringComparison.Ordinal);
    }

    [Fact]
    public void TheCdGamesObjectsHaveNamesAndVerbsThatLandOnCode()
    {
        if (Folder("loom vga en") is not { } folder) return;
        var archive = new GameFolderScanner().Scan(folder).Vga!;
        var owl = archive.ReadObjects(4).Single(o => o.Name == "owl");
        Assert.Equal(100, owl.Id);
        Assert.Equal([0x36, 0x39, 0x56], owl.Verbs.Select(v => v.VerbId).Order());
        Assert.All(owl.Verbs, v => Assert.InRange(v.Offset, 20, owl.Block.Length - 1));
    }

    [Fact]
    public void TheCdGamesTextsAreExtractedFromItsScriptsAndObjects()
    {
        if (Folder("loom vga en") is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        var extraction = new LoomTextExtractor().Extract(game);
        Assert.Equal(0, extraction.UnresolvedPaths);
        Assert.Equal(0, extraction.ResourcesSkipped);
        Assert.Contains(extraction.Entries, e => e.Text.Trim((char)0x19) == "The day the sky is opened." && e.RoomId == 4);
        Assert.Contains(extraction.Entries, e => e.Kind == "Object name" && e.Text == "owl" && e.ObjectId == 100);
        Assert.Contains(extraction.Entries, e => e.Text == "That draft isn't working on the bell." && e.ObjectId == 294);
        Assert.True(extraction.Entries.Count > 1000);
    }

    [Fact]
    public void EveryCostumeOfTheCdGameDrawsAtLeastOnePose()
    {
        if (Folder("loom vga en") is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        var archive = game.Vga!;
        var costumes = archive.ReadCostumes();
        Assert.Equal(165, costumes.Count);
        Assert.Equal(165, game.Resources.Count(r => r.Kind == ResourceKind.Costume));
        foreach (var costume in costumes)
        {
            var poses = archive.DecodeCostumePoses(costume);
            Assert.True(poses.Count > 0, $"Costume {costume.Id} has no decodable pose");
            Assert.All(poses, p => Assert.True(p.Image.IsValid));
        }
    }

    [Fact]
    public void TheCdDataSourceReadsScriptsObjectsAndImagesThroughTheSeam()
    {
        if (Folder("loom vga en") is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        var data = ScummDataSources.For(game);
        Assert.IsType<LoomCdDataSource>(data);
        Assert.Equal(LoomVgaArchive.LocalScriptBase, data.GlobalScriptCount);
        var globals = Enumerable.Range(0, data.GlobalScriptCount).Count(n => data.GlobalScript(n) is not null);
        Assert.Equal(122, globals);
        Assert.Null(data.GlobalScript(data.GlobalScriptCount + 50));

        var logo = data.ReadRoom(1);
        Assert.Equal((320, 200), (logo.Width, logo.Height));
        Assert.Equal(61, logo.Objects.Count);
        Assert.Equal(61, data.ReadObjectImages(1).Count);
        Assert.NotNull(data.RoomEntryScript(1));
        Assert.Contains(200, data.RoomLocalScriptIds(1));
        Assert.NotNull(data.RoomLocalScript(1, 200));
        Assert.Null(data.RoomLocalScript(1, 5));

        // Room 4's owl: the object's verb table and the code behind it read through the same seam.
        var owl = data.ReadRoom(4).Objects.First(o => data.ReadObjectVerbs(4, [o]).TryGetValue(o.Id, out var verbs) && verbs.Any(v => v.VerbId == 0x36));
        var script = data.ReadObjectVerb(4, owl, 0x36, out var entry);
        Assert.NotEmpty(script.Instructions);
        Assert.Equal(owl.Id, entry.ObjectId);

        foreach (var room in game.Rooms)
        {
            var number = room.ResourceId!.Value;
            var objects = data.ReadRoom(number);
            var images = data.ReadObjectImages(number);
            Assert.All(images, image => Assert.Equal(image.Width * image.Height, image.Pixels.Length));
            Assert.All(images, image => Assert.Contains(objects.Objects, o => o.Id == image.Id));
            foreach (var id in data.RoomLocalScriptIds(number)) Assert.NotNull(data.RoomLocalScript(number, id));
        }
    }

    [Fact]
    public void TheSessionRunsTheCdGamesEntryScriptsRoomByRoom()
    {
        if (Folder("loom vga en") is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        var session = new LoomExplorationSession(game, game.Vga!.ReadIndexedObjects(), completeBoot: false);
        var withStates = 0;
        foreach (var room in game.Rooms)
        {
            var snapshot = session.VisitRoom(room);
            Assert.True(room.ResourceId == snapshot.RoomId, $"visiting room {room.ResourceId} ended in room {snapshot.RoomId}");
            if (snapshot.StatesSetInRoom.Count > 0) withStates++;
        }
        // The index gives every object state 0; rooms show what their entry script sets.
        Assert.True(withStates >= 15, $"{withStates} rooms set object states");
    }

    [Fact]
    public void EveryCdRoomComposesItsFirstVisitPicture()
    {
        if (Folder("loom vga en") is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        var painted = 0;
        foreach (var room in game.Rooms)
        {
            var scene = LoomCdRoomScene.FirstVisit(game, room.ResourceId!.Value);
            var pixels = scene.Compose();
            Assert.Equal(scene.Room.Pixels.Length, pixels.Length);
            if (!pixels.AsSpan().SequenceEqual(scene.Room.Pixels.Span)) painted++;
        }
        Assert.True(painted >= 10, $"{painted} rooms show objects over their background");
    }

    [Fact]
    public void EveryCdRoomReadsItsWalkBoxesThroughTheSeam()
    {
        if (Folder("loom vga en") is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        var data = ScummDataSources.For(game);
        var withBoxes = 0;
        var truncated = new List<int>();
        foreach (var room in game.Rooms)
        {
            var read = data.ReadRoom(room.ResourceId.Value);
            if (read.WalkBoxes is null) { Assert.Contains("no BX", read.WalkBoxError); continue; }
            Assert.Null(read.WalkBoxError);
            if (read.WalkBoxes.MatrixTruncated) truncated.Add(room.ResourceId.Value);
            withBoxes++;
        }
        Assert.True(withBoxes > 60);
        // Room 16's matrix lacks the closing row of its last box (the data, as with Indy3 Room 46 on EGA); ScummVM reads on regardless.
        Assert.True(truncated.Count <= 2, string.Join(",", truncated));
        // Room 4: five boxes, the first a strip at y 132 to 134 from x 208 to 664.
        var boxes = data.ReadRoom(4).WalkBoxes!;
        Assert.Equal(5, boxes.Count);
        Assert.Equal(new LoomBoxPoint(208, 132), boxes.Boxes[0].UpperLeft);
        Assert.Equal(new LoomBoxPoint(664, 134), boxes.Boxes[0].LowerRight);
    }
}
