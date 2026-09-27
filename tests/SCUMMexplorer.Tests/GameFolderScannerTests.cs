using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Tests;

public sealed class GameFolderScannerTests
{
    [Fact]
    public void RecognizesLikelyDosLoomAndKeepsIndexOutOfRoomList()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"scummexplorer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllBytes(Path.Combine(folder, "LOOM.EXE"), []);
            WriteOldV3Index(Path.Combine(folder, "00.LFL"));
            File.WriteAllBytes(Path.Combine(folder, "01.LFL"), []);
            File.WriteAllBytes(Path.Combine(folder, "02.LFL"), []);

            var game = new GameFolderScanner().Scan(folder);

            Assert.Equal("LOOM", game.Title);
            Assert.Equal(GameId.Loom, game.Id);
            Assert.Equal(ScummVersion.V3, game.Version);
            Assert.Equal(new int?[] { 1, 2 }, game.Rooms.Select(room => room.ResourceId));
            Assert.Contains(game.Resources, resource => resource.Name == "Game index" && resource.Kind == ResourceKind.Other);
            Assert.Contains(game.Resources, resource => resource.Kind == ResourceKind.Costume && resource.ResourceId == 0);
            Assert.Contains(game.Resources, resource => resource.Kind == ResourceKind.Script && resource.ResourceId == 0);
            Assert.Contains(game.Resources, resource => resource.Kind == ResourceKind.Sound && resource.ResourceId == 0);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void RecognizesIndianaJonesEgaFromItsOldV3Index()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"scummexplorer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllBytes(Path.Combine(folder, "INDY3.EXE"), []);
            WriteOldV3Index(Path.Combine(folder, "00.LFL"));
            File.WriteAllBytes(Path.Combine(folder, "01.LFL"), []);
            File.WriteAllBytes(Path.Combine(folder, "02.LFL"), []);

            var game = new GameFolderScanner().Scan(folder);

            Assert.Equal("Indiana Jones and the Last Crusade", game.Title);
            Assert.Equal(GameId.IndianaJones3, game.Id);
            Assert.Equal(ScummVersion.V3, game.Version);
            Assert.Equal(new int?[] { 1, 2 }, game.Rooms.Select(room => room.ResourceId));
            Assert.Contains("Room backgrounds are supported", game.DetectionNote);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    [Fact]
    public void LeavesVersionUnknownWhenLoomEvidenceIsIncomplete()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"scummexplorer-{Guid.NewGuid():N}");
        Directory.CreateDirectory(folder);
        try
        {
            File.WriteAllBytes(Path.Combine(folder, "01.LFL"), []);
            var game = new GameFolderScanner().Scan(folder);
            Assert.Equal(ScummVersion.Unknown, game.Version);
            Assert.Contains("not confirmed", game.DetectionNote);
        }
        finally
        {
            Directory.Delete(folder, recursive: true);
        }
    }

    private static void WriteOldV3Index(string path)
    {
        using var stream = File.Create(path);
        using var writer = new BinaryWriter(stream);
        writer.Write((ushort)0x0100); // old v3 00.LFL signature
        writer.Write((ushort)0); // global object count
        writer.Write((byte)3); // rooms
        writer.Write(new byte[] { 0, 0, 0 }); // room table
        writer.Write((ushort)0xFFFF); // no room zero
        writer.Write((ushort)0x0020);
        writer.Write((ushort)0x0040);
        writer.Write((byte)1); // costumes
        writer.Write((byte)1); // container file number
        writer.Write((ushort)0x0010);
        writer.Write((byte)1); // scripts
        writer.Write((byte)1);
        writer.Write((ushort)0x0030);
        writer.Write((byte)1); // sounds
        writer.Write((byte)2);
        writer.Write((ushort)0x0040);
    }
}
