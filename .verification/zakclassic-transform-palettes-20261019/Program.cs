using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

var game = new GameFolderScanner().Scan(@"D:\Juegos\Lucas\zak ega en");
var reader = new ScummScriptReader(ScummScriptDialect.V1);
var selected = args.ToHashSet(StringComparer.OrdinalIgnoreCase);
if (args.FirstOrDefault() == "preview")
{
    foreach (var room in args.Skip(1).Select(int.Parse))
    {
        var reached = new List<ScummScriptObservation>();
        var observer = new ScummScriptObserver(reached.Add, site => site.Operation == "actorOps", captureOperandBindings: true);
        var data = new ScummV1DataSource(game);
        var session = new ScummGameSession(game, data.ReadIndexedObjects(), observer: observer, data: data);
        var before = session.Actors.ToDictionary(a => a.Number, a => a.Look.Palette.ToArray());
        reached.Clear();
        session.VisitRoom(game.Rooms.Single(r => r.ResourceId == room));
        for (var f = 0; f < 500; f++) session.AdvanceFrame();
        Console.WriteLine($"PREVIEW room={room} current={session.CurrentRoomId}: direct room preview, native opening only; no story prerequisites assigned");
        foreach (var o in reached.Where(o => o.Site?.Stage == "instruction"))
            Console.WriteLine($"REACHED script={o.Site!.Script} context={o.Site.Context} offset={o.Site.Offset:X4} operands={o.Site.Operands} bindings={string.Join(';',o.Values.Where(p => p.Key is "var[0x000]" or "var[0x00E]" || (o.Values.TryGetValue("var[0x00E]", out var indirect) && p.Key == $"var[0x{indirect:X3}]")).Select(p=>$"{p.Key}={p.Value}"))} estimated={o.PathEstimated || o.Estimated.Count>0}");
        foreach (var a in session.Actors.Where(a=>a.Number is 1 or 2 or 5 or 6 or 10))
            Console.WriteLine($"ACTOR id={a.Number} costume={a.Look.Costume} suit={a.Look.Palette[a.Number]} room={a.Room} visible={a.Visible}");
        var target = room == 42 ? 6 : 1;
        if (!session.Actors.Single(a => a.Number == target).Look.Palette.SequenceEqual(before[target]))
            throw new InvalidOperationException($"Native actor {target} palette changed unexpectedly in this disclosed preview");
        if (room == 42)
        {
            session.ActivateObjectVerb(629, 253);
            var bird = session.Actors.Single(a => a.Number == 6);
            if (bird.Look.Costume != 14 || !bird.Look.Palette.SequenceEqual(before[6]))
                throw new InvalidOperationException("Explicit perched-bird source exploration did not retain palette");
            Console.WriteLine($"EXPLORATION object=629 verb=FD costume={bird.Look.Costume} suit={bird.Look.Palette[6]}; direct source verb, no earned mind-transfer prerequisites asserted");
        }
        Console.WriteLine($"PASS preview={room}: actor={target} palette retained; source estimates retained");
    }
    return;
}
void Dump(ScummScriptDisassembly d, string source)
{
    if (!selected.Contains("all") && !selected.Contains(source)) return;
    Console.WriteLine($"=== {source}");
    foreach (var i in d.Instructions) Console.WriteLine($"{i.Offset:X4} {i.Operation} {i.Operands}");
}
foreach (var r in game.Resources.Where(r => r.Kind == ResourceKind.Script && r.ResourceId.HasValue && r.Offset.HasValue))
{
    var code = ScummOldScripts.ReadGlobalScript(LflEncoding.DecodeXorFF(File.ReadAllBytes(r.FilePath)), r.Offset!.Value);
    if (code is not null) Dump(reader.DecodeCode(code, r.ResourceId!.Value, r.ContainerFileNumber ?? 0, r.Offset.Value), r.Name);
}
foreach (var room in game.Rooms.Where(r => r.ResourceId.HasValue))
foreach (var s in ScummOldScripts.ReadRoomScripts(LflEncoding.DecodeXorFF(File.ReadAllBytes(room.FilePath)), room.ResourceId!.Value))
    Dump(reader.DecodeCode(s.Code, s.ObjectId ?? room.ResourceId.Value, s.Container, s.Offset, s.StartOffset), s.Label);
