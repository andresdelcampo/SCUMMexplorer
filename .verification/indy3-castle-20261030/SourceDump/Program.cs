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
        case "prep":
            var session = ScummGameSession.BootForPlay(game, out _);
            var target = game.Rooms.First(r => r.ResourceId == int.Parse(p[1]));
            var prepared = session.PrepareManualRoom(target);
            Console.WriteLine($"prepare Room {p[1]}: {(prepared.Session is null ? "refused: " + prepared.Reason : "live")} after {prepared.Frames} frames");
            if (prepared.Session is ScummGameSession live) Console.WriteLine($"  input script {live.PlayInputScript}, user input {live.UserInputOn}, cutscene {live.InCutscene}");
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
