namespace SCUMMexplorer.Core.Scumm;

public sealed record ScummSessionSnapshot(
    int? RoomId,
    IReadOnlyDictionary<int, byte> ObjectStates,
    IReadOnlyDictionary<int, RoomObjectPosition> ObjectPositions,
    IReadOnlyDictionary<int, byte> ObjectOwners,
    IReadOnlyDictionary<string, ushort> KnownGlobalVariables,
    IReadOnlyCollection<int> Inventory,
    bool InventoryKnown,
    IReadOnlyCollection<int> ActiveSounds,
    int PendingScripts,
    IReadOnlyList<string> Uncertainties,
    IReadOnlyList<int>? DrawQueue = null,
    IReadOnlyCollection<int>? StatesSetInRoom = null,
    IReadOnlyList<ScummRandomPick>? Picks = null,
    IReadOnlyCollection<int>? EstimatedObjectStates = null,
    IReadOnlyDictionary<int, uint>? Classes = null,
    IReadOnlyList<ScummMessage>? Messages = null)
{
    /// <summary>The messages on the screen: strings drawn since the room was drawn, then the talk message.</summary>
    public IReadOnlyList<ScummMessage> ScreenMessages => Messages ?? [];

    /// <summary>Class bits per object number; empty when the index gave none.</summary>
    public IReadOnlyDictionary<int, uint> ObjectClasses => Classes ?? new Dictionary<int, uint>();

    /// <summary>
    /// ScummVM <c>findObject</c> skips untouchable objects. Unknown classes count as touchable.
    /// </summary>
    public bool IsUntouchable(int objectId) =>
        ObjectClasses.TryGetValue(objectId, out var bits) && ScummObjectClasses.Has(bits, ScummObjectClasses.Untouchable);

    /// <summary>Every seeded <c>getRandomNr</c> stand-in taken on this route, in order.</summary>
    public IReadOnlyList<ScummRandomPick> RandomPicks => Picks ?? [];

    /// <summary>Objects whose state or draw followed a seeded pick. These are estimates, not reached states.</summary>
    public IReadOnlyCollection<int> EstimatedStates => EstimatedObjectStates ?? [];

    /// <summary>Objects drawn since the last redraw, in call order (painted over directory order).</summary>
    public IReadOnlyList<int> DrawOrder => DrawQueue ?? [];

    /// <summary>Objects whose state a reached <c>setState</c> set since this room was entered.</summary>
    public IReadOnlyCollection<int> StatesSetByCode => StatesSetInRoom ?? [];

    /// <summary>Runtime state slots to preserve when estimating artwork, including
    /// estimated execution. This does not promote them into StatesSetByCode.</summary>
    public IReadOnlyCollection<int> ArtworkStateIds() =>
        StatesSetByCode.Concat(EstimatedStates).Distinct().ToArray();

    public bool IsFaithful => RoomId.HasValue && PendingScripts == 0 && Uncertainties.Count == 0;
}

public sealed record ScummObjectActionResult(
    int SourceRoomId, int ObjectId, int VerbId, int ScriptOffset,
    ScummScriptDisassembly? Script, ScummBoundedScriptResult? Run,
    ScummScriptEvent? ReachedRoomLoad, int? DestinationRoomId,
    IReadOnlyList<string> Uncertainties, string? Input = null)
{
    /// <summary>True when the action was a click sent through the game's input scripts, not a direct verb call.</summary>
    public bool IsGameInput => Input is not null;

    private string Source => Input ?? $"Room {SourceRoomId:00} object {ObjectId}, verb 0x{VerbId:X2}, code 0x{ScriptOffset:X4}";

    public string Evidence => ReachedRoomLoad is null
        ? $"{Source}; no room load was reached."
        : $"{Source}; " +
          $"{ReachedRoomLoad.Operation} at {ReachedRoomLoad.SourceLabel} +0x{ReachedRoomLoad.Offset:X4} → " +
          (DestinationRoomId is int target ? $"Room {target:00}" : "unresolved room") +
          (ReachedRoomLoad.Certainty == ScummScriptEventCertainty.Reached ? "." : " (estimated execution).");
}

/// <summary>
/// A sound command on the session's route. <paramref name="Sequence"/> orders it among
/// the route's events; <paramref name="RoomId"/> is the room that was current, and
/// <paramref name="OriginRoomId"/> the room whose code started the chain of scripts
/// that ran the command (null for a chain the engine started: the boot or a room hook).
/// </summary>
public sealed record ScummSessionSoundEvent(ScummScriptEvent Event, int Sequence, int? RoomId, int? OriginRoomId);
