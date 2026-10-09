using System.Reflection;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Tests;

/// <summary>Plays one CD trigger (env CD_STUCK=room,object,verb) and dumps the scripts still waiting after CD_STUCK_FRAMES frames (default 1500); does nothing without CD_STUCK_OUT.</summary>
public sealed class CdStuckProbe
{
    private static object? Get(object o, string name) => o.GetType().GetProperty(name, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)?.GetValue(o);

    [Fact]
    public void Dump()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games", "loom vga en"));
        var outPath = Environment.GetEnvironmentVariable("CD_STUCK_OUT");
        var spec = Environment.GetEnvironmentVariable("CD_STUCK");
        if (outPath is null || spec is null || !Directory.Exists(folder)) return;
        var p = spec.Split(',').Select(int.Parse).ToArray();
        var frames = int.TryParse(Environment.GetEnvironmentVariable("CD_STUCK_FRAMES"), out var f) ? f : 1500;
        var game = new GameFolderScanner().Scan(folder);
        var session = new LoomExplorationSession(game, game.V4Archive!.ReadIndexedObjects(), completeBoot: false);
        if (Environment.GetEnvironmentVariable("CD_STUCK_FORCE") is { } force) // comma list of key=value, e.g. bit[0x077]=1
        {
            var runtime = typeof(LoomExplorationSession).GetField("_runtime", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;
            var set = runtime.GetType().GetMethod("SetGlobalVariable", [typeof(string), typeof(ushort)])!;
            foreach (var kv in force.Split(',').Select(x => x.Split('='))) set.Invoke(runtime, [kv[0], ushort.Parse(kv[1])]);
        }
        if (Environment.GetEnvironmentVariable("CD_STUCK_PRE") is { } pre) // room,object,verb;... run first, unseen, then settled
            foreach (var one in pre.Split(';').Select(x => x.Split(',').Select(int.Parse).ToArray()))
            {
                session.VisitRoom(game.Rooms.First(r => r.ResourceId == one[0]));
                if (one[1] != 0) session.ActivateObjectVerb(one[1], one[2]);
                LoomCdSceneSetupPlanner.Settle(session);
            }
        session.VisitRoom(game.Rooms.First(r => r.ResourceId == p[0]));
        if (p[1] != 0) session.ActivateObjectVerb(p[1], p[2]);
        var log = new List<string>();
        var watchField = typeof(LoomExplorationSession).GetField("_pending", BindingFlags.Instance | BindingFlags.NonPublic)!;
        string Pend() => string.Join(" | ", ((System.Collections.IEnumerable)watchField.GetValue(session)!).Cast<object>().Select(x => Get(Get(x, "Frame")!, "Label") + "+" + ((int)Get(x, "Offset")!).ToString("X4")));
        var lastPend = "";
        var watch = Environment.GetEnvironmentVariable("CD_STUCK_WATCH") == "1";
        for (var i = 0; i < frames; i++)
        {
            if (watch && Pend() is var now && now != lastPend) { log.Add($"WATCH frame {i}: begun {session.CutscenesBegun} msg {session.Messages.HaveMsg} pending {now}"); lastPend = now; }
            session.AdvanceFrame();
        }
        log.Add($"begun {session.CutscenesBegun}"); log.Add($"room {session.Snapshot.RoomId} inCutscene {session.InCutscene} skippable {session.CutsceneSkippable} msg {session.Messages.HaveMsg} egoMoving {session.EgoMoving} camMoving {session.CameraMoving}");
        var pend = (System.Collections.IEnumerable)typeof(LoomExplorationSession).GetField("_pending", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;
        foreach (var item in pend)
        {
            var frame = Get(item, "Frame")!;
            var script = (ScummScriptDisassembly)Get(item, "Script")!;
            var off = (int)Get(item, "Offset")!;
            log.Add($"PENDING {Get(frame, "Label")} #{Get(frame, "Number")} +0x{off:X4} delay {Get(frame, "Delay")} freeze {Get(frame, "FreezeCount")}");
            foreach (var ins in script.Instructions.Where(i => i.Offset >= off - 0x20 && i.Offset <= off + 0x10)) log.Add($"   {(ins.Offset == off ? ">>" : "  ")} {ins.Offset:X4} {ins.Operation} {ins.Operands}");
        }
        log.Add($"voices {session.Messages.VoiceEvents.Count} log {session.Messages.Log.Count}"); log.AddRange(session.Messages.VoiceEvents.Take(12).Select(v => "VOICE " + v)); log.AddRange(session.Messages.Log.Take(6).Select(m => "LOG " + string.Join("|", m.Lines)));
        log.AddRange(session.Snapshot.KnownGlobalVariables.Select(kv => "V " + kv.Key + "=" + kv.Value));
        log.AddRange(session.SessionTrace.Select(x => "T " + x)); log.AddRange(session.Snapshot.Uncertainties.Take(30).Select(u => "U " + u));
        File.WriteAllLines(outPath, log);
    }
}

public sealed class CdCueFind
{
    [Fact]
    public void Find()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games", "loom vga en"));
        var outPath = Environment.GetEnvironmentVariable("CD_STUCK_OUT");
        var lo = Environment.GetEnvironmentVariable("CD_CUEFIND");
        if (outPath is null || lo is null || !Directory.Exists(folder)) return;
        var range = lo.Split('-').Select(s => Convert.ToInt32(s, 16)).ToArray();
        var game = new GameFolderScanner().Scan(folder);
        var cues = LoomCdCueIndex.Scan(ScummDataSources.For(game), game.Rooms.Select(r => r.ResourceId!.Value));
        File.WriteAllLines(outPath, cues.Where(c => c.Cue.Offset >= range[0] && c.Cue.Offset <= range[1]).Select(c => $"{c.Cue.Offset:X4} len {c.Cue.Length:X4} ({c.Cue.Length}) {c.Source} \"{c.Text}\""));
    }
}

