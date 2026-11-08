using System.Globalization;
using System.Text.RegularExpressions;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// What a game's verbs mean to the cutscene list: which verb numbers are its look, walk-to and use verbs (the representative of a group is the
/// trigger the player meets first, and a silent Use is a scene), which verbs are their own entry even when another verb reaches the same scripts,
/// and the words a title names a verb with. A game supplies them as data (LOOM: <c>LoomCutsceneVerbs</c>; a verb-based game: its own verbs'
/// names read from its scripts); the scan itself knows no verb number.
/// </summary>
/// <param name="Look">The look verb, or null when the game has none the list should rank.</param>
/// <param name="WalkTo">The walk-to verb, or null.</param>
/// <param name="Use">The use verb, or null (no silent-Use scenes then).</param>
/// <param name="SeparateFrom">Verbs from this number up are each their own entry (LOOM's drafts), or null when no verb is.</param>
/// <param name="Word">The verb as a word for a title ("Look at", "Push"); null or empty when the game does not name it.</param>
/// <param name="AimsAt">Verbs whose title reads "&lt;word&gt; at &lt;object&gt;" (LOOM's drafts) rather than "&lt;word&gt; &lt;object&gt;".</param>
/// <param name="NamesSharedCode">An object's verbs whose entries point at the same code are named after the lowest of them, so they group as one
/// scene (a verb-based game: Indy3's light switch answers Push, Pull, Use and Turn off with one script). LOOM keeps each verb's own name: its
/// catalog lists each draft separately and its notes name the verb (false).</param>
public sealed record ScummCutsceneVerbs(int? Look, int? WalkTo, int? Use, int? SeparateFrom = null,
    Func<int, string?>? Word = null, Func<int, bool>? AimsAt = null, bool NamesSharedCode = false)
{
    /// <summary>No verb has a role and none is named: every verb is listed by its number.</summary>
    public static ScummCutsceneVerbs None { get; } = new(null, null, null);

    /// <summary>One of the look, walk-to and use verbs.</summary>
    public bool IsPlain(int verbId) => verbId == Look || verbId == WalkTo || verbId == Use;

    /// <summary>The order the player meets triggers in: use, walk to, look, then the rest.</summary>
    public int Rank(int verbId) => verbId == Use ? 1 : verbId == WalkTo ? 2 : verbId == Look ? 3 : 4;

    /// <summary>The verb is its own entry even when another verb of the object reaches the same scripts.</summary>
    public bool Separates(int verbId) => SeparateFrom is int from && verbId >= from;

    /// <summary>The verb in a note: <c>verb 0x36 (look)</c> for the three plain verbs, <c>verb 0xHH</c> for the rest.</summary>
    public string Label(int verbId) =>
        verbId == Look ? $"verb 0x{verbId:X2} (look)" : verbId == WalkTo ? $"verb 0x{verbId:X2} (walk to)" : verbId == Use ? $"verb 0x{verbId:X2} (use)" : $"verb 0x{verbId:X2}";

    /// <summary>The verb's word for a title, or <c>verb 0xHH</c> when the game gives none.</summary>
    public string WordFor(int verbId) => Word?.Invoke(verbId) is { Length: > 0 } word ? word : $"verb 0x{verbId:X2}";
}

