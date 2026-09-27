using System.Buffers.Binary;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;
using SCUMMexplorer.Core.Rendering;

namespace SCUMMexplorer.Tests;

public sealed class LoomObjectVerbTests
{
    [Fact]
    public void VerbTablePointsToCodeWithinObjectResource()
    {
        var encoded = BuildRoom6();
        var record = new LoomRoomObjectDirectory().Decode(encoded).Single();

        var entries = new LoomObjectVerbReader().ReadAll(encoded, [record])[204];

        var entry = Assert.Single(entries);
        Assert.Equal(0x38, entry.VerbId);
        Assert.Equal(62, entry.ScriptOffset);
        Assert.Equal(71, entry.ResourceEnd);
    }

    [Fact]
    public void ReachedObjectVerbRoomLoadChangesSessionRoom()
    {
        WithSyntheticRooms((game, room6) =>
        {
            var session = new LoomExplorationSession(game,
                new LoomIndexedObjectSnapshot(new Dictionary<int, byte> { [204] = 0 },
                    new Dictionary<int, byte> { [204] = 15 }));
            session.VisitRoom(room6);

            var action = session.ActivateObjectVerb(204, 0x38);

            Assert.Equal(6, action.SourceRoomId);
            Assert.Equal(5, action.DestinationRoomId);
            Assert.Equal("loadRoomWithEgo", action.ReachedRoomLoad?.Operation);
            Assert.Equal(5, session.Snapshot.RoomId);
            Assert.Contains("verb 0x38", action.Evidence, StringComparison.OrdinalIgnoreCase);
            Assert.False(session.Snapshot.IsFaithful);
        });
    }

    [Fact]
    public void ZeroInitializedGlobalConditionReachesRoomLoad()
    {
        var encoded = BuildRoom6();
        var plain = encoded.Select(value => (byte)(value ^ 0xFF)).ToArray();
        // The object code starts at 40. Its zero-initialized global condition
        // falls through to the load, as it does in the old-v3 VM.
        var code = new byte[]
        {
            0x28, 0x01, 0x00, 0x09, 0x00,
            0x24, 0xC7, 0x00, 0x05, 0x6C, 0x02, 0x78, 0x00,
            0x00, 0x00
        };
        BinaryPrimitives.WriteUInt16LittleEndian(plain.AsSpan(40, 2), (ushort)(22 + code.Length));
        Array.Resize(ref plain, 40 + 22 + code.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(plain.AsSpan(0, 2), (ushort)plain.Length);
        code.CopyTo(plain, 62);
        var modified = plain.Select(value => (byte)(value ^ 0xFF)).ToArray();
        WithSyntheticRooms((game, room6) =>
        {
            var session = new LoomExplorationSession(game,
                new LoomIndexedObjectSnapshot(new Dictionary<int, byte> { [204] = 0 },
                    new Dictionary<int, byte> { [204] = 15 }));
            session.VisitRoom(room6);

            var action = session.ActivateObjectVerb(204, 0x38);

            Assert.Equal("loadRoomWithEgo", action.ReachedRoomLoad?.Operation);
            Assert.Equal(5, action.DestinationRoomId);
            Assert.Equal(5, session.Snapshot.RoomId);
        }, modified);
    }

    private static void WithSyntheticRooms(Action<GameLibrary, GameResource> check, byte[]? room6Bytes = null)
    {
        var folder = Path.Combine(Path.GetTempPath(), "scumm-object-verbs-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var path6 = Path.Combine(folder, "06.LFL");
            var path5 = Path.Combine(folder, "05.LFL");
            File.WriteAllBytes(path6, room6Bytes ?? BuildRoom6());
            File.WriteAllBytes(path5, BuildEmptyRoom());
            var room6 = new GameResource("Room 06", ResourceKind.Room, path6, 6,
                ContainerFileNumber: 6);
            var room5 = new GameResource("Room 05", ResourceKind.Room, path5, 5,
                ContainerFileNumber: 5);
            var game = new GameLibrary("LOOM", folder, ScummVersion.V3, "DOS", [room6, room5], "test");
            check(game, room6);
        }
        finally { Directory.Delete(folder, recursive: true); }
    }

    private static byte[] BuildRoom6()
    {
        var data = new byte[71];
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0, 2), (ushort)data.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(4, 2), 320);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(6, 2), 144);
        data[20] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(31, 2), 40);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(40, 2), 31);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(44, 2), 204);
        data[47] = 17;
        data[48] = 1;
        data[49] = 6;
        data[55] = 80;
        data[57] = 0x38;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(58, 2), 22);
        byte[] code = [0x24, 0xC7, 0x00, 0x05, 0x6C, 0x02, 0x78, 0x00, 0x00];
        code.CopyTo(data, 62);
        return data.Select(value => (byte)(value ^ 0xFF)).ToArray();
    }

    private static byte[] BuildEmptyRoom()
    {
        var data = new byte[29];
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0, 2), (ushort)data.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(4, 2), 320);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(6, 2), 144);
        return data.Select(value => (byte)(value ^ 0xFF)).ToArray();
    }
}