public sealed class CdLocalDisasm
{
    [Fact]
    public void Dump()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games", "loom vga en"));
        var outPath = Environment.GetEnvironmentVariable("CD_STUCK_OUT");
        var spec = Environment.GetEnvironmentVariable("CD_LDIS"); // room:id (id 0 = entry, -1 = exit, g<id> = global)
        if (outPath is null || spec is null || !Directory.Exists(folder)) return;
        var game = new GameFolderScanner().Scan(folder);
        var data = ScummDataSources.For(game);
        var lines = new List<string>();
        if (spec == "allglobals") // every global script, for searches
            spec = string.Join(';', data.GlobalScriptIds().OrderBy(i => i).Select(i => "g:" + i));
        foreach (var one in spec.Split(';'))
        {
            var parts = one.Split(':');
            ScummScriptDisassembly? script;
            if (parts[0] == "objects") // every verb script of every object of a room
            {
                var room = int.Parse(parts[1]);
                foreach (var obj in data.ReadRoom(room).Objects)
                    for (var verb = 0; verb < 0x80; verb++)
                    {
                        ScummScriptDisassembly? found = null;
                        try { found = data.ReadObjectVerb(room, obj, verb, out _); } catch (Exception) { }
                        if (found is null) continue;
                        lines.Add($"=== object {obj.Id} verb 0x{verb:X2}");
                        lines.AddRange(found.Instructions.Select(i => $"{i.Offset:X4} {i.Operation} {i.Operands}"));
                    }
                continue;
            }
            if (parts[0] == "g") script = data.GlobalScript(int.Parse(parts[1]));
            else if (parts[0] == "o") { var room = int.Parse(parts[1]); script = data.ReadObjectVerb(room, data.ReadRoom(room).Objects.First(o => o.Id == int.Parse(parts[2])), Convert.ToInt32(parts[3], 16), out _); }
            else
            {
                var room = int.Parse(parts[0]); var id = int.Parse(parts[1]);
                script = id == 0 ? data.RoomEntryScript(room) : id == -1 ? data.RoomExitScript(room) : data.RoomLocalScript(room, id);
            }
            if (script is null) { lines.Add("=== " + one + " (none)"); continue; }
            lines.Add("=== " + one);
            var opc = Environment.GetEnvironmentVariable("CD_LDIS_OPC") == "1"; // also print each opcode byte
            lines.AddRange(script!.Instructions.Select(i => $"{i.Offset:X4} {(opc ? i.Opcode.ToString("X2") + " " : "")}{i.Operation} {i.Operands}"));
        }
        File.WriteAllLines(outPath, lines);
    }
}

