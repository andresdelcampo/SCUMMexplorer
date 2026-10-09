
namespace SCUMMexplorer.Core.Scumm;

/// <summary>One actor drawn into the layer: where its pixels are in room coordinates, and why it may differ from the game.</summary>
public sealed record ScummDrawnActor(int Number, int Costume, int X, int Y, int Elevation, int Frame, int Direction,
    (int Left, int Top, int Right, int Bottom)? Bounds, int Pixels, string? Estimate)
{
    /// <summary>The z-plane the actor was drawn behind (0: in front of everything; 1: behind the room's depth mask).</summary>
    public int ZPlane { get; init; }
}

/// <summary>The actors of the current room as an indexed layer the size of the room (<see cref="ScummActorRenderer.Transparent"/> where no actor is).</summary>
public sealed record ScummActorLayer(byte[] Pixels, int Width, int Height, IReadOnlyList<ScummDrawnActor> Actors, IReadOnlyList<string> Skipped,
    bool[]? Coverage = null)
{
    /// <summary>
    /// Whether an actor pixel is at this index of <see cref="Pixels"/>. The CD game's costumes use every palette index (255 as well),
    /// so its coverage is kept apart from the <see cref="ScummActorRenderer.Transparent"/> value.
    /// </summary>
    public bool IsDrawn(int index) => Coverage is not null ? Coverage[index] : Pixels[index] != ScummActorRenderer.Transparent;
}

/// <summary>
/// Draws the actors in the current room as ScummVM's <c>processActors</c> does for v3: the actors in the room are
/// sorted by y with the engine's own exchange sort (for each j, for each i, swap when y[j] &lt; y[i]; ScummVM notes that
/// this unstable order is the original's), and each one with a costume is drawn (<c>drawActorCostume</c>) at
/// (x, y − elevation) in that order, so a lower actor ends up on top. Only visible actors are drawn. Pixels are clipped
/// to the room. With a depth mask, an actor whose walk box has a nonzero mask is hidden where the mask is set
/// (<c>drawActorCostume</c>: <c>_zbuf = getMaskFromBox(_walkbox)</c>, at most <c>_numZBuffer - 1</c> = 1 in v3). The
/// actor's classes come first, as in <c>drawActorCostume</c> before v7: class 21 (AlwaysClip, which <c>putClass</c> turns
/// into <c>_forceClip</c> through <c>classChanged</c>) draws it behind plane 1 whatever its box, and class 20 (NeverClip)
/// draws it in front of every mask. LOOM's scripts set one of the two on most actors they place (for example Room 43's
/// sheep: <c>setClass [0x0000, 0x0096, 0x0094]</c>, ignore boxes and never clip).
/// </summary>
public static class ScummActorRenderer
{
    public const byte Transparent = 0xFF;

    /// <summary><c>kObjectClassNeverClip</c>: the actor is drawn in front of every mask.</summary>
    public const int NeverClipClass = 20;

    /// <summary><c>kObjectClassAlwaysClip</c>: <c>classChanged</c> sets <c>_forceClip</c>, so the actor is drawn behind plane 1.</summary>
    public const int AlwaysClipClass = 21;

    /// <summary>ScummVM's order for the actors in the room (all of them, visible or not, as the engine sorts them).</summary>
    public static IReadOnlyList<ScummActorSnapshot> SortForDrawing(IEnumerable<ScummActorSnapshot> actors, int currentRoom)
    {
        var sorted = actors.Where(actor => actor.Room == currentRoom).OrderBy(actor => actor.Number).ToArray();
        for (var j = 0; j < sorted.Length; j++)
        for (var i = 0; i < sorted.Length; i++)
            if ((sorted[j].Y ?? 0) < (sorted[i].Y ?? 0))
                (sorted[i], sorted[j]) = (sorted[j], sorted[i]);
        return sorted;
    }

    public static ScummActorLayer Render(IEnumerable<ScummActorSnapshot> actors, int currentRoom, int width, int height,
        Func<int, ScummCostume?> costumes, byte[]? depthMask = null, ScummWalkBoxSet? boxes = null,
        IReadOnlyDictionary<int, uint>? classes = null, IReadOnlyList<byte[]>? extraMasks = null)
    {
        var pixels = new byte[width * height];
        Array.Fill(pixels, Transparent);
        var coverage = new bool[width * height];
        var drawn = new List<ScummDrawnActor>();
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
            // Plane 1 is the only mask plane in v3; an unknown box draws the actor in front (noted as an estimate).
            var actorClasses = classes is not null && classes.TryGetValue(actor.Number, out var bits) ? bits : 0u;
            var forceClip = ScummObjectClasses.Has(actorClasses, AlwaysClipClass);
            var neverClip = !forceClip && ScummObjectClasses.Has(actorClasses, NeverClipClass);
            var zPlane = depthMask is null || neverClip ? 0 : forceClip ? 1 :
                actor.WalkBox is int box ? Math.Min((int)(boxes?.GetMask(box) ?? 0), 1 + (extraMasks?.Count ?? 0)) : 0;
            var boxNote = depthMask is not null && !forceClip && !neverClip && actor.WalkBox is null
                ? "walk box unknown: drawn in front of the depth mask" : null;
            // CD game: a box with a scale of its own (word or slot) sets it where the actor stands, else the actor's own (actorOps scale) holds.
            var scale = Math.Clamp((actor.WalkBox is int scaleBox && !actor.IgnoreBoxes.GetValueOrDefault() ? boxes?.BoxScale(scaleBox, y) : null) ?? look.Scale, 1, 255);
            int Scaled(int value) => scale < 255 ? (int)Math.Floor(value * (double)scale / 255) : value;
            var written = costume.Compose(cels, pixels, width, height, x, top, look.Palette, zPlane > 1 ? extraMasks![zPlane - 2] : zPlane > 0 ? depthMask : null, coverage, scale);
            (int, int, int, int)? bounds = cels.Count == 0 ? null : (
                x + Scaled(cels.Min(cel => cel.Left)), top + Scaled(cels.Min(cel => cel.Top)),
                x + Scaled(cels.Max(cel => cel.Left + cel.Width)), top + Scaled(cels.Max(cel => cel.Top + cel.Height)));
            var estimate = string.Join("; ", new[] { actor.Estimate, look.Estimate, boxNote }.Where(text => text is not null));
            drawn.Add(new ScummDrawnActor(actor.Number, costumeId, x, y, elevation, look.Frame ?? -1, direction, bounds,
                written, estimate.Length == 0 ? null : estimate) { ZPlane = zPlane });
        }
        return new ScummActorLayer(pixels, width, height, drawn, skipped, coverage);
    }
}
