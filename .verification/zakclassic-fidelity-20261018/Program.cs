using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

var game = new GameFolderScanner().Scan(args.Length > 0 ? args[0] : @"D:\Juegos\Lucas\zak ega en");
var colors = ScummV1ActorColors.Read(game);
if (args.Contains("boss"))
{
    var data = new ScummV1DataSource(game);
    var session = new ScummGameSession(game, data.ReadIndexedObjects(), data: data, boot: false);
    typeof(ScummSession).GetMethod("RunBootScript", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)!.Invoke(session, null);
    var frames = 0;
    while (frames < 3000 && session.Actors.Single(a => a.Number == 10).Look.Costume != 32) { session.AdvanceFrame(); frames++; }
    var boss = session.Actors.Single(a => a.Number == 10);
    if (boss.Look.Costume != 32 || boss.Look.Palette[10] != 0) throw new InvalidOperationException("Native opening boss palette was not retained at zero");
    Console.WriteLine($"PASS native reached boss assignment: frames={frames} actor=10 costume={boss.Look.Costume} palette={boss.Look.Palette[10]} room={boss.Room} visible={boss.Visible}; global card remains labelled estimate7");
    return;
}
if (args.Contains("reached"))
{
    var observations = new List<ScummScriptObservation>();
    var observer = new ScummScriptObserver(observations.Add,
        site => site.Operation == "actorOps", captureOperandBindings: true);
    var data = new ScummV1DataSource(game);
    var session = new ScummGameSession(game, data.ReadIndexedObjects(), observer: observer, data: data);
    if (args.Length > 2 && int.TryParse(args[1], out var previewRoom))
    {
        session.VisitRoom(game.Rooms.Single(r => r.ResourceId == previewRoom));
        for (var frame = 0; frame < 1200; frame++)
        {
            if (previewRoom != 26 || session.Actors.Where(a => a.Number is 3 or 4).All(a => a.Look.Costume == 7)) break;
            session.AdvanceFrame();
        }
        Console.WriteLine($"STATE-PREVIEW room={previewRoom}: selected without playing prerequisites; observation only, not a normal-route proof");
    }
    var palettes = new Dictionary<int, int>();
    var costumes = Enumerable.Range(0, 13).ToDictionary(i => i, i => i);
    foreach (var o in observations.Where(o => o.Site?.Stage == "instruction"))
    {
        var m = System.Text.RegularExpressions.Regex.Match(o.Site!.Operands ?? "", @"^(0x[0-9A-F]+|var\[0x[0-9A-F]+\]), (0x[0-9A-F]+|var\[0x[0-9A-F]+\]), sub=0x(02|04)$");
        if (!m.Success) continue;
        int Value(string operand)
        {
            if (operand.StartsWith("0x")) return Convert.ToInt32(operand[2..], 16);
            var value = o.Values[operand];
            return operand == "var[0x00E]" ? o.Values[$"var[0x{value:X3}]"] : value;
        }
        var actor = Value(m.Groups[1].Value); var value = Value(m.Groups[2].Value);
        if (m.Groups[3].Value == "02") palettes[actor] = value;
        else costumes[actor] = value;
        Console.WriteLine($"REACHED seq={o.Sequence} script={o.Site.Script} context={o.Site.Context} offset={o.Site.Offset:X4} actor={actor} costume={costumes[actor]} palette={palettes.GetValueOrDefault(actor)} operation={o.Site.Operands} estimated={o.PathEstimated || o.Estimated.Count > 0}");
    }
    foreach (var a in session.Actors.Where(a => a.Number < 13))
        if (a.Look.Palette[a.Number] != palettes.GetValueOrDefault(a.Number)) throw new InvalidOperationException($"Observed palette mismatch for actor {a.Number}: source={palettes.GetValueOrDefault(a.Number)} native={a.Look.Palette[a.Number]}");
    Console.WriteLine("PASS all reached native actor palettes match the copied source-operation timeline");
    Console.WriteLine($"BOOT frames={session.BootReport!.Frames} capped={session.BootReport.HitFrameLimit} room={session.CurrentRoomId} control={session.UserInputOn} cutscene={session.InCutscene}");
    foreach (var a in session.Actors)
        Console.WriteLine($"BOOT-ACTOR actor={a.Number} costume={a.Look.Costume} palette={a.Look.Palette[a.Number]} room={a.Room} visible={a.Visible} paletteEstimate={a.Look.PaletteEstimate}");
    return;
}
foreach (var id in Enumerable.Range(1, 35))
{
    var x = colors.ColorOf(id);
    Console.WriteLine($"costume {id}: suit={x.Suit} basis={x.Basis} alternatives={x.Alternatives} {x.Evidence}");
}
Console.WriteLine($"SETTINGS={colors.Settings.Count} VARIABLE={colors.UnresolvedInstructions}");
foreach (var s in colors.Settings) Console.WriteLine($"SETTING {s.Source} @{s.Offset:X4} actor={s.ActorOperand} value={s.Value} costume={s.IsCostume}");
var estimates = Enumerable.Range(1, 35).Where(id => colors.ColorOf(id).Basis == ScummV1ColorBasis.Estimate).ToHashSet();
var sources = colors.Settings.Where(s => s.IsCostume && estimates.Contains(s.Value ?? -1) || s.Actor is null).Select(s => s.Source).ToHashSet();
var reader = new ScummScriptReader(ScummScriptDialect.V1);
void Dump(ScummScriptDisassembly d, string source)
{
    if (!sources.Contains(source) && !(args.Contains("all"))) return;
    Console.WriteLine($"=== {source}");
    foreach (var i in d.Instructions) Console.WriteLine($"{i.Offset:X4} {i.Bytes} {i.Operation} {i.Operands}");
}
foreach (var r in game.Resources.Where(r => r.Kind == ResourceKind.Script && r.ResourceId.HasValue && r.Offset.HasValue))
{
    var code = ScummOldScripts.ReadGlobalScript(LflEncoding.DecodeXorFF(File.ReadAllBytes(r.FilePath)), r.Offset!.Value);
    if (code is not null) Dump(reader.DecodeCode(code, r.ResourceId!.Value, r.ContainerFileNumber ?? 0, r.Offset.Value), r.Name);
}
foreach (var room in game.Rooms.Where(r => r.ResourceId.HasValue))
foreach (var s in ScummOldScripts.ReadRoomScripts(LflEncoding.DecodeXorFF(File.ReadAllBytes(room.FilePath)), room.ResourceId!.Value))
    Dump(reader.DecodeCode(s.Code, s.ObjectId ?? room.ResourceId.Value, s.Container, s.Offset, s.StartOffset), s.Label);