/// <summary>Discovery only (env CD_NEEDS_OUT): for every derived CD scene that opens no cutscene from a fresh state, which single story bits (read by its own code) flip it when forced to 1.</summary>
public sealed class CdNeedsProbe
{
    private static bool Run(GameLibrary game, LoomCdCutscene t, IReadOnlyList<string> force, int frames = 400)
    {
        var session = new LoomExplorationSession(game, game.V4Archive!.ReadIndexedObjects(), completeBoot: false);
        var runtime = typeof(LoomExplorationSession).GetField("_runtime", BindingFlags.Instance | BindingFlags.NonPublic)!.GetValue(session)!;
        var set = runtime.GetType().GetMethod("SetGlobalVariable", [typeof(string), typeof(ushort)])!;
        foreach (var f in force) set.Invoke(runtime, [f, (ushort)1]);
        session.VisitRoom(game.Rooms.First(r => r.ResourceId == t.Room));
        if (!t.IsRoomEntry) session.ActivateObjectVerb(t.ObjectId, t.VerbId);
        for (var i = 0; i < frames; i++) { if (session.InCutscene || session.CutscenesBegun > 0) return true; session.AdvanceFrame(); }
        return false;
    }

    [Fact]
    public void Probe()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games", "loom vga en"));
        var outPath = Environment.GetEnvironmentVariable("CD_NEEDS_OUT");
        if (outPath is null || !Directory.Exists(folder)) return;
        var game = new GameFolderScanner().Scan(folder);
        var data = ScummDataSources.For(game);
        var scanner = LoomSceneStateScanner.For(game);
        var groups = LoomCdCutsceneIndex.Group(LoomCdCutsceneIndex.Scan(data, game.Rooms.Select(r => r.ResourceId!.Value)));
        var lines = new List<string>();
        foreach (var g in groups)
        {
            var t = g.First;
            if (Run(game, t, [])) continue;
            var scan = scanner.Scan(t.Room, [], t.ObjectId, t.VerbId, entryOfStartRoom: t.IsRoomEntry);
            var bits = scan.Touches.Where(x => !x.Write && x.Variable.StartsWith("bit[")).Select(x => x.Variable).Distinct().ToList();
            var flips = bits.Where(b => Run(game, t, [b])).ToList();
            lines.Add($"room {t.Room:00} obj {t.ObjectId} verb {t.VerbLabel}: reads {bits.Count} bits; single flips: {string.Join(" ", flips)}");
            if (flips.Count == 0)
            {
                // pairs
                var pairs = new List<string>();
                for (var i = 0; i < bits.Count && pairs.Count < 6; i++) for (var j = i + 1; j < bits.Count && pairs.Count < 6; j++)
                    if (Run(game, t, [bits[i], bits[j]])) pairs.Add(bits[i] + "+" + bits[j]);
                lines.Add($"    pair flips: {string.Join(" ", pairs)}  (bits: {string.Join(" ", bits)})");
            }
        }
        File.WriteAllLines(outPath, lines);
    }
}

