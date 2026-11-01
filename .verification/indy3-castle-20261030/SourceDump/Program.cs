using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Scumm;

// Usage: SourceDump <game folder> g:<id> | e:<room> | x:<room> | l:<room>:<local> | o:<room> | find:<text>
// 30 October 2026: disassembly through the game's IScummDataSource (any version), for reading which scripts set a room's objects. Reads only.
var game = new GameFolderScanner().Scan(args[0]);
var data = ScummDataSources.For(game);
void Dump(string title, ScummScriptDisassembly? script)
{
    Console.WriteLine($"== {title}");
    if (script is null) return;
    foreach (var i in script.Instructions) Console.WriteLine($"{i.Offset:X4} {i.Operation} {i.Operands}");
    if (script.DecodeNote is not null) Console.WriteLine($"!! {script.DecodeNote}");
}
foreach (var spec in args.Skip(1))
{
    var p = spec.Split(':');
    switch (p[0])
    {
        case "g": Dump(spec, data.GlobalScript(int.Parse(p[1]))); break;
        case "e": Dump(spec, data.RoomEntryScript(int.Parse(p[1]))); break;
        case "x": Dump(spec, data.RoomExitScript(int.Parse(p[1]))); break;
        case "l": Dump(spec, data.RoomLocalScript(int.Parse(p[1]), int.Parse(p[2]))); break;
        case "o":
            var room = data.ReadRoom(int.Parse(p[1]));
            foreach (var o in room.Objects) Console.WriteLine(o);
            Console.WriteLine("locals: " + string.Join(", ", data.RoomLocalScriptIds(int.Parse(p[1]))));
            break;
        case "v":
            // v:<room>:<object>:<verb> (verb in hex)
            var vroom = int.Parse(p[1]);
            var record = data.ReadRoom(vroom).Objects.First(o => o.Id == int.Parse(p[2]));
            Dump(spec, data.ReadObjectVerb(vroom, record, Convert.ToInt32(p[3], 16), out _));
            break;
        case "visit":
            // visit:<room>: the first visit's trace lines about the ego, the camera and walks, and its uncertainties
            var booted = ScummGameSession.BootForPlay(game, out _);
            booted.VisitRoom(game.Rooms.First(r => r.ResourceId == int.Parse(p[1])));
            foreach (var line in booted.SessionTrace.Where(l => l.Contains("ego", StringComparison.OrdinalIgnoreCase) || l.Contains("camera", StringComparison.OrdinalIgnoreCase) || l.Contains("walk"))) Console.WriteLine("  " + line);
            Console.WriteLine("  ego: " + booted.Actors.FirstOrDefault(a => a.Number == 1));
            foreach (var u in booted.Snapshot.Uncertainties.Where(u => u.Contains("Camera") || u.Contains("ego"))) Console.WriteLine("  ? " + u);
            break;
        case "arr":
            // arr:<room>: the decoded loadRoomWithEgo arrivals into a room (1 November 2026)
            foreach (var item in ScummEgoArrivalIndex.For(game).Into(int.Parse(p[1]))) Console.WriteLine($"  from {item.SourceRoom}: {item.Describe()} loaded as {item.LoadedNumber}");
            break;
        case "box":
            // box:<room>:<x>:<y>[:<object>] - the boxes that hold a point (and an object's walk point), and the matrix's next box between them
            var broom = int.Parse(p[1]);
            var rd = data.ReadRoom(broom);
            var boxes = rd.WalkBoxes!;
            Console.WriteLine($"boxes: {boxes.Count}");
            int BoxOf(int bx, int by) { for (var b = 0; b < boxes.Count; b++) if (boxes.Contains(b, bx, by)) return b; return -1; }
            var (tx, ty) = (int.Parse(p[2]), int.Parse(p[3]));
            var tb = BoxOf(tx, ty);
            Console.WriteLine($"target ({tx},{ty}) in box {tb}");
            if (p.Length > 4)
            {
                var o = rd.Objects.First(item => item.Id == int.Parse(p[4]));
                var sb = BoxOf(o.WalkX, o.WalkY);
                Console.WriteLine($"object {o.Id} at ({o.X},{o.Y}) walk ({o.WalkX},{o.WalkY}) in box {sb}; next box {sb}->{tb}: {boxes.GetNextBox(sb, tb)}");
                for (var b = 0; b < boxes.Count; b++) Console.WriteLine($"  box {b} flags 0x{boxes.GetFlags(b):X2} corners {boxes.Boxes[b]} next->{tb}: {boxes.GetNextBox(b, tb)}");
                var adj = boxes.Adjust(o.WalkX, o.WalkY, true, 0, 0);
                Console.WriteLine($"adjust ({o.WalkX},{o.WalkY}) -> ({adj.X},{adj.Y}) box {adj.Box}");
            }
            break;
        case "prep":
            var session = ScummGameSession.BootForPlay(game, out _);
            var target = game.Rooms.First(r => r.ResourceId == int.Parse(p[1]));
            var prepared = session.PrepareManualRoom(target);
            Console.WriteLine($"prepare Room {p[1]}: {(prepared.Session is null ? "refused: " + prepared.Reason : "live")} after {prepared.Frames} frames");
            if (prepared.Session is ScummGameSession live) Console.WriteLine($"  input script {live.PlayInputScript}, user input {live.UserInputOn}, cutscene {live.InCutscene}");
            break;
        case "ofind":
            // ofind:<room>:<text> - object verb code in one room whose instructions contain the text
            var oroom = int.Parse(p[1]);
            var ord = data.ReadRoom(oroom);
            foreach (var (objectId, entries) in data.ReadObjectVerbs(oroom, ord.Objects))
                foreach (var entry in entries)
                {
                    var code = data.ReadObjectVerb(oroom, ord.Objects.First(o => o.Id == objectId), entry.VerbId, out _);
                    foreach (var i in code.Instructions)
                        if ($"{i.Operation} {i.Operands}".Contains(p[2])) Console.WriteLine($"object {objectId} verb 0x{entry.VerbId:X2} {i.Offset:X4} {i.Operation} {i.Operands}");
                }
            break;
        case "find":
            var needle = spec[5..];
            void Scan(string t, ScummScriptDisassembly? s) { if (s is null) return; foreach (var i in s.Instructions) if ($"{i.Operation} {i.Operands}".Contains(needle)) Console.WriteLine($"{t} {i.Offset:X4} {i.Operation} {i.Operands}"); }
            foreach (var id in data.GlobalScriptIds()) Scan($"g:{id}", data.GlobalScript(id));
            foreach (var r in game.Rooms.Where(r => r.ResourceId.HasValue).Select(r => r.ResourceId!.Value).Distinct())
            {
                Scan($"e:{r}", data.RoomEntryScript(r)); Scan($"x:{r}", data.RoomExitScript(r));
                foreach (var l in data.RoomLocalScriptIds(r)) Scan($"l:{r}:{l}", data.RoomLocalScript(r, l));
            }
            break;
    }
}
