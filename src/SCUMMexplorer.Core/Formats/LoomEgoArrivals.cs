using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Rendering;

namespace SCUMMexplorer.Core.Formats;

/// <summary>
/// One decoded <c>loadRoomWithEgo</c> with literal operands: the ego enters
/// <see cref="TargetRoom"/> at <see cref="ObjectId"/>'s walk point, then walks to
/// (<see cref="WalkX"/>, <see cref="WalkY"/>) unless x is −1.
/// <see cref="SourceRoom"/> is the room whose own code holds it (entry, exit,
/// local script or object verb); null for a global script.
/// </summary>
public sealed record LoomEgoArrival(int TargetRoom, int ObjectId, int WalkX, int WalkY,
    string Source, int Offset, int? SourceRoom)
{
    public string Describe() =>
        $"{Source} +0x{Offset:X4} (object {ObjectId}, " + (WalkX == -1 ? "no walk" : $"walk to ({WalkX}, {WalkY})") + ")";
}

/// <summary>
/// Every decoded <c>loadRoomWithEgo</c> with a literal object and room, across
/// global scripts, room entry, exit and local scripts, and object verbs. These
/// are possible arrivals: the conditions that lead to each one are not evaluated.
/// Instructions whose object is not in the target room's directory are left out.
/// </summary>
public sealed class LoomEgoArrivalIndex
{
    private static readonly ConcurrentDictionary<string, LoomEgoArrivalIndex> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Regex LiteralOperands = new(
        @"^0x(?<object>[0-9A-Fa-f]+), 0x(?<room>[0-9A-Fa-f]+), walkTo=\((?<x>-?\d+), (?<y>-?\d+)\)$",
        RegexOptions.CultureInvariant);
    private readonly Dictionary<int, List<LoomEgoArrival>> _byRoom = [];

    private LoomEgoArrivalIndex() { }

    /// <summary>The literal arrivals into <paramref name="room"/>, in source order.</summary>
    public IReadOnlyList<LoomEgoArrival> Into(int room) =>
        _byRoom.TryGetValue(room, out var list) ? list : [];

    public static LoomEgoArrivalIndex For(GameLibrary game, int globalScriptCount)
    {
        ArgumentNullException.ThrowIfNull(game);
        return Cache.GetOrAdd($"{game.FolderPath}|{game.Id}", _ => Build(game, globalScriptCount));
    }

    private static LoomEgoArrivalIndex Build(GameLibrary game, int globalScriptCount)
    {
        var index = new LoomEgoArrivalIndex();
        var dialect = ScummScriptDialects.For(game.Id);
        var reader = new LoomScriptReader(dialect);
        var verbs = new LoomObjectVerbReader(dialect);
        var directory = new LoomRoomObjectDirectory();
        var objectsByRoom = new Dictionary<int, HashSet<int>>();
        var found = new List<LoomEgoArrival>();

        void Scan(string source, int? sourceRoom, Func<LoomScriptDisassembly?> decode)
        {
            LoomScriptDisassembly? script;
            try { script = decode(); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { return; }
            if (script is null) return;
            foreach (var instruction in script.Instructions)
            {
                if (instruction.Operation != "loadRoomWithEgo") continue;
                var match = LiteralOperands.Match(instruction.Operands);
                if (!match.Success) continue;
                found.Add(new LoomEgoArrival(
                    int.Parse(match.Groups["room"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    int.Parse(match.Groups["object"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    int.Parse(match.Groups["x"].Value, CultureInfo.InvariantCulture),
                    int.Parse(match.Groups["y"].Value, CultureInfo.InvariantCulture),
                    source, instruction.Offset, sourceRoom));
            }
        }

        foreach (var script in game.Resources.Where(item => item.Kind == ResourceKind.Script && item.ResourceId.HasValue)
                     .OrderBy(item => item.ResourceId))
            Scan($"Script {script.ResourceId:000}", null, () => reader.Decode(script));
        foreach (var room in game.Rooms.Where(item => item.ResourceId.HasValue).OrderBy(item => item.ResourceId))
        {
            var id = room.ResourceId!.Value;
            byte[] bytes;
            IReadOnlyList<LoomRoomObjectRecord> records;
            try
            {
                bytes = File.ReadAllBytes(room.FilePath);
                records = directory.Decode(bytes);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { continue; }
            objectsByRoom[id] = records.Select(item => item.Id).ToHashSet();
            Scan($"Room {id:00} entry", id, () => reader.DecodeRoomEntry(room));
            Scan($"Room {id:00} exit", id, () => reader.DecodeRoomExit(room));
            foreach (var local in LoomScriptReader.ReadRoomLocalScriptIds(bytes, globalScriptCount))
                Scan($"Room {id:00} local script {local}", id, () => reader.DecodeRoomLocalScript(room, local, globalScriptCount));
            IReadOnlyDictionary<int, IReadOnlyList<LoomObjectVerbEntry>> tables;
            try { tables = verbs.ReadAll(bytes, records); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { continue; }
            foreach (var record in records)
                if (tables.TryGetValue(record.Id, out var entries))
                    foreach (var group in entries.GroupBy(item => item.ScriptOffset))
                    {
                        var label = string.Join("/", group.Select(item => item.IsFallback ? "fallback" : $"0x{item.VerbId:X2}"));
                        Scan($"Room {id:00} Object {record.Id} Verb {label}", id, () => verbs.Decode(room, record, group.First().VerbId));
                    }
        }

        foreach (var arrival in found)
        {
            if (!objectsByRoom.TryGetValue(arrival.TargetRoom, out var objects) || !objects.Contains(arrival.ObjectId)) continue;
            if (!index._byRoom.TryGetValue(arrival.TargetRoom, out var list)) index._byRoom[arrival.TargetRoom] = list = [];
            list.Add(arrival);
        }
        return index;
    }
}
