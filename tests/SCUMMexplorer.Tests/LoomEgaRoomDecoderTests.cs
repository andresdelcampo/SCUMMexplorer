using System.Buffers.Binary;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Rendering;

namespace SCUMMexplorer.Tests;

public sealed class LoomEgaRoomDecoderTests
{
    [Fact]
    public void DecodesSolidAlternatingAndCopiedColumns()
    {
        var room = new byte[45];
        BinaryPrimitives.WriteUInt16LittleEndian(room, 45);
        BinaryPrimitives.WriteUInt16LittleEndian(room.AsSpan(4), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(room.AsSpan(6), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(room.AsSpan(10), 32);
        BinaryPrimitives.WriteUInt16LittleEndian(room.AsSpan(32), 13);
        BinaryPrimitives.WriteUInt16LittleEndian(room.AsSpan(34), 4);
        new byte[] { 0x40, 0xC4, 0x12, 0x84, 0x44, 0x44, 0x44, 0x44, 0x44 }.CopyTo(room, 36);
        for (var i = 0; i < room.Length; i++) room[i] ^= 0xFF;

        var image = new LoomEgaRoomDecoder().Decode(room);

        Assert.True(image.IsValid);
        Assert.Equal(8, image.Width);
        Assert.Equal(4, image.Height);
        Assert.Equal(new byte[] { 0, 1, 1, 4, 4, 4, 4, 4 }, image.Pixels.Span[..8].ToArray());
        Assert.Equal((byte)2, image.Pixels.Span[9]);
        Assert.Equal((byte)2, image.Pixels.Span[10]);
        Assert.Null(image.DepthMask);
    }

    [Fact]
    public void DecodesRoomDepthMaskOffsetsRunsAndPixelBits()
    {
        var room = new byte[52];
        BinaryPrimitives.WriteUInt16LittleEndian(room, 52);
        BinaryPrimitives.WriteUInt16LittleEndian(room.AsSpan(4), 8);
        BinaryPrimitives.WriteUInt16LittleEndian(room.AsSpan(6), 4);
        BinaryPrimitives.WriteUInt16LittleEndian(room.AsSpan(10), 32);
        BinaryPrimitives.WriteUInt16LittleEndian(room.AsSpan(32), 13);
        BinaryPrimitives.WriteUInt16LittleEndian(room.AsSpan(34), 4);
        new byte[] { 0x40, 0xC4, 0x12, 0x84, 0x44, 0x44, 0x44, 0x44, 0x44 }.CopyTo(room, 36);

        // Mask plane follows the color block. The strip offset is relative to the mask table.
        BinaryPrimitives.WriteUInt16LittleEndian(room.AsSpan(45), 2);
        new byte[] { 4, 0x81, 0x42, 0x18, 0xFF }.CopyTo(room, 47);
        for (var i = 0; i < room.Length; i++) room[i] ^= 0xFF;

        var image = new LoomEgaRoomDecoder().Decode(room);

        Assert.NotNull(image.DepthMask);
        Assert.True(image.DepthMask.IsValid);
        Assert.Equal(new byte[]
        {
            1, 0, 0, 0, 0, 0, 0, 1,
            0, 1, 0, 0, 0, 0, 1, 0,
            0, 0, 0, 1, 1, 0, 0, 0,
            1, 1, 1, 1, 1, 1, 1, 1
        }, image.DepthMask.Pixels.ToArray());
    }

    [Fact]
    public async Task DecodesIndexedRoomsInSuppliedLoomReleaseWhenAvailable()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, TestGames.LoomRelative));
        if (!File.Exists(Path.Combine(folder, "00.LFL"))) return;

        var game = new GameFolderScanner().Scan(folder);
        Assert.Equal(ScummVersion.V3, game.Version);
        var rooms = game.Rooms.ToArray();
        Assert.True(rooms.Length > 50);
        var decoder = new LoomEgaRoomDecoder();
        var variedBackgrounds = 0;
        var decodedByName = new Dictionary<string, RoomImage>(StringComparer.OrdinalIgnoreCase);
        foreach (var resource in rooms.Where(room => File.Exists(room.FilePath)))
        {
            var image = await decoder.DecodeAsync(resource.FilePath);
            Assert.True(image.IsValid, resource.Name);
            Assert.NotNull(image.DepthMask);
            Assert.True(image.DepthMask.IsValid, resource.Name);
            Assert.All(image.DepthMask.Pixels.ToArray(), value => Assert.InRange(value, (byte)0, (byte)1));
            decodedByName[Path.GetFileName(resource.FilePath)] = image;
            if (image.Pixels.Span.ToArray().Distinct().Count() > 1) variedBackgrounds++;
        }
        Assert.True(variedBackgrounds > 40);

        Assert.Equal(0, CountMaskPixels(decodedByName["01.LFL"]));
        Assert.Equal(43_435, CountMaskPixels(decodedByName["02.LFL"]));
        Assert.Equal(22_733, CountMaskPixels(decodedByName["28.LFL"]));
        Assert.Equal(8_980, CountMaskPixels(decodedByName["33.LFL"]));
        Assert.Equal(6_388, CountMaskPixels(decodedByName["41.LFL"]));
    }

    [Fact]
    public void EveryLoomObjectImageCarriesAReadableDepthMask()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, TestGames.LoomRelative));
        if (!File.Exists(Path.Combine(folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(folder);
        var counts = new Dictionary<(int Room, int Object), int>();
        foreach (var resource in game.Rooms.Where(room => File.Exists(room.FilePath)))
        foreach (var image in new LoomRoomObjects().Decode(File.ReadAllBytes(resource.FilePath)))
        {
            Assert.NotNull(image.Mask);
            Assert.Equal(image.Width * image.Height, image.Mask.Length);
            counts[(resource.ResourceId ?? -1, image.Id)] = image.Mask.Count(value => value != 0);
        }
        // Room 02's objects 155 and 157, Room 19's 380 (checked 29 Sep).
        Assert.Equal((1450, 512, 7328), (counts[(2, 155)], counts[(2, 157)], counts[(19, 380)]));
    }

    private static int CountMaskPixels(RoomImage image) =>
        image.DepthMask!.Pixels.ToArray().Count(value => value != 0);
}
