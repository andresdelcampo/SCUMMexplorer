using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

// Usage: ScriptDump <game folder> <global script id>...   |   ScriptDump <game folder> room <room> <local id>
var game = new GameFolderScanner().Scan(args[0]);
var reader = new LoomScriptReader(ScummScriptDialects.For(game.Id));
void Dump(string title, LoomScriptDisassembly? d)
{
    Console.WriteLine($"== {title}");
    if (d is null) { Console.WriteLine("(none)"); return; }
    foreach (var i in d.Instructions) Console.WriteLine($"{i.Offset:X4} {i.Operation} {i.Operands}");
}
if (args[1] == "room")
    Dump($"room {args[2]} local {args[3]}", reader.DecodeRoomLocalScript(game.Rooms.Single(r => r.ResourceId == int.Parse(args[2])), int.Parse(args[3]), game.Resources.Count(r => r.Kind == ResourceKind.Script)));
else foreach (var a in args.Skip(1))
    Dump($"script {a}", reader.Decode(game.Resources.Single(r => r.Kind == ResourceKind.Script && r.ResourceId == int.Parse(a))));
