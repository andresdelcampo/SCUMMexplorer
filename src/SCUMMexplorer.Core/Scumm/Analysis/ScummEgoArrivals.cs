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
    /// <summary>
    /// The pseudo room number the instruction names (0x80 and up, <c>o5_pseudoRoom</c>) when it loads <see cref="TargetRoom"/> under another number:
    /// ScummVM <c>startScene</c> puts that number in <c>VAR_ROOM</c> and loads the real room, so the room's entry code can tell its uses apart.
    /// </summary>
    public int? PseudoRoom { get; init; }

    /// <summary>The number the game's own code loads: the pseudo room number, or the room itself.</summary>
    public int LoadedNumber => PseudoRoom ?? TargetRoom;

    /// <summary>Arrivals with the same key place the ego the same way.</summary>
    public string PlacementKey => $"room {LoadedNumber}, object {ObjectId}, walk {WalkX},{WalkY}";

    public string Describe() => $"{Source} +0x{Offset:X4} (" + (PseudoRoom is int pseudo ? $"pseudo room {pseudo}, " : "") + $"object {ObjectId}, " + (WalkX == -1 ? "no walk" : $"walk to ({WalkX}, {WalkY})") + ")";
}

/// <summary>
/// A script that stands the hero in a room by hand: <c>putActorInRoom</c> then <c>putActor</c> with literal operands (Indy 3's Script 73: Room 9 at 532, 83),
/// the way into a room that no <c>loadRoomWithEgo</c> names.
/// </summary>
public sealed record ScummHeroPlacement(int Room, int X, int Y, string Source, int Offset)
{
    public string Describe() => $"{Source} +0x{Offset:X4} (at {X}, {Y})";
}

/// <summary>
/// Every decoded <c>loadRoomWithEgo</c> with a literal object and room across the game's global scripts, room entry, exit and local scripts, and
/// object verbs. These are possible arrivals (the conditions that lead to each are not evaluated); an arrival whose object is not in the target
/// room's directory is left out. A load of a pseudo room (0x80 and up) counts for the real room the game's literal <c>pseudoRoom</c> instructions map it
/// to (ScummVM <c>_resourceMapper</c>). A room visit that does not play the route to the room takes one of them as its assumed arrival (S5, 14 October 2026).
/// </summary>
public sealed class ScummEgoArrivalIndex
{
    private static readonly ConcurrentDictionary<string, ScummEgoArrivalIndex> Cache = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Regex LiteralOperands = new(
        @"^0x(?<object>[0-9A-Fa-f]+), 0x(?<room>[0-9A-Fa-f]+), walkTo=\((?<x>-?\d+), (?<y>-?\d+)\)$", RegexOptions.CultureInvariant);
    private static readonly Regex HeroInRoom = new(@"^(?:0x01|var\[0x001\]), 0x([0-9A-F]+)$", RegexOptions.CultureInvariant);
    private static readonly Regex HeroAt = new(@"^(?:0x01|var\[0x001\]), 0x([0-9A-F]+), 0x([0-9A-F]+)$", RegexOptions.CultureInvariant);
    private readonly Dictionary<int, List<ScummEgoArrival>> _byRoom = [];

    private ScummEgoArrivalIndex() { }

    /// <summary>The literal arrivals into <paramref name="room"/>, in source order.</summary>
    public IReadOnlyList<ScummEgoArrival> Into(int room) => _byRoom.TryGetValue(room, out var list) ? list : [];

    private readonly Dictionary<int, List<ScummHeroPlacement>> _placed = [];

    /// <summary>The places the game's scripts stand the hero in <paramref name="room"/> by hand, in source order.</summary>
    public IReadOnlyList<ScummHeroPlacement> PlacedInto(int room) => _placed.TryGetValue(room, out var list) ? list : [];

    private readonly Dictionary<int, int> _pseudoRooms = [];

    /// <summary>The pseudo room numbers (0x80 and up) the game's literal <c>pseudoRoom</c> instructions map to <paramref name="room"/>, in ascending order.</summary>
    public IReadOnlyList<int> PseudoNumbersOf(int room) => _pseudoRooms.Where(pair => pair.Value == room).Select(pair => pair.Key).Order().ToArray();

    public static ScummEgoArrivalIndex For(GameLibrary game)
    {
        ArgumentNullException.ThrowIfNull(game);
        return Cache.GetOrAdd($"{game.Key}|{game.Title}", _ => Build(game));
    }

    private static ScummEgoArrivalIndex Build(GameLibrary game)
    {
        var index = new ScummEgoArrivalIndex();
        var data = ScummDataSources.For(game);
        var objectsByRoom = new Dictionary<int, HashSet<int>>();
        var found = new List<ScummEgoArrival>();
        var pseudoRooms = new Dictionary<int, int>();

        void Scan(string source, int? sourceRoom, Func<ScummScriptDisassembly?> decode)
        {
            ScummScriptDisassembly? script;
            try { script = decode(); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or EndOfStreamException or ArgumentException) { return; }
            if (script is null) return;
            // o5_pseudoRoom: the real room, then the bytes from 0x80 up that load it (the reader prints them without bit 7).
            foreach (var instruction in script.Instructions.Where(item => item.Operation == "pseudoRoom"))
            {
                var parts = instruction.Operands.Split([',', '[', ']', ' '], StringSplitOptions.RemoveEmptyEntries);
                static bool Number(string text, out int value) => int.TryParse(text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? text[2..] : text,
                    text.StartsWith("0x", StringComparison.OrdinalIgnoreCase) ? NumberStyles.HexNumber : NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
                if (parts.Length < 2 || !Number(parts[0], out var real)) continue;
                foreach (var item in parts.Skip(1))
                    if (Number(item, out var pseudo)) pseudoRooms[pseudo | 0x80] = real;
            }
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
            for (var i = 0; i < script.Instructions.Count; i++)
            {
                if (script.Instructions[i].Operation != "putActorInRoom" || HeroInRoom.Match(script.Instructions[i].Operands) is not { Success: true } inRoom) continue;
                var put = script.Instructions.Skip(i + 1).Take(3).FirstOrDefault(item => item.Operation == "putActor" && HeroAt.IsMatch(item.Operands));
                if (put is null) continue;
                var at = HeroAt.Match(put.Operands);
                var (target, x, y) = (int.Parse(inRoom.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture),
                    int.Parse(at.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture), int.Parse(at.Groups[2].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture));
                if (target == 0 || x == 0 && y == 0) continue;
                if (!index._placed.TryGetValue(target, out var list)) index._placed[target] = list = [];
                list.Add(new ScummHeroPlacement(target, x, y, source, script.Instructions[i].Offset));
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

        foreach (var literal in found)
        {
            var arrival = literal.TargetRoom >= 0x80 && pseudoRooms.TryGetValue(literal.TargetRoom, out var realRoom)
                ? literal with { TargetRoom = realRoom, PseudoRoom = literal.TargetRoom } : literal;
            if (!objectsByRoom.TryGetValue(arrival.TargetRoom, out var objects) || !objects.Contains(arrival.ObjectId)) continue;
            if (!index._byRoom.TryGetValue(arrival.TargetRoom, out var list)) index._byRoom[arrival.TargetRoom] = list = [];
            list.Add(arrival);
        }
        foreach (var (pseudo, real) in pseudoRooms) index._pseudoRooms[pseudo] = real;
        return index;
    }
}
