using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.App;

var output = Path.GetFullPath(args[1]);
if (Directory.Exists(output)) throw new IOException("Use a fresh evidence directory.");
Directory.CreateDirectory(output);
var route = args[2]; var edition = args[3];
var folder = edition switch { "ega-en" => "loom ega mt32 en", "ega-es" => "ALTERNATIVE LOOMS/loom ega mt32 es", "vga" => "loom vga en", _ => throw new ArgumentException("Unreviewed edition") };
var game = new GameFolderScanner().Scan(Path.Combine(Path.GetFullPath(args[0]), folder));
var data = ScummDataSources.For(game);
var indexed = game.V4Archive?.ReadIndexedObjects() ?? new LflObjectStateReader().ReadSnapshot(Path.Combine(game.FolderPath, "00.LFL"));
var sourceHashes = new Dictionary<string, string>();
void SaveSource(string name, ScummScriptDisassembly? script)
{
    if (script is null) return;
    var text = script.Format(); File.WriteAllText(Path.Combine(output, name + ".txt"), text);
    sourceHashes[name] = Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(text)));
}
if (route == "dump")
{
    foreach (int id in new[] { 1, 5, 6, 17, 18, 23, 28, 30, 97, 98, 106, 110, 120 }) SaveSource($"global-{id}", data.GlobalScript(id));
    foreach (int room in new[] { 2, 4, 23, 24, 25, 26, 34, 46, 49, 66, 69 })
    {
        SaveSource($"room-{room}-entry", data.RoomEntryScript(room));
        foreach (int id in data.RoomLocalScriptIds(room)) SaveSource($"room-{room}-local-{id}", data.RoomLocalScript(room, id));
        var objects = data.ReadRoom(room).Objects;
        foreach (var (id, verbs) in data.ReadObjectVerbs(room, objects))
            foreach (var verb in verbs.Where(v => !v.IsFallback)) SaveSource($"room-{room}-object-{id}-{verb.VerbId:X2}", data.ReadObjectVerb(room, objects.Single(o => o.Id == id), verb.VerbId, out _));
    }
    File.WriteAllText(Path.Combine(output, "hashes.json"), JsonSerializer.Serialize(sourceHashes));
    return 0;
}
var observations = new List<ScummScriptObservation>();
var session = new LoomExplorationSession(game, indexed, observer: new ScummScriptObserver(observations.Add,
    site => site.Script is 25 or 29 or 106 or 110 || site.Script >= 200,
    key => key.StartsWith("local[") || key is "var[0x002]" or "var[0x064]" or "var[0x0C3]"));
