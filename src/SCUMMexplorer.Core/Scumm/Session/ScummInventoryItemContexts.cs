using System.Globalization;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>Read-only source associations for genuinely different items with the same name.
/// A character qualifier describes the native record, never its present owner or a restriction on admin grants.</summary>
internal static class ScummInventoryItemContexts
{
    internal static IReadOnlyDictionary<int, ScummInventoryItemContext> Infer(
        IReadOnlyList<ScummInventoryCatalogItem> catalog,
        IReadOnlyList<(ScummScriptDisassembly? Script, int? Object, int? Room)> roots,
        IReadOnlyDictionary<int, ScummScriptDisassembly?> globals,
        Func<int, int, int, ScummScriptDisassembly?> objectScript,
        IReadOnlySet<int> playableActors)
    {
        var groups = catalog.Where(x => !string.IsNullOrWhiteSpace(x.Name))
            .GroupBy(x => x.Name!.Trim(), StringComparer.OrdinalIgnoreCase).Where(g => g.Count() > 1)
            .Select(g => g.Select(x => x.ObjectId).ToHashSet()).ToArray();
        var candidates = groups.SelectMany(x => x).ToHashSet();
        if (candidates.Count == 0 || playableActors.Count < 2) return new Dictionary<int, ScummInventoryItemContext>();
        var hints = new Dictionary<int, Dictionary<int, HashSet<string>>>();
        void Add(int item, int actor, string evidence)
        {
            if (!candidates.Contains(item) || !playableActors.Contains(actor)) return;
            if (!hints.TryGetValue(item, out var actors)) hints[item] = actors = [];
            if (!actors.TryGetValue(actor, out var source)) actors[actor] = source = [];
            source.Add(evidence);
        }
        static string Source(ScummScriptDisassembly script, ScummScriptInstruction instruction) =>
            $"{script.Context ?? $"Script {script.ScriptId}"} +0x{instruction.Offset:X4}: {instruction.Operation} {instruction.Operands}";
        static bool Complete(ScummScriptDisassembly? script) => script is
        { HasCompleteControlFlow: true, DecodeNote: null } && script.Issues is not { Count: > 0 };

        // A literal native recipient is a source association. Current owners and
        // mutable story variables are deliberately not inputs to this analysis.
        var fixedOwners = new ScummInventoryCatalogAnalysis(globals, objectScript, (_, _, _, _, _) => { }, _ => { },
            ownerWrite: (script, instruction, item, actor) =>
            {
                var parts = instruction.Operands.Split(',', StringSplitOptions.TrimEntries);
                if (Complete(script) && item is int id && actor is int person && parts.Length == 2 && Literal(parts[1]) == person)
                    Add(id, person, Source(script, instruction));
            });
        foreach (var root in roots.Where(x => Complete(x.Script))) fixedOwners.Scan(root.Script, root.Object, root.Room);

        // Some games keep one same-name inventory record per character. The
        // native code selects a distinct record from the recipient argument and
        // then writes that record's owner. Explore the compared actor arguments
        // with the existing bounded source-flow engine; no game/item tables.
        foreach (var root in roots.Where(x => Complete(x.Script)).DistinctBy(x =>
                     (x.Script!.Context, x.Script.ScriptId, x.Script.EntryOffset, x.Object, x.Room)))
        {
            var script = root.Script!;
            foreach (var site in script.Instructions.Where(x => x.Operation == "setOwnerOf"))
            {
                var parts = site.Operands.Split(',', StringSplitOptions.TrimEntries);
                if (parts.Length != 2 || !Variable(parts[0]) || !Variable(parts[1]) || parts[0] == parts[1]) continue;
                var argument = parts[1];
                var compared = script.Instructions.Where(x => x.Operation is "isEqual" or "isNotEqual")
                    .Select(x => x.Operands.Split(',', StringSplitOptions.TrimEntries))
                    .Where(x => x.Length >= 2 && (x[0] == argument || x[1] == argument))
                    .Select(x => Literal(x[0] == argument ? x[1] : x[0]))
                    .OfType<int>().Where(playableActors.Contains).Distinct().Order().ToArray();
                if (compared.Length < 2) continue;
                var selected = compared.ToDictionary(x => x, _ => new HashSet<int>());
                var unresolved = new HashSet<int>();
                foreach (var actor in compared)
                    foreach (var ego in playableActors)
                    {
                        if (argument == ScummVars.V2.Ego && actor != ego) continue;
                        if (argument == ScummVars.V2.ActiveObject1 && root.Object is int bound && bound != actor) continue;
                        var incoming = new Dictionary<string, int> { [ScummVars.V2.Ego] = ego, [argument] = actor };
                        if (root.Object is int active && argument != ScummVars.V2.ActiveObject1)
                            incoming[ScummVars.V2.ActiveObject1] = active;
                        var analysis = new ScummInventoryCatalogAnalysis(globals, objectScript, (_, _, _, _, _) => { },
                            note => { if (note.StartsWith("Catalog path-state bound:") || note.StartsWith("Catalog call-depth bound:") ||
                                note.StartsWith("Unresolved owner-flow:")) unresolved.Add(actor); },
                            ownerWrite: (current, instruction, item, person) =>
                            {
                                if (!ReferenceEquals(current, script) || instruction.Offset != site.Offset) return;
                                if (person != actor || item is not int id || !candidates.Contains(id)) unresolved.Add(actor);
                                else selected[actor].Add(id);
                            });
                        analysis.Scan(script, root.Object, root.Room, incoming);
                    }
                if (unresolved.Count != 0 || selected.Any(x => x.Value.Count != 1)) continue;
                var records = selected.Select(x => (Actor: x.Key, Item: x.Value.Single())).ToArray();
                if (records.Select(x => x.Item).Distinct().Count() != records.Length ||
                    !groups.Any(g => records.All(x => g.Contains(x.Item)))) continue;
                foreach (var record in records)
                    Add(record.Item, record.Actor, Source(script, site) + $"; native recipient argument {argument}=0x{record.Actor:X4} selects this inventory record");
            }
        }

        var result = new Dictionary<int, ScummInventoryItemContext>();
        foreach (var group in groups)
        {
            // A lone scripted handoff does not distinguish two otherwise
            // interchangeable same-name items. Require a complete unique group.
            if (group.Any(id => !hints.TryGetValue(id, out var actors) || actors.Count != 1)) continue;
            var choices = group.Select(id => (Id: id, Actor: hints[id].Single())).ToArray();
            if (choices.Select(x => x.Actor.Key).Distinct().Count() != choices.Length) continue;
            foreach (var choice in choices)
                result[choice.Id] = new(choice.Actor.Key, null, "Source character association; " + string.Join("; ", choice.Actor.Value.Order()));
        }
        return result;
    }

    private static bool Variable(string operand) => operand.StartsWith("var[0x", StringComparison.Ordinal) && operand.EndsWith(']');
    private static int? Literal(string operand) => operand.StartsWith("0x", StringComparison.Ordinal) &&
        int.TryParse(operand.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var value) ? value : null;
}
