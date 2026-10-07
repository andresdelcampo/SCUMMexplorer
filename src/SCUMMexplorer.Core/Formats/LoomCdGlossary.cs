using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Formats;

public sealed partial class LoomGlossary
{
    // The CD's numbering is different. Reuse presentation names only after checking the CD's own instructions.
    private void ReadCdNames(GameLibrary game)
    {
        var data = ScummDataSources.For(game);
        void Add(string kind, int id, string name, string source, int? to = null)
        {
            if (!_byKind.TryGetValue(kind, out var rows)) _byKind[kind] = rows = [];
            rows.Add((id, to ?? id, new(name, GlossaryEvidence.Decoded, source), null));
        }
        void NameScript(int id, string name, Func<IReadOnlyList<LoomScriptInstruction>, bool> matches)
        {
            if (data.GlobalScript(id) is { } script && matches(script.Instructions))
                Add("script", id, name, $"CD Script {id:00} in {script.ContainerName}; decoded instruction pattern");
        }
        bool Has(IReadOnlyList<LoomScriptInstruction> code, string op, string operands) => code.Any(i => i.Operation == op && i.Operands.Contains(operands, StringComparison.Ordinal));
        NameScript(8, "show or hide the close-up in the verb area", c => Has(c, "startObject", "0x39") && Has(c, "verbOps", "image, var["));
        NameScript(16, "set the text line", c => Has(c, "print", "center; position, 0x00A0, var["));
        NameScript(17, "give the ego a costume (by size)", c => Has(c, "actorOps", "costume, local[0x001]; walkSpeed, local[0x002], local[0x003]"));
        NameScript(18, "show the distaff", c => Has(c, "verbOps", "0x08, [at, 0x00C0, local[0x004]; image,") && Has(c, "print", "0x00A9"));
        NameScript(19, "hide the distaff verbs", c => Has(c, "verbOps", "local[0x000], [off]") && Has(c, "verbOps", "0x31, [off]"));
        NameScript(28, "clear the played notes", c => Has(c, "setVarRange", "count=4, [0x00, 0x00, 0x00, 0x00]") && Has(c, "verbOps", "0x35, [off]"));
        NameScript(42, "place and animate actor 2 at random positions", c => Has(c, "putActor", "0x02, local[0x003], local[0x004]") && Has(c, "getRandomNr", "local[0x001], local[0x000]"));
        foreach (var room in game.Rooms)
        {
            try
            {
                foreach (var (id, bytes) in data.ReadRoom(room.ResourceId!.Value).ObjectNames)
                {
                    var name = System.Text.Encoding.Latin1.GetString(bytes).Trim();
                    if (name.Length > 0) _objects.TryAdd(id, (room.ResourceId.Value, name));
                }
            }
            catch (Exception ex) when (ex is IOException or InvalidDataException) { }
        }
        try
        {
            var schema = LoomStatusSchema.Read(game, data);
            int Number(string key) => Convert.ToInt32(key[6..^1], 16);
            Add("bit", Number(schema.StaffKey), "distaff held", "CD status contract: Script 17 decides between showing and hiding the distaff");
            Add("var", Number(schema.NoteLimitKey), "available note limit (inclusive)", "CD status contract: Script 10 rejects notes above this limit");
            Add("var", schema.DraftBase, "draft table (pattern words)", "CD Script 23 reads two words per draft", schema.DraftBase + schema.DraftCount * 2 - 1);
            Add("var", Number(schema.LookKey), "ego costume size", "CD Script 17 stores its size argument");
            Add("var", Number(schema.CostumeKey), "ego costume", "CD Script 17 stores its costume argument");
        }
        catch (InvalidDataException) { } // Unestablished roles remain numeric; readable structure still works.
    }
}
