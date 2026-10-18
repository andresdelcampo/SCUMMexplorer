using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Maniac;
using SCUMMexplorer.Core.Scumm;

var game = new GameFolderScanner().Scan(args.ElementAtOrDefault(0) ?? @"D:\Juegos\Lucas\mmansionE ega es");
var observations = new List<ScummScriptObservation>();
var observer = new ScummScriptObserver(observations.Add, s => s.Script is 2 or 4,
    key => new[] { "var[0x008]", "var[0x009]", "var[0x00A]", "var[0x01A]", "var[0x01B]", "var[0x01C]", "var[0x01D]", "var[0x020]", "var[0x021]", "var[0x022]", "var[0x023]" }.Contains(key));
var data = ScummDataSources.For(game);
var indexed = data is ScummV2DataSource early ? early.ReadIndexedObjects() : throw new InvalidOperationException("V1/V2 only");
var session = new ScummGameSession(game, indexed, observer: observer, data: data);
ManiacFirstVisit.Prepare(game, session);
observations.Clear();
Console.WriteLine($"Retained diagnostic reproduction, disclosed direct pickups and source previews. Fixture {game.Title}; {game.Version}; no assigned story/object/actor state.");
try { foreach (var line in NativeManiacTorch.Activate(game, session)) Console.WriteLine(line); }
catch (InvalidOperationException ex) { Console.WriteLine("RETAINED FAILURE: " + ex.Message); }
foreach (var input in session.DeliveredInput.TakeLast(5)) Console.WriteLine("DELIVERED " + input);
foreach (var o in observations.Where(o => o.Site?.Stage == "instruction" || o.UnresolvedReason is not null).TakeLast(220))
    Console.WriteLine($"OBS seq={o.Sequence} script={o.Site?.Script} stage={o.Site?.Stage} offset={o.Site?.Offset:X4} {o.Site?.Operation} {o.Site?.Operands}; {string.Join(" ", o.Values.Select(p => $"{p.Key}={p.Value}"))}; unresolved={o.UnresolvedReason}");
foreach (var note in session.Snapshot.Uncertainties.Where(n => n.Contains("not modeled") || n.Contains("getObjPreposition") || n.Contains("doSentence"))) Console.WriteLine("LIMIT " + note);
Console.WriteLine("No native success is inferred from this retained-failure reproduction.");
