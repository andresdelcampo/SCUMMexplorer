using SCUMMexplorer.Core.Rendering;
using System.Text.Json;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;
var game = new GameFolderScanner().Scan(args[0]);
var objects = new LflObjectStateReader().ReadSnapshot(Path.Combine(args[0], "00.LFL"));
var cat = JsonDocument.Parse(File.ReadAllText(args[1])).RootElement;
var e = cat.GetProperty("replays").EnumerateArray().Single(r => r.GetProperty("id").GetString() == "sequence-087-098");
var steps = e.GetProperty("steps").Deserialize<LoomReplayStep[]>(new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
var s = new LoomExplorationSession(game, objects);
s.VisitRoom(game.Rooms.Single(r => r.ResourceId == 34));
var seq = new LoomReplaySequence(s, steps);
seq.RunSilently(e.GetProperty("silentSteps").GetInt32());
string Ego()
{
    var a = s.Actors.Single(x => x.Number == 1); var l = a.Look!;
    return $"room {s.Snapshot.RoomId} costume {l.Costume} vis {a.Visible} frame {l.Frame} facing {l.Facing} init {l.InitFrame} stand {l.StandFrame} limbs [{string.Join(",", l.Animation.Positions)}] stopped {l.Animation.Stopped:X} est {l.Estimate} pos ({a.X},{a.Y})";
}
string Cels(LoomExplorationSession ss)
{
    var a = ss.Actors.Single(x => x.Number == 1); var l = a.Look!;
    var c = game is null ? null : LoomCostume.Load(game, l.Costume ?? 1);
    return c is null ? "no costume" : string.Join(" ", c.Draw(l.Animation, l.Direction ?? 2).Select(x => $"L{x.Limb}:{x.Width}x{x.Height}@{x.Left},{x.Top}"));
}
var rres = game.Rooms.Single(r => r.ResourceId == 34);
var raw = File.ReadAllBytes(rres.FilePath);
var bg = new LoomEgaRoomDecoder().Decode(raw);
var records = new LoomRoomObjectDirectory().Decode(raw);
var images = new LoomRoomObjects().Decode(raw);
var costumes = new Dictionary<int, LoomCostume?>();
LoomCostume? Cost(int id) { if (!costumes.TryGetValue(id, out var c)) costumes[id] = c = LoomCostume.Load(game, id); return c; }
string Mask(string tag)
{
    var snap = s.Snapshot;
    var sel = new RoomObjectSelection(images, snap.ObjectStates, snap.ObjectPositions, records, bg, objects.States, snap.DrawOrder);
    var m = sel.ComposeDepthMask(bg);
    var with = LoomActorRenderer.Render(s.Actors, 34, bg.Width, bg.Height, Cost, m, s.CurrentWalkBoxes, s.ObjectClasses);
    var without = LoomActorRenderer.Render(s.Actors, 34, bg.Width, bg.Height, Cost);
    var ego = with.Actors.FirstOrDefault(a => a.Number == 1); var ego2 = without.Actors.FirstOrDefault(a => a.Number == 1);
    return $"{tag}: masked pixels {ego?.Pixels} bounds {ego?.Bounds} | unmasked pixels {ego2?.Pixels} bounds {ego2?.Bounds}; skipped {string.Join("|", with.Skipped)}";
}
Console.WriteLine(Mask("start"));
foreach (var bx in s.CurrentWalkBoxes!.Boxes) Console.WriteLine($"box {bx.Index}: UL{bx.UpperLeft} UR{bx.UpperRight} LR{bx.LowerRight} LL{bx.LowerLeft} mask {bx.Mask} flags {bx.Flags:X}");
{
    var ego0 = s.Actors.Single(x => x.Number == 1);
    var snap = s.Snapshot;
    var sel = new RoomObjectSelection(images, snap.ObjectStates, snap.ObjectPositions, records, bg, objects.States, snap.DrawOrder);
    var m = sel.ComposeDepthMask(bg)!;
    Console.WriteLine($"ego box {ego0.WalkBox} boxmask {(ego0.WalkBox is int b ? s.CurrentWalkBoxes?.GetMask(b) : null)} classes {string.Join(",", s.ObjectClasses.Where(k => k.Key == 1).Select(k => k.Value))}");
    Console.WriteLine("mask rows x 50..80, y 30..80 ('#' set):");
    for (var y = 30; y <= 80; y += 2) Console.WriteLine($"{y,3} " + string.Concat(Enumerable.Range(50, 40).Select(x => m[y * bg.Width + x] != 0 ? '#' : '.')));
    var bgm = bg.DepthMask!.Pixels.ToArray();
    Console.WriteLine("room-only mask rows:");
    for (var y = 30; y <= 80; y += 4) Console.WriteLine($"{y,3} " + string.Concat(Enumerable.Range(50, 40).Select(x => bgm[y * bg.Width + x] != 0 ? '#' : '.')));
    foreach (var o in images) if (o.Mask is not null && o.X < 100 && o.X + o.Width > 50) Console.WriteLine($"obj {o.Id} at {o.X},{o.Y} {o.Width}x{o.Height} active {sel.IsActive(o)} maskSet {o.Mask.Count(v => v != 0)}");
    Console.WriteLine("states 555..570: " + string.Join(",", Enumerable.Range(555, 16).Select(i => $"{i}={snap.ObjectStates.GetValueOrDefault(i)}")) + $" faithful={snap.IsFaithful} byCode=[{string.Join(",", snap.StatesSetByCode.Where(i => i >= 555 && i <= 570))}]");
    foreach (var o in images.Where(o => o.Id >= 555 && o.Id <= 570)) Console.WriteLine($"  img {o.Id} state? at {o.X},{o.Y} {o.Width}x{o.Height} mask {(o.Mask is null ? "none" : "yes")} active {sel.IsActive(o)}");
    var fresh = new LoomExplorationSession(game, objects); fresh.VisitRoom(rres);
    var fs = fresh.Snapshot;
    Console.WriteLine("FRESH states 555..570: " + string.Join(",", Enumerable.Range(555, 16).Select(i => $"{i}={fs.ObjectStates.GetValueOrDefault(i)}")) + $" faithful={fs.IsFaithful}");
    Console.WriteLine("snapshot states of objects: " + string.Join(",", snap.ObjectStates.Take(40).Select(kv => $"{kv.Key}={kv.Value}")));
}
Console.WriteLine("cels start: " + Cels(s));
Console.WriteLine("start: " + Ego());
var f = 0; for (; f < 6000 && seq.Continue(); f++)
{
    s.AdvanceFrame(); seq.FrameAdvanced();
    if (f % 10 == 0 && f < 130) Console.WriteLine(Mask($"f{f}")); Console.WriteLine($"f{f}: frame {s.Actors.Single(x=>x.Number==1).Look!.Frame} dir {s.Actors.Single(x=>x.Number==1).Look!.Direction} limbs [{string.Join(",", s.Actors.Single(x=>x.Number==1).Look!.Animation.Positions.Take(4))}] cels " + Cels(s));
}
Console.WriteLine($"frames={f} complete={seq.Complete} failure={seq.Failure}");
