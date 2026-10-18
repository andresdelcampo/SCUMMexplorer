namespace SCUMMexplorer.Core.Scumm;

public abstract partial class ScummSession
{
    // Native getOBCDFromObject addresses either a loaded room header or its retained inventory copy.
    // These header-only edits do not change walk Y, object state/owner, source bytes or game files.
    private readonly Dictionary<(int Room, int Object), byte> _roomObjectPrepositions = [];
    private readonly Dictionary<int, byte> _inventoryObjectPrepositions = [];

    private (int Room, bool Inventory)? ObjectHeaderLocation(int id)
    {
        var owner = _runtime.ObjectOwners.GetValueOrDefault(id, (byte)15);
        if (owner != 15)
            return _runtime.InventorySlots.Contains(id) && _runtime.InventoryRooms.TryGetValue(id, out var heldRoom)
                ? (heldRoom, true) : null;
        return _currentRoom?.ResourceId is int room && _currentObjects.Any(o => o.Id == id) ? (room, false) : null;
    }

    private int? ObjectPreposition(int id)
    {
        if (ObjectHeaderLocation(id) is not { } location) return 255;
        if (location.Inventory && _inventoryObjectPrepositions.TryGetValue(id, out var held)) return held;
        if (!location.Inventory && _roomObjectPrepositions.TryGetValue((location.Room, id), out var changed)) return changed;
        try { return _data.ReadRoom(location.Room).Objects.LastOrDefault(o => o.Id == id)?.Preposition; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        { _uncertainties.Add($"Object {id} preposition header could not be read: {ex.Message}"); return null; }
    }

    private bool SetObjectPreposition(int id, byte value)
    {
        if (ObjectHeaderLocation(id) is not { } location) return true; // Native missing-object write is a no-op.
        value &= 7; // byte 12 stores only the high three bits; low walk-Y bits survive.
        if (location.Inventory) _inventoryObjectPrepositions[id] = value;
        else _roomObjectPrepositions[(location.Room, id)] = value;
        _runtime.NotifyNativeMutation("object-preposition", id);
        return true;
    }

    private void CopyV2InventoryHeader(int id)
    {
        if (_currentRoom?.ResourceId is not int room || _currentObjects.LastOrDefault(o => o.Id == id) is not { } record) return;
        _inventoryObjectPrepositions[id] = _roomObjectPrepositions.GetValueOrDefault((room, id), record.Preposition);
    }
}
