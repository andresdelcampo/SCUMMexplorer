using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;
using SCUMMexplorer.Core.Rendering;
var game = new GameFolderScanner().Scan(args[0]);
var res = game.Rooms.Single(r => r.ResourceId == 34);
foreach (var r in new LoomRoomObjectDirectory().Decode(File.ReadAllBytes(res.FilePath)))
    Console.WriteLine($"obj {r.Id} at {r.X},{r.Y} {r.Width}x{r.Height} walk {r.WalkX},{r.WalkY} parent {r.ParentLocalIndex}/{r.ParentRequiredState}");
