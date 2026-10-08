namespace SCUMMexplorer.Core.Formats;

/// <summary>A reviewed catalog start, supplied only with its edition's fingerprinted replay data.</summary>
public sealed record LoomCatalogStartRecipe(int Room, string[] EstablishedBits,
    LoomCatalogStartStage[] Stages, string Note);

/// <summary>A source-scene stage omitted when the running state already has its source-established result.</summary>
public sealed record LoomCatalogStartStage(string SkipVariable, int SkipValue, LoomReplayStep[] Steps);

public sealed record LoomCatalogStartResult(bool Prepared, string? Failure, string Note);

/// <summary>Runs explicit source inputs and waits; never assigns story bits, costumes, boxes or actor positions.</summary>
public static class LoomCatalogStartPreparer
{
    public static LoomCatalogStartResult Prepare(LoomExplorationSession session, LoomCatalogStartRecipe recipe)
    {
        // Already reached by the game, or already has a later source state: do not replay its story.
        if (session.Snapshot.RoomId == recipe.Room || recipe.EstablishedBits.Any(bit => Value(session, bit) == 1))
            return new(false, null, "The running session already has this room or its established story state; no preparation ran.");
        foreach (var stage in recipe.Stages)
        {
            if (Value(session, stage.SkipVariable) == stage.SkipValue) continue;
            var replay = new LoomReplaySequence(session, stage.Steps);
            if (!replay.RunSilently(stage.Steps.Length))
                return new(false, replay.Failure ?? "The source preparation exceeded its frame budget.", recipe.Note);
        }
        if (!recipe.EstablishedBits.Any(bit => Value(session, bit) == 1))
            return new(false, "The source scenes did not establish the required story state.", recipe.Note);
        return new(true, null, recipe.Note);
    }

    private static int? Value(LoomExplorationSession session, string key)
    {
        var variables = session.Snapshot.KnownGlobalVariables;
        if (variables.TryGetValue(key, out var direct)) return direct;
        if (!key.StartsWith("bit[0x", StringComparison.Ordinal)) return null;
        if (variables.ContainsKey(LoomBoundedScriptExecutor.SeparateBitStorage)) return 0;
        var bit = Convert.ToInt32(key[6..^1], 16);
        return variables.TryGetValue($"var[0x{bit >> 4:X3}]", out var word) ? (word >> (bit & 15)) & 1 : null;
    }
}
