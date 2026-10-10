using System.Globalization;
using System.Text.RegularExpressions;

namespace SCUMMexplorer.Core.Loom;

/// <summary>
/// A place where the CD game's own code can start a cutscene: a room's entry script (<see cref="ObjectId"/> 0) or an object's verb,
/// together with what the code it reaches does. Everything is read from the scripts; nothing is a list kept by hand.
/// </summary>
/// <param name="Scripts">The scripts, in the order they were reached, that hold a <c>cutscene</c> ... <c>endCutscene</c> span (for example "Room 4 local script 202").</param>
/// <param name="Texts">Distinct printed texts inside those spans (a text is a possible line, since branches decide which one runs).</param>
/// <param name="RoomLoads">Room loads inside those spans.</param>
/// <param name="FirstText">The first printed text of the spans, for a title; empty when there is none.</param>
/// <param name="Destinations">For a look, walk-to or use verb, the rooms its code leads to (the same doorway finder as the room hotspots, so loads in local scripts count); empty when none was asked for or found.</param>
public sealed record LoomCdCutscene(int Room, int ObjectId, string ObjectName, int VerbId, IReadOnlyList<string> Scripts,
    int Texts, int RoomLoads, int SpanInstructions, string FirstText, IReadOnlyList<int>? Destinations = null)
{
    public IReadOnlyList<int> LeadsTo => Destinations ?? [];

    public bool IsRoomEntry => ObjectId == 0;

    /// <summary>The verb as the game's own scripts use it: 0x36 look, 0x37 walk to, 0x38 use are the input scripts' verbs (005 to 007); others are draft-response or object verbs.</summary>
    public string VerbLabel => IsRoomEntry ? "entry script" : VerbId switch
    {
        0x36 => "verb 0x36 (look)",
        0x37 => "verb 0x37 (walk to)",
        0x38 => "verb 0x38 (use)",
        _ => $"verb 0x{VerbId:X2}"
    };
}

/// <summary>Triggers that run the same cutscene scripts: the one to play and the others that reach it.</summary>
public sealed record LoomCdCutsceneGroup(LoomCdCutscene First, IReadOnlyList<LoomCdCutscene> Others)
{
    /// <summary>
    /// A scene: the room's entry, a span that loads a room, or a verb that leads to another room. Everything else, however many lines it
    /// prints, is "dialogue and reactions". Checked on 10 October against the reference EGA catalog (328 replays): this rule agrees with
    /// its categories for 301 of them; the old rule (three or more printed lines also made a scene) agreed far less.
    /// </summary>
    public bool IsScene => First.IsRoomEntry || First.RoomLoads > 0 || First.LeadsTo.Count > 0;
}

/// <summary>
/// Finds the CD game's cutscene triggers from its scripts: every room entry script and every object verb whose code (or a script it starts or
/// chains by a literal number, up to <see cref="Depth"/> levels) holds a <c>cutscene</c> ... <c>endCutscene</c> span.
/// A branch can keep a span from running; what a trigger "possibly" shows is not a measured outcome.
/// </summary>
public static partial class LoomCdCutsceneIndex
{
    public const int Depth = 3;

    [GeneratedRegex(@"^(?:0x)?([0-9A-Fa-f]+),")]
    private static partial Regex LiteralScriptNumber();

    [GeneratedRegex(@"text, ""(.*?)""")]
    private static partial Regex TextOperand();

    public static IReadOnlyList<LoomCdCutscene> Scan(IScummDataSource data, IEnumerable<int> rooms,
        Func<GameResource, ScummScriptDisassembly, IReadOnlyList<int>>? doorways = null, GameLibrary? game = null)
    {
        var found = new List<LoomCdCutscene>();
        foreach (var room in rooms)
        {
            ScummRoomData? roomData;
            try { roomData = data.ReadRoom(room); }
            catch (Exception ex) when (ex is IOException or InvalidDataException) { continue; }
            void Trigger(int objectId, string objectName, int verbId, ScummScriptDisassembly? script, string label)
            {
                if (script is null) return;
                var scripts = new List<string>();
                var visited = new HashSet<(bool Local, int Id)>();
                var texts = new List<string>();
                var summary = new Span();
                Walk(data, room, script, label, 0, visited, scripts, texts, summary);
                if (scripts.Count == 0) return;
                IReadOnlyList<int>? destinations = null;
                if (objectId != 0 && verbId is 0x36 or 0x37 or 0x38 && doorways is not null &&
                    game?.Rooms.FirstOrDefault(r => r.ResourceId == room) is { } resource)
                    destinations = doorways(resource, script);
                found.Add(new(room, objectId, objectName, verbId, scripts, texts.Count, summary.RoomLoads, summary.Instructions, texts.FirstOrDefault() ?? "", destinations));
            }
            Trigger(0, "", 0, Try(() => data.RoomEntryScript(room)), $"Room {room} entry script");
            var verbs = Try(() => data.ReadObjectVerbs(room, roomData.Objects)) ?? new Dictionary<int, IReadOnlyList<ScummObjectVerbEntry>>();
            foreach (var (objectId, entries) in verbs)
            {
                var record = roomData.Objects.First(o => o.Id == objectId);
                var name = roomData.ObjectNames.TryGetValue(objectId, out var raw) ? System.Text.Encoding.Latin1.GetString(raw).Trim().TrimEnd('@').Trim() : "";
                foreach (var verb in entries.Where(v => !v.IsFallback))
                    Trigger(objectId, name, verb.VerbId, Try(() => data.ReadObjectVerb(room, record, verb.VerbId, out _)),
                        $"Room {room} object {objectId} verb 0x{verb.VerbId:X2}");
            }
        }
        return found.OrderBy(c => c.Room).ThenBy(c => c.ObjectId).ThenBy(c => c.VerbId).ToArray();
    }

