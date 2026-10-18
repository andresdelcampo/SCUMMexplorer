using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using SCUMMexplorer.Core.Discovery;

// Usage: Indy4BootTrace <Indy 4 folder (ATLANTIS.cue/.img)> <output folder> [extra frames]
// Copied from .verification/monkeycd-session-20261017/BootTrace for S4b Indy4 (17 October 2026); that copy came from .verification/indy3vga-session-20261015/BootTrace (S4b MonkeyCd, 17 October 2026): boots the v5 game with the generic session (ScummGameSession)
// over the v5 data source and writes the boot trace, the pending scripts, the operations reported as not applied, the unsupported list and a
// picture of the room the boot ends in. Nothing here edits session state.
// Usage: BootTrace dump <folder> g:<script> | e:<room> | x:<room> | l:<room>:<local> | find:TEXT  (disassembly through the v5 data source)
if (args[0] == "cd")
{
    // BootTrace cd <folder>: the sounds that start a CD track (ScummV5Archive.ReadCdTrackSounds) and the disc's audio tracks.
    var cdGame = new GameFolderScanner().Scan(args[1]);
    foreach (var archive in cdGame.V4Disks!.Disks.OfType<ScummV5Archive>())
        foreach (var s in archive.ReadCdTrackSounds()) Console.WriteLine(s);
    foreach (var r in cdGame.Resources.Where(r => r.Kind == ResourceKind.Sound && r.ResourceId >= GameLibrary.CdTrackIdBase)) Console.WriteLine($"{r.ResourceId} {r.Name} {r.Description}");
    return;
}
if (args[0] == "sounds")
{
    // Indy4BootTrace sounds <folder> [ids]: what ScummV5Archive.ReadSounds finds for each sound (the parts the session's iMUSE lifetimes read).
    var soundGame = new GameFolderScanner().Scan(args[1]);
    var wanted = args.Skip(2).Select(int.Parse).ToHashSet();
    var all = soundGame.V4Disks!.Disks.OfType<ScummV5Archive>().First().ReadSounds();
    Console.WriteLine($"{all.Count} sounds; with AdLib part {all.Count(s => s.ImuseAdlib is not null)}");
    foreach (var s in all.Where(s => wanted.Count == 0 || wanted.Contains(s.Id))) Console.WriteLine($"sound {s.Id} room {s.Room} adlib {s.ImuseAdlib?.Length} roland {s.ImuseRoland?.Length} digital {s.ImuseDigital?.Length}");
    return;
}
if (args[0] == "dump")
{
    var dumpGame = new GameFolderScanner().Scan(args[1]);
    var source = (ScummV5DataSource)ScummDataSources.For(dumpGame);
    foreach (var spec in args.Skip(2))
    {
        if (spec.StartsWith("find:"))
        {
            var needle = spec[5..];
            void Scan(string title, ScummScriptDisassembly? s)
            {
                if (s is null) return;
                foreach (var i in s.Instructions) if ($"{i.Operation} {i.Operands}".Contains(needle)) Console.WriteLine($"{title} {i.Offset:X4} {i.Operation} {i.Operands}");
            }
            foreach (var id in source.GlobalScriptIds()) Scan($"g:{id}", source.GlobalScript(id));
            foreach (var room in dumpGame.Rooms.Select(r => r.ResourceId!.Value))
            {
                Scan($"e:{room}", source.RoomEntryScript(room));
                Scan($"x:{room}", source.RoomExitScript(room));
                foreach (var local in source.RoomLocalScriptIds(room)) Scan($"l:{room}:{local}", source.RoomLocalScript(room, local));
            }
            continue;
        }
        var p = spec.Split(':');
        if (p[0] == "objs")
        {
            // S6 Indy4 (17 October 2026): a room's objects with their names, places and the verbs they answer.
            var room = int.Parse(p[1]);
            var archive = dumpGame.V4Disks!.Disks.OfType<ScummV5Archive>().First();
            var records = source.ReadRoom(room).Objects;
            Console.WriteLine($"== objects of room {room}");
            foreach (var group in archive.ReadObjectVerbs(room).GroupBy(v => v.ObjectId))
            {
                var r = records.FirstOrDefault(o => o.Id == group.Key);
                Console.WriteLine($"{group.Key} \"{group.First().ObjectName}\" at ({r?.X},{r?.Y}) {r?.Width}x{r?.Height} walk ({r?.WalkX},{r?.WalkY}) verbs {string.Join(" ", group.Select(v => $"0x{v.VerbId:X2}"))}");
            }
            continue;
        }
        ScummScriptDisassembly? d = p[0] switch
        {
            "g" => source.GlobalScript(int.Parse(p[1])),
            "e" => source.RoomEntryScript(int.Parse(p[1])),
            "x" => source.RoomExitScript(int.Parse(p[1])),
            "l" => source.RoomLocalScript(int.Parse(p[1]), int.Parse(p[2])),
            "v" => source.ReadObjectVerb(int.Parse(p[1]), source.ReadRoom(int.Parse(p[1])).Objects.First(o => o.Id == int.Parse(p[2])),
                Convert.ToInt32(p[3], 16), out _),
            _ => null
        };
        Console.WriteLine($"== {spec}");
        if (d is null) { Console.WriteLine("(none)"); continue; }
        foreach (var i in d.Instructions) Console.WriteLine($"{i.Offset:X4} {i.Operation} {i.Operands}");
    }
    return;
}
var folder = args[0];
var output = args[1];
var extraFrames = args.Length > 2 ? int.Parse(args[2]) : 0;
Directory.CreateDirectory(output);
var game = new GameFolderScanner().Scan(folder);
var data = (ScummV5DataSource)ScummDataSources.For(game);
var watch = System.Diagnostics.Stopwatch.StartNew();
var session = new ScummGameSession(game, data.ReadIndexedObjects(), 0, null, data);
var bootTime = watch.Elapsed;
for (var i = 0; i < extraFrames; i++) session.AdvanceFrame();
var report = session.BootReport!;
var snapshot = session.Snapshot;
var lines = new List<string>
{
    $"game: {game.Title} ({game.Id}, {game.Version}), folder {Path.GetFileName(folder)}, v5 resource file",
    $"boot: {report.Frames} frames (limit {ScummSession.BootFrameLimit}), hit limit: {report.HitFrameLimit}, wall time {bootTime.TotalSeconds:0.0} s",
    $"final room: {report.FinalRoom}; rooms entered in order: {string.Join(", ", report.RoomsEntered)}",
    $"extra frames played after the boot: {extraFrames}; room now {session.CurrentRoomId}",
    $"in cutscene: {session.InCutscene}; pending scripts: {session.PendingScriptSummary.Count}",
    ""
};
lines.Add("== pending scripts");
lines.AddRange(session.PendingScriptSummary.Select(item => "  " + item));
lines.Add("");
lines.Add("== session trace (waits omitted)");
lines.AddRange(session.SessionTrace.Where(item => !item.Contains(" waits ") && !item.Contains("delay ends")).Select(item => "  " + item));
lines.Add("");
var uncertainties = snapshot.Uncertainties;
var presentation = uncertainties.SelectMany(item => item.Split("; "))
    .Where(item => item.Contains("presentation operation not applied:"))
    .Select(item => item[(item.LastIndexOf(':') + 1)..].Trim()).GroupBy(item => item).OrderBy(group => group.Key).ToArray();
