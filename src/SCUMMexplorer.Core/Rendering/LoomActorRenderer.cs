using SCUMMexplorer.Core.Formats;

namespace SCUMMexplorer.Core.Rendering;

/// <summary>One actor drawn into the layer: where its pixels are in room coordinates, and why it may differ from the game.</summary>
public sealed record LoomDrawnActor(int Number, int Costume, int X, int Y, int Elevation, int Frame, int Direction,
    (int Left, int Top, int Right, int Bottom)? Bounds, int Pixels, string? Estimate);

/// <summary>The actors of the current room as an indexed layer the size of the room (<see cref="LoomActorRenderer.Transparent"/> where no actor is).</summary>
public sealed record LoomActorLayer(byte[] Pixels, int Width, int Height, IReadOnlyList<LoomDrawnActor> Actors, IReadOnlyList<string> Skipped);

/// <summary>
/// Draws the actors in the current room as ScummVM's <c>processActors</c> does for v3: the actors in the room are
/// sorted by y with the engine's own exchange sort (for each j, for each i, swap when y[j] &lt; y[i]; ScummVM notes that
/// this unstable order is the original's), and each one with a costume is drawn (<c>drawActorCostume</c>) at
/// (x, y − elevation) in that order, so a lower actor ends up on top. Only visible actors are drawn. Pixels are clipped
/// to the room. Priority masks (actors behind foreground objects) are not applied.
/// </summary>
public static class LoomActorRenderer
{
    public const byte Transparent = 0xFF;

    /// <summary>ScummVM's order for the actors in the room (all of them, visible or not, as the engine sorts them).</summary>
    public static IReadOnlyList<LoomActorSnapshot> SortForDrawing(IEnumerable<LoomActorSnapshot> actors, int currentRoom)
    {
        var sorted = actors.Where(actor => actor.Room == currentRoom).OrderBy(actor => actor.Number).ToArray();
        for (var j = 0; j < sorted.Length; j++)
        for (var i = 0; i < sorted.Length; i++)
            if ((sorted[j].Y ?? 0) < (sorted[i].Y ?? 0))
                (sorted[i], sorted[j]) = (sorted[j], sorted[i]);
        return sorted;
    }

    public static LoomActorLayer Render(IEnumerable<LoomActorSnapshot> actors, int currentRoom, int width, int height,
        Func<int, LoomCostume?> costumes)
    {
        var pixels = new byte[width * height];
        Array.Fill(pixels, Transparent);
        var drawn = new List<LoomDrawnActor>();
        var skipped = new List<string>();
        foreach (var actor in SortForDrawing(actors, currentRoom))
        {
            var look = actor.Look;
            if (look.Costume is 0) continue;
            if (actor.Visible != true)
            {
                if (actor.Visible is null) skipped.Add($"Actor {actor.Number}: its visibility is unknown");
                continue;
            }
            if (look.Costume is not int costumeId || actor.X is not int x || actor.Y is not int y ||
                look.Elevation is not int elevation || look.Direction is not int direction)
            {
                skipped.Add($"Actor {actor.Number}: its costume, position, elevation or facing is unknown");
                continue;
            }
            if (costumes(costumeId) is not { } costume)
            {
                skipped.Add($"Actor {actor.Number}: costume {costumeId} cannot be read");
                continue;
            }
            var cels = costume.Draw(look.Animation, direction);
            var top = y - elevation;
            var written = costume.Compose(cels, pixels, width, height, x, top, look.Palette);
            (int, int, int, int)? bounds = cels.Count == 0 ? null : (
                x + cels.Min(cel => cel.Left), top + cels.Min(cel => cel.Top),
                x + cels.Max(cel => cel.Left + cel.Width), top + cels.Max(cel => cel.Top + cel.Height));
            var estimate = string.Join("; ", new[] { actor.Estimate, look.Estimate }.Where(text => text is not null));
            drawn.Add(new LoomDrawnActor(actor.Number, costumeId, x, y, elevation, look.Frame ?? -1, direction, bounds,
                written, estimate.Length == 0 ? null : estimate));
        }
        return new LoomActorLayer(pixels, width, height, drawn, skipped);
    }
}
