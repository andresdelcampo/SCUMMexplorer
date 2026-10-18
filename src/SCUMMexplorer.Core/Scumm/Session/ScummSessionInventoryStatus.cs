using System.Text.Json;
using System.Text.RegularExpressions;

namespace SCUMMexplorer.Core.Scumm;

public sealed record ScummInventoryCharacter(int ActorId, string? Name, string Evidence);
public sealed record ScummInventoryItem(int ObjectId, int OwnerId, int Slot, int RetainedRoomId, string? Name);
public sealed record ScummInventoryTransfer(int ObjectId, int TargetActorId);
public sealed record ScummInventoryEditResult(bool Applied, string? Refusal, ScummInventoryUndoToken? Undo);

/// <summary>A read-only view and an opaque optimistic edit token; capturing it runs no scripts.</summary>
public sealed class ScummInventoryStatusSnapshot
{
    internal ScummInventoryStatusSnapshot(object session, long frame, string signature, int? room, int actor,
        ScummInventoryCharacter[] characters, ScummInventoryItem[] items, string? refusal)
    {
        Session = session; Frame = frame; Signature = signature; RoomId = room; CurrentActorId = actor;
        Characters = Array.AsReadOnly(characters); Items = Array.AsReadOnly(items); Refusal = refusal;
    }
    internal object Session { get; }
    internal long Frame { get; }
    internal string Signature { get; }
    public int? RoomId { get; }
    public int CurrentActorId { get; }
    public IReadOnlyList<ScummInventoryCharacter> Characters { get; }
    public IReadOnlyList<ScummInventoryItem> Items { get; }
    public string? Refusal { get; }
    public bool CanEdit => Refusal is null;
}

public sealed class ScummInventoryUndoToken
{
    internal ScummInventoryUndoToken(object session, long revision, long frame, string signature, (int Object, byte Owner)[] owners)
    { Session = session; Revision = revision; Frame = frame; Signature = signature; Owners = owners; }
    internal object Session { get; }
    internal long Revision { get; }
    internal long Frame { get; }
    internal string Signature { get; }
    internal (int Object, byte Owner)[] Owners { get; }
}

public abstract partial class ScummSession
{
    private long _inventoryStatusFrame;
    private long _inventoryEditRevision;
    private readonly object _inventoryStatusIdentity = new();
    private (string Variable, int Offset)[]? _inventoryPartyBindings;

    public ScummInventoryStatusSnapshot CaptureInventoryStatus()
    {
        var characters = InventoryCharacters();
        var items = _runtime.InventorySlots.Select((id, slot) => (id, slot)).Where(x => x.id != 0)
            .Select(x => new ScummInventoryItem(x.id, _runtime.ObjectOwners.GetValueOrDefault(x.id), x.slot,
                _runtime.InventoryRooms.GetValueOrDefault(x.id), InventoryObjectName(x.id) is { } bytes
                    ? ScummTextDecoder.DecodeGlyphs(bytes).TrimEnd('\0') : null)).ToArray();
        return new(_inventoryStatusIdentity, _inventoryStatusFrame, InventoryStatusSignature(), CurrentRoomId,
            _runtime.GlobalVariables.GetValueOrDefault(_runtime.EgoVariable), characters, items, InventoryEditRefusal(characters));
    }

