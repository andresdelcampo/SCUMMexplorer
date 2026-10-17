namespace SCUMMexplorer.Core.Scumm;

public abstract partial class ScummSession
{
    private long _nextV2ObjectSlot;

    // script_v2.cpp o2_doSentence reuses the first object slot with BOTH flags
    // matching. FA is background+special; FD is special; ordinary verbs are neither.
    // runObjectScript only stops all nonrecursive copies for version >= 3.
    protected ScriptFrame ReplaceV2ObjectSlot(int objectId, bool background, bool special)
    {
        var previous = V2ObjectSlots(objectId)
            .Where(slot => slot.FreezeResistant == background && slot.Recursive == special)
            .OrderBy(slot => slot.ObjectSlotOrder).FirstOrDefault();
        if (previous is not null)
        {
            if (_executing.Contains(previous))
                _uncertainties.Add($"Object {objectId} replaces an executing v2 object slot; resuming its replaced caller pointer is not modelled.");
            previous.Stopped = true;
            _pending.RemoveAll(item => ReferenceEquals(item.Frame, previous));
        }
        return new ScriptFrame
        {
            ObjectNumber = objectId,
            ObjectSlotOrder = previous?.ObjectSlotOrder ?? ++_nextV2ObjectSlot,
            FreezeResistant = background,
            Recursive = special
        };
    }

    // script.cpp stopObjectScript: FE cancels every object slot, including a
    // suspended caller, without cancelling a global script with the same number.
    protected int StopV2ObjectScripts(int objectId)
    {
        if (objectId == 0) return 0;
        var slots = V2ObjectSlots(objectId).ToArray();
        if (slots.Length > 0)
            _observer?.Cancel(objectId, _executing.Count > 0 ? _executing[^1].ObservationSlot : 0,
                slots.Select(slot => slot.ObservationSlot));
        foreach (var slot in slots) slot.Stopped = true;
        _pending.RemoveAll(item => item.Frame.Stopped);
        return slots.Length;
    }

    private IEnumerable<ScriptFrame> V2ObjectSlots(int objectId) =>
        _executing.Concat(_pending.Select(item => item.Frame))
            .Concat(_frameScripts.Select(item => item.Frame))
            .Where(slot => slot.ObjectNumber == objectId && !slot.Stopped).Distinct();
}
