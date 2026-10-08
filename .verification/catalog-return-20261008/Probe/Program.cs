using System.IO;
using System.Text.Json;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;
using SCUMMexplorer.Core.Rendering;

// Deliberately preserves the reported fresh catalog start. Never loads App settings,
// writes story bits/costumes/placements, or claims original-game route coverage.
var game = new GameFolderScanner().Scan(Path.GetFullPath(args[0]));
var output = Path.GetFullPath(args[1]);
Directory.CreateDirectory(output);
var data = ScummDataSources.For(game);
var indexed = new LflObjectStateReader().ReadSnapshot(Path.Combine(game.FolderPath, "00.LFL"));
var session = new LoomExplorationSession(game, indexed);
var states = new List<object>();
var checks = new List<string>();
var frame = 0;
bool? SourceBit(string key)
{
    var vars = session.Snapshot.KnownGlobalVariables;
    if (vars.TryGetValue(key, out var direct)) return direct != 0;
    if (vars.ContainsKey(LoomBoundedScriptExecutor.SeparateBitStorage)) return false;
    var bit = Convert.ToInt32(key[6..^1], 16);
    return vars.TryGetValue($"var[0x{bit >> 4:X3}]", out var word) ? ((word >> (bit & 15)) & 1) != 0 : null;
}
void Check(bool ok, string label) => checks.Add((ok ? "PASS " : "FAIL ") + label);
void Advance(int count) { for (var i = 0; i < count; i++) { session.AdvanceFrame(); frame++; } }
void Capture(string name)
{
    var number = session.Snapshot.RoomId!.Value;
    var resource = game.Rooms.Single(r => r.ResourceId == number);
    var room = new OldV3EgaRoomDecoder().Decode(File.ReadAllBytes(resource.FilePath));
    var snapshot = session.Snapshot;
    var selection = new RoomObjectSelection(data.ReadObjectImages(number), snapshot.ObjectStates,
        snapshot.ObjectPositions, data.ReadRoom(number).Objects, room, indexed.States, snapshot.DrawQueue);
    var composed = selection.Compose(room);
    var layer = LoomActorRenderer.Render(session.Actors, number, room.Width, room.Height, session.ReadCostume,
        selection.ComposeDepthMask(room), session.CurrentWalkBoxes, snapshot.ObjectClasses,
        room.ExtraDepthMasks.Select(p => p.Pixels.ToArray()).ToArray());
    var pixels = new byte[room.Width * room.Height * 4];
    for (var i = 0; i < composed.Length; i++)
    {
        var colour = room.Palette[layer.IsDrawn(i) ? layer.Pixels[i] : composed[i]];
        pixels[4 * i] = (byte)colour; pixels[4 * i + 1] = (byte)(colour >> 8);
        pixels[4 * i + 2] = (byte)(colour >> 16); pixels[4 * i + 3] = 255;
    }
    var bitmap = BitmapSource.Create(room.Width, room.Height, 96, 96, PixelFormats.Bgra32, null, pixels, room.Width * 4);
    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
    using var file = File.Create(Path.Combine(output, name + ".png")); encoder.Save(file);
    states.Add(new { name, frame, room = number, ghost = SourceBit("bit[0xD9C]"),
        late = SourceBit("bit[0xD7A]"), actor = session.Actors.Single(a => a.Number == 3),
        drawn = layer.Actors.Where(a => a.Number == 3), boundaries = session.CutscenesBegun,
        endBoundaries = session.SessionTrace.Count(t => t.StartsWith("endCutscene at ")) });
}
session.VisitRoomById(46);
Advance(40);
Capture("01-catalog-46");
Check(SourceBit("bit[0xD7A]") == true, "Room 46 entry establishes the late flag");
Check(SourceBit("bit[0xD9C]") == false, "catalog selection does not execute Script 87's ghost prerequisite");
var hole = data.ReadRoom(46).Objects.Single(o => o.Id == 685);
session.Click(hole.X + hole.Width / 2, hole.Y + hole.Height / 2, doubleClick: true);
for (var i = 0; i < 1500 && (session.Snapshot.RoomId != 34 || session.InCutscene); i++) Advance(1);
Advance(40);
Check(session.Snapshot.RoomId == 34, "actual double-click dispatch returns from Room 46 to 34");
Check(session.SessionTrace.Any(t => t.Contains("endCutscene at Script 97 +0x0042")), "return reaches Script 97's source end boundary");
Capture("02-return-34");
Check(session.Actors.Single(a => a.Number == 3).Look.Costume == 112,
    "source entry selects the sleeping-boy costume while the ghost prerequisite is clear");
var portal = data.ReadRoom(34).Objects.Single(o => o.Id == 560);
session.Click(portal.X + portal.Width / 2, portal.Y + portal.Height / 2, doubleClick: true);
var observed = false;
for (var i = 0; i < 1800; i++)
{
    Advance(1);
    var actor = session.Actors.Single(a => a.Number == 3);
    if (!observed && actor.Room == 34 && actor.Visible == true && actor.Moving != 0 && actor.X >= 32)
    {
        Capture("03-moving-sleeping-boy"); observed = true;
    }
    if (session.SessionTrace.Any(t => t.Contains("endCutscene at Script 98 +0x0605"))) break;
}
Check(session.SessionTrace.Any(t => t.Contains("startObject runs object 560 verb 0x37")), "portal look is reached through real input dispatch");
Check(session.SessionTrace.Any(t => t.Contains("cutscene at Script 98")), "the reported conversation starts from this incomplete catalog history");
Check(observed, "the renderer receives a moving sleeping-boy actor during the conversation");
Check(SourceBit("bit[0xD9C]") == false, "the conversation does not establish its missing ghost prerequisite");
File.WriteAllText(Path.Combine(output, "states.json"), JsonSerializer.Serialize(states, new JsonSerializerOptions { WriteIndented = true }));
File.WriteAllLines(Path.Combine(output, "trace.txt"), session.SessionTrace);
File.WriteAllLines(Path.Combine(output, "checks.txt"), checks);
File.WriteAllText(Path.Combine(output, "source.txt"), string.Join("\n", new[] {
    data.RoomEntryScript(46)!.Format(), data.RoomEntryScript(34)!.Format(),
    data.GlobalScript(87)!.Format(), data.GlobalScript(97)!.Format(), data.GlobalScript(98)!.Format(),
    data.ReadObjectVerb(34, portal, 0x37, out _)!.Format() }));
Console.WriteLine(string.Join("\n", checks));
return checks.Any(c => c.StartsWith("FAIL ")) ? 1 : 0;
