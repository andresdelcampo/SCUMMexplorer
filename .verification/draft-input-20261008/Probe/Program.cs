using System.Text.Json;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using SCUMMexplorer.Core.Formats;

// Independent catalog-prepared sessions; actual click/key dispatch, with explicit staff/note setup.
// No native ScummVM interaction, story edits, placements, draft-table edits or coverage promotion.
var root = Path.GetFullPath(args[0]);
var output = Path.GetFullPath(args[1]);
Directory.CreateDirectory(output);
var reports = new List<object>();
var failures = new List<string>();
int targetRoom = args.Length > 2 && int.TryParse(args[2], out var requestedRoom) ? requestedRoom : 2;
foreach (var (edition, relative) in new[] { ("ega-en", "loom ega mt32 en"), ("ega-es", "ALTERNATIVE LOOMS/loom ega mt32 es"), ("vga", "loom vga en") })
{
    var game = new GameFolderScanner().Scan(Path.Combine(root, relative));
    var data = ScummDataSources.For(game);
    var source = new List<string>();
    var lifetime = new List<string>();
    void Inspect(string label, LoomScriptDisassembly? script)
    {
        if (script is null) return;
        foreach (var i in script.Instructions.Where(i => i.Operation is "stopObjectScript" or "pickupObject"))
            lifetime.Add($"{label} +{i.Offset:X4} {i.Operation} {i.Operands}");
    }
    foreach (var id in data.GlobalScriptIds())
    {
        try
        {
            var script = data.GlobalScript(id);
            Inspect($"global {id}", script);
            if (id is 6 or 7 or 10 or 22 or 26) source.Add(script!.Format());
        }
        catch (InvalidDataException e) { lifetime.Add($"global {id}: {e.Message}"); }
    }
    foreach (var room in game.Rooms.Select(r => r.ResourceId!.Value))
    {
        Inspect($"room {room} entry", data.RoomEntryScript(room));
        Inspect($"room {room} exit", data.RoomExitScript(room));
        foreach (var local in data.RoomLocalScriptIds(room))
        {
            var script = data.RoomLocalScript(room, local);
            Inspect($"room {room} local {local}", script);
            if (room is 9 or 41 or 69) source.Add(script!.Format());
        }
        var rd = data.ReadRoom(room);
        foreach (var (id, verbs) in data.ReadObjectVerbs(room, rd.Objects))
        foreach (var verb in verbs.Where(v => !v.IsFallback))
        {
            var obj = rd.Objects.Single(o => o.Id == id);
            var script = data.ReadObjectVerb(room, obj, verb.VerbId, out _);
            Inspect($"room {room} object {id} verb {verb.VerbId:X2}", script);
            if (verb.VerbId is >= 0x4A and <= 0x51)
                source.Add($"room {room} object {id} verb {verb.VerbId:X2}\n{script.Format()}");
        }
    }
    File.WriteAllLines(Path.Combine(output, edition + "-source.txt"), source);
    File.WriteAllLines(Path.Combine(output, edition + "-lifetimes.txt"), lifetime);
    if (args.Length > 2 && args[2] == "source") continue;
    var indexed = game.Vga?.ReadIndexedObjects() ?? new LflObjectStateReader().ReadSnapshot(Path.Combine(game.FolderPath, "00.LFL"));
    foreach (var reverse in new[] { false, true })
    {
        var s = new LoomExplorationSession(game, indexed, completeBoot: game.Id == GameId.Loom);
        int frames = 0;
        var playback = new List<object>();
        void Advance(int count)
        {
            for (int f = 0; f < count; f++)
            {
                s.AdvanceFrame(); frames++;
                if (s.DraftPlaying)
                {
                    var actor = s.Actors.Single(a => a.Number == 1);
                    playback.Add(new { timelineFrame = frames, actor.Look.Costume, actor.Look.Frame, actor.Look.Facing,
                        sounds = s.Snapshot.ActiveSounds.Order().ToArray() });
                }
            }
        }
        void Settle(int max = 3000)
        {
            int quiet = 0;
            for (int f = 0; f < max && quiet < 30; f++)
            {
                Advance(1);
                quiet = !s.InCutscene && !s.DraftPlaying && !s.EgoMoving && !s.CameraMoving && s.Messages.HaveMsg == 0 ? quiet + 1 : 0;
            }
        }
        s.VisitRoomById(targetRoom); Settle();
        s.UpdateStaffPossession(true); s.UpdateAvailableNotes(8);
        var rd = data.ReadRoom(targetRoom);
        var target = data.ReadObjectVerbs(targetRoom, rd.Objects).First(pair => pair.Value.Any(v => v.VerbId == 0x3A && !v.IsFallback) && pair.Value.Any(v => v.VerbId == 0x3B && !v.IsFallback) && Touchable(pair.Key));
        bool Touchable(int id)
        {
            var record = rd.Objects.Single(o => o.Id == id);
            var snapshot = s.Snapshot;
            return LoomObjectHitTest.Find(rd.Objects, snapshot.ObjectStates, snapshot.ObjectPositions, snapshot.ObjectClasses,
                record.X + record.Width / 2, record.Y + record.Height / 2).Found == id;
        }
        var obj = rd.Objects.Single(o => o.Id == target.Key);
        var beforeAim = s.SessionTrace.Count;
        if (targetRoom == 2) { s.Click(obj.X + obj.Width / 2, obj.Y + obj.Height / 2); Settle(); }
        // Room 02's native box-change watcher can interrupt the first walk. A later ordinary
        // click continues it after that scene; keep both inputs visible in the chronology.
        s.Click(obj.X + obj.Width / 2, obj.Y + obj.Height / 2); Settle();
        var draft = s.GameStatus.Drafts.Single(d => d.ForwardVerb == 0x3A);
        var notes = reverse ? draft.Notes.Reverse().ToArray() : draft.Notes;
        int from = s.SessionTrace.Count;
        int messagesFrom = s.Messages.Log.Count;
        var aimedBefore = s.Snapshot.KnownGlobalVariables.GetValueOrDefault(game.Id == GameId.LoomVga ? "var[0x099]" : "var[0x067]");
        foreach (int note in notes) { s.PressKey("qwertyui"[note - 1]); Advance(8); }
        Settle(6000);
        var trace = s.SessionTrace.Skip(from).ToArray();
        var boundaries = trace.Where(t => t.StartsWith("cutscene at ") || t.StartsWith("endCutscene at ")).ToArray();
        var dispatched = trace.Where(t => t.Contains("startObject runs object ") && !t.EndsWith("verb 0x39")).ToArray();
        File.WriteAllLines(Path.Combine(output, $"{edition}-{(reverse ? "reverse" : "forward")}-trace.txt"), s.SessionTrace.Skip(beforeAim));
        int expectedVerb = reverse ? 0x3B : 0x3A;
        bool cast = dispatched.Any(t => t.Contains($"object {obj.Id} verb 0x{expectedVerb:X2}"));
        bool balanced = boundaries.Count(t => t.StartsWith("cutscene at ")) == boundaries.Count(t => t.StartsWith("endCutscene at "));
        bool text = s.Messages.Log.Skip(messagesFrom).Any(m => m.Lines.Any(l => l.Length > 8 && l.Any(char.IsLetter)));
        if (!cast || !balanced || s.InCutscene || s.DraftPlaying || aimedBefore != obj.Id || playback.Count == 0 || !text)
            failures.Add($"{edition} room {targetRoom} {(reverse ? "reverse" : "forward")}: cast={cast}, balanced={balanced}, aimed={aimedBefore}");
        var result = new { edition, targetRoom, reverse, objectId = obj.Id, notes, draft.Origin, draft.Learned, draft.Reversible,
            aimedBefore, cast, balanced, text, playback, ego = s.Actors.Single(a => a.Number == 1),
            dispatched, frames, finalRoom = s.Snapshot.RoomId, s.InCutscene, s.DraftPlaying, boundaries,
            aimed = s.Snapshot.KnownGlobalVariables.GetValueOrDefault(game.Id == GameId.LoomVga ? "var[0x099]" : "var[0x067]"),
            messages = s.Messages.Log.Skip(messagesFrom).Where(m => !m.SourceLabel.StartsWith("Script 18")).SelectMany(m => m.Lines).ToArray(),
            uncertainties = s.Snapshot.Uncertainties.Where(u => u.Contains("unsupported") || u.Contains("unmodelled") || u.Contains("draft", StringComparison.OrdinalIgnoreCase)).ToArray() };
        reports.Add(result);
        Console.WriteLine($"{edition} {(reverse ? "reverse" : "forward")}: {string.Join("; ", dispatched)}, room {s.Snapshot.RoomId}, cutscene {s.InCutscene}, playback {s.DraftPlaying}");
    }
}
File.WriteAllText(Path.Combine(output, "report.json"), JsonSerializer.Serialize(reports, new JsonSerializerOptions { WriteIndented = true }));
File.WriteAllLines(Path.Combine(output, "failures.txt"), failures);
foreach (var failure in failures) Console.Error.WriteLine(failure);
if (failures.Count > 0) Environment.ExitCode = 1;
