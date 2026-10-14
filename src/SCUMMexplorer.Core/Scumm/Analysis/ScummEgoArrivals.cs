using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// One decoded <c>loadRoomWithEgo</c> with literal operands: the ego enters <see cref="TargetRoom"/> at <see cref="ObjectId"/>'s walk point
/// and then walks to (<see cref="WalkX"/>, <see cref="WalkY"/>) unless x is -1. <see cref="SourceRoom"/> is the room whose own code holds it
/// (entry, exit, local script or object verb), null for a global script.
/// </summary>
public sealed record ScummEgoArrival(int TargetRoom, int ObjectId, int WalkX, int WalkY, string Source, int Offset, int? SourceRoom)
{
    /// <summary>Arrivals with the same key place the ego the same way.</summary>
    public string PlacementKey => $"object {ObjectId}, walk {WalkX},{WalkY}";

    public string Describe() => $"{Source} +0x{Offset:X4} (object {ObjectId}, " + (WalkX == -1 ? "no walk" : $"walk to ({WalkX}, {WalkY})") + ")";
}

/// <summary>
/// Every decoded <c>loadRoomWithEgo</c> with a literal object and room across the game's global scripts, room entry, exit and local scripts, and
/// object verbs. These are possible arrivals (the conditions that lead to each are not evaluated); an arrival whose object is not in the target
/// room's directory is left out. A room visit that does not play the route to the room takes one of them as its assumed arrival (S5, 14 October 2026).
/// </summary>
public sealed class ScummEgoArrivalIndex
{
    private static readonly ConcurrentDictionary<string, ScummEgoArrivalIndex> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Regex LiteralOperands = new(
        @"^0x(?<object>[0-9A-Fa-f]+), 0x(?<room>[0-9A-Fa-f]+), walkTo=\((?<x>-?\d+), (?<y>-?\d+)\)$", RegexOptions.CultureInvariant);
    private readonly Dictionary<int, List<ScummEgoArrival>> _byRoom = [];

    private ScummEgoArrivalIndex() { }

    /// <summary>The literal arrivals into <paramref name="room"/>, in source order.</summary>
    public IReadOnlyList<ScummEgoArrival> Into(int room) => _byRoom.TryGetValue(room, out var list) ? list : [];

    public static ScummEgoArrivalIndex For(GameLibrary game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return Cache.GetOrAdd($"{game.FolderPath}|{game.Title}", _ => Build(game));
    }

    private static ScummEgoArrivalIndex Build(GameLibrary game)
    {
        var index = new ScummEgoArrivalIndex();
        var data = ScummDataSources.For(game);
        var objectsByRoom = new Dictionary<int, HashSet<int>>();
        var found = new List<ScummEgoArrival>();

        void Scan(string source, int? sourceRoom, Func<ScummScriptDisassembly?> decode)
        {
            ScummScriptDisassembly? script;
            try { script = decode(); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { return; }
            if (script is null) return;
            foreach (var instruction in script.Instructions.Where(item => item.Operation == "loadRoomWithEgo"))
            {
                var match = LiteralOperands.Match(instruction.Operands);
                if (!match.Success) continue;
                found.Add(new ScummEgoArrival(
                    int.Parse(match.Groups["room"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    int.Parse(match.Groups["object"].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    int.Parse(match.Groups["x"].Value, CultureInfo.InvariantCulture),
                    int.Parse(match.Groups["y"].Value, CultureInfo.InvariantCulture),
                    source, instruction.Offset, sourceRoom));
            }
        }

        foreach (var number in data.GlobalScriptIds())
            Scan($"Script {number:000}", null, () => data.GlobalScript(number));
        foreach (var room in game.Rooms.Where(item => item.ResourceId.HasValue).OrderBy(item => item.ResourceId))
        {
            var id = room.ResourceId!.Value;
            IReadOnlyList<ScummRoomObjectRecord> records;
            IReadOnlyList<int> localIds;
            try
            {
                records = data.ReadRoom(id).Objects;
                localIds = data.RoomLocalScriptIds(id);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { continue; }
            objectsByRoom[id] = records.Select(item => item.Id).ToHashSet();
            Scan($"Room {id:00} entry", id, () => data.RoomEntryScript(id));
            Scan($"Room {id:00} exit", id, () => data.RoomExitScript(id));
            foreach (var local in localIds)
                Scan($"Room {id:00} local script {local}", id, () => data.RoomLocalScript(id, local));
            IReadOnlyDictionary<int, IReadOnlyList<ScummObjectVerbEntry>> tables;
            try { tables = data.ReadObjectVerbs(id, records); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { continue; }
            foreach (var record in records)
                if (tables.TryGetValue(record.Id, out var entries))
                    foreach (var group in entries.GroupBy(item => item.ScriptOffset))
                    {
                        var label = string.Join("/", group.Select(item => item.IsFallback ? "fallback" : $"0x{item.VerbId:X2}"));
                        Scan($"Room {id:00} Object {record.Id} Verb {label}", id, () => data.ReadObjectVerb(id, record, group.First().VerbId, out _));
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
