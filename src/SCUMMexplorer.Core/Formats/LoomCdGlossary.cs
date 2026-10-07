using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Formats;

public sealed partial class LoomGlossary
{
    // The CD's numbering is different. Reuse presentation names only after checking the CD's own instructions.
    private void ReadCdNames(GameLibrary game)
    {
        var data = ScummDataSources.For(game);
        ReadCdReviewedNames(data);
        void Add(string kind, int id, string name, string source, int? to = null, GlossaryEvidence evidence = GlossaryEvidence.Decoded)
        {
            if (!_byKind.TryGetValue(kind, out var rows)) _byKind[kind] = rows = [];
            rows.Add((id, to ?? id, new(name, evidence, source), null));
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
        NameScript(28, "clear the draft buffer", c => Has(c, "setVarRange", "count=4, [0x00, 0x00, 0x00, 0x00]") && Has(c, "verbOps", "0x35, [off]"));
        NameScript(36, "restore the note letters", c => Has(c, "print", "text, \"\\x10\"") && Has(c, "print", "text, \"\\x17\""));
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
            Add("bit", Number(schema.DraftStartKey), "the notes played so far are being collected", "CD status contract: Scripts 20–22 collect and compare the played notes", evidence: GlossaryEvidence.Reading);
            Add("bit", Number(schema.DraftPlayKey), "a draft is playing back", "CD status contract: Script 20 sets this flag and Script 21 clears it", evidence: GlossaryEvidence.Reading);
            Add("var", Number(schema.NoteLimitKey), "available note limit (inclusive)", "CD status contract: Script 10 rejects notes above this limit");
            Add("var", schema.DraftBase, "draft table (pattern words)", "CD Script 23 reads two words per draft", schema.DraftBase + schema.DraftCount * 2 - 1);
            Add("var", Number(schema.LookKey), "ego costume size", "CD Script 17 stores its size argument");
            Add("var", Number(schema.CostumeKey), "ego costume", "CD Script 17 stores its costume argument");
        }
        catch (InvalidDataException) { } // Unestablished roles remain numeric; readable structure still works.
        if (data.GlobalScript(23) is { } draft)
        {
            foreach (var (mask, name) in new[] { ("0x2000", "the current draft is learned"), ("0x1000", "the current draft can be reversed"), ("0x4000", "the current draft has last-note knowledge") })
                for (int i = 0; i + 1 < draft.Instructions.Count; i++)
                    if (draft.Instructions[i] is { Operation: "and" } and && and.Operands == $"local[0x001] &= {mask}" &&
                        draft.Instructions[i + 1] is { Operation: "move" } move &&
                        System.Text.RegularExpressions.Regex.Match(move.Operands, @"^bit\[0x([0-9A-F]+)\] = local\[0x001\]$") is { Success: true } bit)
                        Add("bit", Convert.ToInt32(bit.Groups[1].Value, 16), name, $"CD Script 23 +0x{and.Offset:X4}/+0x{move.Offset:X4}: mask {mask}");
        }
        ReadCdVariableNames(data, (id, name, source, count, evidence) => Add("var", id, name, source, id + count - 1, evidence));
    }

