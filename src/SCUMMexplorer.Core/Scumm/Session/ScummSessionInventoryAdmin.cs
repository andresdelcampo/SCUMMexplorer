using System.Globalization;

namespace SCUMMexplorer.Core.Scumm;

public abstract partial class ScummSession
{
    private ScummInventoryCatalogItem[]? _inventoryCatalog;
    private readonly List<string> _inventoryCatalogNotes = [];
    /// <summary>Source-review detail for Advanced/evidence; never gameplay prerequisites.</summary>
    public IReadOnlyList<string> InventoryCatalogNotes
    {
        get { InventoryCatalog(); return _inventoryCatalogNotes.AsReadOnly(); }
    }
    private (int Id, string Evidence)[]? _inventorySelectableParty;
    private readonly Dictionary<int, (int? Room, int? Object)> _inventoryPartyPictures = [];

    private IEnumerable<(int Id, string Evidence)> InventoryPartyCandidates()
    {
        var current = _inventoryPartyBindings!.Select(b => ((int)_runtime.GlobalVariables.GetValueOrDefault(b.Variable),
            $"Script {PlayInputScript} +0x{b.Offset:X4}: ego <- {b.Variable}, followed by actorFollowCamera"));
        if (_inventorySelectableParty is not null) return current.Concat(_inventorySelectableParty);
        var bindings = _inventoryPartyBindings!.Select(x => x.Variable).ToHashSet();
        var result = new List<(int, string)>();
        var helpers = new List<(int Script, string Argument)>();
        foreach (var id in _data.GlobalScriptIds())
            foreach (var instruction in _data.GlobalScript(id)?.Instructions ?? [])
            {
                var parts = instruction.Operands.Split(',', StringSplitOptions.TrimEntries);
                if (instruction.Operation != "move" || parts.Length != 2 || !bindings.Contains(parts[0])) continue;
                if (InventoryLiteral(parts[1]) is int actor)
                    result.Add((actor, $"Script {id} +0x{instruction.Offset:X4}: native party assignment {instruction.Operands}"));
                else if (!bindings.Contains(parts[1])) helpers.Add((id, parts[1]));
            }
        foreach (var room in _game.Rooms.Where(r => r.ResourceId.HasValue).Select(r => r.ResourceId!.Value).Distinct())
        {
            var data = _data.ReadRoom(room);
            var tables = _data.ReadObjectVerbs(room, data.Objects);
            foreach (var obj in data.Objects.GroupBy(x => x.Id).Select(g => g.Last()))
                foreach (var verb in tables.GetValueOrDefault(obj.Id) ?? [])
                {
                    var script = _data.ReadObjectVerb(room, obj, verb.VerbId, out _);
                    foreach (var helper in helpers.Where(h => script.Instructions.Any(i =>
                        i.Operation is "startScript" or "chainScript" && InventoryLiteral(i.Operands.Split(',')[0].Trim()) == h.Script)))
                        foreach (var instruction in script.Instructions.Where(i => i.Operation == "move"))
                        {
                            var parts = instruction.Operands.Split(',', StringSplitOptions.TrimEntries);
                            if (parts.Length == 2 && parts[0] == helper.Argument && InventoryLiteral(parts[1]) is int actor)
                            {
                                result.Add((actor, $"Room {room} Object {obj.Id} +0x{instruction.Offset:X4}: native party selection through Script {helper.Script}; Script {PlayInputScript} ego switch"));
                                _inventoryPartyPictures.TryAdd(actor, (room, obj.Id));
                            }
                        }
                }
        }
        _inventorySelectableParty = result.Where(x => x.Item1 is > 0 and < 15).Distinct().ToArray();
        return current.Concat(_inventorySelectableParty);
    }

