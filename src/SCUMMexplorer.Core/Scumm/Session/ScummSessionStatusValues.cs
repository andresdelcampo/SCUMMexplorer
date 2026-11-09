namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// The user's Game Status choices beyond the inventory (<see cref="ScummStatusSchema"/>): null or absent leaves a value as it is. Awards and flags map
/// to whether they are given (set) or not.
/// </summary>
public sealed record ScummStatusChoices(int? Health = null, int? Outfit = null, IReadOnlyDictionary<int, bool>? Awards = null,
    IReadOnlyDictionary<string, bool>? Flags = null);

public abstract partial class ScummSession
{
    /// <summary>A global variable's value now, or null when the game has not set it.</summary>
    public int? StatusVariable(string variable) => _runtime.GlobalVariables.TryGetValue(variable, out var value) ? value : null;

    /// <summary>Whether a story bit (<c>bit[0x5CD]</c>) is set now.</summary>
    public bool StatusBit(string bit)
    {
        var variables = _runtime.GlobalVariables;
        if (variables.ContainsKey(ScummBoundedScriptExecutor.SeparateBitStorage)) return variables.GetValueOrDefault(bit) != 0;
        var number = Convert.ToInt32(bit[6..^1], 16);
        return (variables.GetValueOrDefault($"var[0x{number >> 4:X3}]") & (1 << (number & 15))) != 0;
    }

    /// <summary>The points an award slot holds now (0 when not given; the string the award script writes).</summary>
    public int StatusAward(ScummStatusSchema schema, int slot)
    {
        ArgumentNullException.ThrowIfNull(schema);
        return schema.AwardString is int id && _runtime.Strings.TryGetValue(id, out var text) && slot >= 0 && slot < text.Length && text[slot] != (byte)'@'
            ? text[slot] : 0;
    }

    /// <summary>Why Game Status cannot change the session now, or null (the game in control, nothing moving or speaking: as the inventory editor).</summary>
    public string? StatusEditRefusal()
    {
        if (this is not ScummGameSession { BootReport: not null } || CurrentRoomId is null) return "The session is still starting.";
        if (InCutscene || !UserInputOn) return "The game has not released control.";
        if (ActorsMoving || CameraMoving) return "A character or the camera is moving.";
        if (Messages.HaveMsg != 0) return "Dialogue is still playing.";
        if (HasPendingPlayInput || _runtime.Sentences.Count != 0) return "Player input is still pending.";
        if (InventoryPlayerActionRunning) return "A player action is still running.";
        return null;
    }

    /// <summary>
    /// Applies the user's explicit Game Status choices (R-1.8, §12): admin edits of the values the game keeps, written as the game's own code writes them
    /// (the award script's string and total, the dressing script's variable, costume and owners), with no scene or story script played. The inventory
    /// script runs afterwards when an outfit changed hands, as it runs on every inventory change. Returns the refusal, or null when applied.
    /// </summary>
    public string? ApplyStatusChoices(ScummStatusSchema schema, ScummStatusChoices choices)
    {
        ArgumentNullException.ThrowIfNull(schema);
        ArgumentNullException.ThrowIfNull(choices);
        if (StatusEditRefusal() is { } refusal) return refusal;
        var done = new List<string>();
        if (choices.Health is int health && schema.HealthVariable is { } healthVariable && StatusVariable(healthVariable) != health)
        {
            _runtime.SetGlobalVariable(healthVariable, (ushort)Math.Clamp(health, 0, schema.HealthMax));
            done.Add($"health {healthVariable} = {health}");
        }
        var inventoryChanged = false;
        if (choices.Outfit is int outfit && schema.OutfitVariable is { } outfitVariable && schema.Outfits.FirstOrDefault(o => o.ObjectId == outfit) is { } wanted &&
            StatusVariable(outfitVariable) is var worn && worn != outfit)
        {
            // As the dressing script does (Indy 3 Script 52): the outfit taken off goes back to the hero, the one put on is worn (owner 14).
            var ego = _runtime.GlobalVariables.GetValueOrDefault(_runtime.EgoVariable);
            if (worn is int previous && schema.Outfits.Any(o => o.ObjectId == previous) && _runtime.InventorySlots.Contains(previous))
            { _runtime.SetObjectOwner(previous, (byte)ego); inventoryChanged = true; }
            if (_runtime.InventorySlots.Contains(outfit)) { _runtime.SetObjectOwner(outfit, 14); inventoryChanged = true; }
            _runtime.SetGlobalVariable(outfitVariable, (ushort)outfit);
            if (_runtime.Actors.Contains(schema.OutfitActor)) _runtime.Actors.CostumeChanged(schema.OutfitActor, wanted.Costume, CurrentPlacement());
            done.Add($"outfit {outfitVariable} = {outfit}, actor {schema.OutfitActor} costume {wanted.Costume}");
        }
        if (choices.Awards is { Count: > 0 } awards && schema.AwardString is int stringId && schema.AwardTotal is { } total && _runtime.Strings.TryGetValue(stringId, out var text))
            foreach (var (slot, given) in awards)
            {
                if (schema.Awards.FirstOrDefault(a => a.Slot == slot) is not { } award || slot >= text.Length) continue;
                var now = StatusAward(schema, slot);
                if (given == now > 0) continue;
                // Script 125: the points into the slot, and onto the total; taken back, both undone.
                text[slot] = given ? (byte)award.Points : (byte)0;
                var sum = _runtime.GlobalVariables.GetValueOrDefault(total) + (given ? award.Points : -now);
                _runtime.SetGlobalVariable(total, (ushort)Math.Max(0, sum));
                done.Add($"award {slot} {(given ? "given" : "taken back")} ({award.Points})");
            }
        if (choices.Flags is { Count: > 0 } flags)
            foreach (var (bit, set) in flags)
                if (StatusBit(bit) != set && ApplySetupEdit(new ScummSetupEdit(ScummSetupKind.Bit, bit, set ? 1 : 0, "Game Status")))
                    done.Add($"{bit} = {(set ? 1 : 0)}");
        if (inventoryChanged && _runtime.GlobalVariables.GetValueOrDefault(ScummVars.InventoryScript) is var inventoryScript and > 0)
        {
            _dispatchScope++;
            try { RunStartedScript(inventoryScript, 0, null, 0); }
            catch (Exception ex) when (ex is IOException or InvalidDataException) { _uncertainties.Add($"Script {inventoryScript}: {ex.Message}"); }
            finally { _dispatchScope--; }
        }
        if (done.Count > 0)
            _visitTrace.Add("Explicit Game Status choice: " + string.Join("; ", done) + ". Admin edit; no scene or story script was played.");
        return null;
    }
}
