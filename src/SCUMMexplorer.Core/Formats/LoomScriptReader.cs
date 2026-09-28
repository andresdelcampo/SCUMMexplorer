using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Formats;

public sealed record LoomScriptInstruction(int Offset, byte Opcode, string Operation, string Operands, string Bytes);

public sealed record LoomScriptDecodeIssue(int Offset, string Reason);

/// <summary>
/// Game-specific old-v3 operand rules. Indiana Jones and the Last Crusade
/// (non-Macintosh) differs from LOOM in a few opcodes; see ScummVM
/// <c>o5_wait</c>, <c>o5_getActorX/Y</c> and <c>o3_waitForActor</c>.
/// </summary>
public enum ScummScriptDialect
{
    Loom,
    Indy3
}

public static class ScummScriptDialects
{
    public static ScummScriptDialect For(GameId game) =>
        game == GameId.IndianaJones3 ? ScummScriptDialect.Indy3 : ScummScriptDialect.Loom;
}

public sealed record LoomScriptDisassembly(
    int ScriptId,
    int ContainerFileNumber,
    int ResourceOffset,
    int ResourceLength,
    int CodeLength,
    IReadOnlyList<LoomScriptInstruction> Instructions,
    string? DecodeNote,
    int? UndecodedOffset,
    byte[] UndecodedBytes,
    byte[] TrailingBytes,
    string? Context = null,
    IReadOnlyList<LoomScriptDecodeIssue>? Issues = null)
{
    public int DecodedBytes => Instructions.Sum(instruction =>
        instruction.Bytes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length);

    public bool HasCompleteControlFlow => Issues is { Count: 0 };

    public string Format()
    {
        var output = new StringBuilder();
        output.AppendLine(Context is null
            ? $"Script {ScriptId:00}  ·  {ContainerFileNumber:00}.LFL  ·  resource 0x{ResourceOffset:X4}"
            : $"{Context}  ·  {ContainerFileNumber:00}.LFL  ·  file offset 0x{ResourceOffset:X4}");
        output.AppendLine(Context is null
            ? $"Resource length {ResourceLength} bytes  ·  bytecode {CodeLength} bytes  ·  code starts at file offset 0x{ResourceOffset + 4:X4}"
            : $"Embedded bytecode {CodeLength} bytes");
        output.AppendLine($"Control-flow decode: {Instructions.Count} instructions, {DecodedBytes}/{CodeLength} resource bytes; " +
                          $"{Issues?.Count ?? (DecodeNote is null ? 0 : 1)} unresolved paths. Undecoded bytes may be data or unreachable code.");
        output.AppendLine("Static instructions are possible paths, not proof of execution. Bounded execution reports unsupported behavior explicitly.");
        output.AppendLine();

        foreach (var instruction in Instructions)
        {
            var operands = string.IsNullOrEmpty(instruction.Operands) ? "" : " " + instruction.Operands;
            output.AppendLine($"{instruction.Offset:X4}  {instruction.Bytes,-17} {instruction.Operation}{operands}");
        }

        if (Issues is { Count: > 0 })
        {
            output.AppendLine();
            foreach (var issue in Issues)
                output.AppendLine($"Unresolved path at +0x{issue.Offset:X4}: {issue.Reason}");
        }
        else if (DecodeNote is not null)
        {
            output.AppendLine();
            output.AppendLine($"Partial disassembly at byte +0x{UndecodedOffset.GetValueOrDefault():X4}: {DecodeNote}");
            output.AppendLine($"Undecoded bytes: {FormatBytes(UndecodedBytes)}");
        }
        else if (TrailingBytes.Length > 0)
        {
            output.AppendLine();
            output.AppendLine($"Bytes after stopObjectCode: {FormatBytes(TrailingBytes)}");
        }

        return output.ToString();
    }

    private static string FormatBytes(IEnumerable<byte> bytes) => string.Join(" ", bytes.Select(value => value.ToString("X2", CultureInfo.InvariantCulture)));
}

/// <summary>Reads and conservatively disassembles indexed DOS LOOM old-v3 script resources.</summary>
public sealed class LoomScriptReader(ScummScriptDialect dialect = ScummScriptDialect.Loom)
{
    public ScummScriptDialect Dialect { get; } = dialect;

    private const int ResourceHeaderLength = 4;
    private const int RoomExitScriptPointerOffset = 0x19;
    private const int RoomEntryScriptPointerOffset = 0x1B;

    public LoomScriptDisassembly Decode(GameResource resource)
    {
        ArgumentNullException.ThrowIfNull(resource);
        if (resource.Kind != ResourceKind.Script || resource.ResourceId is not int scriptId ||
            resource.ContainerFileNumber is not int container || resource.Offset is not int resourceOffset)
            throw new InvalidDataException("The selected item is not a complete indexed script resource.");
        if (container <= 0)
            throw new InvalidDataException($"Script {scriptId:00} has invalid container file number {container}.");

        var encoded = File.ReadAllBytes(resource.FilePath);
        var data = LflEncoding.DecodeXorFF(encoded);
        if (resourceOffset < 0 || resourceOffset > data.Length - 2)
            throw new InvalidDataException($"Script {scriptId:00} points outside {container:00}.LFL at 0x{resourceOffset:X4}.");

        var resourceLength = ReadWord(data, resourceOffset);
        if (resourceLength < ResourceHeaderLength)
            throw new InvalidDataException($"Script {scriptId:00} has an invalid old-v3 resource length ({resourceLength}).");
        if (resourceLength > data.Length - resourceOffset)
            throw new InvalidDataException($"Script {scriptId:00} declares {resourceLength} bytes at 0x{resourceOffset:X4}, past the end of {container:00}.LFL.");

        var code = data.AsSpan(resourceOffset + ResourceHeaderLength, resourceLength - ResourceHeaderLength).ToArray();
        var decoded = LoomScriptDisassembler.Disassemble(code, Dialect);
        return new LoomScriptDisassembly(scriptId, container, resourceOffset, resourceLength, code.Length,
            decoded.Instructions, decoded.Note, decoded.UndecodedOffset, decoded.UndecodedBytes, decoded.TrailingBytes,
            Issues: decoded.Issues);
    }

    public LoomScriptDisassembly? DecodeRoomEntry(GameResource room) =>
        DecodeRoomScript(room, RoomEntryScriptPointerOffset, "entry");

    public LoomScriptDisassembly? DecodeRoomExit(GameResource room) =>
        DecodeRoomScript(room, RoomExitScriptPointerOffset, "exit");

    /// <summary>The IDs in an old-v3 room's local-script directory, in directory order (empty when it cannot be read).</summary>
    public static IReadOnlyList<int> ReadRoomLocalScriptIds(byte[] roomBytes, int globalScriptCount)
    {
        var data = LflEncoding.DecodeXorFF(roomBytes);
        if (data.Length < 29) return [];
        var roomEnd = Math.Min(ReadWord(data, 0), data.Length);
        var cursor = 29 + data[20] * 4 + data[23] + data[24];
        var ids = new List<int>();
        while (cursor <= roomEnd - 3 && data[cursor] != 0)
        {
            if (data[cursor] >= globalScriptCount && !ids.Contains(data[cursor])) ids.Add(data[cursor]);
            cursor += 3;
        }
        return ids;
    }

