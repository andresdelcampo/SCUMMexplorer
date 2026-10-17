using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Loom;
using SCUMMexplorer.Core.Scumm;

// CutsceneList <folder> loom: every scanned LOOM trigger group (EGA or CD) with its title, category and scan fields, one line each.
// Used before and after the S7 move of the scan to Core/Scumm (17 October 2026): the two outputs must be identical.
var game = new GameFolderScanner().Scan(args[0]);
var data = ScummDataSources.For(game);
var rooms = game.Rooms.Select(r => r.ResourceId!.Value).ToList();
if (args.Length > 1 && args[1] == "loom")
{
    var triggers = LoomCutsceneVerbs.Scan(data, rooms, LoomCutsceneTitles.Doorways(game), game);
    var groups = ScummCutsceneIndex.Group(triggers);
    Console.WriteLine($"triggers {triggers.Count} groups {groups.Count} scenes {groups.Count(g => g.IsScene)}");
    foreach (var g in groups)
    {
        var f = g.First;
        Console.WriteLine($"{f.Room}/{f.ObjectId}/0x{f.VerbId:X2} | {LoomCutsceneTitles.Title(f)} | {(g.IsScene ? "Scenes" : "Dialogue")} | {f.VerbLabel} | " +
                          $"{string.Join(";", f.Scripts)} | t{f.Texts} l{f.RoomLoads} i{f.SpanInstructions} a{f.LoadsAfter} | {f.FirstText} | " +
                          $"to[{string.Join(",", f.LeadsTo)}] lt[{string.Join(",", f.LoadTargets ?? [])}] | others[{string.Join(",", g.Others.Select(o => $"{o.Room}/{o.ObjectId}/0x{o.VerbId:X2} {o.VerbLabel}"))}]");
    }
}