    /// <summary>Explicit admin choices. Grants use native inventory storage but run no acquisition or story scripts.</summary>
    public ScummInventoryEditResult ApplyInventoryEdits(ScummInventoryStatusSnapshot snapshot,
        IReadOnlyList<ScummInventoryTransfer> edits)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(edits);
        var characters = InventoryCharacters();
        if (InventoryEditRefusal(characters) is { } refusal) return new(false, refusal, null);
        if (!ReferenceEquals(snapshot.Session, _inventoryStatusIdentity) || snapshot.Frame != _inventoryStatusFrame ||
            snapshot.Signature != InventoryStatusSignature()) return new(false, "The inventory view is stale; reopen it.", null);
        if (!snapshot.CanEdit) return new(false, "That view was not editable.", null);
        var batch = edits.ToArray();
        if (batch.Length == 0 || batch.Select(x => x.ObjectId).Distinct().Count() != batch.Length)
            return new(false, "Choose each item only once.", null);
        var eligible = characters.Where(x => !string.IsNullOrWhiteSpace(x.Name)).Select(x => x.ActorId).ToHashSet();
        var catalog = InventoryCatalog().ToDictionary(x => x.ObjectId);
        var grants = new Dictionary<int, ScummInventoryCatalogItem>();
        foreach (var edit in batch)
        {
            if (!eligible.Contains(edit.TargetActorId)) return new(false, "The recipient is not a resolved playable character.", null);
            var count = _runtime.InventorySlots.Count(x => x == edit.ObjectId);
            if (count > 1) return new(false, "The native inventory has a duplicate item.", null);
            if (count == 1)
            {
                if (!_runtime.InventoryRooms.ContainsKey(edit.ObjectId) || InventoryObjectName(edit.ObjectId) is not { Length: > 0 })
                    return new(false, "The held item's retained code is unresolved.", null);
            }
            else
            {
                if (!catalog.TryGetValue(edit.ObjectId, out var item) || string.IsNullOrWhiteSpace(item.Name))
                    return new(false, "The item has no resolved inventory source.", null);
                grants.Add(edit.ObjectId, item);
            }
        }
        if (grants.Count > _runtime.InventorySlots.Count(x => x == 0)) return new(false, "The game inventory is full.", null);
        var changed = batch.Where(x => grants.ContainsKey(x.ObjectId) || _runtime.ObjectOwners.GetValueOrDefault(x.ObjectId) != x.TargetActorId).ToArray();
        if (changed.Length == 0) return new(false, "No inventory changes were requested.", null);
        var originals = changed.Select(x => new ScummInventoryOriginal(x.ObjectId,
            _runtime.ObjectOwners.TryGetValue(x.ObjectId, out var owner) ? owner : null,
            _runtime.ObjectStates.TryGetValue(x.ObjectId, out var state) ? state : null,
            _runtime.Inventory.Contains(x.ObjectId), _runtime.InventoryRooms.TryGetValue(x.ObjectId, out var room) ? room : null,
            _inventoryObjectPrepositions.TryGetValue(x.ObjectId, out var prep) ? prep : null,
            Array.IndexOf(_runtime.InventorySlots, x.ObjectId))).ToArray();
        // All source reads and validation finish before the first write.
        Dictionary<int, ScummRoomObjectRecord> headers;
        try { headers = grants.ToDictionary(x => x.Key, x => _data.ReadRoom(x.Value.SourceRoomId).Objects.Last(o => o.Id == x.Key)); }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or InvalidOperationException)
        { return new(false, "An item's retained source code could not be read; no changes were applied.", null); }
        foreach (var edit in changed)
        {
            if (grants.TryGetValue(edit.ObjectId, out var source))
            {
                _runtime.AddToInventorySlots(edit.ObjectId);
                _runtime.InventoryRooms[edit.ObjectId] = source.SourceRoomId;
                var header = headers[edit.ObjectId];
                _inventoryObjectPrepositions[edit.ObjectId] = _roomObjectPrepositions.GetValueOrDefault((source.SourceRoomId, edit.ObjectId), header.Preposition);
                _runtime.AddInventoryObject(edit.ObjectId);
                // o2_pickupObject's intrinsic + untouchable flags; all other state bits survive.
                _runtime.SetObjectState(edit.ObjectId, (byte)(_runtime.ObjectStates.GetValueOrDefault(edit.ObjectId) | 8 | 2));
            }
            _runtime.SetObjectOwner(edit.ObjectId, (byte)edit.TargetActorId);
        }
        _inventoryEditRevision++;
        _visitTrace.Add("Explicit inventory admin choice: " + string.Join("; ", changed.Select(x =>
            $"object {x.ObjectId} {(grants.ContainsKey(x.ObjectId) ? "added" : "transferred")} to actor {x.TargetActorId}")) +
            ". No pickup, scene or story script was played.");
        var owners = originals.Select(x => (x.ObjectId, x.Owner.GetValueOrDefault())).ToArray();
        return new(true, null, new(_inventoryStatusIdentity, _inventoryEditRevision, _inventoryStatusFrame,
            InventoryStatusSignature(), owners) { Originals = originals });
    }

    private ScummInventoryCatalogItem[] InventoryCatalog()
    {
        if (_inventoryCatalog is not null) return _inventoryCatalog;
        if (_data is not ScummV2DataSource source) return _inventoryCatalog = [];
        var indexed = source.ReadIndexedObjects();
        var rooms = _game.Rooms.Where(r => r.ResourceId.HasValue).Select(r => r.ResourceId!.Value).Distinct().Order()
            .ToDictionary(r => r, r => _data.ReadRoom(r));
        var locations = rooms.SelectMany(r => r.Value.Objects.Select(o => (Room: r.Key, Object: o)))
            .GroupBy(x => x.Object.Id).ToDictionary(g => g.Key, g => g.ToArray());
        var candidates = new Dictionary<int, (int? Room, string Evidence)>();
        var pickupRooms = new Dictionary<int, HashSet<int>>();
        foreach (var item in indexed.States.Where(x => (x.Value & 1) != 0))
            candidates[item.Key] = (null, "Index: native v1/v2 Pickupable flag");
        var globals = _data.GlobalScriptIds().ToDictionary(id => id, id => _data.GlobalScript(id));
        var visited = new HashSet<(int Script, int? Object, int? Room)>();
        void Scan(ScummScriptDisassembly? script, int? objectId = null, int? room = null, int depth = 0)
        {
            if (script is null || depth > 16) return;
            var assignments = script.Instructions.Where(i => i.Operation == "move")
                .Select(i => i.Operands.Split(',', StringSplitOptions.TrimEntries)).Where(p => p.Length == 2)
                .GroupBy(p => p[0]).ToDictionary(g => g.Key, g => g.Select(p => p[1]).Distinct().ToArray());
            HashSet<int> Values(string operand, HashSet<string>? seen = null)
            {
                if (InventoryLiteral(operand) is int literal) return [literal];
                if (operand is ScummVars.V2.ActiveObject1 or ScummVars.V2.SentenceObject1 or ScummVars.V2.ClickObject)
                    return objectId is int id ? [id] : [];
                seen ??= [];
                if (!seen.Add(operand) || !assignments.TryGetValue(operand, out var values)) return [];
                return values.SelectMany(v => Values(v, new(seen))).ToHashSet();
            }
            foreach (var instruction in script.Instructions)
            {
                var parts = instruction.Operands.Split(',', StringSplitOptions.TrimEntries);
                if (instruction.Operation == "pickupObject" || instruction.Operation == "setState" && parts.Length > 1 && parts[1] == "Pickupable")
                {
                    var targets = Values(parts[0]);
                    if (targets.Count == 0)
                    {
                        var note = $"Indirect target: {script.Context ?? $"Script {script.ScriptId}"} +0x{instruction.Offset:X4}: {instruction.Operation} {parts[0]}";
                        if (!_inventoryCatalogNotes.Contains(note)) _inventoryCatalogNotes.Add(note);
                    }
                    foreach (var id in targets.Where(locations.ContainsKey))
                    {
                        if (id == objectId && room is int sourceRoom)
                        {
                            if (!pickupRooms.TryGetValue(id, out var sources)) pickupRooms[id] = sources = [];
                            sources.Add(sourceRoom);
                        }
                        candidates[id] = (id == objectId ? room : candidates.GetValueOrDefault(id).Room,
                            $"{script.Context ?? $"Script {script.ScriptId}"} +0x{instruction.Offset:X4}: {instruction.Operation} {parts[0]}");
                    }
                }
                if (instruction.Operation is "startScript" or "chainScript" && InventoryLiteral(parts[0]) is int called &&
                    visited.Add((called, objectId, room)) && globals.TryGetValue(called, out var helper)) Scan(helper, objectId, room, depth + 1);
            }
        }
        foreach (var script in globals.Values) Scan(script);
        foreach (var entry in rooms)
        {
            Scan(_data.RoomEntryScript(entry.Key)); Scan(_data.RoomExitScript(entry.Key));
            foreach (var local in _data.RoomLocalScriptIds(entry.Key)) Scan(_data.RoomLocalScript(entry.Key, local));
            var verbs = _data.ReadObjectVerbs(entry.Key, entry.Value.Objects);
            foreach (var record in entry.Value.Objects.GroupBy(o => o.Id).Select(g => g.Last()))
                foreach (var verb in verbs.GetValueOrDefault(record.Id) ?? [])
                    // FA/FD/FE are resident/special object handlers. Their object is not
                    // the sentence's ActiveObject1; retain only explicit source targets.
                    Scan(_data.ReadObjectVerb(entry.Key, record, verb.VerbId, out _),
                        verb.VerbId is 250 or 253 or 254 ? null : record.Id, entry.Key);
        }
        foreach (var candidate in candidates)
        {
            if (!locations.TryGetValue(candidate.Key, out var sources))
                _inventoryCatalogNotes.Add($"Excluded {candidate.Key}: pickup metadata has no indexed object code.");
            else if (pickupRooms.TryGetValue(candidate.Key, out var picked) && picked.Count > 1 ||
                !candidate.Value.Room.HasValue && sources.Select(x => x.Room).Distinct().Count() > 1)
                _inventoryCatalogNotes.Add($"Excluded {candidate.Key}: more than one source room ({string.Join(',', sources.Select(x => x.Room).Distinct())}).");
        }
        _inventoryCatalogNotes.Add($"Source candidates: {candidates.Count}; object-code locations: {locations.Count}.");
        return _inventoryCatalog = candidates.Where(x => locations.ContainsKey(x.Key))
            .Where(x => !pickupRooms.TryGetValue(x.Key, out var sources) || sources.Count == 1)
            .Where(x => x.Value.Room.HasValue || locations[x.Key].Select(l => l.Room).Distinct().Count() == 1)
            .Select(x =>
            {
                var room = x.Value.Room ?? locations[x.Key][0].Room;
                var name = rooms[room].ObjectNames.TryGetValue(x.Key, out var bytes)
                    ? ScummTextDecoder.DecodeGlyphs(bytes).TrimEnd('\0', '@', ' ') : null;
                return new ScummInventoryCatalogItem(x.Key, room, name, x.Value.Evidence);
            }).OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(x => x.ObjectId).ToArray();
    }

    private static int? InventoryLiteral(string operand) => operand.StartsWith("0x", StringComparison.Ordinal) &&
        int.TryParse(operand.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var number) ? number : null;
}
