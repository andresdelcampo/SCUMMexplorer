namespace SCUMMexplorer.Core.Scumm;

public abstract partial class ScummSession
{
    // script_v2.cpp o2_ifClassOfIs reads immutable OBCD byte 6, from room or retained inventory code.
    // It does not consult the later engine's numbered class table. ScummVM master, 18 October 2026.
    private int? ObjectClassMask(int id)
    {
        if (ObjectHeaderLocation(id) is not { } location) return -1;
        try { return _data.ReadRoom(location.Room).Objects.LastOrDefault(o => o.Id == id) is { } record ? record.V2ClassMask : -1; }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        { _uncertainties.Add($"Object {id} class header could not be read: {ex.Message}"); return null; }
    }
}
