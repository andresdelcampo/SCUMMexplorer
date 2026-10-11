using System.Collections.Concurrent;
using System.Text.RegularExpressions;
using SCUMMexplorer.Core.Discovery;

// CutsceneStateAudit <loom folder> [limit]
// For every scanned cutscene group: how much does the scene change when the state its own code reads is present?
// Variants (applied through the supported Game Status edits after the room visit, before the trigger):
//   base (fresh), staff held, 8 notes, each story bit the scene reads set to 1, and all of them together (full).
// A scene is "state-sensitive" when any variant prints different lines or enters different rooms than base.
var game = new GameFolderScanner().Scan(args[0]);
var data = ScummDataSources.For(game);
var rooms = game.Rooms.Where(r => r.ResourceId.HasValue).Select(r => r.ResourceId!.Value).ToList();
var groups = LoomCutsceneIndex.Group(LoomCutsceneIndex.Scan(data, rooms, LoomCutsceneTitles.Doorways(game), game));
var limit = args.Length > 1 ? int.Parse(args[1]) : int.MaxValue;
var snapshot = new LflObjectStateReader().ReadSnapshot(Path.Combine(game.FolderPath, "00.LFL"));
var scanner = LoomSceneStateScanner.For(game);
var firstBit = LoomStoryEvents.FirstBitFor(game);

string Run(LoomCutscene t, Action<LoomExplorationSession>? edit)
{
    try
    {
        var s = new LoomExplorationSession(game, snapshot);
        s.VisitRoom(game.Rooms.First(r => r.ResourceId == t.Room));
        for (var w = 0; w < 1500 && !s.GameStatus.CanEdit; w++) s.AdvanceFrame();
        edit?.Invoke(s);
        var earlier = s.CutscenesBegun; var mark = s.RoomsEntered.Count; var logMark = s.Messages.Log.Count;
        if (!t.IsRoomEntry) s.ActivateObjectVerb(t.ObjectId, t.VerbId);
        bool begun = false;
        for (var i = 0; i < 6000; i++)
        {
            if (s.InCutscene || s.CutscenesBegun > earlier) begun = true;
            if (begun && !s.InCutscene && s.Messages.HaveMsg == 0) break;
            if (!begun && i >= 600) break;
            s.AdvanceFrame();
        }
        var lines = s.Messages.Log.Skip(logMark).Select(m => m.Text.Replace("\n", " "));
        return $"{(begun ? "B" : "-")} rooms[{string.Join(",", s.RoomsEntered.Skip(mark))}] {string.Join("|", lines)}";
    }
    catch (Exception ex) { return "ERR " + ex.GetType().Name + ": " + ex.Message; }
}

if (args.Length > 2 && args[2] == "readiness") // CutsceneStateAudit <folder> <limit> readiness: what LoomCutsceneReadiness adds per group and what it changes
{
    var readiness = new LoomCutsceneReadiness(game, () => new LoomExplorationSession(game, snapshot));
    var lines = new ConcurrentBag<string>(); int withEdits = 0, newlyBegun = 0, n = 0;
    var clock = System.Diagnostics.Stopwatch.StartNew();
    Parallel.ForEach(groups.Take(limit), new ParallelOptions { MaxDegreeOfParallelism = 8 }, g =>
    {
        var t = g.First;
        var plan = readiness.Plan(t.Room, t.ObjectId, t.VerbId, LoomCutsceneReadiness.ScriptIds(t.Scripts));
        Interlocked.Increment(ref n);
        if (plan.HasEdits) Interlocked.Increment(ref withEdits);
        if (plan.HasEdits && !plan.Base.Begun && plan.Result.Begun) Interlocked.Increment(ref newlyBegun);
        lines.Add($"room {t.Room:00} obj {t.ObjectId} verb 0x{t.VerbId:X2} | base {(plan.Base.Begun ? "began" : "NOT begun")} {plan.Base.Lines}l/{plan.Base.Rooms}r -> {plan.Result.Lines}l/{plan.Result.Rooms}r | staff={plan.Staff} notes={plan.Notes} bits=[{string.Join(" ", plan.Bits)}]");
    });
    foreach (var l in lines.OrderBy(l => l)) Console.WriteLine(l);
    Console.WriteLine($"groups {n}; with state added: {withEdits}; began only with it: {newlyBegun}; {clock.Elapsed.TotalSeconds:F0} s");
    return;
}
var results = new ConcurrentBag<string>();
int sensitive = 0, total = 0, notBegun = 0;
var counts = new ConcurrentDictionary<string, int>();
Parallel.ForEach(groups.Take(limit), new ParallelOptions { MaxDegreeOfParallelism = 8 }, g =>
{
    var t = g.First;
    var ids = t.Scripts.Select(x => Regex.Match(x, @"\d+")).Where(m => m.Success).Select(m => int.Parse(m.Value)).ToArray();
    var scan = scanner.Scan(t.Room, ids, t.ObjectId, t.VerbId);
    var bits = scan.OwnTouches.Where(x => !x.Write && x.Variable.StartsWith("bit[0x") &&
        int.Parse(x.Variable[6..^1], System.Globalization.NumberStyles.HexNumber) >= firstBit).Select(x => x.Variable).Distinct().Take(6).ToList();
    var readsStaff = scan.OwnTouches.Any(x => !x.Write && x.Variable == LoomSceneSetupPlanner.StaffBit);
    var readsCostume = scan.OwnTouches.Any(x => !x.Write && x.Variable == "var[0x07D]");
    var baseSig = Run(t, null);
    var variants = new List<(string Name, Action<LoomExplorationSession> Edit)>
    {
        ("staff", s => s.UpdateStaffPossession(true)),
        ("notes8", s => { s.UpdateStaffPossession(true); s.UpdateAvailableNotes(8); }),
    };
    foreach (var b in bits) variants.Add((b, s => s.UpdateStoryBit(b, true)));
    if (bits.Count > 1) variants.Add(("allbits", s => { foreach (var b in bits) s.UpdateStoryBit(b, true); }));
    variants.Add(("full", s => { s.UpdateStaffPossession(true); s.UpdateAvailableNotes(8); foreach (var b in bits) s.UpdateStoryBit(b, true); }));
    var differing = new List<string>();
    foreach (var (name, edit) in variants)
    {
        var sig = Run(t, edit);
        if (sig.StartsWith("ERR")) { differing.Add(name + "!" + sig); continue; }
        if (sig != baseSig) differing.Add(name);
    }
    var tag = $"room {t.Room:00} obj {t.ObjectId} verb 0x{t.VerbId:X2} | reads bits [{string.Join(" ", bits)}] staff={readsStaff} costume={readsCostume} | base {(baseSig.StartsWith("B") ? "began" : "NOT begun")} | differs under: {(differing.Count == 0 ? "-" : string.Join(", ", differing))}";
    results.Add(tag);
    Interlocked.Increment(ref total);
    if (!baseSig.StartsWith("B")) Interlocked.Increment(ref notBegun);
    if (differing.Count > 0) { Interlocked.Increment(ref sensitive); foreach (var d in differing) counts.AddOrUpdate(d.StartsWith("bit") ? "a story bit" : d, 1, (_, v) => v + 1); }
});
foreach (var r in results.OrderBy(r => r)) Console.WriteLine(r);
Console.WriteLine($"groups {total}; fresh run began no cutscene: {notBegun}; state-sensitive (some variant prints different lines/rooms): {sensitive}");
foreach (var (k, v) in counts.OrderByDescending(p => p.Value)) Console.WriteLine($"  differs under {k}: {v}");