    /// <summary>Decodes an old-v3 local script from the current room's local-script directory.</summary>
    public LoomScriptDisassembly? DecodeRoomLocalScript(GameResource room, int scriptId, int globalScriptCount)
    {
        ArgumentNullException.ThrowIfNull(room);
        if (room.Kind != ResourceKind.Room || room.ResourceId is not int roomId ||
            room.ContainerFileNumber is not int container)
            throw new InvalidDataException("Local scripts require an indexed room.");
        if (globalScriptCount is < 0 or > byte.MaxValue || scriptId < globalScriptCount || scriptId > byte.MaxValue)
            return null;

        var data = LflEncoding.DecodeXorFF(File.ReadAllBytes(room.FilePath));
        if (data.Length < 29) throw new InvalidDataException($"Room {roomId:00} is too short to contain a local-script directory.");
        var roomEnd = ReadWord(data, 0);
        var objectCount = data[20];
        var soundCount = data[23];
        var scriptCount = data[24];
        var tableOffset = checked(29 + objectCount * 4 + soundCount + scriptCount);
        if (roomEnd > data.Length || tableOffset >= roomEnd)
            throw new InvalidDataException($"Room {roomId:00} has an invalid local-script directory offset.");

        var localScripts = new List<(int Id, int Offset)>();
        var cursor = tableOffset;
        while (cursor < roomEnd && data[cursor] != 0)
        {
            if (cursor > roomEnd - 3)
                throw new InvalidDataException($"Room {roomId:00} local-script directory has a truncated entry.");
            localScripts.Add((data[cursor], ReadWord(data, cursor + 1)));
            cursor += 3;
        }
        if (cursor >= roomEnd)
            throw new InvalidDataException($"Room {roomId:00} local-script directory has no terminator.");
        var scriptsStart = cursor + 1;
        if (localScripts.Select(item => item.Id).Distinct().Count() != localScripts.Count)
            throw new InvalidDataException($"Room {roomId:00} local-script directory contains duplicate IDs.");
        if (localScripts.Any(item => item.Id < globalScriptCount || item.Offset < scriptsStart || item.Offset >= roomEnd))
            throw new InvalidDataException($"Room {roomId:00} local-script directory contains an invalid ID or offset.");
        var selectedIndex = localScripts.FindIndex(item => item.Id == scriptId);
        if (selectedIndex < 0) return null;
        var start = localScripts[selectedIndex].Offset;
        var end = localScripts.Where(item => item.Offset > start).Select(item => item.Offset)
            .DefaultIfEmpty(roomEnd).Min();
        if (start < scriptsStart || start >= end || end > roomEnd)
            throw new InvalidDataException($"Room {roomId:00} local script {scriptId:00} has invalid bounds.");

        var code = data.AsSpan(start, end - start).ToArray();
        var decoded = LoomScriptDisassembler.Disassemble(code, Dialect);
        return new LoomScriptDisassembly(scriptId, container, start, code.Length, code.Length,
            decoded.Instructions, decoded.Note, decoded.UndecodedOffset, decoded.UndecodedBytes,
            decoded.TrailingBytes, $"Room {roomId:00} local script {scriptId:00}", decoded.Issues);
    }

    private LoomScriptDisassembly? DecodeRoomScript(GameResource room, int pointerOffset, string phase)
    {
        ArgumentNullException.ThrowIfNull(room);
        if (room.Kind != ResourceKind.Room || room.ResourceId is not int roomId || room.ContainerFileNumber is not int container)
            throw new InvalidDataException("The selected item is not a complete indexed room resource.");

        var data = LflEncoding.DecodeXorFF(File.ReadAllBytes(room.FilePath));
        if (data.Length < RoomEntryScriptPointerOffset + 2)
            throw new InvalidDataException($"Room {roomId:00} is too short to contain its script pointers.");

        var roomEnd = ReadWord(data, 0);
        var scriptOffset = ReadWord(data, pointerOffset);
        if (scriptOffset == 0) return null;
        if (roomEnd > data.Length || scriptOffset >= roomEnd)
            throw new InvalidDataException($"Room {roomId:00} {phase}-script offset 0x{scriptOffset:X4} lies outside its room data.");

        var limit = roomEnd;
        if (phase == "exit")
        {
            var entryOffset = ReadWord(data, RoomEntryScriptPointerOffset);
            if (entryOffset > scriptOffset && entryOffset < limit) limit = entryOffset;
        }
        var code = data.AsSpan(scriptOffset, limit - scriptOffset).ToArray();
        var decoded = LoomScriptDisassembler.Disassemble(code, Dialect);
        return new LoomScriptDisassembly(roomId, container, scriptOffset, code.Length, code.Length,
            decoded.Instructions, decoded.Note, decoded.UndecodedOffset, decoded.UndecodedBytes,
            decoded.TrailingBytes, $"Room {roomId:00} {phase} script", decoded.Issues);
    }

    private static ushort ReadWord(byte[] data, int offset) => BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset, 2));
}

internal static class LoomScriptDisassembler
{
    // SCUMM v3 inherits the v4 small-header actorOps encoding. The low five
    // bits are indices into this translation table; the upper parameter-mode
    // bits remain unchanged. See ScummEngine_v5::o5_actorOps in ScummVM.
    private static readonly byte[] SmallHeaderActorOperations =
        [1, 0, 0, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12, 13, 14, 15, 16, 17, 20];

    internal sealed record Result(
        IReadOnlyList<LoomScriptInstruction> Instructions,
        string? Note,
        int? UndecodedOffset,
        byte[] UndecodedBytes,
        byte[] TrailingBytes,
        IReadOnlyList<LoomScriptDecodeIssue> Issues);

    public static Result Disassemble(byte[] code, ScummScriptDialect dialect = ScummScriptDialect.Loom)
    {
        var instructions = new SortedDictionary<int, LoomScriptInstruction>();
        var issues = new List<LoomScriptDecodeIssue>();
        var queued = new Queue<int>();
        var visited = new HashSet<int>();
        var byteOwners = new int[code.Length];
        Array.Fill(byteOwners, -1);
        if (code.Length > 0) queued.Enqueue(0);

        while (queued.Count > 0)
        {
            var start = queued.Dequeue();
            if (!visited.Add(start)) continue;
            if (start < 0 || start >= code.Length)
            {
                issues.Add(new LoomScriptDecodeIssue(start, "control-flow target is outside the resource"));
                continue;
            }
            if (byteOwners[start] >= 0)
            {
                issues.Add(new LoomScriptDecodeIssue(start,
                    $"control-flow target enters instruction at +0x{byteOwners[start]:X4}"));
                continue;
            }

            var cursor = new Cursor(code, start, dialect);
            try
            {
                var opcode = cursor.ReadByte();
                var (operation, operands) = DecodeInstruction(cursor, opcode);
                var end = cursor.Position;
                var overlap = Enumerable.Range(start, end - start).FirstOrDefault(offset => byteOwners[offset] >= 0, -1);
                if (overlap >= 0)
                {
                    issues.Add(new LoomScriptDecodeIssue(start,
                        $"instruction overlaps decoded instruction at +0x{byteOwners[overlap]:X4}"));
                    continue;
                }
                for (var offset = start; offset < end; offset++) byteOwners[offset] = start;
                instructions.Add(start, new LoomScriptInstruction(start, opcode, operation, operands,
                    FormatBytes(code.AsSpan(start, end - start).ToArray())));

                if (operation == "stopObjectCode") continue;
                // ScummEngine::beginOverride records the jump that follows it (taken
                // only when the player skips the cutscene) and steps over it, so
                // the code after that jump is the cutscene's normal path.
                if (operation == "beginOverride" && end + 3 <= code.Length && code[end] == 0x18)
                    queued.Enqueue(end + 3);
                if (IsBranchOperation(operation))
                {
                    var displacement = BinaryPrimitives.ReadInt16LittleEndian(code.AsSpan(end - 2, 2));
                    var target = end + displacement;
                    queued.Enqueue(target);
                    if (operation == "jumpRelative") continue;
                }
                queued.Enqueue(end);
            }
            catch (UnsupportedBytecodeException ex)
            {
                issues.Add(new LoomScriptDecodeIssue(start, ex.Message));
            }
            catch (EndOfStreamException)
            {
                issues.Add(new LoomScriptDecodeIssue(start, "the instruction or its operands are truncated"));
            }
        }

        var firstIssue = issues.OrderBy(issue => issue.Offset).FirstOrDefault();
        var undecodedBytes = firstIssue is { Offset: >= 0 } && firstIssue.Offset < code.Length
            ? code.AsSpan(firstIssue.Offset).ToArray() : [];
        return new Result(instructions.Values.ToArray(), firstIssue?.Reason, firstIssue?.Offset,
            undecodedBytes, [], issues.OrderBy(issue => issue.Offset).ToArray());
    }

