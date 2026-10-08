using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

var output = Path.GetFullPath(args[0]);
Directory.CreateDirectory(output);
var json = new JsonSerializerOptions { WriteIndented = true };
void Save(string name, object value) => File.WriteAllText(Path.Combine(output, name + ".json"), JsonSerializer.Serialize(value, json));
string Hash(byte[] bytes) => Convert.ToHexString(SHA256.HashData(bytes));
int Word(byte[] bytes, int at) => bytes[at] | bytes[at + 1] << 8;
object? Prop(object item, string name) => item.GetType().GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(item);
object? Field(object item, string name) => item.GetType().GetField(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)?.GetValue(item);

var editions = new[] {
    ("ega-en-roland", "Games/loom ega mt32 en"),
    ("ega-en-plain", "Games/ALTERNATIVE LOOMS/loom ega en"),
    ("ega-es-roland", "Games/ALTERNATIVE LOOMS/loom ega mt32 es"),
    ("ega-es-plain", "Games/ALTERNATIVE LOOMS/loom ega es"),
    ("ega-es-v12", "Games/ALTERNATIVE LOOMS/loom ega v1.2 es"),
    ("vga-en", "Games/loom vga en")
};

if (args.Length > 1 && args[1] == "entry-callbacks")
{
    var game = new GameFolderScanner().Scan(editions[^1].Item2);
    var data = ScummDataSources.For(game);
    var rows = new List<object>();
    foreach (var (room, obj, verb) in new[] { (64, 757, 0x36), (64, 757, 0x38), (93, 899, 0x36), (93, 899, 0x38), (93, 900, 0x36), (93, 900, 0x39) })
    foreach (var settle in new[] { false, true })
    {
        var s = new LoomExplorationSession(game, game.Vga!.ReadIndexedObjects(), completeBoot: false);
        s.VisitRoomById(room);
        var entryFrames = 0;
        if (settle) for (; entryFrames < 6000 && (s.InCutscene || s.Messages.HaveMsg != 0 || s.EgoMoving || s.CameraMoving); entryFrames++) s.AdvanceFrame();
        var runtime = Field(s, "_runtime")!;
        var before = new { entryFrames, s.InCutscene, s.Messages.HaveMsg,
            userPut = Prop(runtime, "UserPut"), owner = s.Snapshot.ObjectOwners.GetValueOrDefault(obj),
            state = s.Snapshot.ObjectStates.GetValueOrDefault(obj), classes = s.ObjectClasses.GetValueOrDefault(obj) };
        var marker = s.SessionTrace.Count;
        var starts = s.CutscenesBegun;
        s.ActivateObjectVerb(obj, verb);
        var quiet = 0;
        var frames = 0;
        for (; frames < 6000 && quiet < 90; frames++)
        {
            s.AdvanceFrame();
            quiet = s.InCutscene || s.Messages.HaveMsg != 0 || s.EgoMoving || s.CameraMoving ? 0 : quiet + 1;
        }
        var boundaries = s.SessionTrace.Skip(marker).Where(t => t.StartsWith("cutscene at ") || t.StartsWith("endCutscene at ")).ToArray();
        rows.Add(new { room, obj, verb, settle, before, frames, roomAfter = s.CurrentRoomId,
            targetBegun = s.CutscenesBegun - starts, targetEnded = boundaries.Count(t => t.StartsWith("endCutscene at ")),
            s.InCutscene, boundaries, action = s.LastAction });
        Console.WriteLine($"Room {room}/{obj}/{verb:X2}, settled {settle}: target starts {s.CutscenesBegun-starts}, end room {s.CurrentRoomId}, open {s.InCutscene}");
    }
    Save("entry-callbacks", rows);
    return;
}

