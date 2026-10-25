using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Scumm;
// Do v1/v2 games start any script number at or above their global script count? (a local script would be started that way)
foreach (var folder in args)
{
    var game = new GameFolderScanner().Scan(folder);
    var data = ScummDataSources.For(game);
    var high = new SortedDictionary<int, int>();
    void Visit(ScummScriptDisassembly? s)
    {
        if (s is null) return;
        foreach (var i in s.Instructions)
            if (i.Operation is "startScript" or "chainScript" && i.Operands.StartsWith("0x") && int.TryParse(i.Operands.Split(',')[0].AsSpan(2), System.Globalization.NumberStyles.HexNumber, null, out var id) && id >= data.GlobalScriptCount)
                high[id] = high.GetValueOrDefault(id) + 1;
    }
    foreach (var id in data.GlobalScriptIds()) Visit(data.GlobalScript(id));
    foreach (var room in game.Rooms)
    {
        var r = room.ResourceId!.Value;
        Visit(data.RoomEntryScript(r)); Visit(data.RoomExitScript(r));
        var objs = data.ReadRoom(r).Objects;
        var verbs = data.ReadObjectVerbs(r, objs);
        foreach (var rec in objs) foreach (var v in verbs.GetValueOrDefault(rec.Id) ?? []) { try { Visit(data.ReadObjectVerb(r, rec, v.VerbId, out _)); } catch (InvalidDataException) { } }
    }
    Console.WriteLine($"{game.Title}: global scripts {data.GlobalScriptCount}; starts of ids >= that: {(high.Count == 0 ? "none" : string.Join(", ", high.Select(p => $"{p.Key}x{p.Value}")))}");
}
