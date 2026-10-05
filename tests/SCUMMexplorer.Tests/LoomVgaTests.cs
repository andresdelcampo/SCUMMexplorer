using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;
using SCUMMexplorer.Core.Storage;
using SCUMMexplorer.Core.Rendering;

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
            if (snapshot.StatesSetInRoom is { Count: > 0 }) withStates++;
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
            var read = data.ReadRoom(room.ResourceId!.Value);
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

    [Fact]
    public void TheCdRoomsCarryDepthMasksInTheTailOfTheirBackgroundAndObjectImages()
    {
        if (Folder("loom vga en") is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        var vga = game.Vga!;
        var withMask = new List<int>();
        var objectMasks = 0;
        foreach (var room in game.Rooms)
        {
            var id = room.ResourceId!.Value;
            var image = vga.DecodeRoom(id);
            if (image.DepthMask is { } mask)
            {
                Assert.True(mask.IsValid);
                Assert.Equal(image.Width * image.Height, mask.Pixels.Length);
                if (mask.Pixels.Span.IndexOf((byte)1) >= 0) withMask.Add(id);
            }
            objectMasks += vga.ReadObjectImages(id).Count(item => item.Mask is not null);
        }
        Assert.Contains(4, withMask);
        Assert.True(withMask.Count >= 10, $"only {withMask.Count} rooms carry a depth mask");
        Assert.True(objectMasks > 0);
    }

    [Fact]
    public void TheDepthMaskHidesPartOfAnActorBehindSceneryInSomeCdRoom()
    {
        if (Folder("loom vga en") is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        var clipped = new List<string>();
        foreach (var room in game.Rooms)
        {
            var id = room.ResourceId!.Value;
            LoomCdScene scene;
            try { scene = LoomCdRoomScene.FirstVisit(game, id); }
            catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException) { continue; }
            var mask = scene.Selection.ComposeDepthMask(scene.Room);
            if (mask is null) continue;
            var open = LoomActorRenderer.Render(scene.Actors, id, scene.Room.Width, scene.Room.Height, scene.Costumes, null, scene.WalkBoxes, scene.Snapshot.ObjectClasses);
            var masked = LoomActorRenderer.Render(scene.Actors, id, scene.Room.Width, scene.Room.Height, scene.Costumes, mask, scene.WalkBoxes, scene.Snapshot.ObjectClasses);
            var hidden = open.Actors.Sum(a => a.Pixels) - masked.Actors.Sum(a => a.Pixels);
            Assert.True(hidden >= 0);
            if (hidden > 0) clipped.Add($"{id}:{hidden}");
        }
        Assert.True(clipped.Count > 0, "no room clips an actor");
        Console.WriteLine("rooms where the mask hides actor pixels: " + string.Join(" ", clipped));
    }

    [Fact]
    public void EveryCdCostumeReadsAsAnAnimatedCostume()
    {
        if (Folder("loom vga en") is not { } folder) return;
        var archive = new GameFolderScanner().Scan(folder).Vga!;
        var drawn = 0;
        foreach (var costume in archive.ReadCostumes())
        {
            var read = LoomCostume.ReadVga(costume.Body, costume.Id);
            Assert.True(read.IsVga);
            var state = new LoomCostumeAnimationState();
            for (var direction = 0; direction < 4; direction++)
                if (read.DecodeAnimation(state, 1, direction) && read.Draw(state, direction).Count > 0) { drawn++; break; }
        }
        Assert.True(drawn > 100, $"{drawn} costumes drew a first animation");
    }

    [Fact]
    public void TheCdSessionHasTheEgoAndShowsBobbinInTheRoomsHeEnters()
    {
        if (Folder("loom vga en") is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        // Room 35 (Room 36 and Room 34 lead into it): an assumed arrival puts actor 1, whom script 001 names as the ego, in the room.
        var scene = LoomCdRoomScene.FirstVisit(game, 35);
        Assert.Equal((ushort)1, scene.Snapshot.KnownGlobalVariables["var[0x001]"]);
        var bobbin = scene.Actors.Single(actor => actor.Number == 1 && actor.Room == 35);
        Assert.True(bobbin.Visible);
        Assert.True(bobbin.Look.Costume > 0);
        Assert.Contains(scene.Snapshot.Uncertainties, note => note.Contains("Assumed arrival", StringComparison.Ordinal));
        var layer = LoomActorRenderer.Render(scene.Actors, 35, scene.Room.Width, scene.Room.Height, scene.Costumes, null, scene.WalkBoxes);
        var drawn = layer.Actors.Single(item => item.Number == 1);
        Assert.True(drawn.Pixels > 100);
        // His feet are at his place, inside the room.
        Assert.InRange(drawn.Bounds!.Value.Bottom, bobbin.Y!.Value - 1, bobbin.Y.Value + 1);
    }

    [Fact]
    public void MatrixOpsRunsInTheExecutorAsAWalkBoxFlagChange()
    {
        if (Folder("loom vga en") is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        var data = new LoomCdDataSource(game);
        var matrixScripts = 0;
        foreach (var room in game.Rooms.Select(item => item.ResourceId!.Value))
        {
            var records = data.ReadRoom(room).Objects;
            var states = records.ToDictionary(record => record.Id, _ => (byte)0);
            foreach (var id in data.RoomLocalScriptIds(room))
            {
                if (data.RoomLocalScript(room, id) is not { } script || !script.Instructions.Any(item => item.Operation == "matrixOps")) continue;
                matrixScripts++;
                var run = new LoomBoundedScriptExecutor().Execute(script, states, records);
                Assert.DoesNotContain("matrixOps", run.UnresolvedReason ?? "");
            }
        }
        Assert.True(matrixScripts > 0);
    }

    [Fact]
    public void TheFourCharsetsReadAndDrawLegibleGlyphs()
    {
        if (Folder("loom vga en") is not { } folder) return;
        var archive = new GameFolderScanner().Scan(folder).Vga!;
        foreach (var number in new[] { 1, 2, 3, 4 })
        {
            var charset = archive.ReadCharset(number)!;
            Assert.NotNull(charset);
            Assert.True(charset.BitsPerPixel is 1 or 2);
            Assert.True(charset.CharWidth('A') > 0, $"charset {number} has no A");
            var rows = new char[16][];
            for (var y = 0; y < rows.Length; y++) rows[y] = Enumerable.Repeat('.', 16).ToArray();
            charset.DrawGlyph('A', (x, y, v) => { if (x >= 0 && x < 16 && y + 4 >= 0 && y + 4 < 16) rows[y + 4][x] = (char)('0' + v); });
            Assert.Contains(rows.SelectMany(r => r), c => c != '.');
            File.AppendAllText(Path.Combine(Path.GetTempPath(), "charset-glyphs.txt"),
                $"charset {number} height {charset.FontHeight} glyphs {charset.GlyphCount} A width {charset.CharWidth('A')} colours {Convert.ToHexString(charset.ColorMap)}" + Environment.NewLine + string.Join(Environment.NewLine, rows.Select(r => new string(r))) + Environment.NewLine);
        }
    }

    [Fact]
    public void TheCdDraftVerbsDrawInTheVerbAreaFromRoomOneImages()
    {
        if (Folder("loom vga en") is not { } folder) return;
        var game = new GameFolderScanner().Scan(folder);
        var session = new LoomExplorationSession(game, game.Vga!.ReadIndexedObjects(), completeBoot: false);
        session.VisitRoom(game.Rooms.First(item => item.ResourceId == 1));
        session.VisitRoom(game.Rooms.First(item => item.ResourceId == 35));
        Assert.Empty(session.VerbArea().Items);
        // What script 018 does when the distaff is held (the CD game has its own staff variables, so the explorer's state edit is not used here).
        var run = typeof(LoomExplorationSession).GetMethod("RunStartedScript", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        var runtime = typeof(LoomExplorationSession).GetField("_runtime", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.GetValue(session)!;
        runtime.GetType().GetMethod("SetGlobalVariable")!.Invoke(runtime, ["var[0x0AC]", (ushort)8]);
        run.Invoke(session, System.Reflection.BindingFlags.Default, Type.DefaultBinder, [18, 0, new Dictionary<string, ushort> { ["local[0x000]"] = 2 }, 0, Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing], null);
        var area = session.VerbArea();
        Assert.Contains(area.Items, item => item.VerbId == 0x01 && item.ImageRoom == 1);
        Assert.True(area.Items.Count(item => item.VerbId is >= 1 and <= 8) == 8);
        Assert.Contains(area.Pixels, pixel => pixel != 0);
    }

    [Fact]
    public void ATalkLineDrawsInTheCdCharset()
    {
        if (Folder("loom vga en") is not { } folder) return;
        var charset = new GameFolderScanner().Scan(folder).Vga!.ReadCharset(2)!;
        var text = System.Text.Encoding.Latin1.GetBytes("It's locked.");
        var message = new LoomMessage(LoomMessageKind.Talk, 1, 0, [text], 160, 100, true, 0, 319, 15, 35, "test", 0, 120, null);
        var layer = LoomVgaMessageRenderer.Render(message, charset);
        Assert.NotNull(layer.Bounds);
        Assert.Contains((byte)15, layer.Pixels);
        Assert.Contains((byte)0, layer.Pixels);
        Assert.InRange(layer.Bounds!.Value.Left, 100, 160);
    }
}

public sealed class LoomVgaCharsetPerMessageTests
{
    [Fact]
    public void AMessageKeepsTheCharsetSetWhenItsPrintRan()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games", "loom vga en"));
        if (!Directory.Exists(folder) || DiscImage.FindImageIn(folder) is null) return;
        var game = new GameFolderScanner().Scan(folder);
        var session = new LoomExplorationSession(game, game.Vga!.ReadIndexedObjects(), completeBoot: false);
        session.VisitRoom(game.Rooms.First(item => item.ResourceId == 35));
        // Script 044 (the intro) +0x0210 sets charset 4, prints the company line, then sets charset 2 for the copyright line.
        var run = typeof(LoomExplorationSession).GetMethod("RunStartedScript", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        run.Invoke(session, System.Reflection.BindingFlags.Default, Type.DefaultBinder,
            [44, 0x210, new Dictionary<string, ushort>(), 0, Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing], null);
        var drawn = session.Messages.OnScreen.ToArray();
        // The talk slot holds one line, so the company line is already replaced on screen; the log keeps it.
        Assert.Contains(session.Messages.Log, message => message.Text == "LucasArts Entertainment Company" && message.Charset == 4);
        Assert.Contains(drawn, message => message.Text.Contains("All rights reserved", StringComparison.Ordinal) && message.Charset == 2);
        var archive = game.Vga!;
        var layer = LoomVgaMessageRenderer.Render(drawn, number => archive.ReadCharset(number ?? 2)!);
        Assert.NotNull(layer.Bounds);
    }

    [Fact]
    public void ASpokenLineKeepsItsVoiceCueAndNoLongerCountsAsUnmodelled()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games", "loom vga en"));
        if (!Directory.Exists(folder) || DiscImage.FindImageIn(folder) is null) return;
        var game = new GameFolderScanner().Scan(folder);
        var session = new LoomExplorationSession(game, game.Vga!.ReadIndexedObjects(), completeBoot: false);
        session.VisitRoom(game.Rooms.First(item => item.ResourceId == 35));
        // Script 022 +0x001F: "It's too noisy to spin any drafts in here!", sayVoice 0x1DAA, 0x001B.
        var run = typeof(LoomExplorationSession).GetMethod("RunStartedScript", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!;
        run.Invoke(session, System.Reflection.BindingFlags.Default, Type.DefaultBinder,
            [22, 0x1F, new Dictionary<string, ushort>(), 0, Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing, Type.Missing], null);
        var line = session.Messages.Log.Last(message => message.Text.StartsWith("It's too noisy", StringComparison.Ordinal));
        Assert.Equal(new LoomVoiceCue(0x1DAA, 0x1B), line.Voice);
        Assert.Equal(759.4, line.Voice!.Value.DiscSeconds, 1);
        Assert.DoesNotContain("sayVoice", line.Estimate ?? "");
        Assert.Contains(new LoomVoiceCue(0x1DAA, 0x1B), session.Messages.VoiceEvents);
    }

    [Fact]
    public void AVoiceCueMapsToFramesOfTheAudioTrackAndTheDiscHoldsThem()
    {
        var cue = new LoomVoiceCue(0x1DAA, 0x1B);
        // ScummVM: offset * 7.5 frames from the disc start, less the first track (22,500) and the two-second lead-in (150).
        Assert.Equal(34305, cue.TrackStartFrame);
        Assert.Equal(207, cue.TrackFrameCount);
        Assert.True(new LoomVoiceCue(0, 0).IsStop);
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games", "loom vga en"));
        if (!Directory.Exists(folder) || DiscImage.FindImageIn(folder) is null) return;
        var disc = DiscImage.Open(folder);
        Assert.Equal(22335, disc.AudioStartSector);
        var pcm = disc.ReadAudio(cue.TrackStartFrame, cue.TrackFrameCount);
        Assert.Equal(cue.TrackFrameCount * DiscImage.AudioSectorBytes, pcm.Length);
        // Speech, not silence: some 16-bit samples are far from zero.
        var loud = 0;
        for (var i = 0; i + 1 < pcm.Length; i += 2) if (Math.Abs(BitConverter.ToInt16(pcm, i)) > 2000) loud++;
        Assert.True(loud > 1000, $"{loud} loud samples");
        var wave = disc.ReadAudioWave(cue.TrackStartFrame, cue.TrackFrameCount);
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(wave, 0, 4));
        Assert.Equal(pcm.Length + 44, wave.Length);
        Assert.Empty(disc.ReadAudio(10_000_000, 10));
    }

    [Fact]
    public void TheCdSoundsAreReadFromTheDiscAndRenderThroughTheEgaPlayers()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games", "loom vga en"));
        if (!Directory.Exists(folder) || DiscImage.FindImageIn(folder) is null) return;
        var archive = LoomVgaArchive.Load(new DiscFileSource(folder));
        var sounds = archive.ReadSounds();
        Assert.Equal(30, sounds.Count);
        Assert.Equal(8, sounds.Count(s => s.Room == 1));
        Assert.DoesNotContain(sounds, s => s.Id is 24 or 32);
        Assert.All(sounds, s => { Assert.NotNull(s.Speaker); Assert.NotNull(s.Adlib); });
        Assert.Equal(18, sounds.Count(s => s.IsMusic));
        var adlib = new LoomAdlibAudio();
        var speaker = new LoomPcSpeakerAudio();
        // Sound 9 (room 11) is a music track; sound 1 an effect. Both parts of each render real audio.
        foreach (var id in new[] { 1, 9, 19 })
        {
            var sound = sounds.First(s => s.Id == id);
            AssertLoud(adlib.RenderWave(sound.Adlib!));
            AssertLoud(speaker.RenderWave(sound.Speaker!, 6));
        }
    }

    [Fact]
    public void TheCdAudioListHoldsEveryCueTheScriptsAskForAndTheDiscPlaysThem()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games", "loom vga en"));
        if (!Directory.Exists(folder) || DiscImage.FindImageIn(folder) is null) return;
        var game = new GameFolderScanner().Scan(folder);
        var cues = LoomCdCueIndex.Scan(ScummDataSources.For(game), game.Rooms.Select(r => r.ResourceId!.Value));
        Assert.True(cues.Count > 200, $"{cues.Count} cues");
        // The 84 s overture (a blank line) and a spoken line are both there.
        Assert.Contains(cues, c => c.Cue.Offset == 0x7654 && c.Cue.Length == 0x347 && c.IsMusicOrNarration);
        Assert.Contains(cues, c => c.Text.StartsWith("Not so fast") && !c.IsMusicOrNarration);
        var overture = cues.First(c => c.Cue.Offset == 0x7654 && c.Cue.Length == 0x347).Cue;
        var wave = game.Vga!.Disc!.ReadAudioWave(overture.TrackStartFrame, overture.TrackFrameCount);
        Assert.True(wave.Length > 44 + 44100 * 4 * 80, $"{wave.Length} bytes");
    }

    private static void AssertLoud(byte[] wave)
    {
        Assert.Equal("RIFF", System.Text.Encoding.ASCII.GetString(wave, 0, 4));
        var loud = 0;
        for (var i = 44; i + 1 < wave.Length; i += 2) if (Math.Abs(BitConverter.ToInt16(wave, i)) > 500) loud++;
        Assert.True(loud > 500, $"{loud} loud samples in {wave.Length} bytes");
    }

    [Fact]
    public void TheCdStatusSchemaIsReadFromTheScriptsAndItsEditsShowTheDraftVerbs()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games", "loom vga en"));
        if (!Directory.Exists(folder) || DiscImage.FindImageIn(folder) is null) return;
        var game = new GameFolderScanner().Scan(folder);
        var session = new LoomExplorationSession(game, game.Vga!.ReadIndexedObjects(), completeBoot: false);
        session.VisitRoom(game.Rooms.First(item => item.ResourceId == 1));
        session.VisitRoom(game.Rooms.First(item => item.ResourceId == 35));
        var status = session.GameStatus;
        Assert.True(status.Supported, status.Evidence);
        Assert.Equal(16, status.Drafts.Count);
        Assert.Contains("bit[0x001]", status.Evidence);
        Assert.Contains("var[0x0AC]", status.Evidence);
        Assert.Contains("bit[0x009]", status.Evidence);
        Assert.True(status.CanEdit);
        session.UpdateStaffPossession(true);
        session.UpdateAvailableNotes(5);
        Assert.True(session.GameStatus.StaffHeld);
        Assert.Equal(5, session.GameStatus.AvailableNoteCount);
        session.UpdateDraftSequence(0, [1, 2, 3, 4]);
        session.UpdateDraftLearned(0, true);
        var first = session.GameStatus.Drafts[0];
        Assert.Equal([1, 2, 3, 4], first.Notes);
        Assert.True(first.Learned);
        var area = session.VerbArea();
        Assert.True(area.Items.Count(item => item.VerbId is >= 1 and <= 8) >= 5, string.Join(",", area.Items.Select(i => i.VerbId)));
        session.UpdateStaffPossession(false);
        Assert.False(session.GameStatus.StaffHeld);
        // 017 starts 019, which runs on the next frames, as in the app.
        for (var frame = 0; frame < 5; frame++) session.AdvanceFrame();
        Assert.DoesNotContain(session.VerbArea().Items, item => item.VerbId is >= 1 and <= 8);
    }
}