lines.Add("== operations reported as not applied (presentation only, nothing in game state)");
lines.AddRange(presentation.Select(group => $"  {group.Key}: {group.Count()} runs"));
lines.Add("");
string[] markers = ["unsupported", "not modeled", "could not", "disassembly stops", "undecoded", "cannot", "needs a session", "unknown", "depends on"];
var unsupported = uncertainties.Where(item => markers.Any(item.Contains) && !item.Contains("seeded pick")).Distinct().ToArray();
lines.Add($"== unsupported (opcode, operand or state the run could not follow): {unsupported.Length}");
lines.AddRange(unsupported.Select(item => "  " + (item.Length > 400 ? item[..400] + "..." : item)));
lines.Add("");
lines.Add($"== random picks: {snapshot.RandomPicks.Count} (seeded; each is labelled an estimate)");
lines.Add("== all other uncertainties");
lines.AddRange(uncertainties.Where(item => !markers.Any(item.Contains) && !item.Contains("seeded pick") && !item.Contains("presentation operation") &&
                                          !item.Contains("the delay at")).Distinct().Select(item => "  " + (item.Length > 300 ? item[..300] + "..." : item)));
lines.Add("");
lines.Add("== actors");
lines.AddRange(session.Actors.Where(actor => actor.Room is not null and not 0).Select(actor =>
    $"  actor {actor.Number}: room {actor.Room}, ({actor.X},{actor.Y}), costume {actor.Look.Costume}, visible {actor.Visible}"));
