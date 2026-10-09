using System.Text.Json;
using System.Text;
using SCUMMexplorer.Core.Discovery;

// One bounded route prefix: play the own-source opening, then aim at the sky.
// Staff/eight-note controls are explicit exploration preparation, not earned gameplay.
// No catalog visit, direct object verb, placement, story edit, native input or listening.
var game = new GameFolderScanner().Scan(Path.GetFullPath(args[0]));
var data = ScummDataSources.For(game);
var output = Path.GetFullPath(args[1]);
Directory.CreateDirectory(output);
// Own-CD callback identities for the remaining comparison sheet; no callback is invoked.
var comparisonSources = new List<object>();
foreach (int number in new[] { 8, 24, 41, 43, 9 })
{
    var room = data.ReadRoom(number);
    foreach (var (id, entries) in data.ReadObjectVerbs(number, room.Objects))
    foreach (var entry in entries.Where(e => !e.IsFallback))
    {
        var script = data.ReadObjectVerb(number, room.Objects.Single(o => o.Id == id), entry.VerbId, out _);
        var calls = script.Instructions.Where(i => i.Operation == "startScript" &&
            new[] { "0x2E,", "0x46,", "0x5B,", "0x5D,", "0x69,", "0xD2," }.Any(i.Operands.StartsWith)).ToArray();
        if (calls.Length > 0) comparisonSources.Add(new { room = number, id, verb = entry.VerbId,
            name = room.ObjectNames.TryGetValue(id, out var bytes) ? Encoding.Latin1.GetString(bytes).Trim('\0') : "",
            calls = calls.Select(i => new { i.Offset, i.Operands }) });
    }
}
File.WriteAllText(Path.Combine(output, "comparison-sources.json"),
    JsonSerializer.Serialize(comparisonSources, new JsonSerializerOptions { WriteIndented = true }));