public sealed class LoomVgaIntroTests
{
    [Fact]
    public void TheCdMusicTimerRestartsWithEachCueAndCountsOneAJiffyUntilTheNext()
    {
        var table = new LoomMessageTable();
        var runtime = new LoomScriptRuntimeState(new Dictionary<int, byte>());
        Assert.False(table.CdTimerRunning);
        table.AdvanceCdTimer(6);
        Assert.Equal(0, table.CdMusicTimer);
        var print = new LoomPrintRequest(0xFF, [new("sayVoice", 3023, 24)], null, "test", 0, null, null);
        table.Print(print, runtime);
        Assert.True(table.CdTimerRunning);
        table.AdvanceCdTimer(6);
        table.AdvanceCdTimer(6);
        Assert.Equal(12, table.CdMusicTimer);
        // A stop zeroes the variable for the script but not the CD timer, which the next frame copies back (ScummVM keeps counting).
        table.Print(new LoomPrintRequest(0xFF, [new("sayVoice", 0, 0)], null, "test", 0, null, null), runtime);
        Assert.Equal(0, runtime.GlobalVariables["var[0x00E]"]);
        Assert.Equal(12, table.CdMusicTimer);
        table.Print(print, runtime);
        Assert.Equal(0, table.CdMusicTimer);
    }