foreach (var (name, folder) in editions.Where(_ => args.Length == 1))
{
    var game = new GameFolderScanner().Scan(folder);
    var data = ScummDataSources.For(game);
    var blocks = new List<(string Place, LoomScriptDisassembly Script)>();
    var readErrors = new List<string>();
    void Read(string place, Func<LoomScriptDisassembly?> read)
    {
        try { if (read() is { } script) blocks.Add((place, script)); }
        catch (Exception ex) { readErrors.Add(place + ": " + ex.Message); }
    }
    foreach (var id in data.GlobalScriptIds()) Read($"global {id}", () => data.GlobalScript(id));
    foreach (var room in game.Rooms.Select(r => r.ResourceId!.Value))
    {
        Read($"room {room} entry", () => data.RoomEntryScript(room));
        Read($"room {room} exit", () => data.RoomExitScript(room));
        foreach (var id in data.RoomLocalScriptIds(room)) Read($"room {room} local {id}", () => data.RoomLocalScript(room, id));
        var rd = data.ReadRoom(room);
        foreach (var (obj, entries) in data.ReadObjectVerbs(room, rd.Objects))
            foreach (var entry in entries)
                Read($"room {room} object {obj} verb {entry.VerbId:X2}", () => data.ReadObjectVerb(room, rd.Objects.Single(o => o.Id == obj), entry.VerbId, out _));
    }
    var matching = blocks.Where(b => new[] { 34, 37, 38, 46, 70 }.Any(r => b.Place.StartsWith($"room {r} ")) || b.Place is "global 87" or "global 97");
    File.WriteAllText(Path.Combine(output, name + "-scripts.txt"), string.Join("\n", matching.Select(b => b.Script.Format())));
    Save(name + "-references", new {
        readErrors,
        calls = blocks.SelectMany(b => b.Script.Instructions.Where(i => i.Operation.Contains("Script", StringComparison.OrdinalIgnoreCase) || i.Operation.Contains("Room", StringComparison.OrdinalIgnoreCase) || i.Operation == "putActor")
            .Select(i => new { b.Place, i.Offset, i.Operation, i.Operands })).ToArray()
    });
    var room34 = data.ReadRoom(34);
    Save(name + "-room34", new { room34.Width, room34.Height, room34.Objects, room34.WalkBoxes,
        names = room34.ObjectNames.ToDictionary(p => p.Key, p => System.Text.Encoding.Latin1.GetString(p.Value)) });
    foreach (var room in new[] { 37, 38, 46 })
    {
        var rd = data.ReadRoom(room);
        Save(name + $"-room{room}", new { rd.Objects, rd.WalkBoxes,
            names = rd.ObjectNames.ToDictionary(p => p.Key, p => System.Text.Encoding.Latin1.GetString(p.Value)) });
    }

    if (game.Id == GameId.Loom)
    {
        var chainReader = new LflResourceChain();
        Save(name + "-interior-globals", game.Resources.Where(r => r.Kind == ResourceKind.Script)
            .Select(r => chainReader.FindLeftover(game, r)).Where(r => r is not null).Select(r => r!.Describe()).ToArray());
        var globals = new List<object>();
        foreach (var id in new[] { 0, 51, 55, 116 })
        {
            var r = game.Resources.SingleOrDefault(r => r.Kind == ResourceKind.Script && r.ResourceId == id);
            if (r is null) continue;
            var encoded = File.ReadAllBytes(r.FilePath);
            var decoded = LflEncoding.DecodeXorFF(encoded);
            var at = r.Offset!.Value;
            var chain = LflResourceChain.Read(encoded);
            var enclosing = chain.FirstOrDefault(c => c.Start <= at && at < c.Start + c.Length);
            var indexEntry = new LflIndexReader().Read(Path.Combine(folder, "00.LFL")).Single(e => e.Kind == ResourceKind.Script && e.ResourceId == id);
            globals.Add(new { id, r.ContainerFileNumber, r.Offset, indexEntry.DirectoryEntryOffset, fileLength = encoded.Length,
                fileHash = Hash(encoded), declared = at + 2 <= decoded.Length ? Word(decoded, at) : -1,
                startsResource = chain.Any(c => c.Start == at), enclosing,
                leftover = chainReader.FindLeftover(game, r)?.Describe(),
                encodedHead = Convert.ToHexString(encoded.AsSpan(at, Math.Min(16, encoded.Length - at))),
                decodedHead = Convert.ToHexString(decoded.AsSpan(at, Math.Min(16, decoded.Length - at))) });
        }
        var r70 = game.Rooms.Single(r => r.ResourceId == 70);
        var enc70 = File.ReadAllBytes(r70.FilePath);
        var dec70 = LflEncoding.DecodeXorFF(enc70);
        var locals = new List<object>();
        foreach (var id in data.RoomLocalScriptIds(70))
        {
            var s = data.RoomLocalScript(70, id)!;
            var bytes = dec70.AsSpan(s.ResourceOffset, s.CodeLength).ToArray();
            locals.Add(new { id, s.ResourceOffset, s.CodeLength, hash = Hash(bytes), bytes = Convert.ToHexString(bytes), s.Issues });
        }
        Save(name + "-resources", new { indexHash = Hash(File.ReadAllBytes(Path.Combine(folder, "00.LFL"))), globals,
            room70 = new { fileLength = enc70.Length, declared = Word(dec70, 0), fileHash = Hash(enc70),
                header = Convert.ToHexString(dec70.AsSpan(0, 29)), locals } });
    }
    Console.WriteLine($"{name}: {blocks.Count} script blocks, {readErrors.Count} read errors; packet saved.");
}

