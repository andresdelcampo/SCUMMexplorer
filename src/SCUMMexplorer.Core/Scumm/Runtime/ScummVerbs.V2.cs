namespace SCUMMexplorer.Core.Scumm;

public sealed partial class ScummVerbTable
{
    /// <summary>o2_verbOps delete takes a zero-based script slot, NOT a verb ID.</summary>
    public string DeleteV2(int rawSlot)
    {
        var index = V2Slot(rawSlot);
        LastCover = null;
        Kill(index);
        return $"v2 verbOps deletes slot {index}";
    }

    /// <summary>o2_verbOps FF looks up the first unsaved verb ID; an absent ID writes scratch slot zero.</summary>
    public string SetV2Mode(int verbId, int mode)
    {
        var index = FindSlot(verbId);
        _slots[index].Mode = mode;
        LastCover = null;
        var drawing = index == 0 ? "scratch slot (never drawn)" : Draw(_slots[index]);
        return $"v2 verbOps 0x{verbId:X2} mode {mode}, slot {index}: {drawing}";
    }

    /// <summary>
    /// o2_verbOps creates text in the explicitly supplied slot, even if the same
    /// verb ID exists elsewhere. Coordinates are script bytes in units of eight
    /// pixels. Defaults describe DOS EGA; edition-specific offsets/colors come
    /// from the profile. This is the engine table, not a verb-area UI implementation.
    /// </summary>
    public string CreateV2(int verbId, int rawSlot, int rawX, int rawY, int prep, string name,
        int? charset = null, int highlightColor = 14, int color = 2, int yOffset = 0, string? estimate = null)
    {
        var index = V2Slot(rawSlot);
        var slot = _slots[index];
        slot.Reset(verbId);
        slot.Mode = 1;
        slot.X = rawX * 8;
        slot.Y = rawY * 8 + yOffset;
        slot.Preposition = prep;
        slot.Name = name;
        slot.Charset = charset;
        slot.Color = color;
        slot.HighlightColor = highlightColor;
        const string keyboard = "qwertasdfgzxcvb";
        slot.Key = index <= keyboard.Length ? keyboard[index - 1] : 0;
        slot.Estimate = estimate;
        LastCover = null;
        return $"v2 verbOps 0x{verbId:X2} creates slot {index}: {Draw(slot)}";
    }

    private static int V2Slot(int rawSlot)
    {
        if (rawSlot < 0 || rawSlot >= Count - 1)
            throw new InvalidDataException($"v2 verbOps slot {rawSlot} + 1 is outside the verb table");
        return rawSlot + 1;
    }
}