    private static bool IsBranchOperation(string operation) => operation is
        "jumpRelative" or "isGreaterEqual" or "isNotEqual" or "isLessEqual" or "isLess" or
        "isEqual" or "isGreater" or "equalZero" or "notEqualZero" or "ifClassOfIs" or
        "isActorInBox" or "ifState" or "ifNotState";

    private static (string Operation, string Operands) DecodeInstruction(Cursor cursor, byte opcode, int nesting = 0)
    {
        if (nesting > 16)
            throw new UnsupportedBytecodeException("nested expression opcode depth exceeds 16");
        if (opcode is 0x00 or 0xA0) return ("stopObjectCode", "");
        if (opcode == 0x80) return ("breakHere", "");
        if (opcode == 0xC0) return ("endCutscene", "");
        if (opcode == 0x20) return ("stopMusic", "");
        if (opcode == 0x18)
        {
            var displacement = cursor.ReadInt16();
            return ("jumpRelative", $"{displacement:+#;-#;0}  ->  0x{cursor.Position + displacement:X4}");
        }

        if (Is(opcode, 0x04, 0x08, 0x38, 0x44, 0x48, 0x78, 0x84, 0x88, 0xB8, 0xC4, 0xC8, 0xF8, 0x28, 0xA8))
            return DecodeConditional(cursor, opcode);

        if (Is(opcode, 0x05, 0x25, 0x45, 0x65, 0x85, 0xA5, 0xC5, 0xE5))
            return DecodeDrawObject(cursor, opcode);
        if (Is(opcode, 0x0F, 0x4F, 0x8F, 0xCF, 0x2F, 0x6F, 0xAF, 0xEF))
            return DecodeStateConditional(cursor, opcode);
        if (Is(opcode, 0x07, 0x47, 0x87, 0xC7))
            return ("setState", Join(ReadParam(cursor, 2, opcode, 0x80), ReadParam(cursor, 1, opcode, 0x40)));
        if (Is(opcode, 0x0A, 0x2A, 0x4A, 0x6A, 0x8A, 0xAA, 0xCA, 0xEA))
            return ("startScript", Join(ReadParam(cursor, 1, opcode, 0x80), ReadVarargList(cursor)));
        if (Is(opcode, 0x37, 0x77, 0xB7, 0xF7))
            return ("startObject", Join(ReadParam(cursor, 2, opcode, 0x80), ReadParam(cursor, 1, opcode, 0x40), ReadVarargList(cursor)));
        if (Is(opcode, 0x42, 0xC2))
            return ("chainScript", Join(ReadParam(cursor, 1, opcode, 0x80), ReadVarargList(cursor)));
        if (opcode == 0x40)
            return ("cutscene", ReadVarargList(cursor));

        if (Is(opcode, 0x50, 0xD0))
            return ("pickupObject", ReadParam(cursor, 2, opcode, 0x80));
        if (Is(opcode, 0x10, 0x90))
            return ("getObjectOwner", Join(ReadResult(cursor), ReadParam(cursor, 2, opcode, 0x80)));
        if (Is(opcode, 0x03, 0x83))
            return ("getActorRoom", Join(ReadResult(cursor), ReadParam(cursor, 1, opcode, 0x80)));
        if (Is(opcode, 0x06, 0x86))
            return ("getActorElevation", Join(ReadResult(cursor), ReadParam(cursor, 1, opcode, 0x80)));
        if (Is(opcode, 0x16, 0x96))
            return ("getRandomNr", Join(ReadResult(cursor), ReadParam(cursor, 1, opcode, 0x80)));
        if (Is(opcode, 0x22, 0xA2))
            return ("saveLoadGame", Join(ReadResult(cursor), ReadParam(cursor, 1, opcode, 0x80)));
        if (Is(opcode, 0x31, 0xB1))
            return ("getInventoryCount", Join(ReadResult(cursor), ReadParam(cursor, 1, opcode, 0x80)));
        if (Is(opcode, 0x3D, 0x7D, 0xBD, 0xFD))
            return ("findInventory", Join(ReadResult(cursor), ReadParam(cursor, 1, opcode, 0x80), ReadParam(cursor, 1, opcode, 0x40)));
        if (Is(opcode, 0x34, 0x74, 0xB4, 0xF4))
            return ("getDist", Join(ReadResult(cursor), ReadParam(cursor, 2, opcode, 0x80), ReadParam(cursor, 2, opcode, 0x40)));
        if (Is(opcode, 0x35, 0x75, 0xB5, 0xF5))
            return ("findObject", Join(ReadResult(cursor), ReadParam(cursor, 1, opcode, 0x80), ReadParam(cursor, 1, opcode, 0x40)));
        // Indy3 (non-Macintosh) reads a byte actor operand; LOOM reads a word.
        if (Is(opcode, 0x43, 0xC3))
            return ("getActorX", Join(ReadResult(cursor), ReadParam(cursor, cursor.Dialect == ScummScriptDialect.Indy3 ? 1 : 2, opcode, 0x80)));
        if (Is(opcode, 0x23, 0xA3))
            return ("getActorY", Join(ReadResult(cursor), ReadParam(cursor, cursor.Dialect == ScummScriptDialect.Indy3 ? 1 : 2, opcode, 0x80)));
        if (Is(opcode, 0x63, 0xE3))
            return ("getActorFacing", Join(ReadResult(cursor), ReadParam(cursor, 1, opcode, 0x80)));
        if (Is(opcode, 0x68, 0xE8))
            return ("isScriptRunning", Join(ReadResult(cursor), ReadParam(cursor, 1, opcode, 0x80)));
        if (Is(opcode, 0x7C, 0xFC))
            return ("isSoundRunning", Join(ReadResult(cursor), ReadParam(cursor, 1, opcode, 0x80)));
        if (Is(opcode, 0x66, 0xE6))
            return ("getClosestObjActor", Join(ReadResult(cursor), ReadParam(cursor, 2, opcode, 0x80)));
        if (Is(opcode, 0x6C, 0xEC))
            return ("getActorWidth", Join(ReadResult(cursor), ReadParam(cursor, 1, opcode, 0x80)));
        if (Is(opcode, 0x67, 0xE7))
            return ("getStringWidth", Join(ReadResult(cursor), ReadParam(cursor, 1, opcode, 0x80)));
        if (Is(opcode, 0x71, 0xF1))
            return ("getActorCostume", Join(ReadResult(cursor), ReadParam(cursor, 1, opcode, 0x80)));
        if (Is(opcode, 0x7B, 0xFB))
            return ("getActorWalkBox", Join(ReadResult(cursor), ReadParam(cursor, 1, opcode, 0x80)));
        if (Is(opcode, 0x56, 0xD6))
            return ("getActorMoving", Join(ReadResult(cursor), ReadParam(cursor, 1, opcode, 0x80)));

        if (Is(opcode, 0x01, 0x21, 0x41, 0x61, 0x81, 0xA1, 0xC1, 0xE1))
            return ("putActor", Join(ReadParam(cursor, 1, opcode, 0x80), ReadParam(cursor, 2, opcode, 0x40), ReadParam(cursor, 2, opcode, 0x20)));
        if (Is(opcode, 0x09, 0x49, 0x89, 0xC9))
            return ("faceActor", Join(ReadParam(cursor, 1, opcode, 0x80), ReadParam(cursor, 2, opcode, 0x40)));
        if (Is(opcode, 0x0D, 0x4D, 0x8D, 0xCD))
            return ("walkActorToActor", Join(ReadParam(cursor, 1, opcode, 0x80), ReadParam(cursor, 1, opcode, 0x40), ReadParam(cursor, 1, opcode, 0)));
        if (Is(opcode, 0x0E, 0x4E, 0x8E, 0xCE))
            return ("putActorAtObject", Join(ReadParam(cursor, 1, opcode, 0x80), ReadParam(cursor, 2, opcode, 0x40)));
        if (Is(opcode, 0x11, 0x51, 0x91, 0xD1))
            return ("animateActor", Join(ReadParam(cursor, 1, opcode, 0x80), ReadParam(cursor, 1, opcode, 0x40)));
        if (Is(opcode, 0x15, 0x55, 0x95, 0xD5))
            return ("actorFromPos", Join(ReadResult(cursor), ReadParam(cursor, 2, opcode, 0x80), ReadParam(cursor, 2, opcode, 0x40)));
        if (Is(opcode, 0x1E, 0x3E, 0x5E, 0x7E, 0x9E, 0xBE, 0xDE, 0xFE))
            return ("walkActorTo", Join(ReadParam(cursor, 1, opcode, 0x80), ReadParam(cursor, 2, opcode, 0x40), ReadParam(cursor, 2, opcode, 0x20)));
        if (Is(opcode, 0x36, 0x76, 0xB6, 0xF6))
            return ("walkActorToObject", Join(ReadParam(cursor, 1, opcode, 0x80), ReadParam(cursor, 2, opcode, 0x40)));
        if (Is(opcode, 0x2D, 0x6D, 0xAD, 0xED))
            return ("putActorInRoom", Join(ReadParam(cursor, 1, opcode, 0x80), ReadParam(cursor, 1, opcode, 0x40)));
        if (Is(opcode, 0x52, 0xD2))
            return ("actorFollowCamera", ReadParam(cursor, 1, opcode, 0x80));

        if (Is(opcode, 0x29, 0x69, 0xA9, 0xE9))
            return ("setOwnerOf", Join(ReadParam(cursor, 2, opcode, 0x80), ReadParam(cursor, 1, opcode, 0x40)));
        if (Is(opcode, 0x24, 0x64, 0xA4, 0xE4))
        {
            var objectNumber = ReadParam(cursor, 2, opcode, 0x80);
            var room = ReadParam(cursor, 1, opcode, 0x40);
            var x = cursor.ReadInt16();
            var y = cursor.ReadInt16();
            return ("loadRoomWithEgo", Join(objectNumber, room, $"walkTo=({x}, {y})"));
        }
        if (Is(opcode, 0x72, 0xF2))
            return ("loadRoom", ReadParam(cursor, 1, opcode, 0x80));
        if (Is(opcode, 0x02, 0x82))
            return ("startMusic", ReadParam(cursor, 1, opcode, 0x80));
        if (Is(opcode, 0x1C, 0x9C))
            return ("startSound", ReadParam(cursor, 1, opcode, 0x80));
        if (Is(opcode, 0x3C, 0xBC))
            return ("stopSound", ReadParam(cursor, 1, opcode, 0x80));
        if (Is(opcode, 0x62, 0xE2))
            return ("stopScript", ReadParam(cursor, 1, opcode, 0x80));
        if (Is(opcode, 0x6E, 0xEE))
            return ("stopObjectScript", ReadParam(cursor, 2, opcode, 0x80));
        if (Is(opcode, 0x70, 0xF0))
            return ("lights", Join(ReadParam(cursor, 1, opcode, 0x80), ReadParam(cursor, 1, opcode, 0), ReadParam(cursor, 1, opcode, 0)));

        if (Is(opcode, 0x19, 0x39, 0x59, 0x79, 0x99, 0xB9, 0xD9, 0xF9))
            return ("doSentence", Join(ReadParam(cursor, 1, opcode, 0x80), ReadParam(cursor, 2, opcode, 0x40), ReadParam(cursor, 2, opcode, 0x20)));
        if (Is(opcode, 0x1A, 0x9A))
            return DecodeVariableOperation(cursor, opcode, "move", "=");
        if (Is(opcode, 0x1B, 0x9B))
            return DecodeVariableOperation(cursor, opcode, "multiply", "*=");
        if (Is(opcode, 0x17, 0x97))
            return DecodeVariableOperation(cursor, opcode, "and", "&=");
        if (Is(opcode, 0x3A, 0xBA))
            return DecodeVariableOperation(cursor, opcode, "subtract", "-=");
        if (Is(opcode, 0x5A, 0xDA))
            return DecodeVariableOperation(cursor, opcode, "add", "+=");
        if (Is(opcode, 0x5B, 0xDB))
            return DecodeVariableOperation(cursor, opcode, "divide", "/=");
        if (Is(opcode, 0x57, 0xD7))
            return DecodeVariableOperation(cursor, opcode, "or", "|=");
        if (opcode is 0x46 or 0xC6)
            return (opcode == 0x46 ? "increment" : "decrement", ReadVar(cursor));

        if (opcode == 0x2E)
        {
            var delay = cursor.ReadByte() | (cursor.ReadByte() << 8) | (cursor.ReadByte() << 16);
            return ("delay", $"0x{delay:X6}");
        }
        if (opcode == 0x2B) return ("delayVariable", ReadVar(cursor));
        // v3 maps 0x3B to o3_waitForActor: a LOOM no-op without operands, and
        // an Indy3 wait on a byte (or variable) actor operand.
        if (opcode is 0x3B or 0xBB)
            return cursor.Dialect == ScummScriptDialect.Indy3
                ? ("waitForActor", ReadParam(cursor, 1, opcode, 0x80))
                : ("waitForActor", "LOOM no-op");
        if (opcode == 0x4C) return ("waitForSentence", "");
        if (opcode == 0x58) return (cursor.ReadByte() == 0 ? "endOverride" : "beginOverride", "");
        if (opcode == 0x32 || opcode == 0xB2) return ("setCameraAt", ReadParam(cursor, 2, opcode, 0x80));
        if (opcode is 0x12 or 0x92) return ("panCameraTo", ReadParam(cursor, 2, opcode, 0x80));
        if (Is(opcode, 0x26, 0xA6)) return DecodeVariableRange(cursor, opcode);
        if (opcode == 0x5D || opcode == 0xDD) return ("setClass", Join(ReadParam(cursor, 2, opcode, 0x80), ReadVarargList(cursor)));
        if (opcode == 0x1D || opcode == 0x9D) return DecodeClassConditional(cursor, opcode);
        if (opcode == 0xAC) return DecodeExpression(cursor, nesting);

        if (opcode is 0x13 or 0x53 or 0x93 or 0xD3) return DecodeActorOps(cursor, opcode);
        if (opcode is 0x33 or 0x73 or 0xB3 or 0xF3) return DecodeRoomOps(cursor, opcode);
        if (opcode is 0x14 or 0x94 or 0xD8) return DecodePrint(cursor, opcode);
        if (opcode is 0x0C or 0x8C) return DecodeResourceRoutines(cursor);
        if (opcode is 0x5C or 0xDC) return DecodeOldRoomEffect(cursor);
        if (opcode == 0xA7) return ("saveLoadVars", $"subopcode 0x{cursor.ReadByte():X2}");
        if (opcode == 0xAB) return DecodeSaveRestoreVerbs(cursor);
        if (opcode is 0x1F or 0x5F or 0x9F or 0xDF) return DecodeActorInBox(cursor, opcode);

        if (opcode == 0x27) return DecodeStringOps(cursor);
        if (opcode == 0x2C) return DecodeCursorCommand(cursor);
        if (opcode == 0x30 || opcode == 0xB0)
            return ("setBoxFlags", Join(ReadParam(cursor, 1, opcode, 0x80), $"0x{cursor.ReadByte():X2}"));
        if (Is(opcode, 0x3F, 0x7F, 0xBF, 0xFF)) return DecodeDrawBox(cursor, opcode);
        if (opcode is 0x0B or 0x4B or 0x8B or 0xCB)
            return ("getVerbEntrypoint", Join(ReadResult(cursor), ReadParam(cursor, 2, opcode, 0x80), ReadParam(cursor, 2, opcode, 0x40)));
        if (opcode is 0x54 or 0xD4)
            return ("setObjectName", Join(ReadParam(cursor, 2, opcode, 0x80), ReadScummString(cursor)));
        if (opcode is 0x7A or 0xFA) return DecodeVerbOps(cursor, opcode);
        if (opcode == 0x60 || opcode == 0xE0) return ("freezeScripts", ReadParam(cursor, 1, opcode, 0x80));
        if (opcode == 0x98) return DecodeSystemOps(cursor);
        if (opcode == 0xAE) return DecodeWait(cursor);
        if (opcode == 0xCC) return DecodePseudoRoom(cursor);
        if (opcode is 0x6B or 0xEB) return ("debug", ReadParam(cursor, 2, opcode, 0x80));

        throw new UnsupportedBytecodeException($"opcode 0x{opcode:X2} has no operand decoder yet");
    }

