using System.Globalization;
using System.Text.RegularExpressions;

namespace SCUMMexplorer.Core.Scumm;

/// <summary>
/// The inventory editor for version 3 (Indy 3, user 9 November 2026: "Similar to the one from Maniac Mansion -even simpler as it does not have multiple
/// people"): one recipient, the hero; the catalog is every object the scripts pick up (<c>pickupObject</c> of the object itself or a literal) or give the
/// hero (<c>setOwnerOf</c> a literal to the hero); a grant writes what <c>o4_pickupObject</c> writes (inventory slot, owner, untouchable, state 1),
/// then the inventory script runs as on every inventory change.
/// </summary>
public abstract partial class ScummSession
{
    /// <summary>A version 3 game with one hero (Indy 3), whose inventory the editor handles.</summary>
    private bool IsV3Inventory => !_runtime.IsV2 && ScummProfile.For(_game).EngineVersion == ScummVersion.V3;

    private ScummInventoryCharacter[] V3InventoryCharacters()
    {
        var ego = _runtime.GlobalVariables.GetValueOrDefault(_runtime.EgoVariable);
        return ego > 0 && _runtime.Actors.Contains(ego)
            // A hero the scripts have not named yet (Indy 3's boot names no actor) is still the one recipient: named by the role.
            ? [new ScummInventoryCharacter(ego, _runtime.ActorNames.GetValueOrDefault(ego) is { Length: > 0 } name ? name : HeroName(ego) ?? "Hero", $"VAR_EGO ({_runtime.EgoVariable}) = {ego}")]
            : [];
    }

    private string? _heroName;

    /// <summary>
    /// The name the scripts give the hero most often (Indy 3's boot names no actor; Script 118 and Room 25 name actor 1 "Indy", Script 85 names him
    /// "chairs" once, for a gag), for a hero not named yet.
    /// </summary>
    private string? HeroName(int ego)
    {
        if (_heroName is not null) return _heroName.Length > 0 ? _heroName : null;
        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        void Scan(ScummScriptDisassembly? script)
        {
            foreach (var ins in script?.Instructions.Where(i => i.Operation == "actorOps") ?? [])
                if (V3Literal(ins.Operands.Split(',', 2)[0].Trim()) == ego && ActorNameOperand().Match(ins.Operands) is { Success: true } m)
                    counts[m.Groups[1].Value] = counts.GetValueOrDefault(m.Groups[1].Value) + 1;
        }
        try
        {
            foreach (var id in _data.GlobalScriptIds()) Scan(_data.GlobalScript(id));
            foreach (var room in _game.Rooms.Where(r => r.ResourceId.HasValue).Select(r => r.ResourceId!.Value).Distinct())
                foreach (var local in _data.RoomLocalScriptIds(room)) Scan(_data.RoomLocalScript(room, local));
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException) { }
        _heroName = counts.OrderByDescending(c => c.Value).Select(c => c.Key).FirstOrDefault() ?? "";
        return _heroName.Length > 0 ? _heroName : null;
    }

    [GeneratedRegex(@"^([^,]+), ""([^""]*)""$")]
    private static partial Regex RenameOperands();

    [GeneratedRegex(@"actorName, ""([^""]+)""")]
    private static partial Regex ActorNameOperand();

