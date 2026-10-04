using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;
using System.Reflection;
using System.Collections;
var game = new GameFolderScanner().Scan(args[0]);
var objects = new LflObjectStateReader().ReadSnapshot(Path.Combine(args[0], "00.LFL"));
var s = new LoomExplorationSession(game, objects);
s.VisitRoom(game.Rooms.Single(r => r.ResourceId == 34));
int frames = 0; int? lastBox = null;
object? Prop(object o, string name) => o.GetType().GetProperty(name)!.GetValue(o);
string Pending() => string.Join("; ", ((IEnumerable)typeof(LoomExplorationSession).GetField("_pending", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(s)!).Cast<object>().Where(p => Prop(Prop(p, "Frame")!, "Label")!.ToString()!.Contains("local script 200")).Select(p => $"off {Prop(p, "Offset")} locals {string.Join(",", ((IReadOnlyDictionary<string, ushort>)Prop(p, "Locals")!).Select(kv => kv.Key + "=" + kv.Value))}"));
void Run(int n) { for (int i = 0; i < n; i++) { s.AdvanceFrame(); frames++; var a = s.Actors.Single(x => x.Number == 1); if (a.WalkBox != lastBox) { Console.WriteLine($"frame {frames}: box {a.WalkBox} at ({a.X},{a.Y}) camera {s.CameraX}"); lastBox = a.WalkBox; } } }
Run(200);
foreach (var (x, y) in new[] { (356, 107), (378, 134), (420, 134), (500, 134) })
{
    Console.WriteLine($"click ({x},{y})");
    s.Click(x, y); Run(600);
    var e1 = s.Actors.Single(x => x.Number == 1); Console.WriteLine($"  settled: ego ({e1.X},{e1.Y}) box {e1.WalkBox} camera {s.CameraX}"); Console.WriteLine("boundaries: " + string.Join(" | ", s.SessionTrace.Where(t => t.StartsWith("cutscene at ") || t.StartsWith("endCutscene at "))));
}