var reports = new List<object>();
var failures = new List<string>();
int checks = 0;
void Check(bool condition, string label)
{
    checks++;
    if (!condition) failures.Add(label);
}
foreach (bool reverse in new[] { false, true })
{
    var observed = new List<ScummScriptObservation>();
    var observer = new ScummScriptObserver(observed.Add,
        includeSite: site => site.Script is 6 or 7 or 10 or 45,
        includeKey: key => key is "var[0x099]" or "var[0x098]" || key.StartsWith("local["),
        captureOperandBindings: true, captureSourceAccess: true);
    var session = new LoomExplorationSession(game, game.V4Archive!.ReadIndexedObjects(),
        completeBoot: false, playCdIntro: true, observer: observer);
    int frames = 0;
    int playing = 0;
    void Advance(int count)
    {
        for (int n = 0; n < count; n++)
        {
            session.AdvanceFrame();
            frames++;
            if (session.DraftPlaying) playing++;
        }
    }
    void Settle(int bound)
    {
        int quiet = 0;
        for (int n = 0; n < bound && quiet < 32; n++)
        {
            Advance(1);
            quiet = !session.BootRunning && !session.InCutscene && !session.DraftPlaying && !session.EgoMoving &&
                !session.CameraMoving && session.Messages.HaveMsg == 0 ? quiet + 1 : 0;
        }
    }
    Settle(6000);
    var bootEnd = new { frames, room = session.CurrentRoomId, session.BootRunning,
        ego = session.Actors.Single(a => a.Number == 1), session.CameraX, session.GameStatus,
        uncertainties = session.Snapshot.Uncertainties };
    bool ready = session.CurrentRoomId == 2 && !session.BootRunning && !session.InCutscene && !session.EgoMoving;
    Check(ready, $"{reverse}: opening settles into Room 02");
    if (!ready)
    {
        reports.Add(new { reverse, bootEnd });
        continue;
    }
    session.UpdateStaffPossession(true);
    session.UpdateAvailableNotes(8);
    var room = data.ReadRoom(2);
    var target = data.ReadObjectVerbs(2, room.Objects).Single(p => p.Value.Any(v => v.VerbId == 0x3A) &&
        p.Value.Any(v => v.VerbId == 0x3B));
    var record = room.Objects.Single(o => o.Id == target.Key);
    int start = session.SessionTrace.Count;
    session.Click(record.X + record.Width / 2, record.Y + record.Height / 2);
    Settle(3000);
    var aimEnd = new { frames, ego = session.Actors.Single(a => a.Number == 1),
        aimed = session.Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x099]"),
        named = session.Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x098]") };
    int castStart = session.SessionTrace.Count;
    int observedCastStart = observed.Count;
    int messagesStart = session.Messages.Log.Count;
    int playedStart = playing;
    var draft = session.GameStatus.Drafts.Single(d => d.ForwardVerb == 0x3A);
    var notes = reverse ? draft.Notes.Reverse().ToArray() : draft.Notes;
    foreach (int note in notes) { session.PressKey("qwertyui"[note - 1]); Advance(8); }
    Settle(6000);
    var trace = session.SessionTrace.Skip(castStart).ToArray();
    var boundaries = trace.Where(t => t.StartsWith("cutscene at ") || t.StartsWith("endCutscene at ")).ToArray();
    int verb = reverse ? 0x3B : 0x3A;
    bool dispatched = trace.Any(t => t.Contains($"startObject runs object {record.Id} verb 0x{verb:X2}"));
    bool balanced = boundaries.Count(t => t.StartsWith("cutscene at ")) == boundaries.Count(t => t.StartsWith("endCutscene at "));
    var lines = session.Messages.Log.Skip(messagesStart).Where(m => !m.SourceLabel.StartsWith("Script 18")).SelectMany(m => m.Lines).ToArray();
    var castGuards = observed.Skip(observedCastStart).Where(o => o.Site is { Script: 7, Offset: 7, Stage: "instruction" }).ToArray();
    Check(aimEnd.aimed == record.Id && aimEnd.named == record.Id, $"{reverse}: sky stays aimed and named");
    Check(aimEnd.ego.X == record.WalkX && aimEnd.ego.Y == record.WalkY, $"{reverse}: source walk point reached");
    Check(draft.Learned == true, $"{reverse}: opening pattern remains learned without a draft edit");
    Check(notes.Length == 4 && draft.ForwardVerb == 0x3A && draft.ReverseVerb == 0x3B, $"{reverse}: own draft mapping");
    Check(dispatched, $"{reverse}: actual note input dispatches the expected sky response");
    Check(playing > playedStart, $"{reverse}: draft playback is observed");
    Check(balanced, $"{reverse}: source scene boundaries balance");
    Check(boundaries.Count(t => t.StartsWith("cutscene at ")) == (reverse ? 0 : 1), $"{reverse}: scene versus ordinary refusal");
    Check(lines.Any(l => l == (reverse ? "I'd have to open it first." : "Is it over?")), $"{reverse}: expected own-source response text");
    Check(session.CurrentRoomId == 2 && !session.InCutscene && !session.DraftPlaying && !session.EgoMoving && !session.CameraMoving,
        $"{reverse}: input settles in Room 02");
    Check(castGuards.Length > 0 && castGuards.All(o => o.Values.GetValueOrDefault("local[0x002]") == 0 &&
        o.Values.GetValueOrDefault("var[0x099]") == record.Id), $"{reverse}: Script 7 distance is zero with the sky aimed");
    File.WriteAllLines(Path.Combine(output, (reverse ? "reverse" : "forward") + "-trace.txt"), session.SessionTrace.Skip(start));
    File.WriteAllText(Path.Combine(output, (reverse ? "reverse" : "forward") + "-guards.json"),
        JsonSerializer.Serialize(observed, new JsonSerializerOptions { WriteIndented = true }));
    reports.Add(new { reverse, bootEnd, record, aimEnd, notes, draft.Origin, draft.Learned, dispatched, balanced,
        playingFrames = playing - playedStart, frames, boundaries, finalRoom = session.CurrentRoomId,
        session.InCutscene, session.DraftPlaying, messages = lines,
        uncertainties = session.Snapshot.Uncertainties });
    Console.WriteLine($"{(reverse ? "reverse" : "forward")}: aimed {aimEnd.aimed}, dispatch {dispatched}, {playing-playedStart} playback frames, balanced {balanced}");
}
File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(reports, new JsonSerializerOptions { WriteIndented = true }));
File.WriteAllLines(Path.Combine(output, "failures.txt"), failures);
File.WriteAllLines(Path.Combine(output, "checks.txt"), new[] { $"{checks - failures.Count}/{checks} input assertions passed" }.Concat(failures));
Console.WriteLine($"{checks - failures.Count}/{checks} input assertions passed");
foreach (string failure in failures) Console.Error.WriteLine(failure);
Environment.ExitCode = failures.Count == 0 ? 0 : 1;