    private static (string, string) DecodeConditional(Cursor cursor, byte opcode)
    {
        string operation;
        string operands;
        if (opcode is 0x28 or 0xA8)
        {
            operation = opcode == 0x28 ? "equalZero" : "notEqualZero";
            operands = ReadVar(cursor);
        }
        else
        {
            operation = (opcode & 0x7F) switch
            {
                0x04 => "isGreaterEqual",
                0x08 => "isNotEqual",
                0x38 => "isLessEqual",
                0x44 => "isLess",
                0x48 => "isEqual",
                0x78 => "isGreater",
                _ => throw new UnsupportedBytecodeException($"conditional opcode 0x{opcode:X2} is not defined")
            };
            operands = Join(ReadVar(cursor), ReadParam(cursor, 2, opcode, 0x80));
        }
        var displacement = cursor.ReadInt16();
        return (operation, Join(operands, $"branch +0x{displacement:X4} -> 0x{cursor.Position + displacement:X4}"));
    }

    private static (string, string) DecodeStateConditional(Cursor cursor, byte opcode)
    {
        var objectNumber = ReadParam(cursor, 2, opcode, 0x80);
        var state = ReadParam(cursor, 1, opcode, 0x40);
        var displacement = cursor.ReadInt16();
        var operation = (opcode & 0x2F) == 0x2F ? "ifNotState" : "ifState";
        return (operation, Join(objectNumber, state,
            $"branch +0x{displacement:X4} -> 0x{cursor.Position + displacement:X4}"));
    }

