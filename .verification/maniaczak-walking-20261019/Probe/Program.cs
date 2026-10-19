using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Scumm;

var root = args.FirstOrDefault() ?? @"D:\Juegos\Lucas";
if (args.Contains("palette"))
{
    var game = new GameFolderScanner().Scan(Path.Combine(root, "zak ega en"));
    var colors = ScummV1ActorColors.Read(game);
    foreach (var id in game.Resources.Where(r => r.Kind == SCUMMexplorer.Core.Domain.ResourceKind.Costume).Select(r => r.ResourceId).OfType<int>().Distinct().Order())
    {
        var color = colors.ColorOf(id);
        if (color.Basis != ScummV1ColorBasis.Estimate) continue;
        Console.WriteLine($"ESTIMATE {id}: {color.Evidence}");
        foreach (var setting in colors.Settings.Where(s => s.IsCostume && s.Value == id))
            Console.WriteLine($" ASSIGN {setting.Source} +{setting.Offset:X4}: actor={setting.ActorOperand}, costume={setting.ValueOperand}");
    }
    Console.WriteLine($"SETTINGS {colors.Settings.Count}; VARIABLE {colors.UnresolvedInstructions}");
    return;
}
foreach (var folder in new[] { "mmansion ega en", "mmansionE ega es", "zak ega en", "zakE ega en" })
{
    var game = new GameFolderScanner().Scan(Path.Combine(root, folder));
    var source = ScummDataSources.For(game);
    int samples = 0, different = 0, objects = 0, objectDifferent = 0;
    foreach (var roomId in game.Rooms.Select(r => r.ResourceId).OfType<int>().Distinct())
    {
        var room = source.ReadRoom(roomId);
        if (room.WalkBoxes is not { } boxes) continue;
        foreach (var point in Enumerable.Range(0, room.Width / 8 + 1).SelectMany(x =>
                     Enumerable.Range(0, room.Height / 2 + 1).Select(y => (X: x * 8, Y: y * 2))))
            Check(point.X, point.Y, false);
        foreach (var obj in room.Objects) Check(obj.WalkX, obj.WalkY, true);

        void Check(int x, int y, bool objectPoint)
        {
            var actual = boxes.Adjust(x, y, true, 0, 0);
            var expected = Native(boxes, x / 8, y / 2);
            if (objectPoint) objects++; else samples++;
            if ((actual.X, actual.Y, actual.Box) == expected) return;
            if (objectPoint) objectDifferent++; else different++;
            if (objectPoint || different <= 6)
                Console.WriteLine($"{folder}: room {roomId} {(objectPoint ? "object" : "grid")} ({x},{y}) old=({actual.X},{actual.Y},box{actual.Box}) native={expected}");
        }
    }
    Console.WriteLine($"TOTAL {folder}: grid {samples}, different {different}; object approaches {objects}, different {objectDifferent}");
}

// Independent source transcription of actor.cpp checkXYInBoxBounds and
// Actor_v2::adjustXYToBeInBox (official master, read19Oct2026). Runtime units
// here remain native8x2 until the returned point is converted to room pixels.
static (int X, int Y, int Box) Native(ScummWalkBoxSet boxes, int x, int y)
{
    var result = (X: x * 8, Y: y * 2, Box: 255);
    var best = 255;
    for (var id = boxes.Count - 1; id >= 0; id--)
    {
        if ((boxes.GetFlags(id) & 128) != 0) continue;
        var b = boxes.Boxes[id];
        int upper = b.UpperLeft.Y / 2, lower = b.LowerLeft.Y / 2;
        int leftTop = b.UpperLeft.X / 8, leftBottom = b.LowerLeft.X / 8;
        int rightTop = b.UpperRight.X / 8, rightBottom = b.LowerRight.X / 8;
        var py = Math.Clamp(y, upper, lower);
        int lo, hi;
        if (y < upper) { lo = leftTop; hi = rightTop; }
        else if (y >= lower) { lo = leftBottom; hi = rightBottom; }
        else
        {
            while (true)
            {
                var middle = (upper + lower) / 2;
                lo = (leftTop + leftBottom) / 2; hi = (rightTop + rightBottom) / 2;
                if (middle == y) break;
                if (middle < y) { upper = middle; leftTop = lo; rightTop = hi; }
                else { lower = middle; leftBottom = lo; rightBottom = hi; }
            }
        }
        var px = Math.Clamp(x, lo, hi);
        int dx = Math.Abs(x - px), dy = Math.Abs(y - py) / 4;
        var distance = Math.Max(dx, dy) + Math.Min(dx, dy) / 2;
        if (distance == 0) return (px * 8, py * 2, id);
        if (distance >= best) continue;
        best = distance; result = (px * 8, py * 2, id);
    }
    return result;
}
