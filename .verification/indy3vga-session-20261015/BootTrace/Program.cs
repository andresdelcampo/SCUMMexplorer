using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using SCUMMexplorer.Core.Discovery;

// Usage: BootTrace <Indiana Jones 256-colour game folder (00.LFL and NN.LFL)> <output folder> [extra frames]
// Copied from .verification/monkey1-session-20261014/BootTrace (S4b Indy3Vga, 15 October 2026): boots the game with the generic session (ScummGameSession)
// over the room-file data source and writes the boot trace, the pending scripts, the operations reported as not applied, the unsupported list and a
// picture of the room the boot ends in. Nothing here edits session state.
var folder = args[0];
var output = args[1];
var extraFrames = args.Length > 2 ? int.Parse(args[2]) : 0;
Directory.CreateDirectory(output);
var game = new GameFolderScanner().Scan(folder);
var data = (ScummV4RoomFileDataSource)ScummDataSources.For(game);
var watch = System.Diagnostics.Stopwatch.StartNew();
var session = new ScummGameSession(game, data.ReadIndexedObjects(), 0, null, data);
var bootTime = watch.Elapsed;
for (var i = 0; i < extraFrames; i++) session.AdvanceFrame();
var report = session.BootReport!;
var snapshot = session.Snapshot;
var lines = new List<string>
{
    $"game: {game.Title} ({game.Id}, {game.Version}), folder {Path.GetFileName(folder)}, room files",
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
string[] markers = ["not modeled", "could not", "disassembly stops", "undecoded", "cannot", "needs a session", "unknown", "depends on"];
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
    game.V4ArchiveFor(resource) is ScummV4Archive roomArchive)
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
