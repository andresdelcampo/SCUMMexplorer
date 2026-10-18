using System.Text.Json;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Scumm;
using SCUMMexplorer.Core.Maniac;

var folder = args.ElementAtOrDefault(0) ?? @"D:\Juegos\Lucas\mmansionE ega es";
var output = args.ElementAtOrDefault(1) ?? ".verification/maniacenhanced-usewith-20261018/output-before";
Directory.CreateDirectory(output);
var armed = false;
var observed = new List<ScummScriptObservation>();
var observer = new ScummScriptObserver(o => { if (armed && observed.Count < 2500) observed.Add(o); }, includeSite: _ => armed,
    includeKey: key => key.StartsWith("var[", StringComparison.Ordinal));
var game = new GameFolderScanner().Scan(folder);
var data = (ScummV2DataSource)ScummDataSources.For(game);
var session = new ScummGameSession(game, data.ReadIndexedObjects(), observer: observer, data: data);
ManiacFirstVisit.Prepare(game, session);
void Frames(int n) { for(var f=0; f<n; f++) session.AdvanceFrame(); }
var report = new List<string>();
void State(string label)
{
    report.Add(label + ": room=" + session.CurrentRoomId + " control=" + session.PlayerHasControl +
        " sentence=" + ScummTextDecoder.DecodeGlyphs(session.PlaySentenceGlyphs()) +
        " vars=" + string.Join(",", session.Snapshot.KnownGlobalVariables.Where(v => v.Key is "var[0x000]" or "var[0x008]" or "var[0x009]" or "var[0x00A]" or "var[0x01A]" or "var[0x01B]" or "var[0x01C]" or "var[0x01D]" or "var[0x020]" or "var[0x021]" or "var[0x023]" or "var[0x032]")));
}
void Click(int x,int y)
{
    var input = session.ClickScreen(x,y,out var refusal);
    report.Add($"Click({x},{y})={input}; refusal={refusal}");
    Frames(5); State("after click");
}
session.VisitRoom(game.Rooms.First(r=>r.ResourceId==2)); session.Settle();
session.ActivateObjectVerb(142,14); session.ActivateObjectVerb(142,1); Frames(120);
session.VisitRoom(game.Rooms.First(r=>r.ResourceId==7)); Frames(10);
session.ActivateObjectVerb(84,14); Frames(10);
State("prepared");
report.Add(JsonSerializer.Serialize(session.CaptureInventoryStatus().Items));
armed=true;
var use=session.PlayVerbArea().Items.Single(v=>v.VerbId==11 && v.Mode==1);
Click(use.X+4,use.Y+3);
Click(184,session.MainScreen.Bottom+36);
Click(8,session.MainScreen.Bottom+44);
Click(8,session.MainScreen.Bottom+4);
Click(8,session.MainScreen.Bottom+44); // Script4 +01F2: repeated second object executes the composed sentence.
Frames(20); State("endpoint");
var on=session.PlayVerbArea().Items.Single(v=>v.VerbId==4 && v.Mode==1);
Click(on.X+4,on.Y+3);
Click(184,session.MainScreen.Bottom+36);
Click(184,session.MainScreen.Bottom+36);
Frames(20); State("switch-on");
session.VisitRoom(game.Rooms.First(r=>r.ResourceId==5)); Frames(20);
session.PointAtScreen((179,79));
report.Add($"LIGHTS {session.CaptureRoom().Lights}; strips {session.CaptureRoom().Flashlight}; nonzero {new ScummPlayScreen(game,session,data.ReadIndexedObjects().States).Scene()!.Compose().Count(v=>v!=0)}");
File.WriteAllLines(Path.Combine(output,"inputs.txt"),report);
File.WriteAllLines(Path.Combine(output,"observations.jsonl"),observed.Select(o=>JsonSerializer.Serialize(o)));
File.WriteAllLines(Path.Combine(output,"instructions.txt"),observed.Where(o=>o.Site?.Stage=="instruction").Select(o=>$"{o.Sequence} Script{o.Site!.Script} {o.Site.Context} +{o.Site.Offset:X4}: {o.Site.Operation} {o.Site.Operands}; "+string.Join(",",o.Values.Where(v=>v.Key is "var[0x020]" or "var[0x021]" or "var[0x023]" or "var[0x01A]" or "var[0x01B]" or "var[0x01C]" or "var[0x01D]" or "var[0x032]"))));
File.WriteAllText(Path.Combine(output,"sources.md"),data.GlobalScript(4)!.Format()+"\n\n"+data.GlobalScript(2)!.Format());
Console.WriteLine(string.Join("\n",report));