    private static (string, string) DecodeOldRoomEffect(Cursor cursor)
    {
        var sub = cursor.ReadByte();
        var effect = (sub & 0x1F) == 3
            ? Join("switchEffect", ReadParam(cursor, 2, sub, 0x80))
            : $"effect subopcode 0x{sub:X2}";
        return ("oldRoomEffect", effect);
    }

    private static (string, string) DecodeDrawObject(Cursor cursor, byte opcode)
    {
        var objectNumber = ReadParam(cursor, 2, opcode, 0x80);
        // DOS LOOM uses the small room header: v3 inherits v4's drawObject
        // opcode, whose X/Y operands are words carried by the original opcode.
        var x = ReadParam(cursor, 2, opcode, 0x40);
        var y = ReadParam(cursor, 2, opcode, 0x20);
        return ("drawObject", Join(objectNumber, "at", x, y));
    }

    private static (string, string) DecodeVariableOperation(Cursor cursor, byte opcode, string name, string symbol) =>
        (name, $"{ReadVar(cursor)} {symbol} {ReadParam(cursor, 2, opcode, 0x80)}");

    private static (string, string) DecodeVariableRange(Cursor cursor, byte opcode)
    {
        var start = ReadResult(cursor);
        var count = cursor.ReadByte();
        var valueCount = count == 0 ? 256 : count;
        var values = new string[valueCount];
        for (var i = 0; i < valueCount; i++) values[i] = (opcode & 0x80) != 0 ? $"0x{cursor.ReadUInt16():X4}" : $"0x{cursor.ReadByte():X2}";
        return ("setVarRange", Join(start, $"count={count}", $"[{string.Join(", ", values)}]"));
    }

    private static (string, string) DecodeSaveRestoreVerbs(Cursor cursor)
    {
        var sub = cursor.ReadByte();
        var args = Join(ReadParam(cursor, 1, sub, 0x80), ReadParam(cursor, 1, sub, 0x40), ReadParam(cursor, 1, sub, 0x20));
        var operation = (sub & 0x1F) switch
        {
            1 => "saveVerbs",
            2 => "restoreVerbs",
            3 => "deleteVerbs",
            _ => throw new UnsupportedBytecodeException($"saveRestoreVerbs subopcode 0x{sub:X2} is not defined")
        };
        return (operation, args);
    }