public sealed class CdGuardProbe
{
    [Fact]
    public void Probe()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games", "loom vga en"));
        var outPath = Environment.GetEnvironmentVariable("CD_GUARDS_OUT");
        var only = Environment.GetEnvironmentVariable("CD_GUARDS_ONLY"); // comma list of room numbers
        if (outPath is null || !Directory.Exists(folder)) return;
        var rooms = only?.Split(',').Select(int.Parse).ToHashSet();
        var game = new GameFolderScanner().Scan(folder);
        var data = ScummDataSources.For(game);
        var groups = LoomCdCutsceneIndex.Group(LoomCdCutsceneIndex.Scan(data, game.Rooms.Select(r => r.ResourceId!.Value)));
        var lines = new List<string>();
        foreach (var g in groups.Where(g => rooms is null || rooms.Contains(g.First.Room)))
        {
            var t = g.First;
            var room = data.ReadRoom(t.Room);
            ScummScriptDisassembly? root = t.IsRoomEntry ? data.RoomEntryScript(t.Room) : data.ReadObjectVerb(t.Room, room.Objects.First(o => o.Id == t.ObjectId), t.VerbId, out _);
            if (root is null) continue;
            lines.Add($"## room {t.Room:00} obj {t.ObjectId} {t.ObjectName} {t.VerbLabel}");
            foreach (var p in LoomCdSceneGuards.Find(data, t.Room, root, "root"))
            {
                lines.Add($"  path {string.Join(" > ", p.Chain)} cutscene +0x{p.SpanOffset:X4}");
                lines.AddRange(p.Guards.Select(x => "     " + x));
            }
        }
        File.WriteAllLines(outPath, lines);
    }
}

public sealed class CdGuardStateProbe
{
    [Fact]
    public void Probe()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games", "loom vga en"));
        var outPath = Environment.GetEnvironmentVariable("CD_GSTATE_OUT");
        if (outPath is null || !Directory.Exists(folder)) return;
        var game = new GameFolderScanner().Scan(folder);
        var data = ScummDataSources.For(game);
        var groups = LoomCdCutsceneIndex.Group(LoomCdCutsceneIndex.Scan(data, game.Rooms.Select(r => r.ResourceId!.Value)));
        var lines = new List<string>();
        foreach (var g in groups)
        {
            var t = g.First;
            var session = new LoomExplorationSession(game, game.V4Archive!.ReadIndexedObjects(), completeBoot: false);
            session.VisitRoom(game.Rooms.First(r => r.ResourceId == t.Room));
            var began = false;
            if (!t.IsRoomEntry) session.ActivateObjectVerb(t.ObjectId, t.VerbId);
            for (var i = 0; i < 300 && !began; i++) { began = session.InCutscene || session.CutscenesBegun > 0; if (!began) session.AdvanceFrame(); }
            if (began) continue;
            var room = data.ReadRoom(t.Room);
            ScummScriptDisassembly root = t.IsRoomEntry ? data.RoomEntryScript(t.Room)! : data.ReadObjectVerb(t.Room, room.Objects.First(o => o.Id == t.ObjectId), t.VerbId, out _);
            var vars = session.Snapshot.KnownGlobalVariables;
            var classes = session.ObjectClasses;
            lines.Add($"## room {t.Room:00} obj {t.ObjectId} {t.ObjectName} {t.VerbLabel}");
            foreach (var p in LoomCdSceneGuards.Find(data, t.Room, root, "root"))
            {
                var verdicts = p.Guards.Select(x => (x, v: LoomCdSceneGuards.Evaluate(x, k => vars.TryGetValue(k, out var u) ? u : null, id => classes.TryGetValue(id, out var c) ? c : null))).ToList();
                var bad = verdicts.Count(x => x.v == LoomCdGuardVerdict.Unsatisfied);
                var unk = verdicts.Count(x => x.v == LoomCdGuardVerdict.Unknown);
                lines.Add($"  path {string.Join(" > ", p.Chain)} +0x{p.SpanOffset:X4}: unsatisfied {bad}, unknown {unk}");
                foreach (var (x, v) in verdicts.Where(x => x.v != LoomCdGuardVerdict.Satisfied)) lines.Add($"     {v}: {x}  [now {(vars.TryGetValue(x.Subject, out var cur) ? cur : "unset/0")}]");
            }
        }
        File.WriteAllLines(outPath, lines);
    }
}