/// <summary>
/// A place where a game's own code can start a cutscene: a room's entry script (<see cref="ObjectId"/> 0) or an object's verb,
/// together with what the code it reaches does. Everything is read from the scripts; nothing is a list kept by hand.
/// </summary>
/// <param name="Scripts">The scripts, in the order they were reached, that hold a <c>cutscene</c> ... <c>endCutscene</c> span (for example "Room 4 local script 202").</param>
/// <param name="Texts">Distinct printed texts inside those spans (a text is a possible line, since branches decide which one runs).</param>
/// <param name="RoomLoads">Room loads inside those spans.</param>
/// <param name="FirstText">The first printed text of the spans, for a title; empty when there is none.</param>
/// <param name="LoadTargets">The literal rooms the span's room loads name (a load with a computed room is counted in <paramref name="RoomLoads"/> only).</param>
/// <param name="LoadsAfter">Room loads in the reached scripts outside any span (a cutscene that ends and then changes room).</param>
/// <param name="Destinations">For a look, walk-to or use verb, the rooms its code leads to (the same doorway finder as the room hotspots, so loads in local scripts count); empty when none was asked for or found.</param>
/// <param name="Verbs">The game's verb roles and words (<see cref="ScummCutsceneVerbs"/>); <see cref="ScummCutsceneVerbs.None"/> when not given.</param>
public sealed record ScummCutscene(int Room, int ObjectId, string ObjectName, int VerbId, IReadOnlyList<string> Scripts,
    int Texts, int RoomLoads, int SpanInstructions, string FirstText, IReadOnlyList<int>? Destinations = null,
    IReadOnlyList<int>? LoadTargets = null, int LoadsAfter = 0, ScummCutsceneVerbs? Verbs = null, bool Departure = false)
{
    public IReadOnlyList<int> LeadsTo => Destinations ?? [];

    public bool IsRoomEntry => ObjectId == 0;

    /// <summary>A room's arrival scene that the script loading the room starts (no object; <see cref="VerbId"/> is the scene's script): the room's entry does not begin it.</summary>
    public bool IsArrivalScript => ObjectId == 0 && VerbId != 0;

    /// <summary>
    /// A scene the room's exit script starts as the hero leaves (no object; <see cref="VerbId"/> is the scene's script; 8 November 2026, user: Maniac
    /// Mansion Room 41, the publisher reading the manuscript, Script 105 started by Room 1's exit script +0x0028). Played from its cutscene, as an arrival scene.
    /// </summary>
    public bool IsDepartureScript => Departure && IsArrivalScript;

    /// <summary>The game's verb roles (never null).</summary>
    public ScummCutsceneVerbs Roles => Verbs ?? ScummCutsceneVerbs.None;

    /// <summary>The verb as the game's own scripts use it: <c>entry script</c>, or the verb with its role (LOOM: 0x36 look, 0x37 walk to, 0x38 use).</summary>
    public string VerbLabel => IsRoomEntry ? "entry script" : Roles.Label(VerbId);
}

/// <summary>Triggers that run the same cutscene scripts: the one to play and the others that reach it.</summary>
public sealed record ScummCutsceneGroup(ScummCutscene First, IReadOnlyList<ScummCutscene> Others)
{
    /// <summary>
    /// A scene: the room's entry, a span that loads a room (or whose scripts load one right after it ends), a verb that leads to another
    /// room, or a silent Use (LOOM: Bobbin goes into a tent, the camera pans to something). Everything else, however many
    /// lines it prints, is "dialogue and reactions". Checked on 10 October against the LOOM reference EGA catalog (328 replays, 238 of them in
    /// a group): the first three conditions agreed with its categories for 211 of the 238, the silent-use one brings it to 225. The rest
    /// depend on which branch runs (a load that only happens in some answers), which a scan of the code cannot tell.
    /// </summary>
    public bool IsScene => First.IsRoomEntry || First.RoomLoads > 0 || First.LeadsTo.Count > 0 || First.LoadsAfter > 0 ||
                           (First.Texts == 0 && First.Roles.Use is int use && First.VerbId == use);
}

/// <summary>
/// Finds a game's cutscene triggers from its scripts (any SCUMM version with an <see cref="IScummDataSource"/>): every room entry script and every
/// object verb whose code (or a script it starts or chains by a literal number, up to <see cref="Depth"/> levels) holds a <c>cutscene</c> ...
/// <c>endCutscene</c> span. A branch can keep a span from running; what a trigger "possibly" shows is not a measured outcome. Moved from
/// <c>LoomCutsceneIndex</c> to the engine layer on 17 October 2026 (S7, K-06); LOOM's verb table stays in <c>Loom/</c>.
/// </summary>
public static partial class ScummCutsceneIndex
{
    public const int Depth = 3;

    [GeneratedRegex(@"^(?:0x)?([0-9A-Fa-f]+)(?:,|$)")]
    private static partial Regex LiteralScriptNumber();

