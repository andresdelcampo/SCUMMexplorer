using System.Globalization;
using System.Text.RegularExpressions;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Rendering;

namespace SCUMMexplorer.Core.Formats;

/// <summary>
/// Collects the <c>drawObject</c> runs stored in a game's scripts, grouped by the
/// room that owns the drawn objects. A run ends at a frame break, a jump or
/// branch, a stop or a room load, so its images appear together on screen.
/// The draws directly after a script's first literal <c>loadRoom</c> of a room
/// are that room's opening frame, the strongest static evidence of what a first
/// visit shows. A later load of the same room in the same script is a return to
/// it (LOOM script 68 shows Room 56 as 779 and 782, and only later as 781). These
/// batches are static candidates used to close gaps; they do not prove which
/// branch runs.
/// </summary>
public sealed class LoomSceneDrawBatches
{
    public const int OpeningFramePriority = 3;
    public const int EntryScriptPriority = 2;
    public const int OtherScriptPriority = 1;

    private static readonly Regex Literal = new(@"^0x(?<value>[0-9A-Fa-f]+)$", RegexOptions.CultureInvariant);
    private readonly IReadOnlyDictionary<int, IReadOnlyList<RoomDrawBatch>> _byRoom;

    private LoomSceneDrawBatches(IReadOnlyDictionary<int, IReadOnlyList<RoomDrawBatch>> byRoom) => _byRoom = byRoom;

    public static LoomSceneDrawBatches Empty { get; } = new(new Dictionary<int, IReadOnlyList<RoomDrawBatch>>());

    public IReadOnlyList<RoomDrawBatch> ForRoom(int roomId) =>
        _byRoom.TryGetValue(roomId, out var batches) ? batches : [];

