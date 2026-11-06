using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Scumm;

// Usage: Still <game folder> <room>
// 6 November 2026: a still room's session against the room's first-visit picture: what each paints, and where they differ.
var game = new GameFolderScanner().Scan(args[0]);
var id = int.Parse(args[1]);
var room = game.Rooms.First(r => r.ResourceId == id);
var boot = ScummGameSession.BootForPlay(game, out _);
ScummCutscenePlanner.IntoControl(boot);
var picture = ScummRoomFirstVisit.Run(game, id);
var result = ((ScummGameSession)ScummBootSnapshot.Of(boot).Start()).PrepareAmbientRoom(room, requireAmbient: false, picture: picture.State);
var session = (ScummGameSession)result.Session!;
var (top, bottom) = picture.MainScreen ?? (16, 144);
session.UseScreen((top, bottom));
var screen = new ScummPlayScreen(game, session, picture.IndexedStates);
var scene = screen.Scene()!;
string Painted(ScummRoomScene s) => string.Join(",", s.Selection.PaintOrder().Select(o => $"{o.Id}@{s.Selection.GetPosition(o).X},{s.Selection.GetPosition(o).Y}"));
Console.WriteLine($"picture outcome {picture.Outcome} frames {picture.Frames} camera {picture.State.CameraX} viewLeft {picture.State.ViewLeft}; session camera {session.CameraX} viewLeft {session.ViewLeft}");
Console.WriteLine("picture paints " + Painted(picture));
Console.WriteLine("session paints " + Painted(scene));
Console.WriteLine($"images {picture.Images.Count} and {scene.Images.Count}; room {picture.Room.Width}x{picture.Room.Height} and {scene.Room.Width}x{scene.Room.Height}");
string Actors(ScummRoomScene s) => string.Join(" ", s.State.Actors.Where(a => a.Room == id).Select(a => $"{a.Number}:c{a.Look.Costume}@{a.X},{a.Y}{(a.Visible == true ? "" : "h")}"));
Console.WriteLine("picture actors " + Actors(picture));
Console.WriteLine("session actors " + Actors(scene));
var a = picture.Compose();
var b = scene.Compose();
var differentRoom = a.Zip(b).Count(p => p.First != p.Second);
Console.WriteLine($"room layer differs by {differentRoom} of {a.Length} pixels");
var pa = picture.BackgroundPalette();
var pb = scene.BackgroundPalette();
Console.WriteLine($"palette differs in {pa.Zip(pb).Count(p => p.First != p.Second)} of {pa.Count}");
var la = picture.ActorLayer();
var lb = scene.ActorLayer();
var actorsDiffer = 0;
for (var i = 0; i < a.Length; i++) if (la.IsDrawn(i) != lb.IsDrawn(i) || la.IsDrawn(i) && la.Pixels[i] != lb.Pixels[i]) actorsDiffer++;
Console.WriteLine($"actor layer differs by {actorsDiffer} pixels");
