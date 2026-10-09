using System.Buffers.Binary;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Tests;

public sealed class LoomRoomLocalScriptTests
{
    [Fact]
    public void LocalScriptDirectoryResolvesScriptAndStopsBeforeTheNextEntry()
    {
        var room = WriteRoom(BuildRoom());
        try
        {
            var script = new ScummScriptReader().DecodeRoomLocalScript(room, 200, 200);

            Assert.NotNull(script);
            Assert.Equal(36, script.ResourceOffset);
            Assert.Equal(9, script.CodeLength);
            var load = Assert.Single(script.Instructions, item => item.Operation == "loadRoomWithEgo");
            Assert.Contains(", 0x04,", load.Operands, StringComparison.Ordinal);
            Assert.Equal("stopObjectCode", script.Instructions[^1].Operation);
            Assert.Equal("Room 03 local script 200", script.Context);
        }
        finally { Directory.Delete(Path.GetDirectoryName(room.FilePath)!, recursive: true); }
    }

    [Fact]
    public void LocalScriptDirectoryRejectsOffsetsIntoItsOwnTable()
    {
        var data = BuildRoom();
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(30, 2), 34);
        var room = WriteRoom(data);
        try
        {
            Assert.Throws<InvalidDataException>(() =>
                new ScummScriptReader().DecodeRoomLocalScript(room, 200, 200));
        }
        finally { Directory.Delete(Path.GetDirectoryName(room.FilePath)!, recursive: true); }
    }

    [Fact]
    public void IndexedScriptIdIsNotReinterpretedAsRoomLocal()
    {
        var room = WriteRoom(BuildRoom());
        try
        {
            Assert.Null(new ScummScriptReader().DecodeRoomLocalScript(room, 199, 200));
        }
        finally { Directory.Delete(Path.GetDirectoryName(room.FilePath)!, recursive: true); }
    }

    // LOOM Room 03 local script 200: breakHere; equalZero bit[0xD7A] back to
    // breakHere; stopScript 0x2A; loadRoomWithEgo 0xA7, 0x04. SCUMM's
    // jumpRelative branches when the condition is false, so the script waits
    // while the bit is set and continues once it is clear.
    private static readonly byte[] WaitWhileBitSetThenLoad =
    [
        0x80, 0x28, 0x7A, 0x8D, 0xFA, 0xFF, 0x62, 0x2A,
        0x24, 0xA7, 0x00, 0x04, 0x44, 0x02, 0x82, 0x00, 0xA0
    ];

    [Fact]
    public void StartedRoomLocalScriptReachesItsRoomLoadAfterAFrameWhenTheBitIsClear()
    {
        WithSession(BuildRoom03LocalAction(WaitWhileBitSetThenLoad), (session, room) =>
        {
            session.VisitRoom(room);
            var action = session.ActivateObjectVerb(162, 0x38);

            Assert.Null(action.DestinationRoomId);
            Assert.True(session.Snapshot.PendingScripts > 0);
            Assert.DoesNotContain(action.Uncertainties, item => item.Contains("Indexed script 200", StringComparison.Ordinal));

            var afterFrame = session.AdvanceFrame();

            Assert.Equal(4, afterFrame.RoomId);
            Assert.Equal(4, session.LastAction?.DestinationRoomId);
            Assert.Equal(8, session.LastAction?.ReachedRoomLoad?.Offset);
            // Script 42 is not running, so stopScript 0x2A has nothing to end.
            Assert.Contains(session.SessionTrace, line => line.Contains("script 42 was not running", StringComparison.Ordinal));
        });
    }

    [Fact]
    public void StartedRoomLocalScriptWaitsWhileItsBitIsSet()
    {
        // move bit[0xD7A] = 1, then the same wait-and-load code.
        WithSession(BuildRoom03LocalAction([0x1A, 0x7A, 0x8D, 0x01, 0x00, .. WaitWhileBitSetThenLoad]), (session, room) =>
        {
            session.VisitRoom(room);
            session.ActivateObjectVerb(162, 0x38);

            Assert.False(session.ContinueUntilRoomChanges(3));
            Assert.Equal(3, session.Snapshot.RoomId);
            Assert.True(session.Snapshot.PendingScripts > 0);
            Assert.Null(session.LastAction?.ReachedRoomLoad);
        });
    }

    [Fact]
    public void StartedRoomLocalScriptPropagatesItsReachedRoomLoad()
    {
        WithSession(BuildRoom03LocalAction([
            0x24, 0xA7, 0x00, 0x04, 0x44, 0x02, 0x82, 0x00, 0xA0
        ]), (session, room) =>
        {
            session.VisitRoom(room);
            var action = session.ActivateObjectVerb(162, 0x38);

            Assert.Equal("loadRoomWithEgo", action.ReachedRoomLoad?.Operation);
            Assert.Equal(4, action.DestinationRoomId);
            Assert.Equal(4, session.Snapshot.RoomId);
        });
    }

    private static void WithSession(byte[] roomBytes, Action<LoomExplorationSession, GameResource> check)
    {
        var directory = Path.Combine(Path.GetTempPath(), "loom-local-session-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var room03Path = Path.Combine(directory, "03.LFL");
            var room04Path = Path.Combine(directory, "04.LFL");
            File.WriteAllBytes(room03Path, roomBytes.Select(value => (byte)(value ^ 0xFF)).ToArray());
            var room03 = new GameResource("Room 03", ResourceKind.Room, room03Path, 3, ContainerFileNumber: 3);
            var room04 = new GameResource("Room 04", ResourceKind.Room, room04Path, 4, ContainerFileNumber: 4);
            File.WriteAllBytes(room04Path, BuildEmptyRoom().Select(value => (byte)(value ^ 0xFF)).ToArray());
            var script199 = new GameResource("Script 199", ResourceKind.Script, room03Path, 199,
                ContainerFileNumber: 3, Offset: 0);
            var game = new GameLibrary("LOOM", directory, ScummVersion.V3, "DOS",
                [room03, room04, script199], "test");
            var session = new LoomExplorationSession(game,
                new ScummIndexedObjectSnapshot(new Dictionary<int, byte> { [162] = 0 },
                    new Dictionary<int, byte> { [162] = 15 }));
            check(session, room03);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    private static byte[] BuildRoom03LocalAction(byte[] localCode)
    {
        var localScriptOffset = 70;
        var data = new byte[localScriptOffset + localCode.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0, 2), (ushort)data.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(4, 2), 320);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(6, 2), 144);
        data[20] = 1;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(31, 2), 40);
        data[33] = 200;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(34, 2), (ushort)localScriptOffset);
        data[36] = 0;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(40, 2), 25);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(44, 2), 162);
        data[49] = 9;
        data[55] = 72;
        data[57] = 0x38;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(58, 2), 21);
        data[60] = 0;
        data[61] = 0x0A;
        data[62] = 0xC8;
        data[63] = 0xFF;
        data[64] = 0x00;
        localCode.CopyTo(data, localScriptOffset);
        return data;
    }

    private static GameResource WriteRoom(byte[] plain)
    {
        var directory = Path.Combine(Path.GetTempPath(), "loom-local-script-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "03.LFL");
        File.WriteAllBytes(path, plain.Select(value => (byte)(value ^ 0xFF)).ToArray());
        return new GameResource("Room 03", ResourceKind.Room, path, 3, ContainerFileNumber: 3);
    }

    private static byte[] BuildRoom()
    {
        byte[] code = [0x24, 0xA7, 0x00, 0x04, 0x44, 0x02, 0x82, 0x00, 0xA0];
        var data = new byte[46];
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0, 2), (ushort)data.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(4, 2), 320);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(6, 2), 144);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(30, 2), 36);
        data[29] = 200;
        data[32] = 201;
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(33, 2), 45);
        data[35] = 0;
        code.CopyTo(data, 36);
        data[45] = 0xA0;
        return data;
    }

    private static byte[] BuildEmptyRoom()
    {
        var data = new byte[29];
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(0, 2), (ushort)data.Length);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(4, 2), 320);
        BinaryPrimitives.WriteUInt16LittleEndian(data.AsSpan(6, 2), 144);
        return data;
    }
}