    [GeneratedRegex(@"text,?\s+""(.*?)""")]
    private static partial Regex TextOperand();

    /// <param name="doorways">The rooms a look, walk-to or use verb's code leads to (the game's doorway finder), or null.</param>
    /// <param name="verbs">The game's verb roles and words; <see cref="ScummCutsceneVerbs.None"/> when null.</param>
    /// <param name="arrivalScripts">Also list the arrival scenes that the script loading a room starts (<see cref="FindArrivalScripts"/>).</param>
    public static IReadOnlyList<ScummCutscene> Scan(IScummDataSource data, IEnumerable<int> rooms,
        Func<GameResource, ScummScriptDisassembly, IReadOnlyList<int>>? doorways = null, GameLibrary? game = null, ScummCutsceneVerbs? verbs = null, bool arrivalScripts = false,
        bool departureScripts = false)
    {
        verbs ??= ScummCutsceneVerbs.None;
        var found = new List<ScummCutscene>();
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
                Walk(data, room, script, label, 0, false, visited, scripts, texts, summary);
                if (scripts.Count == 0) return;
                IReadOnlyList<int>? destinations = null;
                if (objectId != 0 && verbs.IsPlain(verbId) && doorways is not null &&
                    game?.Rooms.FirstOrDefault(r => r.ResourceId == room) is { } resource)
                    destinations = doorways(resource, script);
                found.Add(new(room, objectId, objectName, verbId, scripts, texts.Count, summary.RoomLoads, summary.Instructions, texts.FirstOrDefault() ?? "", destinations,
                    summary.Targets.Distinct().ToArray(), summary.LoadsAfter, verbs));
            }
            Trigger(0, "", 0, Try(() => data.RoomEntryScript(room)), $"Room {room} entry script");
            var objectVerbs = Try(() => data.ReadObjectVerbs(room, roomData.Objects)) ?? new Dictionary<int, IReadOnlyList<ScummObjectVerbEntry>>();
            foreach (var (objectId, entries) in objectVerbs)
            {
                var record = roomData.Objects.First(o => o.Id == objectId);
                var name = roomData.ObjectNames.TryGetValue(objectId, out var raw) ? ScummTextDecoder.DecodeGlyphs(raw).Trim().TrimEnd('@').Trim() : "";
                // Verbs whose entries point at the same code run one script (Indy3's light switch: Push, Pull, Use and Turn off), so the code is
                // named after the lowest of them and the triggers group as one scene.
                var real = entries.Where(v => !v.IsFallback).ToArray();
                foreach (var verb in real)
                {
                    var owner = verbs.NamesSharedCode ? real.Where(v => v.ScriptOffset == verb.ScriptOffset).Min(v => v.VerbId) : verb.VerbId;
                    Trigger(objectId, name, verb.VerbId, Try(() => data.ReadObjectVerb(room, record, verb.VerbId, out _)),
                        $"Room {room} object {objectId} verb 0x{owner:X2}");
                }
            }
        }
        if (arrivalScripts) FindArrivalScripts(data, rooms.ToHashSet(), found, verbs);
        if (departureScripts) FindDepartureScripts(data, rooms, found, verbs);
        return found.OrderBy(c => c.Room).ThenBy(c => c.ObjectId).ThenBy(c => c.VerbId).ToArray();
    }

    /// <summary>
    /// A room's arrival scene started by the script that loads the room, not by its entry (6 November 2026, user report: Indy 3 Room 15, where Elsa meets Indy:
    /// Script 91 +0x00AC <c>loadRoom 0x0F</c>, +0x00AE <c>startScript 0x62</c>; Script 98 holds the cutscene). It belongs to that room: a trigger with no object
    /// whose verb number is the scene's script, so a room can have one beside its entry.
    /// </summary>
    private static void FindArrivalScripts(IScummDataSource data, HashSet<int> rooms, List<ScummCutscene> found, ScummCutsceneVerbs verbs)
    {
        foreach (var number in data.GlobalScriptIds())
        {
            var loader = Try(() => data.GlobalScript(number));
            if (loader is null) continue;
            var ins = loader.Instructions;
            for (var i = 0; i < ins.Count; i++)
            {
                if (ins[i].Operation != "loadRoom" || LiteralScriptNumber().Match(ins[i].Operands) is not { Success: true } roomOperand) continue;
                var room = int.Parse(roomOperand.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                // A pseudo room number (0x80 and up) is not a room of the list.
                if (!rooms.Contains(room)) continue;
                if (ins.Skip(i + 1).Take(3).FirstOrDefault(next => next.Operation is "startScript" or "chainScript") is not { } call ||
                    LiteralScriptNumber().Match(call.Operands) is not { Success: true } scriptOperand) continue;
                var id = int.Parse(scriptOperand.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                var label = $"global script {id}";
                // The opening's own chain (Script 1) is Play Game Intro's; a scene already listed for the room stays one.
                if (number == 1 || id >= data.GlobalScriptCount || found.Any(c => c.Room == room && c.Scripts.Contains(label))) continue;
                var scene = Try(() => data.GlobalScript(id));
                if (scene is null) continue;
                var scripts = new List<string>();
                var texts = new List<string>();
                var summary = new Span();
                Walk(data, room, scene, label, 1, false, [], scripts, texts, summary);
                if (scripts.Count == 0 || scripts[0] != label) continue;
                found.Add(new(room, 0, "", id, scripts, texts.Count, summary.RoomLoads, summary.Instructions, texts.FirstOrDefault() ?? "", null,
                    summary.Targets.Distinct().ToArray(), summary.LoadsAfter, verbs));
            }
        }
    }

    /// <summary>
    /// The scenes a room's exit script starts (a literal global script whose own code, or a script it starts, holds a scene): they belong to the room being
    /// left. A script a trigger of the same room already reaches stays that trigger's scene.
    /// </summary>
    private static void FindDepartureScripts(IScummDataSource data, IEnumerable<int> rooms, List<ScummCutscene> found, ScummCutsceneVerbs verbs)
    {
        foreach (var room in rooms)
        {
            if (Try(() => data.RoomExitScript(room)) is not { } exit) continue;
            foreach (var call in exit.Instructions.Where(i => i.Operation is "startScript" or "chainScript"))
            {
                if (LiteralScriptNumber().Match(call.Operands) is not { Success: true } operand) continue;
                var id = int.Parse(operand.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
                var label = $"global script {id}";
                if (id >= data.GlobalScriptCount || found.Any(c => c.Room == room && c.Scripts.Contains(label))) continue;
                if (Try(() => data.GlobalScript(id)) is not { } scene) continue;
                var scripts = new List<string>();
                var texts = new List<string>();
                var summary = new Span();
                Walk(data, room, scene, label, 1, false, [], scripts, texts, summary);
                if (scripts.Count == 0 || scripts[0] != label) continue;
                found.Add(new(room, 0, "", id, scripts, texts.Count, summary.RoomLoads, summary.Instructions, texts.FirstOrDefault() ?? "", null,
                    summary.Targets.Distinct().ToArray(), summary.LoadsAfter, verbs, Departure: true));
            }
        }
    }

    /// <summary>Triggers that reach the same cutscene scripts are one scene (LOOM: four owl holes, five wool piles); a script reached from more triggers than this is a shared helper (a generic reaction), not a scene of its own.</summary>
    public const int SharedHelperTriggers = 20;

    /// <summary>
    /// Groups the triggers by the cutscene scripts they reach and leaves out shared helpers. The representative is the trigger the player
    /// meets first: an entry script, then the use, walk-to and look verbs, then the lowest verb and object number.
    /// </summary>
    public static IReadOnlyList<ScummCutsceneGroup> Group(IReadOnlyList<ScummCutscene> triggers)
    {
        // A verb the game's roles separate (LOOM's drafts, 0x39 and up) is its own scene even when another verb of the object reaches the same
        // scripts: the EGA catalog lists each spell (Twisting, Sharpening, its reverse) separately, and the player casts them one by one.
        static string ScriptsKey(ScummCutscene c) => string.Join("|", c.Scripts);
        static string Key(ScummCutscene c) => ScriptsKey(c) + (c.IsRoomEntry || !c.Roles.Separates(c.VerbId) ? "" : $"#{c.VerbId}");
        static int Rank(ScummCutscene c) => c.IsRoomEntry ? 0 : c.Roles.Rank(c.VerbId);
        // Count uses before separating verbs: a generic response reached by many spells is still a shared helper.
        var helpers = triggers.GroupBy(ScriptsKey).Where(g => g.Count() > SharedHelperTriggers)
            .Select(g => g.Key).ToHashSet(StringComparer.Ordinal);
        return triggers.Where(c => !helpers.Contains(ScriptsKey(c))).GroupBy(Key)
            .Select(g =>
            {
                var ordered = g.OrderBy(Rank).ThenBy(c => c.VerbId).ThenBy(c => c.ObjectId).ToArray();
                return new ScummCutsceneGroup(ordered[0], ordered.Skip(1).ToArray());
            })
            .OrderBy(g => g.First.Room).ThenBy(g => Rank(g.First)).ThenBy(g => g.First.ObjectId).ThenBy(g => g.First.VerbId)
            .ToArray();
    }

    private sealed class Span { public int RoomLoads; public int Instructions; public int LoadsAfter; public List<int> Targets = []; }

    /// <param name="spanOpen">True when the script was started from inside a cutscene span: everything it does runs inside that span, even though the
    /// script holds no <c>cutscene</c> opcode itself (a span of two instructions that starts the script that does the walking and talking).</param>
    private static void Walk(IScummDataSource data, int room, ScummScriptDisassembly script, string label, int depth, bool spanOpen,
        HashSet<(bool, int)> visited, List<string> scripts, List<string> texts, Span summary)
    {
        var ins = script.Instructions;
        var open = spanOpen; var counted = false;
        var calls = new List<(ScummScriptInstruction Call, bool InSpan)>();
        foreach (var i in ins)
        {
            if (ScummSceneMarks.Starts(i)) { open = true; if (!counted) { scripts.Add(label); counted = true; } continue; }
            if (ScummSceneMarks.Ends(i)) { open = false; continue; }
            if (i.Operation is "startScript" or "chainScript") calls.Add((i, open));
            if (!open)
            {
                if (i.Operation is "loadRoom" or "loadRoomWithEgo") summary.LoadsAfter++;
                continue;
            }
            summary.Instructions++;
            if (i.Operation is "loadRoom" or "loadRoomWithEgo")
            {
                summary.RoomLoads++;
                var parts = i.Operands.Split(',', StringSplitOptions.TrimEntries);
                var operand = i.Operation == "loadRoom" ? parts[0] : parts.ElementAtOrDefault(1) ?? "";
                if (operand.StartsWith("0x", StringComparison.Ordinal) && int.TryParse(operand[2..], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var target)) summary.Targets.Add(target);
            }
            if ((i.Operation == "print" || i.Operation == "printEgo" && data is ScummV2DataSource) && TextOperand().Match(i.Operands) is { Success: true } m)
            {
                var text = ScummTextDecoder.DecodeLine(m.Groups[1].Value).Trim();
                if (text.Length > 0 && !texts.Contains(text)) texts.Add(text);
            }
        }
        if (depth >= Depth) return;
        foreach (var (i, inSpan) in calls)
        {
            if (LiteralScriptNumber().Match(i.Operands) is not { Success: true } m) continue;
            var id = int.Parse(m.Groups[1].Value, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
            var local = id >= data.GlobalScriptCount;
            if (!visited.Add((local, id))) continue;
            var next = Try(() => local ? data.RoomLocalScript(room, id) : data.GlobalScript(id));
            if (next is not null) Walk(data, room, next, local ? $"Room {room} local script {id}" : $"global script {id}", depth + 1, inSpan, visited, scripts, texts, summary);
        }
    }

    private static T? Try<T>(Func<T?> read) where T : class
    {
        try { return read(); }
        catch (Exception ex) when (ex is IOException or InvalidDataException) { return null; }
    }
}