var ega = new GameFolderScanner().Scan(editions[0].Item2);
var session = new LoomExplorationSession(ega, new LflObjectStateReader().ReadSnapshot(Path.Combine(ega.FolderPath, "00.LFL")));
if (args.Length > 1 && args[1] == "rusty-prepared")
{
    // The existing app's disclosed FirewoodSteps; native scenes write D99.
    LoomReplayStep[] steps = [
        new("visit", 34), new("verb", 562, 0x44), new("scene", EndBoundary: "endCutscene at Script 85 +0x05A8", ExpectedRoom: 34),
        new("verb", 562, 0x4C), new("scene", EndBoundary: "endCutscene at Room 34 local script 202 +0x015F", ExpectedRoom: 34),
        new("visit", 36), new("staff", 1), new("frames", Frames: 8),
        new("verb", 600, 0x38), new("frames", Frames: 32), new("click", X: 109, Y: 102),
        new("scene", EndBoundary: "endCutscene at Script 86 +0x0536", ExpectedRoom: 38)
    ];
    var sequence = new LoomReplaySequence(session, steps);
    var ok = sequence.RunSilently(steps.Length);
    Save("rusty-prelude", new { ok, sequence.Failure, room = session.CurrentRoomId, session.InCutscene,
        bitD99 = session.Snapshot.KnownGlobalVariables.GetValueOrDefault("bit[0xD99]") });
    if (!ok) throw new InvalidDataException("Existing FirewoodSteps failed: " + sequence.Failure);
    Console.WriteLine("Existing FirewoodSteps completed; native scene set D99; retrying straw look.");
}
else session.VisitRoomById(38);
var traceStart = session.SessionTrace.Count;
var startBaseline = session.CutscenesBegun;
session.ActivateObjectVerb(611, 0x36);
var states = new List<object>();
for (var frame = 0; frame <= 6000; frame++)
{
    if (frame is 0 or 1000 or 3000 or 6000)
    {
        var pending = ((IEnumerable)Field(session, "_pending")!).Cast<object>().Select(p => new {
            script = ((LoomScriptDisassembly)Prop(p, "Script")!).Context ?? $"global {((LoomScriptDisassembly)Prop(p, "Script")!).ScriptId}",
            offset = Prop(p, "Offset"), locals = Prop(p, "Locals"), room = Prop(p, "RoomId"), number = Prop(p, "ScriptNumber"),
            next = ((LoomScriptDisassembly)Prop(p, "Script")!).Instructions.FirstOrDefault(i => i.Offset == (int)Prop(p, "Offset")!)
        }).ToArray();
        states.Add(new { frame, room = session.CurrentRoomId, session.InCutscene, session.CameraX,
            session.Actors, session.Messages.HaveMsg, pending, globals = session.Snapshot.KnownGlobalVariables,
            boxes = session.CurrentWalkBoxes?.Boxes.Select(b => new { b.Index, flags = session.CurrentWalkBoxes.GetFlags(b.Index) }).ToArray() });
    }
    if (frame < 6000) session.AdvanceFrame();
}
Save("ega-room38-trace", new { states, targetBegun = session.CutscenesBegun - startBaseline,
    targetEnded = session.SessionTrace.Skip(traceStart).Count(t => t.StartsWith("endCutscene at ")), session.InCutscene,
    trace = session.SessionTrace.Skip(traceStart).ToArray(), uncertainties = session.Snapshot.Uncertainties.TakeLast(25).ToArray() });
Console.WriteLine($"EGA Room 38 after 6000 frames: room {session.CurrentRoomId}, cutscene {session.InCutscene}; pending trace saved.");