var records = new List<object>(); var checks = new List<string>(); var frames = 0;
var before = session.GameStatus; string? failure = null;
void Check(bool condition, string label) { checks.Add((condition ? "PASS " : "FAIL ") + label); Console.WriteLine(checks[^1]); }
void Record(string stage) => records.Add(new { stage, frames, status = session.GameStatus, snapshot = session.Snapshot, actors = session.Actors });
void Advance() { session.AdvanceFrame(); frames++; }
void Settle(string stage, int bound = 6000)
{
    int quiet = 0;
    for (int f = 0; f < bound && quiet < 32; f++)
    {
        quiet = session.GameStatus.CanEdit && !session.EgoMoving && !session.CameraMoving && session.Messages.HaveMsg == 0 ? quiet + 1 : 0;
        Advance();
    }
    Record(stage + (quiet == 32 ? "" : "-bound"));
    if (quiet != 32) throw new InvalidOperationException(stage + ": did not settle");
}
int[] Roots(int room, int verb, string operation, string operand)
{
    var objects = data.ReadRoom(room).Objects;
    return data.ReadObjectVerbs(room, objects).Where(pair => pair.Value.Any(v => v.VerbId == verb && !v.IsFallback))
        .Where(pair => data.ReadObjectVerb(room, objects.Single(o => o.Id == pair.Key), verb, out _)?.Instructions.Any(i => i.Operation == operation && i.Operands.StartsWith(operand)) == true).Select(pair => pair.Key).ToArray();
}
void Use(int room, int id, int verb, string stage)
{
    if (session.CurrentRoomId != room) throw new InvalidOperationException(stage + ": wrong room");
    SaveSource($"room-{room}-object-{id}-{verb:X2}", data.ReadObjectVerb(room, data.ReadRoom(room).Objects.Single(o => o.Id == id), verb, out _));
    session.ActivateObjectVerb(id, verb); Settle(stage);
}
void WalkBox(int boxId, bool left = false)
{
    var box = session.CurrentWalkBoxes!.Boxes.Single(b => b.Index == boxId);
    var corner = box.Corners.OrderBy(p => p.X).First();
    int x = left ? corner.X : (int)box.Corners.Average(p => p.X);
    int y = left ? corner.Y : (int)box.Corners.Average(p => p.Y);
    records.Add(new { stage = "walk-request", boxId, x, y, box });
    session.Click(x, y); Settle("walk-box-" + boxId);
}
void Lesson(int slot)
{
    Check(before.Drafts[slot].Learned != true && session.GameStatus.Drafts[slot].Learned == true, $"slot {slot} changed from unlearned to learned");
    Check(observations.Any(o => o.Site is { Script: 25, Offset: 0, Stage: "instruction" } && o.Values.GetValueOrDefault("local[0x000]") == slot), $"observed actual Script 25({slot})");
    Check((before.Drafts[slot].Packed!.Value & ~0x2000) == (session.GameStatus.Drafts[slot].Packed!.Value & ~0x2000), $"slot {slot} changes only learned mask");
}
void ApproachSlot7()
{
    // Old-v3 relational operands compare the following constant against the
    // variable. The monitor fires when Bobbin's x is below its own threshold.
    var guard = data.RoomLocalScript(23, 201)!.Instructions.Single(i => i.Operation == "isGreater");
    int threshold = Convert.ToInt32(Regex.Match(guard.Operands, @", 0x([0-9A-F]+),").Groups[1].Value, 16);
    var point = session.CurrentWalkBoxes!.Boxes.Where(b => (b.Flags & 0x80) == 0).SelectMany(b => b.Corners)
        .Where(p => p.X > 0 && p.X < threshold && p.Y > 0).OrderByDescending(p => p.X).First();
    records.Add(new { stage = "approach-monitor", threshold, point, geometry = session.CurrentWalkBoxes });
    session.Click(point.X, point.Y); Settle("approach-monitor");
}
void PastureLesson()
{
    SaveRoom(24); SaveRoom(25);
    session.VisitRoomById(24); Settle("outside-pasture");
    var objects = data.ReadRoom(24).Objects;
    var entry = data.ReadObjectVerbs(24, objects).Where(p => p.Value.Any(v => v.VerbId == 0x38 && !v.IsFallback)).Single(p =>
        data.ReadObjectVerb(24, objects.Single(o => o.Id == p.Key), 0x38, out _)!.Instructions.Any(i => i.Operation == "loadRoomWithEgo" && i.Operands.Split(", ")[1] == "0x19")).Key;
    Use(24, entry, 0x38, "actual-pasture-arrival");
    if (session.CurrentRoomId != 25) throw new InvalidOperationException("pasture arrival did not reach Room 25");
    WalkBox(4); WalkBox(3);
    Use(25, Roots(25, 0x36, "startScript", "0xCC,").Single(), 0x36, "lamb-lesson");
}
void Cast(int room, int id, int verb, string stage)
{
    if (session.CurrentRoomId != room) throw new InvalidOperationException(stage + ": wrong room");
    var target = data.ReadRoom(room).Objects.Single(o => o.Id == id);
    session.Click(target.X + target.Width / 2, target.Y + target.Height / 2); Settle(stage + "-aim");
    var draft = session.GameStatus.Drafts.Single(d => d.ForwardVerb == verb || d.ReverseVerb == verb);
    bool reverse = draft.ReverseVerb == verb;
    bool available = draft.Learned == true && (!reverse || draft.Reversible == true) && draft.Notes.Max() <= session.GameStatus.AvailableNoteCount;
    Check(available, stage + ": draft source-available before input");
    if (!available) throw new InvalidOperationException(stage + ": source prerequisite missing");
    int start = session.SessionTrace.Count;
    foreach (int note in reverse ? draft.Notes.Reverse() : draft.Notes) { session.PressKey("qwertyui"[note - 1]); for (int f = 0; f < 8; f++) Advance(); }
    Settle(stage);
    Check(session.SessionTrace.Skip(start).Any(t => t.Contains($"startObject runs object {id} verb 0x{verb:X2}")), stage + ": input dispatched own response");
}
void SaveRoom(int room)
{
    SaveSource($"room-{room}-entry", data.RoomEntryScript(room));
    foreach (int id in data.RoomLocalScriptIds(room)) SaveSource($"room-{room}-local-{id}", data.RoomLocalScript(room, id));
}
bool? Bit(string key)
{
    var vars = session.Snapshot.KnownGlobalVariables;
    if (vars.TryGetValue(key, out var value)) return value != 0;
    if (vars.ContainsKey(ScummBoundedScriptExecutor.SeparateBitStorage)) return false;
    int bit = Convert.ToInt32(key[6..^1], 16);
    return vars.TryGetValue($"var[0x{bit >> 4:X3}]", out var word) ? (word & (1 << (bit & 15))) != 0 : null;
}
try
{
    var staffKey = Regex.Match(data.GlobalScript(17)!.Instructions.Last(i => i.Operation == "notEqualZero" && i.Operands.StartsWith("bit[")).Operands, @"bit\[0x[0-9A-F]+\]").Value;
    var pickup = LoomSceneStateScanner.For(game).FindConstantWriters(staffKey, 1).OrderBy(w => w.Room).First(w => w.ObjectId is not null && w.VerbId is not null);
    session.VisitRoomById(pickup.Room); Use(pickup.Room, pickup.ObjectId!.Value, pickup.VerbId!.Value, "source-pickup");
    before = session.GameStatus;
    SaveSource("global-25", data.GlobalScript(25));
    SaveSource("global-29", data.GlobalScript(29));
    if (route == "slot3")
    {
        session.VisitRoomById(4); Settle("arrival");
        SaveRoom(4);
        // Local 201 tests camera x (var 2), not pointer x. The owlet/wolves
        // scene puts Bobbin on the left and fills the fourth hole through source.
        var owlet = Roots(4, 0x36, "startScript", "0xCA,").Single();
        Use(4, owlet, 0x36, "owlet-and-wolves");
        var holes = Roots(4, 0x36, "startScript", "0xC8,");
        foreach (int id in holes) Use(4, id, 0x36, "owl-look-" + id);
        Lesson(3);
    }
    else if (route == "slot7")
    {
        SaveRoom(23);
        session.VisitRoomById(23); Settle("arrival");
        ApproachSlot7();
        Lesson(7);
    }
    else if (route == "slot7-departure-control")
    {
        SaveRoom(23); SaveRoom(66); SaveSource("global-106", data.GlobalScript(106));
        var gate = Regex.Match(data.RoomEntryScript(23)!.Instructions.First(i => i.Operation == "equalZero" && i.Operands.StartsWith("bit[")).Operands, @"bit\[0x[0-9A-F]+\]").Value;
        Check(Bit(gate) == false, "departure-completed bit starts clear");
        session.VisitRoomById(66); Settle("departure-start");
        Use(66, Roots(66, 0x54, "startScript", "0x6A,").Single(), 0x54, "actual-Script-106-control");
        Check(Bit(gate) == true, "actual Script 106 sets completion bit");
        session.VisitRoomById(23); Settle("post-departure-arrival");
        ApproachSlot7();
        Check(session.GameStatus.Drafts[7].Learned != true, "completed departure suppresses early slot-7 lesson");
    }
    else if (route == "slot11")
    {
        PastureLesson();
        Lesson(11);
    }
    else if (route == "seven")
    {
        // Source-earned Healing and six notes are needed to restore Rusty on
        // the Room 34 excursion. Catalog requests are explicit bounded starts.
        PastureLesson(); Lesson(11);
        session.VisitRoomById(13); Settle("crystal-arrival"); SaveRoom(13);
        Use(13, Roots(13, 0x38, "startScript", "0x36,").First(), 0x38, "actual-crystal-use");
        Check(session.GameStatus.AvailableNoteCount == 5, "actual crystal Use earns five notes");
        session.VisitRoomById(62); Settle("gold-arrival"); SaveRoom(62);
        Use(62, Roots(62, 0x36, "startScript", "0xC8,").First(), 0x36, "gold-lesson");
        session.VisitRoomById(28); Settle("dragon-gold-arrival"); SaveRoom(28);
        Cast(28, Roots(28, 0x3D, "startScript", "0xC8,").First(), 0x3D, "reverse-dragon-gold");
        Check(session.GameStatus.AvailableNoteCount == 6, "actual reverse-gold input earns six notes");
        if (session.GameStatus.AvailableNoteCount != 6) throw new InvalidOperationException("six-note prerequisite was not earned");
        var recipe = LoomReplayCatalog.Load(game).CatalogStarts.SingleOrDefault(r => r.Room == 46);
        if (recipe is not null)
        {
            // A real pickup already supplied the staff. Omit the catalog's
            // redundant status-edit step; keep its reviewed source scenes.
            if (session.GameStatus.StaffHeld != true) throw new InvalidOperationException("actual staff pickup absent");
            recipe = recipe with { Stages = recipe.Stages.Select(stage => stage with { Steps = stage.Steps.Where(step => step.Kind != "staff").ToArray() }).ToArray() };
            var result = LoomCatalogStartPreparer.Prepare(session, recipe);
            if (result.Failure is not null) throw new InvalidOperationException(result.Failure);
            Record("approved-source-catalog-prefix");
        }
        session.VisitRoomById(46); Settle("late-catalog-start");
        SaveRoom(46);
        var rd = data.ReadRoom(46);
        var holes = data.ReadObjectVerbs(46, rd.Objects).Where(p => p.Value.Any(v => v.VerbId == 0x3B && !v.IsFallback)).Select(p => p.Key).ToArray();
        foreach (int id in holes)
        {
            Use(46, id, 0x36, "hole-excursion-" + id);
            if (session.CurrentRoomId != 46)
            {
                int room = session.CurrentRoomId!.Value; SaveRoom(room);
                var objects = data.ReadRoom(room).Objects;
                var returns = data.ReadObjectVerbs(room, objects).Where(p => p.Value.Any(v => v.VerbId == 0x38 && !v.IsFallback)).Where(p =>
                    data.ReadObjectVerb(room, objects.Single(o => o.Id == p.Key), 0x38, out _)!.Instructions.Any(i => i.Operation == "loadRoomWithEgo" && i.Operands.Split(", ")[1] == "0x2E")).Select(p => p.Key).ToArray();
                if (returns.Length != 1) throw new InvalidOperationException($"Room {room}: actual source return needs review ({returns.Length} direct roots)");
                var portal = objects.Single(o => o.Id == returns[0]);
                SaveSource($"room-{room}-object-{portal.Id}-38", data.ReadObjectVerb(room, portal, 0x38, out _));
                // Normal input also runs the Walk callback before Use. Rusty's
                // portal first completes its conversation, then accepts a later click.
                for (int attempt = 0; attempt < 2 && session.CurrentRoomId == room; attempt++)
                {
                    session.Click(portal.X + portal.Width / 2, portal.Y + portal.Height / 2);
                    Settle($"actual-hole-return-{id}-click-{attempt + 1}");
                    if (session.CurrentRoomId == room)
                    {
                        var healing = Roots(room, 0x4D, "startScript", "0xD1,");
                        if (healing.Length == 1) Cast(room, healing[0], 0x4D, "earned-Healing-return");
                    }
                }
            }
            if (session.CurrentRoomId != 46) throw new InvalidOperationException("excursion did not return to Room 46");
            Cast(46, id, 0x3B, "cast-Closing-" + id);
            Check(session.ObjectClasses.TryGetValue(id, out var cls) && (cls & (1u << 23)) != 0, "actual input closes hole " + id);
        }
        Check(observations.Any(o => o.Site is { Script: 29, Offset: 0, Stage: "instruction" } && o.Values.GetValueOrDefault("local[0x000]") == 7), "observed actual Script 29(7)");
        Check(session.GameStatus.AvailableNoteCount == 7, "four actual closures earn seven notes");
    }
    else throw new ArgumentException("Unknown route");
    Check(!session.InCutscene && session.GameStatus.CanEdit, "settled outside cutscene");
    Check(session.CutscenesBegun == session.SessionTrace.Count(t => t.StartsWith("endCutscene at ")), "balanced source scopes");
}
catch (Exception ex) { failure = ex.ToString(); Record("failure"); Console.WriteLine(failure); }
File.WriteAllText(Path.Combine(output, "packet.json"), JsonSerializer.Serialize(new { edition, route, before, after = session.GameStatus, frames, failure, checks, records, observations, trace = session.SessionTrace, sourceHashes }, new JsonSerializerOptions { WriteIndented = true }));
File.WriteAllLines(Path.Combine(output, "checks.txt"), checks);
return failure is not null || checks.Any(s => s.StartsWith("FAIL ")) ? 1 : 0;