    private static (string, string) DecodeCursorCommand(Cursor cursor)
    {
        var sub = cursor.ReadByte();
        var kind = sub & 0x1F;
        var command = kind switch
        {
            0x01 => "cursorOn",
            0x02 => "cursorOff",
            0x03 => "userInputOn",
            0x04 => "userInputOff",
            0x05 => "cursorSoftOn",
            0x06 => "cursorSoftOff",
            0x07 => "userInputSoftOn",
            0x08 => "userInputSoftOff",
            0x0A => Join("cursorImage", ReadParam(cursor, 1, sub, 0x80), ReadParam(cursor, 1, sub, 0x40)),
            0x0B => Join("cursorHotspot", ReadParam(cursor, 1, sub, 0x80), ReadParam(cursor, 1, sub, 0x40), ReadParam(cursor, 1, sub, 0x20)),
            0x0C => Join("cursorSet", ReadParam(cursor, 1, sub, 0x80)),
            0x0D => Join("charsetSet", ReadParam(cursor, 1, sub, 0x80)),
            0x0E => Join("v3CharsetInit", ReadParam(cursor, 1, sub, 0x80), ReadParam(cursor, 1, sub, 0x40)),
            _ => throw new UnsupportedBytecodeException($"cursorCommand subopcode 0x{sub:X2} is not defined for DOS LOOM old-v3")
        };
        return ("cursorCommand", command);
    }

    private static (string, string) DecodeStringOps(Cursor cursor)
    {
        var sub = cursor.ReadByte();
        var kind = sub & 0x1F;
        var operation = kind switch
        {
            0x01 => Join("loadString", ReadParam(cursor, 1, sub, 0x80), ReadScummString(cursor)),
            0x02 => Join("copyString", ReadParam(cursor, 1, sub, 0x80), ReadParam(cursor, 1, sub, 0x40)),
            0x03 => Join("setStringChar", ReadParam(cursor, 1, sub, 0x80), ReadParam(cursor, 1, sub, 0x40), ReadParam(cursor, 1, sub, 0x20)),
            0x04 => Join("getStringChar", ReadResult(cursor), ReadParam(cursor, 1, sub, 0x80), ReadParam(cursor, 1, sub, 0x40)),
            0x05 => Join("createString", ReadParam(cursor, 1, sub, 0x80), ReadParam(cursor, 1, sub, 0x40)),
            _ => throw new UnsupportedBytecodeException($"stringOps subopcode 0x{sub:X2} is not defined")
        };
        return ("stringOps", operation);
    }

    private static (string, string) DecodeResourceRoutines(Cursor cursor)
    {
        var sub = cursor.ReadByte();
        var kind = sub & 0x3F;
        var resourceId = sub == 17 ? "" : ReadParam(cursor, 1, sub, 0x80);
        if (kind == 17) return ("resourceRoutines", Join("clearHeap", resourceId));

        var operation = kind switch
        {
            0x01 => Join("loadScript", resourceId),
            0x02 => Join("loadSound", resourceId),
            0x03 => Join("loadCostume", resourceId),
            0x04 => Join("loadRoom", resourceId),
            0x05 => Join("nukeScript", resourceId),
            0x06 => Join("nukeSound", resourceId),
            0x07 => Join("nukeCostume", resourceId),
            0x08 => Join("nukeRoom", resourceId),
            0x09 => Join("lockScript", resourceId),
            0x0A => Join("lockSound", resourceId),
            0x0B => Join("lockCostume", resourceId),
            0x0C => Join("lockRoom", resourceId),
            0x0D => Join("unlockScript", resourceId),
            0x0E => Join("unlockSound", resourceId),
            0x0F => Join("unlockCostume", resourceId),
            0x10 => Join("unlockRoom", resourceId),
            0x12 => Join("loadCharset", resourceId),
            0x13 => Join("nukeCharset", resourceId),
            0x14 => Join("loadObject", resourceId, ReadParam(cursor, 2, sub, 0x40)),
            _ => throw new UnsupportedBytecodeException($"resourceRoutines subopcode 0x{sub:X2} is not defined for DOS LOOM old-v3")
        };
        return ("resourceRoutines", operation);
    }

    private static (string, string) DecodeSystemOps(Cursor cursor)
    {
        var sub = cursor.ReadByte();
        var operation = sub switch
        {
            1 => "restart",
            2 => "pause",
            3 => "quit",
            _ => throw new UnsupportedBytecodeException($"systemOps subopcode 0x{sub:X2} is not defined")
        };
        return ("systemOps", operation);
    }

    private static (string, string) DecodeWait(Cursor cursor)
    {
        // Indy3 (non-Macintosh) has no wait subopcode: it always waits for the message.
        if (cursor.Dialect == ScummScriptDialect.Indy3) return ("waitFor", "message");
        var sub = cursor.ReadByte();
        var waitFor = (sub & 0x1F) switch
        {
            0x01 => Join("actor", ReadParam(cursor, 1, sub, 0x80)),
            0x02 => "message",
            0x03 => "camera",
            0x04 => "sentence",
            _ => throw new UnsupportedBytecodeException($"wait subopcode 0x{sub:X2} is not defined for DOS LOOM old-v3")
        };
        return ("waitFor", waitFor);
    }

    private static (string, string) DecodeExpression(Cursor cursor, int nesting)
    {
        var destination = ReadResult(cursor);
        var terms = new List<string>();
        for (var count = 0; count < 4096; count++)
        {
            var sub = cursor.ReadByte();
            if (sub == 0xFF)
                return ("expression", Join(destination, $"[{string.Join(" ", terms)}]"));

            var term = (sub & 0x1F) switch
            {
                0x01 => ReadParam(cursor, 2, sub, 0x80),
                0x02 => "+",
                0x03 => "-",
                0x04 => "*",
                0x05 => "/",
                0x06 => DecodeExpressionOpcode(cursor, nesting + 1),
                _ => throw new UnsupportedBytecodeException($"expression subopcode 0x{sub:X2} is not defined")
            };
            terms.Add(term);
        }
        throw new UnsupportedBytecodeException("expression did not reach its 0xFF terminator within 4096 terms");
    }

    private static string DecodeExpressionOpcode(Cursor cursor, int nesting)
    {
        var opcode = cursor.ReadByte();
        var (operation, operands) = DecodeInstruction(cursor, opcode, nesting);
        return string.IsNullOrEmpty(operands) ? operation : $"{operation}({operands})";
    }

    private static (string, string) DecodeVerbOps(Cursor cursor, byte opcode)
    {
        var verb = ReadParam(cursor, 1, opcode, 0x80);
        var operations = new List<string>();
        for (var count = 0; count < 256; count++)
        {
            var sub = cursor.ReadByte();
            if (sub == 0xFF) return ("verbOps", Join(verb, $"[{string.Join("; ", operations)}]"));
            var item = (sub & 0x1F) switch
            {
                0x01 => Join("image", ReadParam(cursor, 2, sub, 0x80)),
                0x02 => Join("name", ReadScummString(cursor)),
                0x03 => Join("color", ReadParam(cursor, 1, sub, 0x80)),
                0x04 => Join("highlightColor", ReadParam(cursor, 1, sub, 0x80)),
                0x05 => Join("at", ReadParam(cursor, 2, sub, 0x80), ReadParam(cursor, 2, sub, 0x40)),
                0x06 => "on",
                0x07 => "off",
                0x08 => "delete",
                0x09 => "new",
                0x10 => Join("dimColor", ReadParam(cursor, 1, sub, 0x80)),
                0x11 => "dim",
                0x12 => Join("key", ReadParam(cursor, 1, sub, 0x80)),
                0x13 => "center",
                0x14 => Join("nameString", ReadParam(cursor, 2, sub, 0x80)),
                0x16 => Join("assignObject", ReadParam(cursor, 2, sub, 0x80), ReadParam(cursor, 1, sub, 0x40)),
                0x17 => Join("backColor", ReadParam(cursor, 1, sub, 0x80)),
                _ => throw new UnsupportedBytecodeException($"verbOps subopcode 0x{sub:X2} is not defined")
            };
            operations.Add(item);
        }
        throw new UnsupportedBytecodeException("verbOps did not reach its 0xFF terminator within 256 subcommands");
    }

