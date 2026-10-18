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
    private bool _inventoryPartyEstablished;

    private IEnumerable<(int Id, string Evidence)> InventoryPartyCandidates()
    {
        var current = _inventoryPartyBindings!.Select(b => ((int)_runtime.GlobalVariables.GetValueOrDefault(b.Variable),
            $"Script {PlayInputScript} +0x{b.Offset:X4}: ego <- {b.Variable}, followed by actorFollowCamera")).ToArray();
        // Filling the menu slots is not Start: the native startup subsequently
        // places every chosen actor. Remember that commitment, so later hiding
        // or removing a party member does not make the other recipients vanish.
        if (!_inventoryPartyEstablished)
            _inventoryPartyEstablished = current.Length > 0 && current.Select(x => x.Item1).Distinct().Count() == current.Length &&
                current.All(x => _runtime.Actors.Contains(x.Item1) && _runtime.Actors[x.Item1].Room is > 0);
        return _inventoryPartyEstablished ? current : [];
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
        var sourceEffectTargets = new HashSet<int>();
        foreach (var item in indexed.States.Where(x => (x.Value & 1) != 0))
            candidates[item.Key] = (null, "Index: native v1/v2 Pickupable flag");
        var globals = _data.GlobalScriptIds().ToDictionary(id => id, id => _data.GlobalScript(id));
        var roots = new List<(ScummScriptDisassembly? Script, int? Object, int? Room)>();
        foreach (var script in globals.Values) roots.Add((script, null, null));
        foreach (var entry in rooms)
        {
            roots.Add((_data.RoomEntryScript(entry.Key), null, entry.Key));
            roots.Add((_data.RoomExitScript(entry.Key), null, entry.Key));
            foreach (var local in _data.RoomLocalScriptIds(entry.Key)) roots.Add((_data.RoomLocalScript(entry.Key, local), null, entry.Key));
            var verbs = _data.ReadObjectVerbs(entry.Key, entry.Value.Objects);
            foreach (var record in entry.Value.Objects.GroupBy(o => o.Id).Select(g => g.Last()))
                foreach (var verb in verbs.GetValueOrDefault(record.Id) ?? [])
                    roots.Add((_data.ReadObjectVerb(entry.Key, record, verb.VerbId, out _),
                        verb.VerbId is 250 or 253 or 254 ? null : record.Id, entry.Key));
        }
        void Note(string text) { if (!_inventoryCatalogNotes.Contains(text)) _inventoryCatalogNotes.Add(text); }
        var objectScripts = new Dictionary<(int Room, int Object, int Verb), ScummScriptDisassembly?>();
        ScummScriptDisassembly? ObjectScript(int room, int id, int verb)
        {
            var key = (room, id, verb);
            if (objectScripts.TryGetValue(key, out var cached)) return cached;
            // The resource may be an off-room retained item; resolve only a unique
            // source, never a room/object allowlist or the first arbitrary match.
            var records = rooms.GetValueOrDefault(room)?.Objects.Where(o => o.Id == id).ToArray() ?? [];
            if (records.Length == 0 && locations.TryGetValue(id, out var found) && found.Select(x => x.Room).Distinct().Count() == 1)
            { room = found[0].Room; records = [found.Last().Object]; }
            if (records.Length == 0) return objectScripts[key] = null;
            var verbs = _data.ReadObjectVerbs(room, records).GetValueOrDefault(id) ?? [];
            if (!verbs.Any(x => x.VerbId == verb || x.VerbId == 255)) return objectScripts[key] = null;
            return objectScripts[key] = _data.ReadObjectVerb(room, records.Last(), verb, out _);
        }
        // Only an immutable Pickupable bit can decide a branch from index state.
        // First census every possible writer, without pruning on that bit.
        var writerSites = new Dictionary<(string Label, int Offset), HashSet<int>>();
        var writes = new ScummInventoryCatalogAnalysis(globals, ObjectScript, (script, instruction, id, _, stateWrite) =>
        {
            if (!stateWrite) return;
            var key = (script.Context ?? $"Script {script.ScriptId}", instruction.Offset);
            if (!writerSites.TryGetValue(key, out var targets)) writerSites[key] = targets = [];
            if (id is int target) targets.Add(target);
        }, Note);
        foreach (var root in roots) writes.Scan(root.Script, root.Object, root.Room);
        var mutable = writerSites.Values.SelectMany(x => x).ToHashSet();
        var unknownWriter = writerSites.Values.Any(x => x.Count == 0);
        Note($"Pickupable writers: {string.Join(';', writerSites.Select(x => $"{x.Key.Label} +0x{x.Key.Offset:X4} -> [{string.Join(',', x.Value.Order())}]"))}; unbound-only writer: {unknownWriter}.");
        var flags = indexed.States.ToDictionary(x => x.Key,
            x => unknownWriter || mutable.Contains(x.Key) ? (bool?)null : (x.Value & 1) != 0);
        var analysis = new ScummInventoryCatalogAnalysis(globals, ObjectScript, (script, instruction, id, room, stateWrite) =>
        {
            if (stateWrite && instruction.Operands.EndsWith(", Unpickupable", StringComparison.Ordinal)) return;
            if (id is not int target)
            {
                Note($"Parameterized pickup template: {script.Context ?? $"Script {script.ScriptId}"} +0x{instruction.Offset:X4}: {instruction.Operation} {instruction.Operands.Split(',')[0]}");
                return;
            }
            if (!locations.ContainsKey(target)) return;
            sourceEffectTargets.Add(target);
            // A known source room is useful only when it actually contains the
            // target's code, not merely its resident proxy/helper handler.
            int? sourceRoom = room is int r && locations[target].Any(x => x.Room == r) ? r : null;
            if (sourceRoom is int nativeRoom)
            {
                if (!pickupRooms.TryGetValue(target, out var sources)) pickupRooms[target] = sources = [];
                sources.Add(nativeRoom);
            }
            candidates[target] = (sourceRoom ?? candidates.GetValueOrDefault(target).Room,
                $"{script.Context ?? $"Script {script.ScriptId}"} +0x{instruction.Offset:X4}: {instruction.Operation} {instruction.Operands.Split(',')[0]}");
        }, Note, flags);
        foreach (var root in roots) analysis.Scan(root.Script, root.Object, root.Room);
        foreach (var candidate in candidates)
        {
            if (!locations.TryGetValue(candidate.Key, out var sources))
                _inventoryCatalogNotes.Add($"Excluded {candidate.Key}: pickup metadata has no indexed object code.");
            else if (pickupRooms.TryGetValue(candidate.Key, out var picked) && picked.Count > 1 ||
                !candidate.Value.Room.HasValue && sources.Select(x => x.Room).Distinct().Count() > 1)
                _inventoryCatalogNotes.Add($"Excluded {candidate.Key}: more than one source room ({string.Join(',', sources.Select(x => x.Room).Distinct())}).");
        }
        _inventoryCatalogNotes.Add($"Source candidates: {candidates.Count}; object-code locations: {locations.Count}.");
        var catalog = candidates.Where(x => locations.ContainsKey(x.Key))
            .Where(x => !pickupRooms.TryGetValue(x.Key, out var sources) || sources.Count == 1)
            .Where(x => x.Value.Room.HasValue || locations[x.Key].Select(l => l.Room).Distinct().Count() == 1)
            .Select(x =>
            {
                var room = x.Value.Room ?? locations[x.Key][0].Room;
                var name = rooms[room].ObjectNames.TryGetValue(x.Key, out var bytes)
                    ? ScummTextDecoder.DecodeGlyphs(bytes).TrimEnd('\0', '@', ' ') : null;
                return new ScummInventoryCatalogItem(x.Key, room, name, x.Value.Evidence);
            }).OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(x => x.ObjectId).ToArray();
        bool KnownEmptyObjectVerb(int room, int id, int verb)
        {
            var records = rooms.GetValueOrDefault(room)?.Objects.Where(o => o.Id == id).ToArray() ?? [];
            if (records.Length == 0 && locations.TryGetValue(id, out var found) && found.Select(x => x.Room).Distinct().Count() == 1)
            { room = found[0].Room; records = [found.Last().Object]; }
            if (records.Length == 0) return false;
            var verbs = _data.ReadObjectVerbs(room, records).GetValueOrDefault(id) ?? [];
            return !verbs.Any(x => x.VerbId == verb || x.VerbId == 255);
        }
        var retainedCode = catalog.ToDictionary(x => x.ObjectId, x =>
            (_data.ReadObjectVerbs(x.SourceRoomId, rooms[x.SourceRoomId].Objects.Where(o => o.Id == x.ObjectId).ToArray())
                .GetValueOrDefault(x.ObjectId) ?? []).Any(v => v.VerbId != 14 &&
                    ObjectScript(x.SourceRoomId, x.ObjectId, v.VerbId) is { Instructions.Count: > 0 }));
        var aliases = ScummInventoryCatalogIdentity.FindSceneAliases(catalog, sourceEffectTargets, globals,
            ObjectScript, KnownEmptyObjectVerb, id => retainedCode.GetValueOrDefault(id), Note);
        foreach (var alias in aliases.OrderBy(x => x.Key))
            Note($"Scene alias {alias.Key} returns retained inventory item {alias.Value}; excluded as an additional catalog item.");
        return _inventoryCatalog = catalog.Where(x => !aliases.ContainsKey(x.ObjectId)).ToArray();
    }

    private static int? InventoryLiteral(string operand) => operand.StartsWith("0x", StringComparison.Ordinal) &&
        int.TryParse(operand.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var number) ? number : null;
}