    [Fact]
    public void TheCdIntroPlaysFromTheSkillScreenThroughTheOvertureToTheGameStart()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games", "loom vga en"));
        if (!Directory.Exists(folder) || DiscImage.FindImageIn(folder) is null) return;
        var game = new GameFolderScanner().Scan(folder);
        var session = new LoomExplorationSession(game, game.Vga!.ReadIndexedObjects(), completeBoot: false, playCdIntro: true);
        Assert.True(session.BootRunning);
        Assert.Equal(69, session.Snapshot.RoomId);
        var frames = 0;
        var timerMax = 0;
        while (session.BootRunning && frames++ < LoomExplorationSession.BootFrameLimit)
        {
            session.AdvanceFrame();
            if (session.Snapshot.KnownGlobalVariables.TryGetValue("var[0x00E]", out var timer)) timerMax = Math.Max(timerMax, timer);
        }
        Assert.False(session.BootRunning);
        Assert.True(frames < 1500, $"{frames} frames");
        Assert.Equal(2, session.Snapshot.RoomId);
        var log = session.Messages.Log;
        Assert.Contains(log, m => m.Text == "Please choose your skill level.");
        Assert.Contains(log, m => m.Text == "LucasArts Entertainment Company" && m.Charset == 4);
        Assert.Contains(log, m => m.Text == "Rise, son of Cygna.");
        // The overture (3029.2 s, 83.9 s long) is one of the cues, and the timer ran past the values script 044 waits for.
        Assert.Contains(session.Messages.VoiceEvents, cue => cue.Offset == 30292 && cue.Length == 839);
        Assert.True(timerMax > 1200, $"timer {timerMax}");
        Assert.Contains(session.Snapshot.Uncertainties, note => note.StartsWith("Player input assumed", StringComparison.Ordinal));
    }
}