    public ScummInventoryEditResult ApplyInventoryTransfers(ScummInventoryStatusSnapshot snapshot,
        IReadOnlyList<ScummInventoryTransfer> transfers)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(transfers);
        var characters = InventoryCharacters();
        if (InventoryEditRefusal(characters) is { } refusal) return new(false, refusal, null);
        if (!ReferenceEquals(snapshot.Session, _inventoryStatusIdentity) || snapshot.Frame != _inventoryStatusFrame ||
            snapshot.Signature != InventoryStatusSignature()) return new(false, "The inventory view is stale; reopen it.", null);
        if (!snapshot.CanEdit) return new(false, "That view was not editable.", null);
        var batch = transfers.ToArray();
        if (batch.Length == 0) return new(false, "No ownership changes were requested.", null);
        if (batch.Select(x => x.ObjectId).Distinct().Count() != batch.Length)
            return new(false, "An item occurs more than once in the batch.", null);
        var eligible = characters.Where(x => !string.IsNullOrWhiteSpace(x.Name)).Select(x => x.ActorId).ToHashSet();
        foreach (var change in batch)
        {
            if (!eligible.Contains(change.TargetActorId) || change.TargetActorId is <= 0 or >= 15)
                return new(false, "The recipient is not a resolved playable character.", null);
            if (_runtime.InventorySlots.Count(id => id == change.ObjectId) != 1 ||
                !_runtime.InventoryRooms.ContainsKey(change.ObjectId) ||
                !_runtime.ObjectOwners.TryGetValue(change.ObjectId, out var owner) || !eligible.Contains(owner) ||
                InventoryObjectName(change.ObjectId) is not { Length: > 0 })
                return new(false, "The item is not a resolved held inventory item.", null);
        }
        var changed = batch.Where(x => _runtime.ObjectOwners[x.ObjectId] != x.TargetActorId).ToArray();
        if (changed.Length == 0) return new(false, "No ownership changes were requested.", null);
        var original = changed.Select(x => (x.ObjectId, _runtime.ObjectOwners[x.ObjectId])).ToArray();
        // Explicit exploration edit: native setOwnerOf's owner-table component only.
        // No inventory callback, pickup, slot removal, object-state write or script runs.
        foreach (var change in changed) _runtime.SetObjectOwner(change.ObjectId, (byte)change.TargetActorId);
        _inventoryEditRevision++;
        _visitTrace.Add("Explicit inventory choice: " + string.Join("; ", changed.Select((x, i) =>
            $"object {x.ObjectId}, actor {original[i].Item2} -> actor {x.TargetActorId}")) +
            ". Explicit exploration ownership edit; no pickup, scene or script was played.");
        return new(true, null, new(_inventoryStatusIdentity, _inventoryEditRevision, _inventoryStatusFrame, InventoryStatusSignature(), original));
    }

    public ScummInventoryEditResult UndoInventoryTransfers(ScummInventoryUndoToken token)
    {
        ArgumentNullException.ThrowIfNull(token);
        if (InventoryEditRefusal(InventoryCharacters()) is { } refusal) return new(false, refusal, null);
        if (!ReferenceEquals(token.Session, _inventoryStatusIdentity) || token.Revision != _inventoryEditRevision || token.Frame != _inventoryStatusFrame ||
            token.Signature != InventoryStatusSignature())
            return new(false, "The last change no longer matches this inventory.", null);
        foreach (var (id, owner) in token.Owners) _runtime.SetObjectOwner(id, owner);
        _inventoryEditRevision++;
        _visitTrace.Add("Explicit inventory undo: " + string.Join("; ", token.Owners.Select(x =>
            $"object {x.Object}, restored actor {x.Owner}")) + ". No pickup, scene or script was played.");
        return new(true, null, null);
    }

    private string? InventoryEditRefusal(ScummInventoryCharacter[] characters)
    {
        if (!_runtime.IsV2) return "This engine has no reviewed inventory edit contract.";
        if (this is not ScummGameSession { BootReport: not null } || CurrentRoomId is null)
            return "The session is still starting.";
        if (InCutscene || !UserInputOn) return "The game has not released control.";
        if (ActorsMoving || CameraMoving) return "A character or the camera is moving.";
        if (Messages.HaveMsg != 0) return "Dialogue is still playing.";
        if (HasPendingPlayInput || _runtime.Sentences.Count != 0) return "Player input is still pending.";
        if (InventoryPlayerActionRunning)
            return "A player action is still running.";
        var ego = _runtime.GlobalVariables.GetValueOrDefault(_runtime.EgoVariable);
        if (characters.Length == 0 || !characters.Any(x => x.ActorId == ego && !string.IsNullOrWhiteSpace(x.Name)))
            return "Playable character identities are unresolved.";
        return null;
    }

    // o2_doSentence distinguishes ordinary foreground object slots from FA
    // background+special and FD special slots. Those resident handlers may
    // animate indefinitely while the player has control; only an ordinary
    // pending slot is an unfinished player action. Executing code still blocks.
    protected bool InventoryPlayerActionRunning => _executing.Count != 0 ||
        IsScriptRunning(PlayInputScript).Running || IsScriptRunning(2).Running ||
        _pending.Any(x => x.Frame.ObjectNumber is not null && !x.Frame.Stopped &&
            !x.Frame.Recursive && !x.Frame.FreezeResistant);

    private ScummInventoryCharacter[] InventoryCharacters()
    {
        if (!_runtime.IsV2) return [];
        // Native input script assignments followed by actorFollowCamera establish
        // selected-party variables, without hard-coding this edition's party slots.
        if (_inventoryPartyBindings is null)
        {
            var bindings = new List<(string, int)>();
            try
            {
                var code = _data.GlobalScript(PlayInputScript)?.Instructions ?? [];
                for (var i = 0; i + 1 < code.Count; i++)
                {
                    var op = code[i];
                    if (op.Operation != "move" || code[i + 1].Operation != "actorFollowCamera" ||
                        code[i + 1].Operands != _runtime.EgoVariable) continue;
                    var parts = op.Operands.Split(',', StringSplitOptions.TrimEntries);
                    if (parts.Length == 2 && parts[0] == _runtime.EgoVariable &&
                        Regex.IsMatch(parts[1], @"^var\[0x[0-9A-Fa-f]{3}\]$")) bindings.Add((parts[1], op.Offset));
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException) { }
            _inventoryPartyBindings = bindings.Distinct().ToArray();
        }
        IEnumerable<(int Id, string Evidence)> candidates;
        if (_inventoryPartyBindings.Length > 0)
            candidates = _inventoryPartyBindings.Select(b => ((int)_runtime.GlobalVariables.GetValueOrDefault(b.Variable),
                $"Script {PlayInputScript} +0x{b.Offset:X4}: ego <- {b.Variable}, followed by actorFollowCamera"));
        else
        {
            var first = _runtime.GlobalVariables.GetValueOrDefault(ScummVars.V2.FirstPlayerActor);
            var last = _runtime.GlobalVariables.GetValueOrDefault(ScummVars.V2.LastPlayerActor);
            candidates = first > 0 && last >= first && last < 15
                ? Enumerable.Range(first, last - first + 1).Select(id => (id,
                    $"Native Actor_v2::isPlayer range {ScummVars.V2.FirstPlayerActor}..{ScummVars.V2.LastPlayerActor}")) : [];
        }
        return candidates.Where(x => x.Id > 0 && x.Id < 15 && _runtime.Actors.Contains(x.Id))
            .GroupBy(x => x.Id).Select(g => new ScummInventoryCharacter(g.Key,
                _runtime.ActorNames.GetValueOrDefault(g.Key), g.First().Evidence)).OrderBy(x => x.ActorId).ToArray();
    }

    // Exact relevant state, not a lossy hash. Includes state/class/code-room/slots
    // and eligible identities so undo cannot overwrite a later game inventory edit.
    private string InventoryStatusSignature() => JsonSerializer.Serialize(new
    {
        Room = CurrentRoomId, Ego = _runtime.GlobalVariables.GetValueOrDefault(_runtime.EgoVariable),
        Characters = InventoryCharacters(), Slots = _runtime.InventorySlots,
        Rooms = _runtime.InventoryRooms.OrderBy(x => x.Key),
        Objects = _runtime.InventorySlots.Where(id => id != 0).Distinct().Order().Select(id => new
        { Id = id, Owner = _runtime.ObjectOwners.GetValueOrDefault(id), State = _runtime.ObjectStates.GetValueOrDefault(id),
            Classes = _runtime.ObjectClasses.GetValueOrDefault(id) }), Revision = _inventoryEditRevision
    });
}