    /// <summary>Bind familiar LOOM roles to operands in the CD's own helper code, not to EGA addresses or a numeric offset.</summary>
    private static void ReadCdVariableNames(IScummDataSource data, Action<int, string, string, int, GlossaryEvidence> add)
    {
        const string variable = @"var\[0x(?<id>[0-9A-F]+)\]";
        var scripts = new Dictionary<int, LoomScriptDisassembly?>();
        LoomScriptDisassembly? Script(int id) => scripts.TryGetValue(id, out var code) ? code : scripts[id] = data.GlobalScript(id);
        void Role(int scriptId, string operation, string pattern, string name, int count = 1,
            GlossaryEvidence evidence = GlossaryEvidence.Decoded, Func<int, bool>? corroborates = null)
        {
            if (Script(scriptId) is not { } script) return;
            var matches = script.Instructions.Where(i => i.Operation == operation)
                .Select(i => (Instruction: i, Match: System.Text.RegularExpressions.Regex.Match(i.Operands, pattern)))
                .Where(x => x.Match.Success).Select(x => (x.Instruction, Id: Convert.ToInt32(x.Match.Groups["id"].Value, 16)))
                .Where(x => corroborates is null || corroborates(x.Id)).ToArray();
            // An ambiguous pattern establishes no name. Repeated reads of the same variable are fine.
            if (matches.Length == 0 || matches.Select(x => x.Id).Distinct().Count() != 1) return;
            var first = matches[0];
            add(first.Id, name, $"CD Script {scriptId:00} +0x{first.Instruction.Offset:X4} in {script.ContainerName}: {first.Instruction.Operation} {first.Instruction.Operands}", count, evidence);
        }
        bool Has(int id, string operation, string operands) => Script(id)?.Instructions.Any(i => i.Operation == operation && i.Operands == operands) == true;

        Role(18, "move", "^" + variable + @" = local\[0x000\]$", "distaff in use (2 when held)", corroborates:
            id => Script(10)?.Instructions.Any(i => i.Operation == "isEqual" && i.Operands.StartsWith($"var[0x{id:X3}], 0x0002,", StringComparison.Ordinal)) == true);
        Role(18, "isEqual", "^" + variable + @", 0x0002,", "difficulty (0 practice, 1 standard, 2 expert)", corroborates: id =>
        {
            // Each of the menu's three selectable buttons assigns one of these values.
            var menu = data.ReadRoom(69);
            var verbs = data.ReadObjectVerbs(69, menu.Objects);
            var assignments = menu.Objects.SelectMany(obj => (verbs.GetValueOrDefault(obj.Id) ?? [])
                .Select(v => data.ReadObjectVerb(69, obj, v.VerbId, out _))).Where(s => s is not null)
                .SelectMany(s => s!.Instructions).Where(i => i.Operation == "move").Select(i => i.Operands).ToHashSet();
            return Enumerable.Range(0, 3).All(value => assignments.Contains($"var[0x{id:X3}] = 0x{value:X4}"));
        });
        Role(28, "move", "^" + variable + @" = 0x0000$", "wrong tries", evidence: GlossaryEvidence.Reading,
            corroborates: id => Has(9, "move", $"local[0x001] = var[0x{id:X3}]") && Has(9, "decrement", "local[0x001]"));
        Role(28, "setVarRange", "^" + variable + @", count=4, \[0x00, 0x00, 0x00, 0x00\]$", "draft buffer", 4);
        Role(10, "move", @"^var\[0x(?<id>[0-9A-F]+)\+var\[0x[0-9A-F]+\]\] = local\[0x000\]$", "notes the player has played", 4);
        Role(10, "move", @"^var\[0x[0-9A-F]+\+var\[0x(?<id>[0-9A-F]+)\]\] = local\[0x000\]$", "number of notes played so far");
        Role(23, "move", @"^var\[0x(?<id>[0-9A-F]+)\+0x00[0-3]\] = local\[0x001\]$", "notes of the current draft", 4);
        Role(23, "move", "^" + variable + @" = var\[0x[0-9A-F]+\+local\[0x000\]\]$", "current draft response word");
        Role(8, "move", "^" + variable + @" = local\[0x000\]$", "object whose close-up is shown");
        Role(8, "verbOps", @"image, " + variable + @"; on\]$", "close-up image object");
        Role(6, "move", "^" + variable + @" = local\[0x002\]$", "object under the pointer or clicked", corroborates:
            id => Has(7, "move", $"var[0x{id:X3}] = 0x0000") && Has(10, "getDist", $"local[0x001], var[0x001], var[0x{id:X3}]"));
        Role(16, "move", "^" + variable + @" = local\[0x000\]$", "text line y");
        Role(15, "move", "^" + variable + @" = var\[0x004\]$", "previous room");
        Role(6, "isLessEqual", @"^var\[0x014\], " + variable + ",", "last click x");
        Role(6, "isLessEqual", @"^var\[0x015\], " + variable + ",", "last click y");
        // The click loop also keeps a pointer position; its findObject operands disambiguate these from the last click.
        Role(6, "findObject", @"^local\[0x002\], " + variable + @", var\[0x[0-9A-F]+\]$", "pointer x");
        Role(6, "findObject", @"^local\[0x002\], var\[0x[0-9A-F]+\], " + variable + "$", "pointer y");
        Role(6, "walkActorTo", @"^var\[0x001\], " + variable + @", var\[0x[0-9A-F]+\]$", "walk target x");
        Role(6, "walkActorTo", @"^var\[0x001\], var\[0x[0-9A-F]+\], " + variable + "$", "walk target y");
        Role(6, "increment", "^" + variable + "$", "click-recorded flag (double-click test)");
        Role(6, "move", "^" + variable + @" = local\[0x002\]$", "exit the player arrived through", corroborates:
            id => Script(6)!.Instructions.Any(i => i.Operation == "loadRoomWithEgo" && i.Operands.StartsWith($"var[0x{id:X3}],", StringComparison.Ordinal)));
        Role(42, "move", @"^local\[0x003\] = var\[0x(?<id>[0-9A-F]+)\+local\[0x002\]\]$", "flying actor x positions", 5,
            corroborates: id => Script(44)?.Instructions.Any(i => i.Operation == "setVarRange" && i.Operands.StartsWith($"var[0x{id:X3}], count=5,", StringComparison.Ordinal)) == true);
        Role(42, "move", @"^local\[0x004\] = var\[0x(?<id>[0-9A-F]+)\+local\[0x002\]\]$", "flying actor y positions", 5,
            corroborates: id => Script(44)?.Instructions.Any(i => i.Operation == "setVarRange" && i.Operands.StartsWith($"var[0x{id:X3}], count=5,", StringComparison.Ordinal)) == true);
        Role(37, "startSound", "^" + variable + "$", "queued sound");
        Role(37, "move", "^" + variable + @" = var\[0x[0-9A-F]+\]$", "sound currently playing");
        var costume = Script(17)?.Instructions ?? [];
        for (int size = 0; size < 3; size++)
        {
            var start = costume.ToList().FindIndex(i => i.Operation == "isEqual" && i.Operands.StartsWith($"local[0x000], 0x{size:X4},", StringComparison.Ordinal));
            if (start < 0) continue;
            var reads = costume.Skip(start + 1).TakeWhile(i => !(i.Operation == "isEqual" && i.Operands.StartsWith("local[0x000],", StringComparison.Ordinal)))
                .Where(i => i.Operation == "move").Select(i => (i, m: System.Text.RegularExpressions.Regex.Match(i.Operands, @"^local\[0x001\] = " + variable + "$"))).Where(x => x.m.Success).ToArray();
            if (reads.Length == 1) add(Convert.ToInt32(reads[0].m.Groups["id"].Value, 16), $"ego costume for size {size} ({new[] { "large", "middle", "small" }[size]})",
                $"CD Script 17 +0x{reads[0].i.Offset:X4}: the size-{size} branch reads its default costume", 1, GlossaryEvidence.Decoded);
        }
        foreach (var (parameter, name) in new[] { ("initAnimation", "ego initial animation"), ("walkAnimation", "ego walk animation"), ("standAnimation", "ego standing animation") })
            Role(17, "actorOps", parameter + @", " + variable + "(?:;|\\])", name);
    }
}
