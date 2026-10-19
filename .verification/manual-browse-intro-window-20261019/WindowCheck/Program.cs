using System.IO;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Loom;
using SCUMMexplorer.Core.Scumm;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        // This source-only command does not create App/MainWindow or execute game code.
        if (args[0] == "window") { WindowProof.Run(args); return; }
        if (args[0] == "census")
        {
            var censusOutput = Path.GetFullPath(args[2]); Directory.CreateDirectory(censusOutput);
            var census = new List<string>();
            foreach (var fixture in new[] { "indy3 ega en", "indy3 vga en", "monkey1 ega en", "monkey1 vga en", "mi2 talkie en", "indy4 vga en", "mmansionE ega es", "mmansion ega en", "zakE ega en", "zak ega en" })
            {
                var library = new GameFolderScanner().Scan(Path.Combine(args[1], fixture)); var data = ScummDataSources.For(library);
                var indexed = data.GlobalScriptIds().Contains(1); var script = indexed ? data.GlobalScript(1) : null;
                var resource = library.Resources.Any(r => r.Kind == ResourceKind.Script && r.ResourceId == 1 && r.ScriptRoom is null);
                var native = ScummProfile.For(library).PlaysThroughVerbs && indexed && resource && script is { Instructions.Count: > 0 };
                census.Add($"{GameSupport.FamilyOf(library)} | {fixture} | PlaysThroughVerbs={ScummProfile.For(library).PlaysThroughVerbs} | IndexedGlobal1={indexed} | GlobalScriptResource1={resource} | instructions={script?.Instructions.Count ?? 0} | NativeIntroAvailable={native}");
                if (!native) throw new InvalidDataException("Native intro boot source absent: " + fixture);
            }
            census.Add($"PASS: {census.Count} supplied generic families have actual indexed Script1 boot source; no MainWindow or game execution");
            File.WriteAllLines(Path.Combine(censusOutput, "intro-source-census.txt"), census); Console.WriteLine(string.Join(Environment.NewLine, census)); return;
        }
        if (args[0] != "source") throw new ArgumentException("Expected source or window mode.");
        var output = Path.GetFullPath(args[2]); Directory.CreateDirectory(output);
        var game = new GameFolderScanner().Scan(args[1]);
        var source = ScummDataSources.For(game);
        var lines = new List<string> { $"Decoded source only: {GameSupport.FamilyOf(game)}; {game.Scenes.Count()} selector scenes; no session/window/native execution" };
        void Dump(string name, ScummScriptDisassembly? script)
        {
            if (script is null) return;
            lines.Add(name);
            foreach (var instruction in script.Instructions)
                lines.Add($" +0x{instruction.Offset:X4}: {instruction.Operation} {instruction.Operands}");
        }
        foreach (var id in args.Skip(3).Select(int.Parse)) Dump($"Global Script {id}", source.GlobalScript(id));
        foreach (var id in source.GlobalScriptIds())
        {
            var script = source.GlobalScript(id);
            if (script?.Instructions.Any(i => i.Operation == "cutscene") == true && !script.Instructions.Any(i => i.Operation == "beginOverride"))
                lines.Add($"Global Script {id}: contains cutscene, no decoded beginOverride; conditional source possibility only");
        }
        if (GameSupport.FamilyOf(game) == GameFamily.ManiacEnhanced)
            foreach (var room in new[] { 1, 28 })
                foreach (var item in source.ReadRoom(room).Objects.Where(o => o.Id is 394 or 413))
                    lines.Add($"Native clickable geometry: Room {room}: {item}");
        foreach (var resource in game.Scenes)
        {
            var entry = source.RoomEntryScript(resource.ResourceId!.Value);
            if (entry?.Instructions.Any(i => i.Operation == "cutscene") == true)
                Dump($"Room {resource.ResourceId} entry begins native cutscene", entry);
        }
        if (GameSupport.FamilyOf(game) == GameFamily.MonkeyCd)
        {
            var finder = new LoomDoorwayFinder(game);
            foreach (var resource in game.Scenes)
            {
                var records = source.ReadRoom(resource.ResourceId!.Value).Objects;
                foreach (var (objectId, verbs) in source.ReadObjectVerbs(resource.ResourceId.Value, records))
                foreach (var verb in verbs.Where(v => v.VerbId == 11))
                {
                    var record = records.Last(o => o.Id == objectId);
                    var code = source.ReadObjectVerb(resource.ResourceId.Value, record, verb.VerbId, out _);
                    foreach (var target in finder.Find(resource, code).Where(t => t.RoomId is 35 or 51))
                        lines.Add($"Possible native WalkTo doorway: Room{resource.ResourceId} object{objectId} -> Room{target.RoomId} at +{target.InstructionOffset:X4} ({target.Path}); {record}");
                }
            }
        }
        File.WriteAllLines(Path.Combine(output, "source-candidates.txt"), lines);
        Console.WriteLine($"Decoded {GameSupport.FamilyOf(game)}: {lines.Count} source rows; output {output}; no MainWindow created");
    }
}
