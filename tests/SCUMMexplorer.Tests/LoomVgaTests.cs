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
}