    private ScummInventoryCatalogItem[] V3InventoryCatalog()
    {
        var found = new Dictionary<int, (int Room, string Evidence)>();
        var rooms = _game.Rooms.Where(r => r.ResourceId.HasValue).Select(r => r.ResourceId!.Value).Distinct().Order().ToList();
        var locations = new Dictionary<int, List<int>>();
        var names = new Dictionary<int, string>();
        var ego = _runtime.GlobalVariables.GetValueOrDefault(_runtime.EgoVariable);
        void Take(int id, int room, string evidence) { if (!found.ContainsKey(id)) found[id] = (room, evidence); }
        // The name the pickup itself gives the object (setObjectName in the same code): the three books of Room 17 ("tomo" in the Spanish copy) become
        // "libro mapas", "manual" and "Mein Kampf" as Indy takes them. A rename elsewhere (the package becomes the Grail diary when opened) is not the
        // item as granted.
        var renames = new Dictionary<int, HashSet<string>>();
        void Scan(ScummScriptDisassembly? script, string label, int room, int self)
        {
            var picks = new HashSet<int>();
            foreach (var ins in script?.Instructions ?? [])
                if (ins.Operation is "pickupObject" or "setOwnerOf" && (ins.Operands.Split(',')[0].Trim() is var first && first == "var[0x007]" && self > 0 ? self : V3Literal(first)) is int taken)
                    picks.Add(taken);
            foreach (var ins in script?.Instructions ?? [])
            {
                if (ins.Operation == "setObjectName" && RenameOperands().Match(ins.Operands) is { Success: true } rename &&
                    (rename.Groups[1].Value == "var[0x007]" && self > 0 ? self : V3Literal(rename.Groups[1].Value)) is int renamed and > 0 && picks.Contains(renamed) &&
                    rename.Groups[2].Value.Trim('@', ' ') is { Length: > 0 } newName)
                    (renames.TryGetValue(renamed, out var set) ? set : renames[renamed] = []).Add(newName);
                var parts = ins.Operands.Split(',', StringSplitOptions.TrimEntries);
                if (ins.Operation == "pickupObject" && (parts[0] == "var[0x007]" && self > 0 ? self : V3Literal(parts[0])) is int picked and > 0)
                    Take(picked, room, $"{label} +0x{ins.Offset:X4}: pickupObject {parts[0]}");
                else if (ins.Operation == "setOwnerOf" && parts.Length == 2 && V3Literal(parts[0]) is int given and > 0 &&
                         (parts[1] == _runtime.EgoVariable || V3Literal(parts[1]) == ego && ego > 0))
                    Take(given, 0, $"{label} +0x{ins.Offset:X4}: setOwnerOf {parts[0]}, {parts[1]}");
            }
        }
        foreach (var id in _data.GlobalScriptIds())
        {
            try { Scan(_data.GlobalScript(id), $"Script {id}", 0, 0); }
            catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException) { }
        }
        foreach (var room in rooms)
        {
            try
            {
                var record = _data.ReadRoom(room);
                foreach (var item in record.Objects)
                {
                    (locations.TryGetValue(item.Id, out var list) ? list : locations[item.Id] = []).Add(room);
                    if (record.ObjectNames.TryGetValue(item.Id, out var bytes) && ScummTextDecoder.DecodeGlyphs(bytes).TrimEnd('\0', '@', ' ') is { Length: > 0 } name)
                        names.TryAdd(item.Id, name);
                }
                Scan(_data.RoomEntryScript(room), $"Room {room} entry", room, 0);
                foreach (var local in _data.RoomLocalScriptIds(room)) Scan(_data.RoomLocalScript(room, local), $"Room {room} local {local}", room, 0);
                foreach (var (objectId, verbs) in _data.ReadObjectVerbs(room, record.Objects))
                    foreach (var verb in verbs)
                        Scan(_data.ReadObjectVerb(room, record.Objects.Last(o => o.Id == objectId), verb.VerbId, out _), $"Room {room} object {objectId} verb 0x{verb.VerbId:X2}", room, objectId);
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException or NotSupportedException or ArgumentException) { }
        }
        // An item is granted from the room that holds its code: the picking room when it holds it, else the only room that does.
        return found.Where(f => locations.ContainsKey(f.Key))
            .Select(f => (f.Key, Room: locations[f.Key].Contains(f.Value.Room) ? f.Value.Room : locations[f.Key].Distinct().Count() == 1 ? locations[f.Key][0] : 0, f.Value.Evidence))
            .Where(f => f.Room > 0)
            .Select(f => renames.TryGetValue(f.Key, out var given) && given.Count == 1
                ? new ScummInventoryCatalogItem(f.Key, f.Room, given.First(), f.Evidence + $"; named by the pickup's setObjectName \"{given.First()}\"") { Renamed = true }
                : new ScummInventoryCatalogItem(f.Key, f.Room, names.GetValueOrDefault(f.Key), f.Evidence))
            .OrderBy(x => x.Name, StringComparer.CurrentCultureIgnoreCase).ThenBy(x => x.ObjectId).ToArray();
    }

    /// <summary>
    /// A version 3 grant: what <c>o4_pickupObject</c> writes (the executor's <c>TryApplyPickupObject</c>), without the pickup's verb or scene; and the name
    /// the pickup gives it (<paramref name="name"/>, when the catalog took it from the pickup's own <c>setObjectName</c>).
    /// </summary>
    private void V3Grant(int id, int room, int owner, string? name = null)
    {
        if (name is not null) _runtime.ObjectNameOverrides[id] = System.Text.Encoding.Latin1.GetBytes(name);
        _runtime.AddToInventorySlots(id);
        _runtime.InventoryRooms[id] = room;
        _runtime.SetObjectOwner(id, (byte)owner);
        _runtime.AddInventoryObject(id);
        _runtime.SetObjectClasses(id, _runtime.ObjectClasses.GetValueOrDefault(id) | (1u << (ScummObjectClasses.Untouchable - 1)));
        _runtime.SetObjectState(id, 1);
    }

    /// <summary>The inventory script, run as the engine runs it on every inventory change (<c>runInventoryScript</c>), so the strip shows the change.</summary>
    private void RunInventoryScript()
    {
        if (_runtime.GlobalVariables.GetValueOrDefault(ScummVars.InventoryScript) is not (var script and > 0)) return;
        _dispatchScope++;
        try { RunStartedScript(script, 0, new Dictionary<string, ushort> { ["local[0x000]"] = 1 }, 0); }
        catch (Exception ex) when (ex is IOException or InvalidDataException) { _uncertainties.Add($"Script {script}: {ex.Message}"); }
        finally { _dispatchScope--; }
    }

    private static int? V3Literal(string operand) => operand.StartsWith("0x", StringComparison.Ordinal) &&
        int.TryParse(operand.AsSpan(2), NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var number) ? number : null;
}
