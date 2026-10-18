namespace SCUMMexplorer.Core.Scumm;

public abstract partial class ScummSession
{
    // Language comes from reviewed edition hashes, never the folder name.
    private string OldPlayLanguage => _game.Edition?.StartsWith("Spanish", StringComparison.OrdinalIgnoreCase) == true ? "es" : "en";

    /// <summary>The native sentence's raw DOS glyphs (string.cpp drawSentence, read 17 October 2026).</summary>
    public IReadOnlyList<byte> PlaySentenceGlyphs()
    {
        if (!_runtime.IsV2 || (_runtime.V2UserState & 0x20) == 0) return [];
        var sentence = _runtime.V2DrawnSentence;
        if (sentence is null || PlayVerbName(sentence.Value.Verb) is not { } verb) return [];
        var glyphs = new List<byte>(verb);
        void Name(int id)
        {
            if (id > 0 && _runtime.ObjectName?.Invoke(id) is { } name)
            { glyphs.Add(32); glyphs.AddRange(name.TakeWhile(c => c != 0)); }
        }
        Name(sentence.Value.Object1);
        if (sentence.Value.Preposition is > 0 and <= 4)
        {
            string[] prepositions = OldPlayLanguage == "es" ? ["", " en", " con", " en", " a"] : ["", " in", " with", " on", " to"];
            glyphs.AddRange(System.Text.Encoding.ASCII.GetBytes(prepositions[sentence.Value.Preposition]));
        }
        Name(sentence.Value.Object2);
        return glyphs.Where(c => c != '@').Take(40).ToArray();
    }

    private ScummVerbAreaPicture DrawOldInterface(ScummVerbAreaPicture picture, IScummTextCharset charset)
    {
        if (!_runtime.IsV2) return picture;
        var top = MainScreen.Bottom;
        charset.DrawPlainLine(picture.Pixels, 320, ScummVerbArea.Top, PlaySentenceGlyphs(), 0, top, 13);
        if ((_runtime.V2UserState & 0x40) == 0) return picture;
        var inventory = V2Inventory();
        for (var slot = 0; slot < 4 && _v2InventoryOffset + slot < inventory.Length; slot++)
        {
            var x = slot % 2 == 0 ? 0 : 176;
            var y = top + 32 + slot / 2 * 8;
            var name = _runtime.ObjectName?.Invoke(inventory[_v2InventoryOffset + slot]) ?? [];
            var over = CursorShown && _screenPointer is { } p && p.X >= x && p.X < x + 144 && p.Y >= y && p.Y < y + 8;
            charset.DrawPlainLine(picture.Pixels, 320, ScummVerbArea.Top, name.TakeWhile(c => c != 0).Take(18).ToArray(), x, y, (byte)(over ? 14 : 13));
        }
        for (var row = 0; row < 2; row++)
        {
            if (row == 0 ? _v2InventoryOffset == 0 : _v2InventoryOffset + 4 >= inventory.Length) continue;
            var y = top + 32 + row * 8;
            var over = CursorShown && _screenPointer is { } p && p.X >= 144 && p.X < 176 && p.Y >= y && p.Y < y + 8;
            charset.DrawPlainLine(picture.Pixels, 320, ScummVerbArea.Top, new byte[] { 32, (byte)(1 + row * 2), (byte)(2 + row * 2) }, 144, y, (byte)(over ? 14 : 1));
        }
        return picture;
    }
}
