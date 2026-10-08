using SCUMMexplorer.Core.Rendering;

namespace SCUMMexplorer.Core.Formats;

/// <summary>The shared SCUMM findObject selection, in room-directory order, for script input and explorer doorway fallback.</summary>
public static class LoomObjectHitTest
{
    public static (int Found, string? Unknown) Find(IReadOnlyList<LoomRoomObjectRecord> records,
        IReadOnlyDictionary<int, byte> states, IReadOnlyDictionary<int, RoomObjectPosition> positions,
        IReadOnlyDictionary<int, uint> classes, int x, int y)
    {
        var byLocalIndex = records.GroupBy(item => item.LocalIndex).ToDictionary(group => group.Key, group => group.First());
        foreach (var record in records)
        {
            if (record.Id < 1 || classes.TryGetValue(record.Id, out var bits) &&
                LoomObjectClasses.Has(bits, LoomObjectClasses.Untouchable)) continue;
            var at = positions.TryGetValue(record.Id, out var moved) ? (moved.X, moved.Y) : (record.X, record.Y);
            if (!(at.Item1 <= x && at.Item1 + record.Width > x && at.Item2 <= y && at.Item2 + record.Height > y)) continue;
            var current = record;
            var parentsMatch = true;
            for (var remaining = records.Count; current.ParentLocalIndex != 0; remaining--)
            {
                if (remaining < 0 || !byLocalIndex.TryGetValue(current.ParentLocalIndex, out var parent))
                    return (0, $"hits object {record.Id}, whose parent chain cannot be read");
                if (!states.TryGetValue(parent.Id, out var parentState))
                    return (0, $"hits object {record.Id}, whose parent {parent.Id} has an unknown state");
                if ((parentState & 0x0F) != current.ParentRequiredState) { parentsMatch = false; break; }
                current = parent;
            }
            if (parentsMatch) return (record.Id, null);
        }
        return (0, null);
    }
}