lines.Insert(3, "rooms by name: " + string.Join(", ", report.RoomsEntered.Distinct().Select(id => $"{id} {(game.Rooms.FirstOrDefault(r => r.ResourceId == id)?.Name ?? "(none)")}")));
File.WriteAllLines(Path.Combine(output, "boot-trace.txt"), lines);
if (session.CurrentRoomId is int roomId && game.Rooms.FirstOrDefault(room => room.ResourceId == roomId) is { } resource &&
    game.V4ArchiveFor(resource) is ScummV5Archive roomArchive)
{
    var background = roomArchive.DecodeRoom(roomId);
    var records = data.ReadRoom(roomId).Objects;
    var images = data.ReadObjectImages(roomId);
    var indexed = data.ReadIndexedObjects();
    var selection = new RoomObjectSelection(images, snapshot.ObjectStates, snapshot.ObjectPositions, records, background, indexed.States, snapshot.DrawOrder);
    var pixels = selection.Compose(background);
    var layer = ScummActorRenderer.Render(session.Actors, roomId, background.Width, background.Height, session.ReadCostume);
    for (var i = 0; i < pixels.Length; i++) if (layer.IsDrawn(i)) pixels[i] = layer.Pixels[i];
    var bgra = new byte[pixels.Length * 4];
    for (var i = 0; i < pixels.Length; i++)
    {
        var colour = background.Palette[pixels[i] % background.Palette.Count];
        bgra[i * 4] = (byte)colour; bgra[i * 4 + 1] = (byte)(colour >> 8); bgra[i * 4 + 2] = (byte)(colour >> 16); bgra[i * 4 + 3] = 255;
    }
    var bitmap = System.Windows.Media.Imaging.BitmapSource.Create(background.Width, background.Height, 96, 96, System.Windows.Media.PixelFormats.Bgra32, null, bgra, background.Width * 4);
    var encoder = new System.Windows.Media.Imaging.PngBitmapEncoder();
    encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(bitmap));
    using var stream = File.Create(Path.Combine(output, $"room-{roomId:00}-after-boot.png"));
    encoder.Save(stream);
    File.AppendAllLines(Path.Combine(output, "boot-trace.txt"), [
        "", $"== room {roomId}: {records.Count} objects, {images.Count} stored images, {layer.Actors.Count} actors drawn, {layer.Skipped.Count} skipped",
        .. layer.Skipped.Select(item => "  " + item)]);
}
Console.WriteLine(string.Join(Environment.NewLine, lines.Take(5)));
Console.WriteLine($"unsupported: {unsupported.Length}");
foreach (var item in unsupported.Take(30)) Console.WriteLine("  " + (item.Length > 300 ? item[..300] : item));
