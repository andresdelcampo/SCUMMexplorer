using SCUMMexplorer.Core.Domain;

// DemoProbe <folder> index: the index directory entries by kind, and whether each starts a resource of its container (XOR 0xFF chain), lies inside one, or has no container.
static class IndexDump
{
    public static void Run(string folder)
    {
        var entries = new LflIndexReader().Read(Path.Combine(folder, "00.LFL"));
        var chains = new Dictionary<int, IReadOnlyList<LflChainResource>?>();
        foreach (var file in Directory.GetFiles(folder, "*.LFL").OrderBy(f => f))
        {
            var c = LflResourceChain.Read(File.ReadAllBytes(file));
            var end = c.Count == 0 ? 0 : c[^1].Start + c[^1].Length;
            Console.WriteLine($"chain {Path.GetFileName(file)}: {c.Count} resources, covers {end} of {new FileInfo(file).Length}");
        }
        var scanned = new SCUMMexplorer.Core.Discovery.GameFolderScanner().Scan(folder);
        foreach (var costume in scanned.Resources.Where(r => r.Kind == ResourceKind.Costume && r.ResourceId is 1 or 2 or 3))
            try { Console.WriteLine($"costume {costume.ResourceId}: " + (SCUMMexplorer.Core.Scumm.ScummCostume.Load(scanned, costume.ResourceId!.Value) is { } c ? "loads" : "null")); }
            catch (Exception ex) { Console.WriteLine($"costume {costume.ResourceId}: {ex.GetType().Name}: {ex.Message}"); }
        foreach (var sound in scanned.Resources.Where(r => r.Kind == ResourceKind.Sound && r.ResourceId is 41 or 40 or 3))
            try { Console.WriteLine($"sound {sound.ResourceId}: " + new ScummAdlibAudio().ReadLifetime(sound)); }
            catch (Exception ex) { Console.WriteLine($"sound {sound.ResourceId}: {ex.GetType().Name}: {ex.Message}"); }
        var lifetimes = scanned.Resources.Where(r => r.Kind == ResourceKind.Sound).Select(r => (r, l: new ScummAdlibAudio().ReadLifetime(r))).ToArray();
        Console.WriteLine($"sounds kept {lifetimes.Length}: lifetime Unknown {lifetimes.Count(x => x.l.Kind == ScummSoundLifetimeKind.Unknown)}");
        foreach (var group in entries.Where(e => e.Kind != ResourceKind.Room).GroupBy(e => (e.ContainerFileNumber, e.Offset)).Where(g => g.Select(e => e.Kind).Distinct().Count() > 1))
            Console.WriteLine($"shared start {group.Key.ContainerFileNumber:00}.LFL @0x{group.Key.Offset:X4}: {string.Join(", ", group.Select(e => $"{e.Kind} {e.ResourceId}"))}");
        foreach (var kind in new[] { ResourceKind.Script, ResourceKind.Costume, ResourceKind.Sound })
        {
            var counts = new SortedDictionary<string, int>();
            Console.WriteLine($"== {kind}");
            foreach (var e in entries.Where(x => x.Kind == kind).OrderBy(x => x.ContainerFileNumber).ThenBy(x => x.Offset))
            {
                var path = Path.Combine(folder, $"{e.ContainerFileNumber:00}.LFL");
                if (!chains.TryGetValue(e.ContainerFileNumber, out var chain))
                    chains[e.ContainerFileNumber] = chain = File.Exists(path) ? LflResourceChain.Read(File.ReadAllBytes(path)) : null;
                string state;
                if (chain is null) state = "no container file";
                else if (chain.Any(c => c.Start == e.Offset)) state = "starts a resource";
                else if (chain.Any(c => c.Start < e.Offset && e.Offset < c.Start + c.Length)) state = "inside a resource (leftover)";
                else state = "outside the chain";
                counts[state] = counts.GetValueOrDefault(state) + 1;
                if (state != "starts a resource") Console.WriteLine($"  {kind} {e.ResourceId,3} {e.ContainerFileNumber:00}.LFL @0x{e.Offset:X4}: {state}");
            }
            Console.WriteLine("  => " + string.Join(", ", counts.Select(p => $"{p.Value} {p.Key}")));
        }
    }
}
