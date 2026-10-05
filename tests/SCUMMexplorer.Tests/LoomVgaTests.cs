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
}
