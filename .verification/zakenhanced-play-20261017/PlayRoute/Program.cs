using SCUMMexplorer.Core.Discovery;

// Genuine boot and screen inputs only. No scene visits, direct sentence calls, or state edits.
var game = new GameFolderScanner().Scan(args[0]);
var data = ScummDataSources.For(game);
if (args.Length > 2 && args[2] == "source")
{
    var source = new List<string>();
    foreach (var id in data.GlobalScriptIds())
    {
        source.Add($"== Global {id}");
        source.AddRange(data.GlobalScript(id)?.Instructions.Select(i => $"{i.Offset:X4} {i.Operation} {i.Operands}") ?? []);
    }
    foreach (var room in game.Rooms.Select(r => r.ResourceId!.Value))
    {
        var raw = data.ReadRoom(room);
        var verbs = data.ReadObjectVerbs(room, raw.Objects);
        foreach (var record in raw.Objects)
        foreach (var entry in verbs.GetValueOrDefault(record.Id) ?? [])
        {
            source.Add($"== Room {room} object {record.Id} {ScummTextDecoder.DecodeGlyphs(raw.ObjectNames.GetValueOrDefault(record.Id) ?? [])} entry {entry}");
            source.AddRange(data.ReadObjectVerb(room, record, entry.VerbId, out _).Instructions.Select(i => $"{i.Offset:X4} {i.Operation} {i.Operands}"));
        }
    }
    File.WriteAllLines(args[1], source);
    return;
}
var session = ScummGameSession.BootForPlay(game, out _);
var lines = new List<string>();
void Log(string label)
{
    lines.Add($"== {label} room={session.Snapshot.KnownGlobalVariables.GetValueOrDefault(ScummVars.Room)} input={session.UserInputOn} cutscene={session.InCutscene} screen={session.MainScreen} camera={session.ViewLeft}");
    lines.AddRange(session.Verbs.Select(v => $"verb {v}"));
    lines.AddRange(session.PlayVerbArea().Items.Select(v => $"hit {v}"));
    lines.AddRange(session.Messages.OnScreen.Select(m => $"message {m.Text}"));
    lines.AddRange(session.Snapshot.ObjectOwners.Where(p => p.Value == 1).Select(p => $"owner {p.Key}={p.Value}"));
    lines.AddRange(new[] { 0, 4, 8, 9, 10, 26, 27, 28, 29, 30, 31, 32, 33, 35 }.Select(n => $"var{n}={session.Snapshot.KnownGlobalVariables.GetValueOrDefault($"var[0x{n:X3}]")}"));
    lines.AddRange(session.Actors.Where(a => a.Number == 1).Select(a => $"ego room={a.Room} position={a.X},{a.Y}"));
}
Log("boot");
var commands = args.Skip(2).SelectMany(arg => arg.StartsWith('@') ? File.ReadAllLines(arg[1..]).Where(line => line.Length > 0 && !line.StartsWith('#')) : new[] { arg });
foreach (var command in commands)
{
    var fields = command.Split(',');
    if (fields[0] == "wait")
        for (var frame = 0; frame < int.Parse(fields[1]); frame++) session.AdvanceFrame();
    else if (fields[0] is "untilskip" or "untilcontrol")
    {
        bool Reached() => fields[0] == "untilskip" ? session.CutsceneSkippable : session.UserInputOn && !session.InCutscene;
        for (var frame = 0; frame < int.Parse(fields[1]) && !Reached(); frame++) session.AdvanceFrame();
        if (!Reached()) throw new InvalidOperationException($"{command}: condition not reached");
    }
    else if (fields[0] == "click")
        lines.Add($"input {session.ClickScreen(int.Parse(fields[1]), int.Parse(fields[2]), out var why)} refused={why}");
    else if (fields[0] == "key")
        lines.Add($"input {session.PressPlayKey(int.Parse(fields[1]), out var why)} refused={why}");
    else if (fields[0] == "escape")
        lines.Add($"escape {session.PressKey(27)}");
    else if (fields[0] == "roomclick")
    {
        var x = int.Parse(fields[1]) - session.ViewLeft;
        if (x is < 0 or >= 320) throw new InvalidOperationException("Requested room point is outside the current view; walk there through screen input first.");
        lines.Add($"input {session.ClickScreen(x, int.Parse(fields[2]) + session.MainScreen.Top, out var why)} refused={why}");
    }
    Log(command);
}
foreach (var room in new[] { 1, 2, 3, 5, 7 })
{
    var raw = data.ReadRoom(room);
    foreach (var record in raw.Objects)
        lines.Add($"room{room} {record} name={ScummTextDecoder.DecodeGlyphs(raw.ObjectNames.GetValueOrDefault(record.Id) ?? [])}");
}
lines.AddRange(session.SessionTrace.Where(s => !s.Contains(" waits ") && !s.Contains("delay ends")).TakeLast(150));
lines.AddRange(session.Snapshot.Uncertainties.Distinct());
File.WriteAllLines(args[1], lines);
Console.WriteLine(string.Join(Environment.NewLine, lines.Where(line => line.StartsWith("==") || line.StartsWith("ego ") || line.StartsWith("escape ")).TakeLast(20)));
