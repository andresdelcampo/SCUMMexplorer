namespace SCUMMexplorer.Core.Formats;

/// <summary>
/// One verb slot as <c>o5_verbOps</c> leaves it. Mode: 0 off, 1 on, 2 dim. An
/// image verb shows <see cref="ImageObject"/>'s image, grabbed from
/// <see cref="ImageRoom"/>. Null fields are unknown; <see cref="Estimate"/> says
/// why a value follows an estimate.
/// </summary>
public sealed record LoomVerbSnapshot(int Slot, int VerbId, int? Mode, int? X, int? Y,
    int? ImageObject, int? ImageRoom, string? Name, string? Unknown, string? Estimate)
{
    public string Describe()
    {
        var mode = Mode switch { 0 => "off", 1 => "on", 2 => "dim", _ => "mode unknown" };
        var what = ImageObject is int image ? $"image of object {image}" + (ImageRoom is int room ? $" (Room {room:00})" : "")
            : Name is not null ? $"name \"{Name}\"" : "no image";
        return $"verb 0x{VerbId:X2} (slot {Slot}): {what}, at ({X?.ToString() ?? "?"}, {Y?.ToString() ?? "?"}), {mode}";
    }
}

/// <summary>
/// The verb slots of ScummVM's <c>_verbs</c> for v3 (<c>_numVerbs</c> = 100), as
/// <c>o5_verbOps</c> changes them: <c>getVerbSlot</c> finds a verb's slot or
/// returns 0, the scratch slot that is never drawn; <c>new</c> takes the first
/// free slot and resets it; <c>image</c> and <c>assignObject</c> apply only on a
/// real slot and only when the object is in the current room
/// (<c>setVerbObject</c>, old bundle); <c>delete</c> is <c>killVerb</c>. All
/// slots are empty when the engine starts, which is where the session starts.
/// </summary>
public sealed class LoomVerbTable
{
    public const int Count = 100;
    private readonly Slot[] _slots = Enumerable.Range(0, Count).Select(index => new Slot(index)).ToArray();

    /// <summary>Set when a verb number could not be read: any slot may have changed.</summary>
    public string? Unknown { get; private set; }

    /// <summary>The slots in use (slot 1 upwards), in slot order.</summary>
    public IReadOnlyList<LoomVerbSnapshot> Snapshot() =>
        _slots.Skip(1).Where(slot => slot.VerbId != 0).Select(slot => slot.ToSnapshot()).ToArray();

    public LoomVerbSnapshot? Find(int verbId) =>
        FindSlot(verbId) is var slot and > 0 ? _slots[slot].ToSnapshot() : null;

    public void ForgetAll(string reason)
    {
        Unknown = reason;
        foreach (var slot in _slots) slot.Forget(reason);
    }

    /// <summary>
    /// Applies one <c>verbOps</c>. <paramref name="operations"/> hold resolved
    /// values (null when an operand could not be read). Returns what it did, for the trace.
    /// </summary>
    public string Apply(int verbId, IReadOnlyList<(string Name, int?[] Values, string? Text)> operations,
        Func<int, bool?> objectInCurrentRoom, int? currentRoom, string? estimate)
    {
        var index = FindSlot(verbId);
        var slot = _slots[index];
        slot.VerbId = verbId;
        var notes = new List<string>();
        foreach (var (name, values, text) in operations)
        {
            switch (name)
            {
                case "new":
                    index = FindSlot(verbId);
                    if (index == 0)
                    {
                        index = Array.FindIndex(_slots, 1, item => item.VerbId == 0);
                        if (index < 0) throw new InvalidDataException("verbOps new: too many verbs");
                    }
                    slot = _slots[index];
                    slot.Reset(verbId);
                    notes.Add($"new in slot {index}");
                    break;
                case "image" or "assignObject" when index == 0:
                    notes.Add($"{name} ignored: verb 0x{verbId:X2} has no slot");
                    break;
                case "image" or "assignObject":
                    var image = values.ElementAtOrDefault(0);
                    var room = name == "image" ? currentRoom : values.ElementAtOrDefault(1);
                    if (image is null || room is null)
                    {
                        slot.ImageObject = null;
                        slot.ImageRoom = null;
                        slot.Unknown ??= $"its {name} operand is unknown";
                        notes.Add($"{name} unknown");
                        break;
                    }
                    if (name == "assignObject" && slot.ImageObject == image) break;
                    // setVerbObject copies the image only when the object is one of the
                    // current room's local objects; otherwise the slot keeps its image.
                    if (objectInCurrentRoom(image.Value) is not true)
                    {
                        notes.Add($"image of object {image} not taken: it is not in Room {currentRoom:00}");
                        break;
                    }
                    slot.ImageObject = image;
                    slot.ImageRoom = room;
                    slot.Name = null;
                    notes.Add($"image of object {image}");
                    break;
                case "name":
                    slot.Name = text;
                    slot.ImageObject = slot.ImageRoom = null;
                    notes.Add($"name \"{text}\"");
                    break;
                case "at":
                    slot.X = values.ElementAtOrDefault(0);
                    slot.Y = values.ElementAtOrDefault(1);
                    if (slot.X is null || slot.Y is null) slot.Unknown ??= "its position is unknown";
                    notes.Add($"at ({slot.X?.ToString() ?? "?"}, {slot.Y?.ToString() ?? "?"})");
                    break;
                case "on": slot.Mode = 1; notes.Add("on"); break;
                case "off": slot.Mode = 0; notes.Add("off"); break;
                case "dim": slot.Mode = 2; notes.Add("dim"); break;
                case "delete":
                    if (index != 0) slot.Reset(0);
                    notes.Add("deleted");
                    break;
                default:
                    // Colours, key, centring and back colour do not change what a
                    // verb shows or where; they are not kept.
                    break;
            }
            if (estimate is not null) slot.Estimate ??= estimate;
        }
        var target = index == 0 ? "scratch slot 0 (never drawn)" : $"slot {index}";
        return $"verbOps 0x{verbId:X2} → {target}: {string.Join(", ", notes)}";
    }

    private int FindSlot(int verbId)
    {
        for (var index = 1; index < Count; index++)
            if (_slots[index].VerbId == verbId) return index;
        return 0;
    }

    private sealed class Slot(int index)
    {
        public int VerbId { get; set; }
        public int? Mode { get; set; } = 0;
        public int? X { get; set; } = 0;
        public int? Y { get; set; } = 0;
        public int? ImageObject { get; set; }
        public int? ImageRoom { get; set; }
        public string? Name { get; set; }
        public string? Unknown { get; set; }
        public string? Estimate { get; set; }

        public void Reset(int verbId)
        {
            VerbId = verbId;
            Mode = 0;
            ImageObject = ImageRoom = null;
            Name = null;
            Unknown = Estimate = null;
        }

        public void Forget(string reason)
        {
            Mode = X = Y = ImageObject = ImageRoom = null;
            Name = null;
            Unknown = reason;
        }

        public LoomVerbSnapshot ToSnapshot() =>
            new(index, VerbId, Mode, X, Y, ImageObject, ImageRoom, Name, Unknown, Estimate);
    }
}