    /// <summary>Triggers that reach the same cutscene scripts are one scene (four owl holes, five wool piles); a script reached from more triggers than this is a shared helper (a generic reaction), not a scene of its own.</summary>
    public const int SharedHelperTriggers = 20;

    /// <summary>
    /// Groups the triggers by the cutscene scripts they reach and leaves out shared helpers. The representative is the trigger the player
    /// meets first: an entry script, then the use, walk-to and look verbs (0x38, 0x37, 0x36), then the lowest verb and object number.
    /// </summary>
    public static IReadOnlyList<LoomCdCutsceneGroup> Group(IReadOnlyList<LoomCdCutscene> triggers)
    {
        // A draft verb (0x39 and up) is its own scene even when another verb of the object reaches the same scripts: the EGA catalog lists
        // each spell (Twisting, Sharpening, its reverse) separately, and the player casts them one by one.
        static string ScriptsKey(LoomCdCutscene c) => string.Join("|", c.Scripts);
        static string Key(LoomCdCutscene c) => ScriptsKey(c) + (c.IsRoomEntry || c.VerbId < 0x39 ? "" : $"#{c.VerbId}");
        static int Rank(LoomCdCutscene c) => c.IsRoomEntry ? 0 : c.VerbId switch { 0x38 => 1, 0x37 => 2, 0x36 => 3, _ => 4 };
        // Count uses before separating draft verbs: a generic response reached by many spells is still a shared helper.
        var helpers = triggers.GroupBy(ScriptsKey).Where(g => g.Count() > SharedHelperTriggers)
            .Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
        return triggers.Where(c => !helpers.Contains(ScriptsKey(c))).GroupBy(Key)
            .Select(g =>
            {
                var ordered = g.OrderBy(Rank).ThenBy(c => c.VerbId).ThenBy(c => c.ObjectId).ToArray();
                return new LoomCdCutsceneGroup(ordered[0], ordered.Skip(1).ToArray());
            })
            .OrderBy(g => g.First.Room).ThenBy(g => Rank(g.First)).ThenBy(g => g.First.ObjectId).ThenBy(g => g.First.VerbId)
            .ToArray();
    }

    private sealed class Span { public int RoomLoads; public int Instructions; }

    private static void Walk(IScummDataSource data, int room, ScummScriptDisassembly script, string label, int depth,
        HashSet<(bool, int)> visited, List<string> scripts, List<string> texts, Span summary)
    {
        var ins = script.Instructions;
        var open = false; var counted = false;
        foreach (var i in ins)
        {
            if (i.Operation == "cutscene") { open = true; if (!counted) { scripts.Add(label); counted = true; } continue; }
            if (i.Operation == "endCutscene") { open = false; continue; }
            if (!open) continue;
            summary.Instructions++;
            if (i.Operation is "loadRoom" or "loadRoomWithEgo") summary.RoomLoads++;
            if (i.Operation == "print" && TextOperand().Match(i.Operands) is { Success: true } m)
            {
                var text = Regex.Replace(m.Groups[1].Value, @"\\x[0-9A-F]{2}", " ").Trim();
                if (text.Length > 0 && !texts.Contains(text)) texts.Add(text);
            }
        }
        if (depth >= Depth) return;
        foreach (var i in ins.Where(i => i.Operation is "startScript" or "chainScript"))
        {
            if (LiteralScriptNumber().Match(i.Operands) is not { Success: true } m) continue;
            var id = int.Parse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var local = id >= data.GlobalScriptCount;
            if (!visited.Add((local, id))) continue;
            var next = Try(() => local ? data.RoomLocalScript(room, id) : data.GlobalScript(id));
            if (next is not null) Walk(data, room, next, local ? $"Room {room} local script {id}" : $"global script {id}", depth + 1, visited, scripts, texts, summary);
        }
    }

    private static T? Try<T>(Func<T?> read) where T : class
    {
        try { return read(); }
        catch (Exception ex) when (ex is IOException or InvalidDataException) { return null; }
    }
}