public sealed class CdWritersProbe
{
    [Fact]
    public void Probe()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games", "loom vga en"));
        var outPath = Environment.GetEnvironmentVariable("CD_WRITERS_OUT");
        var vars = Environment.GetEnvironmentVariable("CD_WRITERS"); // bit[0x00E],bit[0x01B]
        if (outPath is null || vars is null || !Directory.Exists(folder)) return;
        var game = new GameFolderScanner().Scan(folder);
        var scanner = LoomSceneStateScanner.For(game);
        var lines = new List<string>();
        foreach (var v in vars.Split(','))
        {
            lines.Add("## " + v);
            foreach (var w in scanner.FindWriters(v)) lines.Add($"   {w.Where}: {w.Instruction}");
        }
        File.WriteAllLines(outPath, lines);
        var schema = typeof(LoomExplorationSession).Assembly.GetType("SCUMMexplorer.Core.Loom.LoomStatusSchema")!;
        var read = schema.GetMethod("Read")!.Invoke(null, [game, ScummDataSources.For(game)]);
        File.AppendAllLines(outPath, ["SCHEMA " + read]);
    }
}

public sealed class CdBitWatch
{
    [Fact]
    public void Watch()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games", "loom vga en"));
        var outPath = Environment.GetEnvironmentVariable("CD_BITWATCH_OUT");
        var key = Environment.GetEnvironmentVariable("CD_BITWATCH"); // bit[0x025]
        if (outPath is null || key is null || !Directory.Exists(folder)) return;
        var game = new GameFolderScanner().Scan(folder);
        var session = new LoomExplorationSession(game, game.V4Archive!.ReadIndexedObjects(), completeBoot: false);
        var lines = new List<string>();
        ushort Value() => session.Snapshot.KnownGlobalVariables.TryGetValue(key, out var v) ? v : (ushort)0;
        lines.Add($"after construct: {Value()}");
        foreach (var r in Environment.GetEnvironmentVariable("CD_BITWATCH_ROOMS")!.Split(',').Select(int.Parse))
        {
            session.VisitRoom(game.Rooms.First(x => x.ResourceId == r));
            lines.Add($"after visit {r}: {Value()}");
        }
        lines.AddRange(session.SessionTrace.Select(x => "T " + x)); lines.AddRange(session.LastVisitEvents.Select(e => "E " + e.SourceLabel + " +" + e.Offset.ToString("X4") + " " + e.Operation + " " + e.Operands));
        File.WriteAllLines(outPath, lines);
    }
}

public sealed class CdBitWriter
{
    [Fact]
    public void Who()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games", "loom vga en"));
        var outPath = Environment.GetEnvironmentVariable("CD_BITWATCH_OUT");
        var key = Environment.GetEnvironmentVariable("CD_BITWHO"); // bit[0x025]
        if (outPath is null || key is null || !Directory.Exists(folder)) return;
        var game = new GameFolderScanner().Scan(folder);
        var lines = new List<string>();
        var observer = new ScummScriptObserver(o => { if (o.Values.TryGetValue(key, out var v)) lines.Add($"{key}={v} after script {o.Site?.Script} {o.Site?.Context} +{o.Site?.Offset:X4} {o.Site?.Operation} {o.Site?.Operands}"); }, includeKey: k => k == key);
        var session = new LoomExplorationSession(game, game.V4Archive!.ReadIndexedObjects(), completeBoot: false, observer: observer);
        foreach (var r in Environment.GetEnvironmentVariable("CD_BITWATCH_ROOMS")!.Split(',').Select(int.Parse))
        {
            lines.Add("-- visit " + r);
            session.VisitRoom(game.Rooms.First(x => x.ResourceId == r));
        }
        File.WriteAllLines(outPath, lines);
    }
}

public sealed class CdSetupProbe
{
    [Fact]
    public void Derive()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games", "loom vga en"));
        var outPath = Environment.GetEnvironmentVariable("CD_SETUP_OUT");
        if (outPath is null || !Directory.Exists(folder)) return;
        var game = new GameFolderScanner().Scan(folder);
        var data = ScummDataSources.For(game);
        var groups = LoomCdCutsceneIndex.Group(LoomCdCutsceneIndex.Scan(data, game.Rooms.Select(r => r.ResourceId!.Value)));
        var planner = new LoomCdSceneSetupPlanner(game, LoomCdCutsceneIndex.Scan(data, game.Rooms.Select(r => r.ResourceId!.Value)));
        var lines = new List<string>();
        var results = groups.Select(g => planner.Derive(g.First)).ToList();
        var fresh = results.Count(s => s == LoomCdStartSetup.None);
        var derived = results.Count(s => s != LoomCdStartSetup.None && s.Plays);
        var enabled = fresh + derived;
        lines.Add($"SUMMARY scenes {groups.Count()}: {fresh} begin from a fresh start, {derived} from a derived setup, {results.Count - enabled} without a start; {100.0 * enabled / results.Count:F1}% enabled");
        foreach (var g in groups)
        {
            var s = planner.Derive(g.First);
            if (s == LoomCdStartSetup.None) continue;
            lines.Add($"## room {g.First.Room:00} obj {g.First.ObjectId} {g.First.ObjectName} {g.First.VerbLabel}: plays {s.Plays}");
            lines.AddRange(s.Notes.Select(n => "   " + n));
        }
        File.WriteAllLines(outPath, lines);
    }
}

