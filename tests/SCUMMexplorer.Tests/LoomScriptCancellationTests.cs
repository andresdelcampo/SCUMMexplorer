using System.Buffers.Binary;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Tests;

/// <summary>Isolated old-v3 slots; no game resources or prerequisite state.</summary>
public sealed class LoomScriptCancellationTests
{
    [Theory]
    [InlineData(false, 0, 1)]
    [InlineData(true, 1, 2)]
    public void RestartDistinguishesTheOldUpcomingSlotFromItsNewCopy(bool recursive, int first, int second)
    {
        WithSession(new Dictionary<int, byte[]>
        {
            [2] = [0x80, recursive ? (byte)0x4A : (byte)0x0A, 3, 0xFF, 0],
            [3] = [0x80, 0x46, 0xF1, 0, 0]
        }, session =>
        {
            Start(session, 2, 3);
            session.AdvanceFrame();
            Assert.Equal(first, Value(session, 0xF1));
            session.AdvanceFrame();
            Assert.Equal(second, Value(session, 0xF1));
        });
    }

    [Fact]
    public void AChildStopsItsExecutingCallerBeforeItsFollowingWrite()
    {
        WithSession(new Dictionary<int, byte[]>
        {
            [2] = [0x0A, 3, 0xFF, 0x1A, 0xF1, 0, 1, 0, 0],
            [3] = [0x62, 2, 0x68, 0xF3, 0, 2, 0x1A, 0xF2, 0, 1, 0, 0]
        }, session =>
        {
            Start(session, 2);
            Assert.Equal(0, Value(session, 0xF1));
            Assert.Equal(1, Value(session, 0xF2));
            Assert.Equal(0, Value(session, 0xF3)); // isScriptRunning must see the cancellation immediately.
            session.AdvanceFrame();
            Assert.Equal(0, Value(session, 0xF1));
        });
    }

    [Fact]
    public void ACurrentScriptCanStopItselfByNumber()
    {
        WithSession(new Dictionary<int, byte[]>
        {
            [2] = [0x62, 2, 0x1A, 0xF1, 0, 1, 0, 0]
        }, session =>
        {
            Start(session, 2);
            Assert.Equal(0, Value(session, 0xF1));
            Assert.Equal(0, session.Snapshot.PendingScripts);
        });
    }

    [Fact]
    public void RestartingAnExecutingScriptKeepsOnlyTheReplacementContinuation()
    {
        WithSession(new Dictionary<int, byte[]>
        {
            // local0=0 starts a new copy with local0=1; only that copy may resume.
            [2] = [0x28, 0, 0x40, 12, 0, 0x0A, 2, 1, 1, 0, 0xFF,
                0x1A, 0xF1, 0, 1, 0, 0, 0x80, 0x1A, 0xF2, 0, 1, 0, 0]
        }, session =>
        {
            Start(session, 2);
            Assert.Equal(0, Value(session, 0xF1));
            Assert.Equal(1, session.Snapshot.PendingScripts);
            session.AdvanceFrame();
            Assert.Equal(0, Value(session, 0xF1));
            Assert.Equal(1, Value(session, 0xF2));
        });
    }

    [Fact]
    public void StopThenRestartKeepsTheReplacementAliveOnTheFollowingFrame()
    {
        WithSession(new Dictionary<int, byte[]>
        {
            [2] = [0x80, 0x62, 3, 0x0A, 3, 0xFF, 0],
            [3] = [0x80, 0x46, 0xF1, 0, 0]
        }, session =>
        {
            Start(session, 2, 3);
            session.AdvanceFrame();
            Assert.Equal(0, Value(session, 0xF1));
            Assert.Equal(1, session.Snapshot.PendingScripts);
            session.AdvanceFrame();
            Assert.Equal(1, Value(session, 0xF1));
        });
    }

    [Fact]
    public void CancellationDoesNotExecuteTheProducersCompletionWrite()
    {
        WithSession(new Dictionary<int, byte[]>
        {
            [2] = [0x1A, 0xF1, 0, 1, 0, 0x80, 0x1A, 0xF1, 0, 0, 0, 0],
            [3] = [0x62, 2, 0]
        }, session =>
        {
            Start(session, 2, 3);
            session.AdvanceFrame();
            Assert.Equal(1, Value(session, 0xF1));
            Assert.Equal(0, session.Snapshot.PendingScripts);
        });
    }

    private static ushort Value(LoomExplorationSession session, int id) =>
        session.Snapshot.KnownGlobalVariables[$"var[0x{id:X3}]"];

    private static void Start(LoomExplorationSession session, params int[] ids)
    {
        var code = ids.SelectMany(id => new byte[] { 0x0A, (byte)id, 0xFF }).Append((byte)0).ToArray();
        var decoded = DecodeFixture(code);
        session.RunTopLevelScript(decoded);
    }

    private static LoomScriptDisassembly DecodeFixture(byte[] code)
    {
        // Top-level test driver; child programs use the normal resource reader.
        var instructions = new List<LoomScriptInstruction>();
        for (var at = 0; at < code.Length - 1; at += 3)
            instructions.Add(new(at, 0x0A, "startScript", $"0x{code[at + 1]:X2}, []", $"0A {code[at + 1]:X2} FF"));
        instructions.Add(new(code.Length - 1, 0, "stopObjectCode", "", "00"));
        return new(0, 1, 0, code.Length + 4, code.Length, instructions, null, null, [], []);
    }

    private static void WithSession(Dictionary<int, byte[]> programs, Action<LoomExplorationSession> check)
    {
        var directory = Path.Combine(Path.GetTempPath(), "loom-cancellation-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            programs.Add(1, [0]); // Empty boot; every test starts its own source slots.
            var resources = new List<GameResource>();
            foreach (var (id, code) in programs)
            {
                var data = new byte[code.Length + 4];
                BinaryPrimitives.WriteUInt16LittleEndian(data, (ushort)data.Length);
                code.CopyTo(data, 4);
                var path = Path.Combine(directory, $"{id:00}.LFL");
                File.WriteAllBytes(path, data.Select(b => (byte)(b ^ 0xFF)).ToArray());
                resources.Add(new($"Script {id}", ResourceKind.Script, path, id, ContainerFileNumber: id, Offset: 0));
            }
            var game = new GameLibrary("LOOM", directory, ScummVersion.V3, "DOS", resources, "synthetic cancellation fixtures", GameId.Loom);
            check(new LoomExplorationSession(game, new LoomIndexedObjectSnapshot(new Dictionary<int, byte>(),
                new Dictionary<int, byte>()), completeBoot: false));
        }
        finally { Directory.Delete(directory, recursive: true); }
    }
}
