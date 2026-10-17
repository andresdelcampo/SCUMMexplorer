using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Scumm;
using SCUMMexplorer.Core.Domain;
foreach(var folder in args) {
var game=new GameFolderScanner().Scan(folder);
var colors=ScummV1ActorColors.Read(game);
var colorList=game.Resources.Where(r=>r.Kind==ResourceKind.Costume&&r.ResourceId.HasValue).Select(r=>r.ResourceId!.Value).Distinct().Order().Select(colors.ColorOf).ToArray();
Console.WriteLine($"COLORS: total={colorList.Length}, derived={colorList.Count(c=>c.Basis!=ScummV1ColorBasis.Estimate)}, estimates={string.Join(",",colorList.Where(c=>c.Basis==ScummV1ColorBasis.Estimate).Select(c=>c.Costume))}");
var data=new ScummV1DataSource(game);
var session=new ScummGameSession(game,data.ReadIndexedObjects(),data:data);
Console.WriteLine($"== {folder}\nBoot {session.BootReport}; control {session.PlayerHasControl}; cutscene {session.InCutscene}");
for(int f=0;f<500;f++) session.AdvanceFrame();
foreach(var line in session.SessionTrace.Where(s=>!s.Contains("waits")&&!s.Contains("delay ends")).TakeLast(35)) Console.WriteLine(line);
Console.WriteLine("PENDING:"); foreach(var line in session.PendingScriptSummary)Console.WriteLine(line);
Console.WriteLine("UNCERTAIN:"); foreach(var line in session.Snapshot.Uncertainties.Where(s=>!s.Contains("seeded pick")&&!s.Contains("when it runs again")).Distinct().Take(30))Console.WriteLine(line);
}
