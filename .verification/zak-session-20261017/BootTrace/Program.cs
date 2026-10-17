// Usage: BootTrace dump|boot|rooms|sound <game folder> <output file>
// Reads the fixture and executes only its own boot scripts. No state editing or native engine execution.
var game = new GameFolderScanner().Scan(args[1]);
var lines = new List<string> { $"{game.Title}; {game.Version}; {Path.GetFileName(args[1])}" };
if (args[0] == "rooms")
{
    var data = (ScummV2DataSource)ScummDataSources.For(game);
    lines.Add($"rooms {game.Rooms.Count()}; indexed objects {data.ReadIndexedObjects().States.Count}; global script capacity {data.GlobalScriptCount}; present scripts {data.GlobalScriptIds().Count}");
    foreach (var room in game.Rooms)
    {
        var records = data.ReadRoom(room.ResourceId!.Value).Objects;
        foreach (var duplicate in records.GroupBy(r => r.Id).Where(g => g.Count() > 1))
            lines.Add($"Room {room.ResourceId} duplicate {duplicate.Key}: {string.Join("; ", duplicate)}");
    }
}
else if (args[0] == "sound")
{
    foreach (var resource in game.Resources.Where(r => r.Kind == ResourceKind.Sound))
    {
        var life = new ScummPcSpeakerAudio().ReadV2Lifetime(resource);
        lines.Add($"{resource.ResourceId}: {life.Kind}, callbacks {life.Callbacks}, timer values {life.TimerValues?.Length}, last timer {life.TimerValues?.LastOrDefault()?.Value}, loop {life.LoopStartCallback}-{life.LoopEndCallback}: {life.Reason}");
    }
}
else if (args[0] == "dump")
{
    var reader = new ScummScriptReader(ScummProfile.For(game).Dialect);
    foreach (var resource in game.Resources.Where(r => r.Kind == ResourceKind.Script && r.Offset.HasValue))
    {
        var code = ScummOldScripts.ReadGlobalScript(LflEncoding.DecodeXorFF(File.ReadAllBytes(resource.FilePath)), resource.Offset!.Value);
        if (code is null) continue;
        lines.Add($"== Global {resource.ResourceId}");
        var script = reader.DecodeCode(code, resource.ResourceId ?? 0, resource.ResourceId ?? 0, 0);
        lines.AddRange(script.Instructions.Select(i => $"{i.Offset:X4} {i.Operation} {i.Operands}"));
    }
    foreach (var room in game.Rooms)
    foreach (var raw in ScummOldScripts.ReadRoomScripts(LflEncoding.DecodeXorFF(File.ReadAllBytes(room.FilePath)), room.ResourceId!.Value).Where(s => s.ObjectId is null))
    {
        lines.Add($"== {raw.Label}");
        var script = reader.DecodeCode(raw.Code, raw.Room ?? 0, 0, raw.StartOffset);
        lines.AddRange(script.Instructions.Select(i => $"{i.Offset:X4} {i.Operation} {i.Operands}"));
    }
}
else
{
    var data = (ScummV2DataSource)ScummDataSources.For(game);
    var session = new ScummGameSession(game, data.ReadIndexedObjects(), data: data);
    var report = session.BootReport!;
    lines.Add($"boot frames {report.Frames}; limit hit {report.HitFrameLimit}; final room {report.FinalRoom}; rooms {string.Join(",", report.RoomsEntered)}");
    lines.Add($"player control {session.PlayerHasControl}; in cutscene {session.InCutscene}");
    lines.Add("== pending");
    lines.AddRange(session.PendingScriptSummary);
    lines.Add("== trace");
    lines.AddRange(session.SessionTrace.Where(s => !s.Contains(" waits ") && !s.Contains("delay ends")));
    lines.Add("== uncertainties");
    lines.AddRange(CompactUncertainties(session.Snapshot.Uncertainties));
    Console.WriteLine(string.Join(Environment.NewLine, CompactUncertainties(session.Snapshot.Uncertainties).TakeLast(12)));
    lines.Add("== engine/source variables");
    foreach (var variable in new[] { 0, 2, 3, 4, 5, 6, 17, 25, 39, 122 })
    {
        var key = $"var[0x{variable:X3}]";
        lines.Add($"{key} = {session.Snapshot.KnownGlobalVariables.GetValueOrDefault(key)}");
    }
    lines.Add("== actors");
    lines.AddRange(session.Actors.Where(a => a.Room is not null and not 0).Select(a => $"actor {a.Number} room {a.Room} position {a.X},{a.Y} costume {a.Look.Costume}"));
    for (var i = 0; i < 120; i++) session.AdvanceFrame();
    lines.Add($"after 120 frames: room {session.CurrentRoomId}; control {session.PlayerHasControl}; cutscene {session.InCutscene}");
    lines.AddRange(CompactUncertainties(session.Snapshot.Uncertainties));
}
File.WriteAllLines(args[2], lines);
Console.WriteLine(string.Join(Environment.NewLine, lines.Take(4)));

static IEnumerable<string> CompactUncertainties(IEnumerable<string> source)
{
    var items = source.Distinct().ToArray();
    var random = items.Where(item => item.Contains("getRandomNr at", StringComparison.Ordinal)).ToArray();
    foreach (var item in items.Where(item => !item.Contains("getRandomNr at", StringComparison.Ordinal))) yield return item;
    if (random.Length == 0) yield break;
    yield return $"{random.Length} labelled getRandomNr estimate notices; first four samples follow (all other uncertainties retained).";
    foreach (var item in random.Take(4)) yield return item;
}