    private static (string, string) DecodeDrawBox(Cursor cursor, byte opcode)
    {
        var x1 = ReadParam(cursor, 2, opcode, 0x80);
        var y1 = ReadParam(cursor, 2, opcode, 0x40);
        var parameters = cursor.ReadByte();
        var x2 = ReadParam(cursor, 2, parameters, 0x80);
        var y2 = ReadParam(cursor, 2, parameters, 0x40);
        var color = ReadParam(cursor, 1, parameters, 0x20);
        return ("drawBox", Join(x1, y1, x2, y2, color));
    }

    private static (string, string) DecodeClassConditional(Cursor cursor, byte opcode)
    {
        var objectNumber = ReadParam(cursor, 2, opcode, 0x80);
        var classes = ReadVarargList(cursor, 2);
        var displacement = cursor.ReadInt16();
        return ("ifClassOfIs", Join(objectNumber, classes, $"branch +0x{displacement:X4} -> 0x{cursor.Position + displacement:X4}"));
    }

    private static (string, string) DecodeActorInBox(Cursor cursor, byte opcode)
    {
        var actor = ReadParam(cursor, 1, opcode, 0x80);
        var box = ReadParam(cursor, 1, opcode, 0x40);
        var displacement = cursor.ReadInt16();
        return ("isActorInBox", Join(actor, box, $"branch +0x{displacement:X4} -> 0x{cursor.Position + displacement:X4}"));
    }

    private static (string, string) DecodeActorOps(Cursor cursor, byte opcode)
    {
        var actor = ReadParam(cursor, 1, opcode, 0x80);
        var operations = new List<string>();
        for (var count = 0; count < 128; count++)
        {
            var sub = cursor.ReadByte();
            if (sub == 0xFF) return ("actorOps", Join(actor, $"[{string.Join("; ", operations)}]"));
            var compactKind = sub & 0x1F;
            if (compactKind is < 1 or > 20)
                throw new UnsupportedBytecodeException($"small-header actorOps subopcode 0x{sub:X2} is not defined");
            var kind = SmallHeaderActorOperations[compactKind - 1];
            var item = kind switch
            {
                0x00 => Join("unknown", ReadParam(cursor, 1, sub, 0x80)),
                0x01 => Join("costume", ReadParam(cursor, 1, sub, 0x80)),
                0x02 => Join("walkSpeed", ReadParam(cursor, 1, sub, 0x80), ReadParam(cursor, 1, sub, 0x40)),
                0x03 => Join("sound", ReadParam(cursor, 1, sub, 0x80)),
                0x04 => Join("walkAnimation", ReadParam(cursor, 1, sub, 0x80)),
                0x05 => Join("talkAnimation", ReadParam(cursor, 1, sub, 0x80), ReadParam(cursor, 1, sub, 0x40)),
                0x06 => Join("standAnimation", ReadParam(cursor, 1, sub, 0x80)),
                0x07 => Join("animation", ReadParam(cursor, 1, sub, 0x80), ReadParam(cursor, 1, sub, 0x40), ReadParam(cursor, 1, sub, 0x20)),
                0x08 => "init",
                0x09 => Join("elevation", ReadParam(cursor, 2, sub, 0x80)),
                0x0A => "defaultAnimations",
                0x0B => Join("palette", ReadParam(cursor, 1, sub, 0x80), ReadParam(cursor, 1, sub, 0x40)),
                0x0C => Join("talkColor", ReadParam(cursor, 1, sub, 0x80)),
                // o5_actorOps loads an inline, zero-terminated resource string
                // for SO_ACTOR_NAME (loadPtrToResource), even when it is empty.
                0x0D => Join("actorName", ReadScummString(cursor)),
                0x0E => Join("initAnimation", ReadParam(cursor, 1, sub, 0x80)),
                0x10 => Join("width", ReadParam(cursor, 1, sub, 0x80)),
                0x11 => Join("scale", ReadParam(cursor, 1, sub, 0x80), ReadParam(cursor, 1, sub, 0x40)),
                0x12 => "neverZClip",
                0x13 => Join("alwaysZClip", ReadParam(cursor, 1, sub, 0x80)),
                0x14 => "ignoreBoxes",
                0x15 => "followBoxes",
                0x16 => Join("animationSpeed", ReadParam(cursor, 1, sub, 0x80)),
                0x17 => Join("shadow", ReadParam(cursor, 1, sub, 0x80)),
                _ => throw new UnsupportedBytecodeException($"actorOps subopcode 0x{sub:X2} is not defined")
            };
            operations.Add(item);
        }
        throw new UnsupportedBytecodeException("actorOps did not reach its 0xFF terminator within 128 subcommands");
    }

    private static (string, string) DecodeRoomOps(Cursor cursor, byte opcode)
    {
        var first = ReadParam(cursor, 2, opcode, 0x80);
        var second = ReadParam(cursor, 2, opcode, 0x40);
        var sub = cursor.ReadByte();
        var kind = sub & 0x1F;
        var prefix = Join(first, second);
        var args = kind switch
        {
            0x01 => Join("roomScroll", prefix),
            0x02 => Join("roomColorLegacy", prefix),
            0x03 => Join("setScreen", prefix),
            0x04 => Join("roomPalette", prefix),
            0x05 => Join("shakeOn", prefix),
            0x06 => Join("shakeOff", prefix),
            0x07 => Join("setRoomScale", prefix, DecodeRoomScale(cursor, sub)),
            0x08 => Join("roomIntensity", prefix, ReadParam(cursor, 1, sub, 0x80), ReadParam(cursor, 1, sub, 0x40), ReadParam(cursor, 1, sub, 0x20)),
            0x09 => Join("saveLoad", prefix, ReadParam(cursor, 1, sub, 0x80), ReadParam(cursor, 1, sub, 0x40)),
            0x0A => Join("screenEffect", prefix, ReadParam(cursor, 2, sub, 0x80)),
            0x0B => DecodeRoomRgbIntensity(cursor, sub, prefix),
            0x0C => throw new UnsupportedBytecodeException("room shadow parameters are not decoded yet"),
            _ => throw new UnsupportedBytecodeException($"roomOps subopcode 0x{sub:X2} is not defined")
        };
        return ("roomOps", args);
    }

    private static string DecodeRoomScale(Cursor cursor, byte sub)
    {
        var x = ReadParam(cursor, 1, sub, 0x80);
        var y = ReadParam(cursor, 1, sub, 0x40);
        var next = cursor.ReadByte();
        var scaleA = ReadParam(cursor, 1, next, 0x80);
        var scaleB = ReadParam(cursor, 1, next, 0x40);
        var third = cursor.ReadByte();
        var scaleC = ReadParam(cursor, 1, third, 0x40);
        return Join(x, y, scaleA, scaleB, scaleC);
    }