public sealed class CdBoxProbe
{
    [Fact]
    public void Boxes()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games", "loom vga en"));
        var outPath = Environment.GetEnvironmentVariable("CD_BOX_OUT");
        var spec = Environment.GetEnvironmentVariable("CD_BOX"); // room,box
        if (outPath is null || spec is null || !Directory.Exists(folder)) return;
        var p = spec.Split(',').Select(int.Parse).ToArray();
        var game = new GameFolderScanner().Scan(folder);
        var session = new LoomExplorationSession(game, game.V4Archive!.ReadIndexedObjects(), completeBoot: false);
        session.VisitRoom(game.Rooms.First(r => r.ResourceId == p[0]));
        for (var i = 0; i < 20; i++) session.AdvanceFrame();
        var lines = new List<string>();
        var set = session.CurrentWalkBoxes!;
        foreach (var b in set.Boxes) lines.Add($"box {b.Index}: {string.Join(" ", b.Corners.Select(c => $"({c.X},{c.Y})"))} flags {set.GetFlags(b.Index):X2}");
        var ego = session.Actors.First(a => a.Number == 1);
        lines.Add($"ego before: {ego.X},{ego.Y} box {ego.WalkBox}");
        var box = set.Coordinates(p[1])!;
        var x = box.Corners.Sum(c => c.X) / 4; var y = box.Corners.Sum(c => c.Y) / 4;
        lines.Add($"centroid {x},{y} contains {set.Contains(p[1], x, y)}");
        session.PlaceEgo(x, y);
        ego = session.Actors.First(a => a.Number == 1);
        lines.Add($"ego after: {ego.X},{ego.Y} box {ego.WalkBox}");
        File.WriteAllLines(outPath, lines);
    }
}

public sealed class CdGrepScripts
{
    [Fact]
    public void Grep()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../Games", "loom vga en"));
        var outPath = Environment.GetEnvironmentVariable("CD_GREP_OUT");
        var needle = Environment.GetEnvironmentVariable("CD_GREP");
        if (outPath is null || needle is null || !Directory.Exists(folder)) return;
        var game = new GameFolderScanner().Scan(folder);
        var data = ScummDataSources.For(game);
        var lines = new List<string>();
        void Look(string label, ScummScriptDisassembly? s) { if (s is null) return; foreach (var i in s.Instructions) if ((i.Operation + " " + i.Operands).Contains(needle)) lines.Add($"{label} +{i.Offset:X4} {i.Operation} {i.Operands}"); }
        foreach (var id in data.GlobalScriptIds()) Look($"global {id}", data.GlobalScript(id));
        foreach (var r in game.Rooms.Select(x => x.ResourceId!.Value))
        {
            Look($"room {r} entry", data.RoomEntryScript(r)); Look($"room {r} exit", data.RoomExitScript(r));
            foreach (var id in data.RoomLocalScriptIds(r)) Look($"room {r} local {id}", data.RoomLocalScript(r, id));
            var room = data.ReadRoom(r);
            foreach (var (obj, verbs) in data.ReadObjectVerbs(r, room.Objects)) foreach (var v in verbs.Where(v => !v.IsFallback)) Look($"room {r} obj {obj} verb {v.VerbId:X2}", data.ReadObjectVerb(r, room.Objects.First(o => o.Id == obj), v.VerbId, out _));
        }
        File.WriteAllLines(outPath, lines);
    }
}
