using System.Text.Json;
using System.Text.RegularExpressions;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;

var output = Path.GetFullPath(args[1]); Directory.CreateDirectory(output);
var packets = new List<object>(); var checks = new List<string>();
var prior = args.Length > 2 && File.Exists(args[2]) ? JsonDocument.Parse(File.ReadAllText(args[2])).RootElement.EnumerateArray().Select(p => p.Clone()).ToArray() : [];
var levelsOnly = args.Length > 3 && args[3] == "levels";
var dumpOnly = args.Length > 3 && args[3] == "dump";
var chainsOnly = args.Length > 3 && args[3] is "chains" or "late";
packets.AddRange(prior.Cast<object>());
void Check(bool ok, string label) { checks.Add((ok ? "PASS " : "FAIL ") + label); File.WriteAllLines(Path.Combine(output, "checks.txt"), checks); }
foreach (var (edition, folder) in new[] { ("ega-en", "loom ega mt32 en"), ("ega-es", "ALTERNATIVE LOOMS/loom ega mt32 es"), ("vga", "loom vga en") })
{
    var game = new GameFolderScanner().Scan(Path.Combine(Path.GetFullPath(args[0]), folder)); var data = ScummDataSources.For(game);
    var indexed = game.Vga?.ReadIndexedObjects() ?? new LflObjectStateReader().ReadSnapshot(Path.Combine(game.FolderPath, "00.LFL"));
    if (dumpOnly)
    {
        foreach (var (room, local) in new[] { (4, 200), (4, 201), (46, 202), (46, 203), (46, 204), (9, 209), (9, 223), (9, 224), (23, 200), (23, 201), (23, 202), (25, 204), (25, 208) })
            File.WriteAllText(Path.Combine(output, $"{edition}-{room}-{local}.txt"), data.RoomLocalScript(room, local)?.Format());
        foreach (int room in new[] { 4, 7, 23, 25, 46, 50 })
        {
            File.WriteAllText(Path.Combine(output, $"{edition}-{room}-entry.txt"), data.RoomEntryScript(room)?.Format());
            var objects = data.ReadRoom(room).Objects;
            foreach (var (id, verbs) in data.ReadObjectVerbs(room, objects))
                foreach (var verb in verbs.Where(v => !v.IsFallback))
                    File.WriteAllText(Path.Combine(output, $"{edition}-{room}-{id}-{verb.VerbId:X2}.txt"), data.ReadObjectVerb(room, objects.Single(o => o.Id == id), verb.VerbId, out _)?.Format());
        }
        File.WriteAllText(Path.Combine(output, edition + "-global-106.txt"), data.GlobalScript(106)?.Format());
        var writerLines = new List<string>();
        var guards = new[] { data.RoomEntryScript(23), data.RoomEntryScript(25) }
            .SelectMany(s => s!.Instructions).Where(i => i.Operation == "equalZero" && i.Operands.StartsWith("bit["))
            .Select(i => Regex.Match(i.Operands, @"bit\[0x[0-9A-F]+\]").Value);
        foreach (var guard in guards)
            writerLines.AddRange(LoomSceneStateScanner.For(game).FindConstantWriters(guard, 1).Select(w => $"{guard}: {JsonSerializer.Serialize(w)}"));
        File.WriteAllLines(Path.Combine(output, edition + "-gate-writers.txt"), writerLines);
        continue;
    }
    var staffTest = data.GlobalScript(17)!.Instructions.Last(i => i.Operation == "notEqualZero" && i.Operands.StartsWith("bit["));
    var staffKey = Regex.Match(staffTest.Operands, @"bit\[0x[0-9A-F]+\]").Value;
    var pickup = LoomSceneStateScanner.For(game).FindConstantWriters(staffKey, 1).OrderBy(w => w.Room).First(w => w.ObjectId is not null && w.VerbId is not null);
    var baseline = new LoomExplorationSession(game, indexed);
    File.WriteAllText(Path.Combine(output, edition + "-initial-status.json"), JsonSerializer.Serialize(baseline.GameStatus, new JsonSerializerOptions { WriteIndented = true }));
    var candidates = new List<Lesson>(); var levels = new List<Lesson>();
    var scripts = new Dictionary<(int, int), LoomScriptDisassembly?>();
    LoomScriptDisassembly? Script(int room, int id)
    {
        var key = (id < data.GlobalScriptCount ? 0 : room, id);
        if (!scripts.TryGetValue(key, out var script))
            try { scripts[key] = script = id < data.GlobalScriptCount ? data.GlobalScript(id) : data.RoomLocalScript(room, id); }
            catch (InvalidDataException) { scripts[key] = null; }
        return script;
    }
    void Find(int rootRoom, int objectId, int verb, LoomScriptDisassembly? script, int depth, HashSet<(int, int)> seen)
    {
        if (script is null || depth > 4) return;
        foreach (var i in script.Instructions.Where(i => i.Operation is "startScript" or "chainScript"))
        {
            var m = Regex.Match(i.Operands, @"^0x([0-9A-F]+), \[(.*?)\]$");
            if (!m.Success) continue; int id = Convert.ToInt32(m.Groups[1].Value, 16);
            var arg = Regex.Match(m.Groups[2].Value, @"^0x([0-9A-F]+)");
            if (id == 25 && arg.Success && Convert.ToInt32(arg.Groups[1].Value, 16) is int slot && slot < 16)
                candidates.Add(new(rootRoom, objectId, verb, slot, depth, $"{script.Context ?? $"Script {script.ScriptId}"} +0x{i.Offset:X4}: {i.Operation} {i.Operands}"));
            else if (id == 29 && arg.Success && Convert.ToInt32(arg.Groups[1].Value, 16) is int level && level is >= 4 and <= 8)
                levels.Add(new(rootRoom, objectId, verb, level, depth, $"{script.Context ?? $"Script {script.ScriptId}"} +0x{i.Offset:X4}: {i.Operation} {i.Operands}"));
            else if (seen.Add((rootRoom, id))) Find(rootRoom, objectId, verb, Script(rootRoom, id), depth + 1, seen);
        }
    }
    foreach (int room in game.Rooms.Select(r => r.ResourceId!.Value).Where(r => r != 69))
    {
        Find(room, 0, 0, data.RoomEntryScript(room), 0, []);
        var objects = data.ReadRoom(room).Objects;
        foreach (var (id, verbs) in data.ReadObjectVerbs(room, objects))
            foreach (var v in verbs.Where(v => !v.IsFallback))
                Find(room, id, v.VerbId, data.ReadObjectVerb(room, objects.Single(o => o.Id == id), v.VerbId, out _), 0, []);
    }
    File.WriteAllText(Path.Combine(output, edition + "-candidates.json"), JsonSerializer.Serialize(candidates.Distinct(), new JsonSerializerOptions { WriteIndented = true }));
    // Source-only lesson requests, same seeded boot as Explorer. No status/floor/flag/note/actor writes.
    // Each case is independent; prospective call graphs are not successful earning evidence.
    foreach (int slot in levelsOnly || chainsOnly ? [] : Enumerable.Range(0, 16))
    {
        if (prior.Any(p => p.GetProperty("edition").GetString() == edition && p.TryGetProperty("slot", out var priorSlot) && priorSlot.GetInt32() == slot))
        { Console.WriteLine($"{edition} slot {slot}: retained reviewed packet; not repeated"); continue; }
        if (baseline.GameStatus.Drafts[slot].Learned == true)
        { Console.WriteLine($"{edition} slot {slot}: already initialized learned by boot; not a new lesson"); continue; }
        var roots = candidates.Where(c => c.Slot == slot).DistinctBy(c => (c.Room, c.Object, c.Verb))
            .OrderBy(c => c.Depth).ThenBy(c => c.Object == 0 ? 1 : 0).ThenBy(c => c.Verb is 0x36 or 0x38 ? 0 : 1).ThenBy(c => c.Room).Take(slot == 3 ? 4 : 2).ToArray();
        var earned = false;
        foreach (var root in roots)
        {
            var observations = new List<LoomScriptObservation>();
            var observer = new LoomScriptObserver(observations.Add, site => site.Script is 25 or 29, key => key == "local[0x000]");
            var session = new LoomExplorationSession(game, indexed, observer: observer);
            void Frames(int count) { for (int f = 0; f < count; f++) session.AdvanceFrame(); }
            void Settle(int bound = 3500) { int quiet = 0; for (int f = 0; f < bound && quiet < 32; f++) { quiet = session.GameStatus.CanEdit ? quiet + 1 : 0; session.AdvanceFrame(); } }
            session.VisitRoomById(pickup.Room); session.ActivateObjectVerb(pickup.ObjectId!.Value, pickup.VerbId!.Value); Settle();
            var before = session.GameStatus.Drafts[slot]; observations.Clear();
            session.VisitRoomById(root.Room);
            if (root.Object != 0) { Frames(40); session.ActivateObjectVerb(root.Object, root.Verb); }
            Settle();
            var after = session.GameStatus.Drafts[slot];
            var calls = observations.Where(o => o.Site is { Script: 25, Stage: "instruction", Offset: 0 } && o.Values.GetValueOrDefault("local[0x000]") == slot).ToArray();
            earned = before.Learned != true && after.Learned == true && calls.Length > 0;
            // The observer's pre-instruction chronology and final packed word are independent checks.
            packets.Add(new { edition, slot, root, before, after, earned, helperObserved = calls.Length,
                observations = calls, session.InCutscene, session.GameStatus.CanEdit, session.CurrentRoomId,
                scopes = session.CutscenesBegun, ended = session.SessionTrace.Count(t => t.StartsWith("endCutscene at ")), level = session.GameStatus.AvailableNoteCount });
            File.WriteAllText(Path.Combine(output, "lessons.json"), JsonSerializer.Serialize(packets, new JsonSerializerOptions { WriteIndented = true }));
            if (earned) { Check((after.Packed!.Value & 0x2000) != 0 && (after.Packed.Value & ~0x2000) == (before.Packed!.Value & ~0x2000), $"{edition} slot {slot}: actual lesson sets only learned mask"); break; }
        }
        Console.WriteLine($"{edition} slot {slot}: {(earned ? "source-earned" : roots.Length == 0 ? "no direct lesson root (review initialization)" : "not earned by bounded candidate requests; preserved")}");
    }
    if (chainsOnly)
    {
        if (args[3] == "chains")
        {
        var observations = new List<LoomScriptObservation>();
        var session = new LoomExplorationSession(game, indexed, observer: new LoomScriptObserver(observations.Add,
            site => site.Script is 25 or 29, key => key == "local[0x000]"));
        void Settle() { int quiet = 0; for (int f = 0; f < 5000 && quiet < 32; f++) { quiet = session.GameStatus.CanEdit ? quiet + 1 : 0; session.AdvanceFrame(); } }
        session.VisitRoomById(pickup.Room); session.ActivateObjectVerb(pickup.ObjectId!.Value, pickup.VerbId!.Value); Settle();
        var before = session.GameStatus;
        session.VisitRoomById(4);
        // The lesson is guarded by cumulative source observations, not independent fresh looks.
        var holes = candidates.Where(c => c.Slot == 3 && c.Room == 4 && c.Verb == 0x36).DistinctBy(c => c.Object).OrderBy(c => c.Object).ToArray();
        foreach (var hole in holes.Concat(holes)) { session.ActivateObjectVerb(hole.Object, hole.Verb); Settle(); }
        var after = session.GameStatus;
        packets.Add(new { edition, route = "cumulative-hole-looks", holes, before, after, observations, session.InCutscene,
            session.GameStatus.CanEdit, session.CurrentRoomId, variables = session.Snapshot.KnownGlobalVariables });
        File.WriteAllText(Path.Combine(output, "chains.json"), JsonSerializer.Serialize(packets, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"{edition} cumulative hole looks: learned slot 3 = {after.Drafts[3].Learned}; retained source guard");
        }
        // The reviewed 3 October late-story prefix; derive the other editions' source objects.
        var lateObservations = new List<LoomScriptObservation>();
        var late = new LoomExplorationSession(game, indexed, observer: new LoomScriptObserver(lateObservations.Add,
            site => site.Script is 25 or 29, key => key == "local[0x000]"));
        void LateSettle() { int quiet = 0; for (int f = 0; f < 5000 && quiet < 32; f++) { quiet = late.GameStatus.CanEdit ? quiet + 1 : 0; late.AdvanceFrame(); } }
        late.VisitRoomById(pickup.Room); late.ActivateObjectVerb(pickup.ObjectId!.Value, pickup.VerbId!.Value); LateSettle();
        SourceRoot? FindRoot(int room, string operation, string operand)
        {
            var objects = data.ReadRoom(room).Objects;
            foreach (var (id, verbs) in data.ReadObjectVerbs(room, objects))
                foreach (var verb in verbs.Where(v => v.VerbId == 0x38 && !v.IsFallback))
                    if (data.ReadObjectVerb(room, objects.Single(o => o.Id == id), verb.VerbId, out _)!.Instructions.Any(i => i.Operation == operation &&
                        (operation == "loadRoomWithEgo" ? i.Operands.Split(", ")[1] == operand : i.Operands.StartsWith(operand)))) return new(id, verb.VerbId);
            return null;
        }
        var arrival = FindRoot(50, "startScript", "0x59,");
        var portal = FindRoot(7, "loadRoomWithEgo", "0x09");
        if (arrival is { } a && portal is { } p)
        {
            late.VisitRoomById(50); late.ActivateObjectVerb(a.Object, a.Verb); LateSettle();
            if (late.CurrentRoomId == 7) { late.ActivateObjectVerb(p.Object, p.Verb); LateSettle(); }
            if (late.CurrentRoomId == 9)
                foreach (int slot in new[] { 12, 13, 14 })
                {
                    var root = candidates.First(c => c.Slot == slot && c.Room == 9 && (slot != 12 || c.Verb == 0x36));
                    var draftBefore = late.GameStatus.Drafts[slot]; lateObservations.Clear();
                    late.ActivateObjectVerb(root.Object, root.Verb); LateSettle();
                    var draftAfter = late.GameStatus.Drafts[slot];
                    var calls = lateObservations.Where(o => o.Site is { Script: 25, Stage: "instruction", Offset: 0 } && o.Values.GetValueOrDefault("local[0x000]") == slot).ToArray();
                    var earned = draftBefore.Learned != true && draftAfter.Learned == true && calls.Length > 0;
                    packets.Add(new { edition, route = "late-story-source-prefix", slot, arrival, portal, root, before = draftBefore, after = draftAfter, earned, observations = calls,
                        late.InCutscene, late.GameStatus.CanEdit, late.CurrentRoomId, scopes = late.CutscenesBegun,
                        ended = late.SessionTrace.Count(t => t.StartsWith("endCutscene at ")), trace = late.SessionTrace.ToArray() });
                    File.WriteAllText(Path.Combine(output, "chains.json"), JsonSerializer.Serialize(packets, new JsonSerializerOptions { WriteIndented = true }));
                    if (earned) Check((draftAfter.Packed!.Value & ~0x2000) == (draftBefore.Packed!.Value & ~0x2000), $"{edition} late slot {slot}: own source lesson sets only learned mask");
                    Console.WriteLine($"{edition} late source prefix slot {slot}: {(earned ? "source-earned" : "not qualified; retained")}");
                    if (late.CurrentRoomId != 9 || late.InCutscene) break;
                }
        }
        else Console.WriteLine($"{edition} late prefix: source root unresolved; no inferred substitute");
    }
    if (levelsOnly)
    {
        File.WriteAllText(Path.Combine(output, edition + "-level-candidates.json"), JsonSerializer.Serialize(levels.Distinct(), new JsonSerializerOptions { WriteIndented = true }));
        // The established level-four routes are reviewed, not replayed here. Later literal helper callers only.
        foreach (int level in new[] { 5, 6, 7, 8 })
        {
            if (level != 7 && prior.Any(p => p.GetProperty("edition").GetString() == edition && p.TryGetProperty("level", out var pLevel) && pLevel.GetInt32() == level))
            { Console.WriteLine($"{edition} level {level}: retained reviewed packet; not repeated"); continue; }
            var roots = levels.Where(c => c.Slot == level && c.Object != 0).DistinctBy(c => (c.Room, c.Object, c.Verb))
                .OrderBy(c => c.Depth).ThenBy(c => c.Verb == 0x38 ? 0 : 1).ThenBy(c => c.Room).ToArray();
            if (roots.Length == 0) { Console.WriteLine($"{edition} level {level}: no direct caller root; source prerequisites remain"); continue; }
            var observations = new List<LoomScriptObservation>();
            var observer = new LoomScriptObserver(observations.Add, site => site.Script == 29, key => key == "local[0x000]");
            var session = new LoomExplorationSession(game, indexed, observer: observer);
            void Settle() { int quiet = 0; for (int f = 0; f < 5000 && quiet < 32; f++) { quiet = session.GameStatus.CanEdit ? quiet + 1 : 0; session.AdvanceFrame(); } }
            session.VisitRoomById(pickup.Room); session.ActivateObjectVerb(pickup.ObjectId!.Value, pickup.VerbId!.Value); Settle();
            observations.Clear();
            // Level seven's own callback checks every hole. Run each actual closure once in one session.
            var sequence = level == 7 ? roots.Where(c => c.Room == roots[0].Room).DistinctBy(c => c.Object).ToArray() : [roots[0]];
            session.VisitRoomById(sequence[0].Room);
            foreach (var step in sequence)
            {
                // Local 202 requires class 3 established by the hole's actual Look callback.
                if (level == 7)
                {
                    session.ActivateObjectVerb(step.Object, 0x36); Settle();
                    if (session.CurrentRoomId != step.Room) break; // Look starts a real excursion; no catalog return is substituted.
                }
                session.ActivateObjectVerb(step.Object, step.Verb); Settle();
            }
            var calls = observations.Where(o => o.Site is { Script: 29, Stage: "instruction", Offset: 0 } && o.Values.GetValueOrDefault("local[0x000]") == level).ToArray();
            var earned = calls.Length > 0 && session.GameStatus.AvailableNoteCount == level;
            packets.Add(new { edition, level, sequence, earned, observations = calls, notes = session.GameStatus.AvailableNoteCount,
                session.InCutscene, session.GameStatus.CanEdit, session.CurrentRoomId });
            File.WriteAllText(Path.Combine(output, "levels.json"), JsonSerializer.Serialize(packets, new JsonSerializerOptions { WriteIndented = true }));
            if (earned) Check(true, $"{edition} level {level}: actual source caller reaches helper 29 and note limit");
            Console.WriteLine($"{edition} level {level}: {(earned ? "source-executed" : "bounded source request not qualified; retained")}");
        }
    }
}
Console.WriteLine($"{checks.Count} qualified source checks; candidate misses are recorded limits, not promoted");
return checks.Any(s => s.StartsWith("FAIL ")) ? 1 : 0;
internal sealed record Lesson(int Room, int Object, int Verb, int Slot, int Depth, string Source);
internal sealed record SourceRoot(int Object, int Verb);
