using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;

var game = new GameFolderScanner().Scan(@"D:\Juegos\Lucas\zak ega en");
var data = ScummDataSources.For(game);
void Dump(ScummScriptDisassembly script)
{
    Console.WriteLine($"SCRIPT {script.ScriptId} {script.Context}");
    foreach (var i in script.Instructions) Console.WriteLine($"+{i.Offset:X4} {i.Operation} {i.Operands}");
}
if (args[0] == "script")
{
    foreach (var id in args.Skip(1).Select(int.Parse)) if (data.GlobalScript(id) is { } script) Dump(script);
    return;
}
if (args[0] == "room")
{
    foreach (var id in args.Skip(1).Select(int.Parse))
    {
        var room = game.Rooms.Single(r => r.ResourceId == id);
        foreach (var s in ScummOldScripts.ReadRoomScripts(LflEncoding.DecodeXorFF(File.ReadAllBytes(room.FilePath)), id))
            Dump(new ScummScriptReader(ScummScriptDialect.V1).DecodeCode(s.Code, s.ObjectId ?? id, s.Container, s.Offset, s.StartOffset));
    }
    return;
}
if (args[0] == "calls")
{
    foreach (var r in game.Resources.Where(r => r.Kind == ResourceKind.Script && r.ResourceId.HasValue))
    {
        if (data.GlobalScript(r.ResourceId!.Value) is not { } script) continue;
        foreach (var i in script.Instructions.Where(i => args.Skip(1).Any(a => i.Operands?.Contains(a) == true)))
            Console.WriteLine($"Script {r.ResourceId} +{i.Offset:X4} {i.Operation} {i.Operands}");
    }
    foreach (var room in game.Rooms)
    foreach (var s in ScummOldScripts.ReadRoomScripts(LflEncoding.DecodeXorFF(File.ReadAllBytes(room.FilePath)), room.ResourceId!.Value))
    {
        var script = new ScummScriptReader(ScummScriptDialect.V1).DecodeCode(s.Code, s.ObjectId ?? room.ResourceId.Value, s.Container, s.Offset, s.StartOffset);
        foreach (var i in script.Instructions.Where(i => args.Skip(1).Any(a => i.Operands?.Contains(a) == true)))
            Console.WriteLine($"{s.Label} +{i.Offset:X4} {i.Operation} {i.Operands}");
    }
    return;
}
var observations = new List<ScummScriptObservation>();
var observer = new ScummScriptObserver(observations.Add, site => site.Operation is "actorOps" or "startScript" or "chainScript", captureOperandBindings: true);
var v1 = (ScummV1DataSource)data;
var session = new ScummGameSession(game, v1.ReadIndexedObjects(), observer: observer, data: data);
void Frames(int count) { for (var f = 0; f < count; f++) session.AdvanceFrame(); }
void Click(int x, int y) { if (session.ClickScreen(x, y, out var refusal) is null) throw new InvalidOperationException(refusal); Frames(5); }
void Verb(int id) { var v = session.PlayVerbArea().Items.Single(v => v.VerbId == id && v.Mode == 1); Click(v.X + 3, v.Y + 3); }
void Item(int id)
{
    var inventory = session.CaptureInventoryStatus();
    var items = inventory.Items.Where(i => i.OwnerId == inventory.CurrentActorId).OrderBy(i => i.Slot).ToArray();
    var index = Array.FindIndex(items, i => i.ObjectId == id);
    if (index is < 0 or >= 4) throw new InvalidOperationException("Item outside native visible inventory");
    Click(index % 2 == 0 ? 8 : 184, session.MainScreen.Bottom + 36 + 8 * (index / 2));
}
void Use(int item, int target)
{
    Verb(13); Verb(11); Item(item);
    var o = data.ReadRoom(session.CurrentRoomId!.Value).Objects.Single(o => o.Id == target);
    Click(o.X + o.Width / 2 - session.ViewLeft, o.Y + Math.Max(1, o.Height / 2) + session.MainScreen.Top);
    Click(8, session.MainScreen.Bottom + 4);
}
void Log(string action)
{
    Console.WriteLine($"STATE {action} room={session.CurrentRoomId} logical={session.Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x004]")} ego={session.Snapshot.KnownGlobalVariables.GetValueOrDefault(ScummVars.V2.Ego)} control={session.PlayerHasControl}");
    foreach (var a in session.Actors.Where(a => a.Number is >= 1 and <= 4)) Console.WriteLine($"ACTOR {a.Number} costume={a.Look.Costume} suit={a.Look.Palette[a.Number]} room={a.Room} visible={a.Visible}");
    Console.WriteLine($"SOURCE-STATE owner57={session.Snapshot.ObjectOwners.GetValueOrDefault(57)} state169={session.Snapshot.ObjectStates.GetValueOrDefault(169)} color4={session.Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x0C4]")} sentence={ScummTextDecoder.DecodeGlyphs(session.PlaySentenceGlyphs())}");
}
Log("boot");
foreach (var action in args)
{
    if (action.StartsWith("room:")) session.VisitRoom(game.Rooms.Single(r => r.ResourceId == int.Parse(action[5..])));
    if (action.StartsWith("key:")) session.PressKey(int.Parse(action[4..]));
    if (action.StartsWith("playkey:")) session.PressPlayKey(int.Parse(action[8..]), out _);
    if (action.StartsWith("frames:")) for (var f = 0; f < int.Parse(action[7..]); f++) session.AdvanceFrame();
    if (action.StartsWith("object:")) { var p = action[7..].Split(':'); session.ActivateObjectVerb(int.Parse(p[0]), int.Parse(p[1])); }
    if (action.StartsWith("use:")) { var p = action[4..].Split(':'); Use(int.Parse(p[0]), int.Parse(p[1])); }
    Log(action);
}
foreach (var o in observations.Where(o => o.Site?.Stage == "instruction"))
    Console.WriteLine($"REACHED seq={o.Sequence} script={o.Site!.Script} context={o.Site.Context} +{o.Site.Offset:X4} {o.Site.Operation} {o.Site.Operands} color4={o.Values.GetValueOrDefault("var[0x0C4]")} estimated={o.PathEstimated}");
Console.WriteLine("MESSAGES " + string.Join('|',session.Messages.OnScreen.Select(m => m.Text)));