    private static string DecodeRoomRgbIntensity(Cursor cursor, byte sub, string prefix)
    {
        var blue = ReadParam(cursor, 2, sub, 0x20);
        var next = cursor.ReadByte();
        return Join("setRgbRoomIntensity", prefix, blue, ReadParam(cursor, 1, next, 0x80), ReadParam(cursor, 1, next, 0x40));
    }

    private static (string, string) DecodePrint(Cursor cursor, byte opcode)
    {
        var actor = opcode == 0xD8 ? "ego" : ReadParam(cursor, 1, opcode, 0x80);
        var operations = new List<string>();
        for (var count = 0; count < 128; count++)
        {
            var sub = cursor.ReadByte();
            if (sub == 0xFF) return (opcode == 0xD8 ? "printEgo" : "print", Join(actor, $"[{string.Join("; ", operations)}]"));
            var item = (sub & 0x0F) switch
            {
                0x00 => Join("position", ReadParam(cursor, 2, sub, 0x80), ReadParam(cursor, 2, sub, 0x40)),
                0x01 => Join("color", ReadParam(cursor, 1, sub, 0x80)),
                0x02 => Join("clipped", ReadParam(cursor, 2, sub, 0x80)),
                0x03 => Join("erase", ReadParam(cursor, 2, sub, 0x80), ReadParam(cursor, 2, sub, 0x40)),
                0x04 => "center",
                // v3 reads the line height here (ScummVM decodeParseString, SO_LEFT for later versions).
                0x06 => Join("height", ReadParam(cursor, 2, sub, 0x80)),
                0x07 => "overhead",
                0x08 => Join("sayVoice", ReadParam(cursor, 2, sub, 0x80), ReadParam(cursor, 2, sub, 0x40)),
                0x0F => Join("text", ReadScummString(cursor)),
                _ => throw new UnsupportedBytecodeException($"print subopcode 0x{sub:X2} is not defined")
            };
            operations.Add(item);
            if ((sub & 0x0F) == 0x0F)
                return (opcode == 0xD8 ? "printEgo" : "print", Join(actor, $"[{string.Join("; ", operations)}]"));
        }
        throw new UnsupportedBytecodeException("print did not reach its 0xFF terminator within 128 subcommands");
    }

    private static (string, string) DecodePseudoRoom(Cursor cursor)
    {
        var room = cursor.ReadByte();
        var mapped = new List<string>();
        for (var i = 0; i < 128; i++)
        {
            var value = cursor.ReadByte();
            if (value == 0) return ("pseudoRoom", Join(room.ToString(CultureInfo.InvariantCulture), $"[{string.Join(", ", mapped)}]"));
            mapped.Add((value & 0x80) != 0 ? (value & 0x7F).ToString(CultureInfo.InvariantCulture) : "inventory");
        }
        throw new UnsupportedBytecodeException("pseudoRoom did not reach its zero terminator within 128 entries");
    }

    private static string ReadVarargList(Cursor cursor, int width = 2)
    {
        var values = new List<string>();
        for (var i = 0; i < 64; i++)
        {
            var selector = cursor.ReadByte();
            if (selector == 0xFF) return $"[{string.Join(", ", values)}]";
            values.Add(ReadParam(cursor, width, selector, 0x80));
        }
        throw new UnsupportedBytecodeException("variable argument list did not reach its 0xFF terminator within 64 items");
    }

    private static string ReadResult(Cursor cursor) => ReadVar(cursor);

    private static string ReadParam(Cursor cursor, int width, byte opcode, byte variableMask)
    {
        if ((opcode & variableMask) != 0) return ReadVar(cursor);
        return width switch
        {
            1 => $"0x{cursor.ReadByte():X2}",
            2 => $"0x{cursor.ReadUInt16():X4}",
            _ => throw new ArgumentOutOfRangeException(nameof(width))
        };
    }

    private static string ReadVar(Cursor cursor)
    {
        var variable = cursor.ReadUInt16();
        var indirect = (variable & 0x2000) != 0;
        var kind = (variable & 0x8000) != 0 ? "bit" : (variable & 0x4000) != 0 ? "local" : "var";
        var index = variable & 0x1FFF;
        if (!indirect) return $"{kind}[0x{index:X3}]";
        var offset = cursor.ReadUInt16();
        var offsetIsVariable = (offset & 0x2000) != 0;
        var offsetText = offsetIsVariable ? $"{FormatVariable((ushort)(offset & 0xDFFF))}" : $"0x{offset & 0x0FFF:X3}";
        return $"{kind}[0x{index:X3}+{offsetText}]";
    }

    private static string FormatVariable(ushort value)
    {
        var kind = (value & 0x8000) != 0 ? "bit" : (value & 0x4000) != 0 ? "local" : "var";
        return $"{kind}[0x{value & 0x1FFF:X3}]";
    }

    private static string ReadScummString(Cursor cursor)
    {
        var bytes = new List<byte>();
        for (var i = 0; i < 4096; i++)
        {
            var value = cursor.ReadByte();
            if (value == 0) return $"\"{EscapeText(bytes)}\"";
            bytes.Add(value);
            if (value == 0xFF)
            {
                var control = cursor.ReadByte();
                bytes.Add(control);
                if (control is not (1 or 2 or 3 or 8))
                {
                    bytes.Add(cursor.ReadByte());
                    bytes.Add(cursor.ReadByte());
                }
            }
        }
        throw new UnsupportedBytecodeException("SCUMM string did not reach its zero terminator within 4096 bytes");
    }

    private static string EscapeText(IEnumerable<byte> bytes)
    {
        var output = new StringBuilder();
        foreach (var value in bytes)
        {
            if (value is >= 0x20 and <= 0x7E && value is not (byte)'\\' and not (byte)'"') output.Append((char)value);
            else if (value == (byte)'\\' || value == (byte)'"') output.Append('\\').Append((char)value);
            else output.Append($"\\x{value:X2}");
        }
        return output.ToString();
    }

    private static bool Is(byte opcode, params byte[] values) => Array.IndexOf(values, opcode) >= 0;
    private static string Join(params string[] values) => string.Join(", ", values.Where(value => !string.IsNullOrEmpty(value)));
    private static string FormatBytes(IEnumerable<byte> values) => string.Join(" ", values.Select(value => value.ToString("X2", CultureInfo.InvariantCulture)));

    private sealed class Cursor(byte[] data, int start = 0, ScummScriptDialect dialect = ScummScriptDialect.Loom)
    {
        public ScummScriptDialect Dialect { get; } = dialect;

        public int Position { get; private set; } = start;

        public byte PeekByte()
        {
            if (Position >= data.Length) throw new EndOfStreamException();
            return data[Position];
        }

        public byte ReadByte()
        {
            var value = PeekByte();
            Position++;
            return value;
        }

        public ushort ReadUInt16()
        {
            if (Position > data.Length - 2) throw new EndOfStreamException();
            var value = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(Position, 2));
            Position += 2;
            return value;
        }

        public short ReadInt16() => unchecked((short)ReadUInt16());
    }

    private sealed class UnsupportedBytecodeException(string message) : Exception(message);
}
