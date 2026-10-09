using System.Text.Json;
using System.Text.RegularExpressions;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;

var folder = args.Length > 0 ? args[0] : "Games/indy3/EGA";
var game = new GameFolderScanner().Scan(Path.GetFullPath(folder));
if (args.Length > 2 && args[1] == "--script" && int.TryParse(args[2], out var requestedScript))
{
    var selected = game.Resources.First(r => r.Kind == ResourceKind.Script && r.ResourceId == requestedScript);
    Console.WriteLine(new ScummScriptReader(ScummScriptDialect.Indy3).Decode(selected).Format());
    return;
}
var extraction = new LoomTextExtractor().Extract(game);
var roomResolver = new ConversationRoomResolver(game);
var groups = extraction.Entries.Where(e => e.ConversationKind == LoomTextExtractor.ChoiceConversationKind)
    .GroupBy(e => e.ConversationId).Where(g => g.Key is not null)
    .Select(g => new {
        id = g.Key,
        room = g.First().RoomId,
        script = g.First().ScriptId,
        source = g.First().Source,
        choices = g.Where(e => e.IsPlayerChoice).Select(e => new { e.Text, e.ChoiceVerbId, e.CodeOffset }).ToArray(),
        replies = g.Where(e => !e.IsPlayerChoice).Select(e => new { e.Text, e.ChoiceVerbId, e.CodeOffset }).ToArray()
        ,contexts = g.First().ScriptId is int sourceScript
            ? roomResolver.At(sourceScript, g.Where(e => e.IsPlayerChoice).Min(e => e.CodeOffset))
            : []
    }).ToArray();
if (args.Length > 1 && args[1] == "--verify")
{
    VerifyRoom(29, [1], []);
    VerifyRoom(30, [3], []);
    VerifyRoom(78, [39], [40]);
    VerifyRoom(104, [31], []);
    VerifyRoom(117, [45, 46], []);
    Console.WriteLine("Indy3 room provenance assertions passed (scripts 29, 30, 78, 104, 117).");
    return;
}
void VerifyRoom(int scriptId, int[] expected, int[] excluded)
{
    var group = groups.First(g => g.script == scriptId);
    var found = group.contexts.Select(c => c.RoomId).ToHashSet();
    if (expected.Any(room => !found.Contains(room)) || excluded.Any(found.Contains))
        throw new InvalidDataException($"Script {scriptId} menu {group.id}: rooms [{string.Join(',', found)}], " +
            $"expected to include [{string.Join(',', expected)}] and exclude [{string.Join(',', excluded)}].");
}
var conversationSources = groups.Select(g => g.source).ToHashSet();
var orphanLabels = extraction.Entries
    .Where(e => e.Kind == "Verb label" && e.ConversationKind is null && conversationSources.Contains(e.Source))
    .Select(e => new { e.Source, e.ScriptId, e.RoomId, e.CodeOffset, e.Text }).ToArray();
var outsideLabels = extraction.Entries
    .Where(e => e.Kind == "Verb label" && !conversationSources.Contains(e.Source))
    .GroupBy(e => e.Source)
    .Select(g => new { source = g.Key, labels = g.Select(e => new { e.ScriptId, e.RoomId, e.CodeOffset, e.Text }).ToArray() })
    .ToArray();

var reader = new ScummScriptReader(ScummScriptDialect.Indy3);
var verbReader = new ScummObjectVerbReader(ScummScriptDialect.Indy3);
var objectDirectory = new ScummRoomObjectDirectory();
var scripts = new List<(string Source, int? Room, int? Script, ScummScriptDisassembly Data)>();
foreach (var resource in game.Resources.Where(r => r.Kind == ResourceKind.Script && r.ResourceId.HasValue))
{
    try { scripts.Add(($"Indexed script {resource.ResourceId}", null, resource.ResourceId, reader.Decode(resource))); }
    catch { }
}
var globalCount = game.Resources.Count(r => r.Kind == ResourceKind.Script && r.ResourceId.HasValue);
foreach (var room in game.Rooms.Where(r => r.ResourceId.HasValue))
{
    try { if (reader.DecodeRoomEntry(room) is { } data) scripts.Add(($"Room {room.ResourceId} entry", room.ResourceId, null, data)); } catch { }
    try { if (reader.DecodeRoomExit(room) is { } data) scripts.Add(($"Room {room.ResourceId} exit", room.ResourceId, null, data)); } catch { }
    try
    {
        var encoded = File.ReadAllBytes(room.FilePath);
        foreach (var local in ScummScriptReader.ReadRoomLocalScriptIds(encoded, globalCount))
            if (reader.DecodeRoomLocalScript(room, local, globalCount) is { } data)
                scripts.Add(($"Room {room.ResourceId} local {local}", room.ResourceId, local, data));
        var objects = objectDirectory.Decode(encoded);
        var verbs = verbReader.ReadAll(encoded, objects);
        foreach (var obj in objects)
            if (verbs.TryGetValue(obj.Id, out var table))
                foreach (var verb in table)
                    try { scripts.Add(($"Room {room.ResourceId} object {obj.Id} verb {verb.VerbId:X2}",
                        room.ResourceId, null, verbReader.Decode(room, obj, verb.VerbId))); }
                    catch { }
    }
    catch { }
}
var calls = scripts.SelectMany(s => s.Data.Instructions
    .Where(i => i.Operation is "startScript" or "chainScript" or "loadRoom" or "loadRoomWithEgo")
    .Select(i => new { source = s.Source, room = s.Room, script = s.Script,
        container = s.Data.ContainerFileNumber, offset = i.Offset,
        operation = i.Operation, operands = i.Operands,
        target = Regex.Match(i.Operands, @"^\s*0x([0-9A-Fa-f]+)").Groups[1].Value }))
    .ToArray();
var dispatcherWrites = scripts.SelectMany(s => s.Data.Instructions
    .Where(i => Regex.IsMatch(i.Operands, @"^(?:var\[0x0(?:32|33|34)\]|local\[0x000\])\s*=") ||
                i.Operation is "move" or "expression" && Regex.IsMatch(i.Operands, @"0x00(?:1D|1E|56)\b"))
    .Select(i => new { source = s.Source, room = s.Room, script = s.Script,
        container = s.Data.ContainerFileNumber, offset = i.Offset,
        operation = i.Operation, operands = i.Operands })).ToArray();
Console.WriteLine(JsonSerializer.Serialize(new {
    extraction = new { extraction.ResourcesVisited, extraction.ResourcesSkipped,
        extraction.UnresolvedPaths, extraction.DecodedBytes, extraction.CandidateBytes,
        extraction.Diagnostics },
    totalGroups = groups.Length,
    groupsWithRoom = groups.Count(g => g.room.HasValue),
    groupsWithoutRoom = groups.Count(g => !g.room.HasValue),
    groups, orphanLabels, outsideLabels, calls, dispatcherWrites
}, new JsonSerializerOptions { WriteIndented = true }));
