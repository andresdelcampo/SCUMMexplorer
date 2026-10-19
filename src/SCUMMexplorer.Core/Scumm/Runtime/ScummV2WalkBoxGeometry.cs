namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// Native v1/v2 row search and distance (ScummVM actor.cpp,
/// checkXYInBoxBounds / Actor_v2::adjustXYToBeInBox, read 19 October 2026).
/// The public boundary uses room pixels; all search arithmetic uses 8x2 units.
/// </summary>
internal static class ScummV2WalkBoxGeometry
{
    internal static BoxAdjustment Adjust(ScummWalkBoxSet boxes, int x, int y, bool isPlayer, int lastValidX, int lastValidY)
    {
        // V1/v2 keep the requested point on failure; the v3 last-valid-point
        // behavior and threshold passes do not belong to this actor version.
        var result = new BoxAdjustment(x, y, ScummWalkBoxSet.InvalidBox, lastValidX, lastValidY);
        var best = 0xFF;
        for (int id = boxes.Count - 1; id >= 0; id--)
        {
            var flags = boxes.GetFlags(id);
            if ((flags & ScummWalkBox.InvisibleFlag) != 0 &&
                !((flags & ScummWalkBox.PlayerOnlyFlag) != 0 && !isPlayer)) continue;
            var point = ClosestPoint(boxes.Boxes[id], x, y);
            if (point.Distance == 0)
                return new(point.X, point.Y, id, lastValidX, lastValidY);
            if (point.Distance >= best) continue;
            best = point.Distance;
            result = new(point.X, point.Y, id, lastValidX, lastValidY);
        }
        return result;
    }

    internal static (int Distance, int X, int Y) ClosestPoint(ScummWalkBox box, int pixelX, int pixelY)
    {
        int x = pixelX >> 3, y = pixelY >> 1;
        int top = box.UpperLeft.Y >> 1, bottom = box.LowerLeft.Y >> 1;
        int ul = box.UpperLeft.X >> 3, ur = box.UpperRight.X >> 3;
        int ll = box.LowerLeft.X >> 3, lr = box.LowerRight.X >> 3;
        int destY, left, right;
        if (y < top) { destY = top; left = ul; right = ur; }
        else if (y >= bottom) { destY = bottom; left = ll; right = lr; }
        else if (x >= ul && x >= ll && x < ur && x < lr)
        { destY = y; left = right = x; }
        else
        {
            destY = y;
            int middle;
            do
            {
                left = (ul + ll) / 2; right = (ur + lr) / 2;
                middle = (top + bottom) / 2;
                if (middle < y) { top = middle; ul = left; ur = right; }
                else if (middle > y) { bottom = middle; ll = left; lr = right; }
            } while (middle != y);
        }
        var destX = x < left ? left : x > right ? right : x;
        int dx = Math.Abs(x - destX), dy = Math.Abs(y - destY) / 4;
        var distance = dx < dy ? (dx >> 1) + dy : (dy >> 1) + dx;
        return (distance, destX * 8, destY * 2);
    }
}