    public static LoomSceneDrawBatches Build(GameLibrary game)
    {
        ArgumentNullException.ThrowIfNull(game);
        if (game.Version != ScummVersion.V3) return Empty;
        var dialect = ScummScriptDialects.For(game.Id);
        var reader = new LoomScriptReader(dialect);
        var verbReader = new LoomObjectVerbReader(dialect);
        var directory = new LoomRoomObjectDirectory();

        var roomByObject = new Dictionary<int, int>();
        var roomRecords = new Dictionary<int, (GameResource Room, IReadOnlyList<LoomRoomObjectRecord> Records, byte[] Bytes)>();
        foreach (var room in game.Rooms.Where(item => item.ResourceId.HasValue))
        {
            try
            {
                var bytes = File.ReadAllBytes(room.FilePath);
                var records = directory.Decode(bytes);
                roomRecords[room.ResourceId!.Value] = (room, records, bytes);
                foreach (var record in records) roomByObject.TryAdd(record.Id, room.ResourceId.Value);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException) { }
        }

        var byRoom = new Dictionary<int, List<RoomDrawBatch>>();
        void Scan(LoomScriptDisassembly? script, int priority, string source)
        {
            if (script is null) return;
            foreach (var (roomId, batch) in ScanScript(script, priority, source, roomByObject))
            {
                if (!byRoom.TryGetValue(roomId, out var list)) byRoom[roomId] = list = [];
                list.Add(batch);
            }
        }

        foreach (var resource in game.Resources.Where(item => item.Kind == ResourceKind.Script && item.ResourceId.HasValue))
        {
            try { Scan(reader.Decode(resource), OtherScriptPriority, $"script {resource.ResourceId:00}"); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { }
        }

        var globalCount = ReadGlobalScriptCount(game);
        foreach (var (roomId, (room, records, bytes)) in roomRecords)
        {
            try { Scan(reader.DecodeRoomEntry(room), EntryScriptPriority, $"Room {roomId:00} entry script"); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { }
            foreach (var localId in ReadLocalScriptIds(bytes, globalCount))
            {
                try { Scan(reader.DecodeRoomLocalScript(room, localId, globalCount), OtherScriptPriority,
                    $"Room {roomId:00} local script {localId}"); }
                catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { }
            }
            IReadOnlyDictionary<int, IReadOnlyList<LoomObjectVerbEntry>> tables;
            try { tables = verbReader.ReadAll(bytes, records); }
            catch (Exception ex) when (ex is InvalidDataException or ArgumentException) { continue; }
            foreach (var record in records)
            {
                if (!tables.TryGetValue(record.Id, out var verbs)) continue;
                foreach (var verb in verbs.DistinctBy(item => item.ScriptOffset))
                {
                    try { Scan(verbReader.Decode(room, record, verb.VerbId), OtherScriptPriority,
                        $"Room {roomId:00} object {record.Id} {verb.Label}"); }
                    catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { }
                }
            }
        }

        return new LoomSceneDrawBatches(byRoom.ToDictionary(pair => pair.Key,
            pair => (IReadOnlyList<RoomDrawBatch>)pair.Value));
    }

    internal static IEnumerable<(int RoomId, RoomDrawBatch Batch)> ScanScript(LoomScriptDisassembly script,
        int priority, string source, IReadOnlyDictionary<int, int> roomByObject)
    {
        var results = new List<(int, RoomDrawBatch)>();
        var steps = new List<RoomDrawStep>();
        int? loadedRoom = null;
        var openedRooms = new HashSet<int>();
        var batchStart = 0;

        void Flush()
        {
            if (steps.Count > 0)
            {
                foreach (var group in steps.Where(step => roomByObject.ContainsKey(step.ObjectId))
                             .GroupBy(step => roomByObject[step.ObjectId]))
                {
                    var opening = loadedRoom == group.Key;
                    results.Add((group.Key, new RoomDrawBatch(group.ToArray(),
                        opening ? OpeningFramePriority : priority,
                        opening ? $"opening frame of Room {group.Key:00} in {source} +0x{batchStart:X4}"
                                : $"{source} +0x{batchStart:X4}")));
                }
            }
            steps.Clear();
        }

        foreach (var instruction in script.Instructions)
        {
            switch (instruction.Operation)
            {
                case "drawObject":
                    if (TryReadDraw(instruction.Operands, out var step))
                    {
                        if (steps.Count == 0) batchStart = instruction.Offset;
                        steps.Add(step);
                    }
                    break;
                case "loadRoom":
                case "loadRoomWithEgo":
                    Flush();
                    var parts = instruction.Operands.Split(',', StringSplitOptions.TrimEntries);
                    var roomOperand = instruction.Operation == "loadRoom" ? parts[0] : parts.ElementAtOrDefault(1) ?? "";
                    loadedRoom = TryReadLiteral(roomOperand, out var room) && openedRooms.Add(room) ? room : null;
                    break;
                case "breakHere":
                case "stopObjectCode":
                case "jumpRelative":
                case "isGreaterEqual" or "isNotEqual" or "isLessEqual" or "isLess" or "isEqual" or "isGreater" or
                    "equalZero" or "notEqualZero" or "ifClassOfIs" or "isActorInBox" or "ifState" or "ifNotState":
                    Flush();
                    loadedRoom = null;
                    break;
                case "setState":
                    // A redraw separates what was drawn before from what follows.
                    Flush();
                    break;
            }
        }
        Flush();
        return results;
    }

    private static bool TryReadDraw(string operands, out RoomDrawStep step)
    {
        step = default;
        var parts = operands.Split(',', StringSplitOptions.TrimEntries);
        if (parts.Length == 0 || !TryReadLiteral(parts[0], out var id)) return false;
        int? x = null, y = null;
        if (parts.Length >= 4 && parts[1] == "at" && TryReadLiteral(parts[2], out var tileX) &&
            TryReadLiteral(parts[3], out var tileY) && tileX != 0xFF)
        {
            x = tileX * 8;
            y = tileY * 8;
        }
        else if (parts.Length >= 4 && parts[1] == "at" && !(TryReadLiteral(parts[2], out _) && TryReadLiteral(parts[3], out _)))
            return false;
        step = new RoomDrawStep(id, x, y);
        return true;
    }

    private static bool TryReadLiteral(string text, out int value)
    {
        value = 0;
        var match = Literal.Match(text.Trim());
        return match.Success && int.TryParse(match.Groups["value"].Value, NumberStyles.HexNumber,
            CultureInfo.InvariantCulture, out value);
    }

    private static int ReadGlobalScriptCount(GameLibrary game)
    {
        var indexPath = Path.Combine(game.FolderPath, "00.LFL");
        try
        {
            if (File.Exists(indexPath)) return new LflIndexReader().ReadDeclaredScriptCount(indexPath);
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException) { }
        return game.Resources.Where(item => item.Kind == ResourceKind.Script && item.ResourceId.HasValue)
                   .Select(item => item.ResourceId!.Value).DefaultIfEmpty(-1).Max() + 1;
    }

    internal static IReadOnlyList<int> ReadLocalScriptIds(byte[] encoded, int globalScriptCount)
    {
        var data = LflEncoding.DecodeXorFF(encoded);
        if (data.Length < 29) return [];
        var roomEnd = data[0] | data[1] << 8;
        var cursor = 29 + data[20] * 4 + data[23] + data[24];
        if (roomEnd > data.Length || cursor >= roomEnd) return [];
        var found = new List<int>();
        while (cursor < roomEnd - 2 && data[cursor] != 0)
        {
            if (data[cursor] >= globalScriptCount) found.Add(data[cursor]);
            cursor += 3;
        }
        return found.Distinct().ToArray();
    }
}
