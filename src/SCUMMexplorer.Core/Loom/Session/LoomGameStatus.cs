using System.Globalization;
using System.Text.RegularExpressions;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Loom;

public sealed record LoomDraftStatus(int Slot, ushort? Packed, int[] Notes, bool? Learned,
    bool? Reversible, int? ForwardVerb, int? ReverseVerb, string Origin);
public sealed record LoomGameStatus(bool Supported, string Evidence, bool CanEdit, bool? StaffHeld,
    string StaffOrigin, int? AvailableNoteCount, string NotesOrigin, IReadOnlyList<LoomDraftStatus> Drafts);

/// <summary>
/// Verify the loaded LOOM script contract before interpreting or editing its fields. Instructions are found by their pattern
/// (neighbouring instructions and operands), not by byte offset: the Spanish and English DOS releases have scripts of other
/// lengths, and the English one lacks the Spanish bit 0xD62 and numbers the bits from 0xD63 on one lower, so the bits are read from the scripts too.
/// </summary>
internal sealed record LoomStatusSchema(string StaffKey, string NoteLimitKey, int DraftBase, int DraftCount, int InitialNotes, string CheckpointKey, int CheckpointNotes,
    string DraftStartKey, string DraftPlayKey, int BitShift, string Evidence, string LookKey = "", string CostumeKey = "")
{
    /// <summary>The Spanish release's learned-draft bit; the edition's bit minus this is <see cref="BitShift"/>.</summary>
    private const int SpanishLearnedBit = 0xD69;

    public static LoomStatusSchema Read(GameLibrary game, IScummDataSource? data = null)
    {
        var cd = game.Id == GameId.LoomVga;
        if (!cd && (game.Id != GameId.Loom || game.Version != ScummVersion.V3)) throw new InvalidDataException("Game status fields have only been established for LOOM old-v3 and LOOM on CD.");
        data ??= ScummDataSources.For(game);
        var scripts = new Dictionary<int, IReadOnlyList<ScummScriptInstruction>>();
        IReadOnlyList<ScummScriptInstruction> Code(int id)
        {
            if (!scripts.TryGetValue(id, out var code))
                scripts[id] = code = (data.GlobalScript(id) ?? throw new InvalidDataException($"Loaded script {id:000} is missing.")).Instructions.ToList();
            return code;
        }
        InvalidDataException Fail(string where) => new($"Loaded script {where} does not establish the status contract.");
        // The instruction with this operation and operands and the one before it (both must exist).
        (ScummScriptInstruction Before, ScummScriptInstruction At) Pair(IReadOnlyList<ScummScriptInstruction> code, string where, string operation, string operands)
        {
            for (int i = 1; i < code.Count; i++)
                if (code[i].Operation == operation && code[i].Operands == operands) return (code[i - 1], code[i]);
            throw Fail(where);
        }
        string Key(string text, string pattern) => Regex.Match(text, pattern) is { Success: true } match ? match.Value : throw new InvalidDataException("Status operand could not be resolved.");
        int Number(string bit) => int.Parse(bit.AsSpan(6, bit.Length - 7), NumberStyles.HexNumber);

        // 017: the instruction before "var[0x072] = 8" tests the staff bit; zero selects the without-staff animations.
        // On CD it is the bit whose test decides between starting 018 with 2 (the note controls, staff held) and 019 (they go away).
        ScummScriptInstruction staffTest, staffMove;
        if (!cd)
        {
            (staffTest, staffMove) = Pair(Code(17), "017", "move", "var[0x072] = 0x0008");
            if (staffTest.Operation != "equalZero") throw Fail("017");
        }
        else
        {
            var c17 = Code(17);
            var at = c17.ToList().FindIndex(i => i.Operation == "startScript" && i.Operands == "0x12, [0x0002]");
            if (at < 2 || c17[at - 1].Operation != "isNotEqual" || !c17[at - 1].Operands.Contains(", 0x0002,", StringComparison.Ordinal) || c17[at - 2].Operation != "notEqualZero") throw Fail("017");
            (staffTest, staffMove) = (c17[at - 2], c17[at]);
        }
        var staff = Key(staffTest.Operands, @"bit\[0x[0-9A-F]+\]");
        // 010: old-v3 tests limit < ID; IDs above the limit are refused (chain 0x0B).
        var (limitTest, limitChain) = Pair(Code(10), "010", "chainScript", "0x0B, []");
        if (limitTest.Operation != "isLess" || !limitTest.Operands.StartsWith("local[0x000], var[", StringComparison.Ordinal)) throw Fail("010");
        var noteLimit = Key(limitTest.Operands, @"var\[0x[0-9A-F]+\]");
        // 023: two words per draft, four 3-bit notes + 1, and the three flag bits, each copied into its own bit.
        var draft = Code(23);
        if (draft.Count == 0 || draft[0].Operation != "multiply" || draft[0].Operands != "local[0x000] *= 0x0002") throw Fail("023");
        var table = draft.Where(i => i.Operation == "move").Select(i => Regex.Match(i.Operands, @"var\[0x([0-9A-F]+)\+local\[0x000\]\]")).FirstOrDefault(m => m.Success)
            ?? throw new InvalidDataException("Draft table base is unresolved.");
        foreach (var mask in new[] { "0x0007", "0x0038", "0x01C0", "0x0E00" })
            if (!draft.Any(i => i.Operation == "and" && i.Operands == $"local[0x001] &= {mask}")) throw Fail("023");
        string FlagFor(string mask)
        {
            for (int i = 0; i + 1 < draft.Count; i++)
                if (draft[i].Operation == "and" && draft[i].Operands == $"local[0x001] &= {mask}" && draft[i + 1].Operation == "move" &&
                    Regex.Match(draft[i + 1].Operands, @"^(bit\[0x[0-9A-F]+\]) = local\[0x001\]$") is { Success: true } m) return m.Groups[1].Value;
            throw Fail("023");
        }
        var learned = FlagFor("0x2000"); var reverse = FlagFor("0x1000"); var lastNote = FlagFor("0x4000");
        // 026 refuses matched unlearned drafts; 022 searches the slots and skips reverse matching without the reverse flag.
        if (Code(26) is not [{ Operation: "equalZero" } refuse, ..] || !refuse.Operands.StartsWith(learned + ",", StringComparison.Ordinal)) throw Fail("026");
        var search = Code(22);
        // The two "wait until set" loops that follow starting script 028: the first ends when the draft starts, the second when its playback is over.
        var waits = search.Where(i => i.Operation == "equalZero" && i.Operands.StartsWith("bit[", StringComparison.Ordinal) && i.Operands.Contains("branch +0xFFFA", StringComparison.Ordinal)).ToList();
        if (waits.Count < 2) throw Fail("022");
        var startKey = Key(waits[0].Operands, @"bit\[0x[0-9A-F]+\]"); var playKey = Key(waits[1].Operands, @"bit\[0x[0-9A-F]+\]");
        if (!search.Any(i => i.Operation == "notEqualZero" && i.Operands.StartsWith(reverse + ",", StringComparison.Ordinal)) ||
            !search.Any(i => i.Operation == "isLess" && i.Operands.StartsWith("local[0x000], 0x000F,", StringComparison.Ordinal))) throw Fail("022");
        // 025 and 034 set the learned and last-note flags.
        var learnedScript = Code(25); var lastScript = Code(34);
        if (learnedScript.Count == 0 || learnedScript[0].Operands != $"{learned} = 0x0001" || !learnedScript.Any(i => i.Operation == "or" && i.Operands == "local[0x002] |= 0x2000") ||
            lastScript.Count == 0 || lastScript[0].Operands != $"{lastNote} = 0x0001" || !lastScript.Any(i => i.Operation == "or" && i.Operands == "local[0x002] |= 0x4000")) throw Fail("025/034");
        // 018 puts the note controls on screen.
        if (!Code(18).Any(i => i.Operation == "verbOps" && Regex.IsMatch(i.Operands, @"^0x08, \[at, 0x00C0, local\[0x004\]; image, 0x[0-9A-F]+; on\]$") && (cd || i.Operands.Contains("image, 0x008E;", StringComparison.Ordinal)))) throw Fail("018");
        if (cd)
        {
            // 017 keeps its two arguments, the look and the costume, in two variables; running it again with them redraws Bobbin.
            var c17All = Code(17);
            string Stored(string local) => c17All.Where(i => i.Operation == "move").Select(i => Regex.Match(i.Operands, @"^(var\[0x[0-9A-F]+\]) = " + Regex.Escape(local) + "$")).FirstOrDefault(m => m.Success)?.Groups[1].Value
                ?? throw Fail("017");
            var lookKey = Stored("local[0x000]"); var costumeKey = Stored("local[0x001]");
            // Read the CD's initial three-note limit from its own opening initialization, as for EGA.
            var initial = data!.RoomLocalScriptIds(1).Select(id => data.RoomLocalScript(1, id))
                .FirstOrDefault(s => s?.Instructions.Any(i => i.Operation == "move" && i.Operands == $"{noteLimit} = 0x0003") == true)
                ?? throw Fail("Room 01 initial notes");
            return new(staff, noteLimit, int.Parse(table.Groups[1].Value, NumberStyles.HexNumber), 16, 3, "", 0, startKey, playKey, Number(learned) - SpanishLearnedBit,
                $"Loaded CD scripts 017 +0x{staffTest.Offset:X4}/+0x{staffMove.Offset:X4}: {staff}=1 means held (it starts 018 with 2, else 019); 010 +0x{limitTest.Offset:X4}/+0x{limitChain.Offset:X4}: note IDs 1..8 up to and including {noteLimit} are available. " +
                $"023: two words per draft, four 3-bit notes +1; flags 0x2000 learned ({learned})/0x1000 reverse ({reverse})/0x4000 last-note knowledge ({lastNote}). " +
                "022 searches slots 0..15 inclusively. 026 refuses matched unlearned drafts before target dispatch; 022 skips reverse matching without 0x1000. " +
                $"{initial.Context} sets {noteLimit}=3, the initial browsing floor while the staff is held. " +
                "Staff edits refresh the look through 017 and the note controls through 018/019. " +
                "These explicit edits are exploration choices, not source-established pickups or learning.", lookKey, costumeKey);
        }
        var init = data.RoomLocalScript(1, 211);
        var checkpoint = data.RoomLocalScript(46, 204);
        // The opening sets the note limit (the Spanish and Roland copies start with 3; the plain English release's script assigns 8 at the same place).
        var initialNotes = init?.Instructions.Select(i => i.Operation == "move" ? Regex.Match(i.Operands, @"^var\[0x07A\] = 0x([0-9A-F]+)$") : Match.Empty)
            .FirstOrDefault(m => m.Success) is { } initialMatch ? int.Parse(initialMatch.Groups[1].Value, NumberStyles.HexNumber) : (int?)null;
        if (initialNotes is null || checkpoint is null)
            throw new InvalidDataException("Loaded staff progression checkpoints are unresolved.");
        var cp = checkpoint.Instructions.ToList();
        int cpIndex = cp.FindIndex(i => i.Operation == "startScript" && i.Operands == "0x1D, [0x0007, 0x000C]");
        if (cpIndex < 1 || cp[cpIndex - 1].Operation != "move" || Regex.Match(cp[cpIndex - 1].Operands, @"^(bit\[0x[0-9A-F]+\]) = 0x0001$") is not { Success: true } cpBit)
            throw new InvalidDataException("Loaded staff progression checkpoints are unresolved.");
        return new(staff, noteLimit, int.Parse(table.Groups[1].Value, NumberStyles.HexNumber), 16, initialNotes.Value, cpBit.Groups[1].Value, 7, startKey, playKey, Number(learned) - SpanishLearnedBit,
            $"Loaded scripts 017 +0x{staffTest.Offset:X4}/+0x{staffMove.Offset:X4}: {staff}=1 means held; 010 +0x{limitTest.Offset:X4}/+0x{limitChain.Offset:X4}: note IDs 1..8 up to and including {noteLimit} are available. " +
            $"023: two words per draft, four 3-bit notes +1; flags 0x2000 learned ({learned})/0x1000 reverse ({reverse})/0x4000 last-note knowledge ({lastNote}). " +
            "022 searches slots 0..15 inclusively, including Transcend. 026 refuses matched unlearned drafts before target dispatch; 022 skips reverse matching without 0x1000. " +
            "Sequence edits preserve flags and response verbs; Learned edits change only 0x2000. " +
            "Staff edits refresh the existing normal Bobbin appearance through 017 and note controls through 018/019; special room costumes remain under room code. " +
            "These explicit edits are exploration choices, not source-established pickups, learning or replay prerequisites.");
    }
}

public sealed partial class LoomExplorationSession
{
    private LoomStatusSchema? _statusSchema;
    private string? _statusSchemaFailure;
    private readonly Dictionary<string, (ushort Value, ushort Mask, string Origin)> _statusEdits = [];
    private int? _browsingStaffFloor;
    private int _browsingStaffQuietFrames;

    private LoomStatusSchema? StatusSchema
    {
        get
        {
            if (_statusSchema is null && _statusSchemaFailure is null)
                try { _statusSchema = LoomStatusSchema.Read(_game, _data); }
                catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException) { _statusSchemaFailure = ex.Message; }
            return _statusSchema;
        }
    }
    private bool StatusEditable => _currentRoom is not null && !BootRunning && !InCutscene && !EgoMoving && !CameraMoving &&
        Messages.HaveMsg == 0 && _pending.All(p => p.Frame.FreezeCount == 0) &&
        StatusSchema is { } schema && _runtime.GlobalVariables.GetValueOrDefault(schema.DraftStartKey) == 0 && _runtime.GlobalVariables.GetValueOrDefault(schema.DraftPlayKey) == 0;

    public LoomGameStatus GameStatus
    {
        get
        {
            if (StatusSchema is not { } schema) return new(false, _statusSchemaFailure ?? "Status schema unresolved.", false, null, "Unresolved", null, "Unresolved", []);
            ushort? Value(string key) => _runtime.GlobalVariables.TryGetValue(key, out var v) ? v : null;
            var staff = Value(schema.StaffKey);
            var limit = Value(schema.NoteLimitKey);
            var drafts = Enumerable.Range(0, schema.DraftCount).Select(slot =>
            {
                var key = $"var[0x{schema.DraftBase + 2 * slot:X3}]";
                var packed = Value(key); var response = Value($"var[0x{schema.DraftBase + 2 * slot + 1:X3}]");
                return new LoomDraftStatus(slot, packed, packed is ushort p ? Enumerable.Range(0, 4).Select(i => ((p >> (3 * i)) & 7) + 1).ToArray() : [],
                    packed is ushort learned ? (learned & 0x2000) != 0 : null, packed is ushort reversed ? (reversed & 0x1000) != 0 : null,
                    response is ushort forward ? forward & 0xFF : null, response is ushort reverse ? reverse >> 8 : null, StatusOrigin(key));
            }).ToArray();
            return new(true, schema.Evidence, StatusEditable, staff is 0 or 1 ? staff == 1 : null, StatusOrigin(schema.StaffKey),
                limit is >= 0 and <= 8 ? limit : null, StatusOrigin(schema.NoteLimitKey), drafts);
        }
    }

    private string StatusOrigin(string key)
    {
        if (!_runtime.GlobalVariables.TryGetValue(key, out var value)) return "Unresolved";
        bool estimated = _runtime.EstimatedVariables.Contains(key) || key.StartsWith("bit[", StringComparison.Ordinal) &&
            _runtime.EstimatedVariables.Contains($"var[0x{int.Parse(key.AsSpan(6, key.Length - 7), NumberStyles.HexNumber) >> 4:X3}]");
        if (_statusEdits.TryGetValue(key, out var edit))
            return (value & edit.Mask) == (edit.Value & edit.Mask)
                ? edit.Origin + "; current choice unchanged" + (estimated ? "; retained flags estimated" : "")
                : "Game-driven change after " + edit.Origin.ToLowerInvariant() + (estimated ? " (estimated)" : "");
        return estimated ? "Estimated from seeded source execution" : "Known in this exploration session";
    }

    private LoomStatusSchema RequireStatusEdit()
    {
        if (StatusSchema is not { } schema) throw new InvalidOperationException(_statusSchemaFailure);
        if (!StatusEditable) throw new InvalidOperationException("Finish the current scene, draft, movement or dialogue before editing game status.");
        return schema;
    }
    private void SetStatusWord(string key, ushort value, ushort mask = 0xFFFF, string origin = "Explicit user edit")
    {
        _runtime.SetGlobalVariable(key, value);
        int word = int.Parse(key.AsSpan(6, key.Length - 7), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        for (int bit = 0; bit < 16; bit++) _runtime.SetGlobalVariable($"bit[0x{word * 16 + bit:X3}]", (ushort)((value >> bit) & 1));
        if (mask == 0xFFFF) _runtime.EstimatedVariables.Remove(key);
        if (_statusEdits.TryGetValue(key, out var earlier)) mask |= earlier.Mask;
        _statusEdits[key] = (value, mask, origin);
        var note = $"{origin}: {key}=0x{value:X4}; exploration choice, not a source-established prerequisite.";
        _visitTrace.Add(note); _uncertainties.Add(note);
    }

    public void UpdateStaffPossession(bool held)
    {
        var schema = RequireStatusEdit();
        int bit = int.Parse(schema.StaffKey.AsSpan(6, schema.StaffKey.Length - 7), NumberStyles.HexNumber);
        var key = $"var[0x{bit >> 4:X3}]";
        ushort mask = (ushort)(1 << (bit & 15));
        var value = _runtime.GlobalVariables[key];
        SetStatusWord(key, held ? (ushort)(value | mask) : (ushort)(value & ~mask), mask);
        _statusEdits[schema.StaffKey] = ((ushort)(held ? 1 : 0), 1, "Explicit user edit");
        RefreshStatusEgo(held);
        RefreshStatusNotes(held && !IsStaffMenu);
    }

    private void RefreshStatusEgo(bool held)
    {
        if (StatusSchema is not { } schema) return;
        // Only the normal appearance managed by017 can be refreshed through017.
        // A boat/disguise/close-up costume is left to its source room scripts.
        var ego = Actors.FirstOrDefault(a => a.Number == 1);
        if (schema.LookKey.Length > 0)
        {
            // CD: 017 takes the look and the costume it stored last. It starts 018 or 019 itself only while its own flag (bit 0x00E)
            // is set, which a first-visit session may not have, so the note controls are refreshed explicitly as well.
            // The CD game has one Bobbin costume with the distaff and one without, where EGA changes the animations of one costume:
            // the scripts set the ego's costume variable together with the staff bit, so the edit does the same.
            var staffCostume = StaffCostumes(schema) is { } pair ? (held ? pair.With : pair.Without) : (ushort?)null;
            var variable = StaffCostumeVariable(schema);
            var defaultCostume = _runtime.GlobalVariables.GetValueOrDefault(variable);
            // The large costume is also the default for a later room's look-0 entry, even when Bobbin currently wears a smaller look.
            if (staffCostume is ushort newCostume && StaffCostumes(schema) is { } normal &&
                (defaultCostume == normal.With || defaultCostume == normal.Without))
            {
                _runtime.SetGlobalVariable(variable, newCostume);
                if (ego?.Look.Costume == _runtime.GlobalVariables.GetValueOrDefault(schema.CostumeKey) &&
                    (ego.Look.Costume == normal.With || ego.Look.Costume == normal.Without))
                    RunStartedScript(17, 0, new Dictionary<string, ushort> { ["local[0x000]"] = _runtime.GlobalVariables.GetValueOrDefault(schema.LookKey), ["local[0x001]"] = newCostume }, 0);
                else if (ego?.Look.Costume == _runtime.GlobalVariables.GetValueOrDefault(schema.CostumeKey) && ego.Look.Costume > 0)
                    RunStartedScript(17, 0, new Dictionary<string, ushort> { ["local[0x000]"] = _runtime.GlobalVariables.GetValueOrDefault(schema.LookKey), ["local[0x001]"] = (ushort)ego.Look.Costume }, 0);
            }
            else if (ego?.Look.Costume == _runtime.GlobalVariables.GetValueOrDefault(schema.CostumeKey) && ego?.Look.Costume > 0)
                RunStartedScript(17, 0, new Dictionary<string, ushort> { ["local[0x000]"] = _runtime.GlobalVariables.GetValueOrDefault(schema.LookKey), ["local[0x001]"] = (ushort)ego.Look.Costume }, 0);
        }
        else if (ego?.Look.Costume == _runtime.GlobalVariables.GetValueOrDefault("var[0x07D]") && ego?.Look.Costume > 0)
            RunStartedScript(17, 0, new Dictionary<string, ushort> { ["local[0x000]"] = _runtime.GlobalVariables["var[0x07C]"], ["local[0x001]"] = (ushort)ego.Look.Costume }, 0);
    }

    private (ushort With, ushort Without)? _staffCostumes;
    private bool _staffCostumesRead;

    /// <summary>The variable script 017 reads Bobbin's own costume from (its look-0 branch: <c>local[1] = var[X]</c>).</summary>
    private string StaffCostumeVariable(LoomStatusSchema schema)
    {
        var read = ScummDataSources.For(_game).GlobalScript(17)?.Instructions
            .Select(i => System.Text.RegularExpressions.Regex.Match(i.Operation + " " + i.Operands, @"^move local\[0x001\] = (var\[0x[0-9A-F]+\])$"))
            .FirstOrDefault(m => m.Success);
        return read?.Groups[1].Value ?? "";
    }

    /// <summary>
    /// The costumes the CD scripts give Bobbin with and without the distaff: wherever a script sets the staff bit and, within the next
    /// three instructions, the ego costume variable to a literal, that literal goes with that bit value (the most common one wins). Null when the scripts do not show both.
    /// </summary>
    private (ushort With, ushort Without)? StaffCostumes(LoomStatusSchema schema)
    {
        if (_staffCostumesRead) return _staffCostumes;
        _staffCostumesRead = true;
        var variable = StaffCostumeVariable(schema);
        if (variable.Length == 0) return null;
        var data = ScummDataSources.For(_game);
        var votes = new Dictionary<(int Value, ushort Costume), int>();
        foreach (var id in data.GlobalScriptIds())
        {
            var code = data.GlobalScript(id)?.Instructions;
            if (code is null) continue;
            for (var i = 0; i < code.Count; i++)
            {
                var set = System.Text.RegularExpressions.Regex.Match(code[i].Operation + " " + code[i].Operands, @"^move " + System.Text.RegularExpressions.Regex.Escape(schema.StaffKey) + @" = 0x([0-9A-F]+)$");
                if (!set.Success) continue;
                var value = int.Parse(set.Groups[1].Value, NumberStyles.HexNumber);
                if (value is not (0 or 1)) continue;
                for (var j = i + 1; j < Math.Min(code.Count, i + 4); j++)
                {
                    var costume = System.Text.RegularExpressions.Regex.Match(code[j].Operation + " " + code[j].Operands, @"^move " + System.Text.RegularExpressions.Regex.Escape(variable) + @" = 0x([0-9A-F]+)$");
                    if (!costume.Success) continue;
                    var key = (value, (ushort)int.Parse(costume.Groups[1].Value, NumberStyles.HexNumber));
                    votes[key] = votes.GetValueOrDefault(key) + 1;
                    break;
                }
            }
        }
        ushort? Best(int value) => votes.Where(v => v.Key.Value == value).OrderByDescending(v => v.Value).Select(v => (ushort?)v.Key.Costume).FirstOrDefault();
        return Best(1) is ushort with && Best(0) is ushort without && with != without ? _staffCostumes = (with, without) : null;
    }

    private void RefreshStatusNotes(bool held)
    {
        RunStartedScript(19, 0, null, 0); // Source clears any partial input as part of this explicit edit.
        if (held && !IsStaffMenu) RunStartedScript(18, 0, new Dictionary<string, ushort> { ["local[0x000]"] = 2 }, 0);
    }

    private IReadOnlyList<LoomStoryEvent>? _storyEvents;

    /// <summary>Whether a draft is being played back (script 020 keeps this bit set until 021 finishes it); the bit is read from the scripts.</summary>
    public bool DraftPlaying => StatusSchema is { } schema && _runtime.GlobalVariables.GetValueOrDefault(schema.DraftPlayKey) != 0;

    /// <summary>The story bits the game's code sets (see <see cref="LoomStoryEvent"/>), read once per session.</summary>
    public IReadOnlyList<LoomStoryEvent> StoryEvents => _storyEvents ??= LoomStoryEvents.Read(_game);

    /// <summary>Whether a story bit is set now; null when the game has not defined it yet.</summary>
    public bool? StoryBit(string key) => _runtime.GlobalVariables.TryGetValue(key, out var v) ? v != 0 : null;

    /// <summary>
    /// Marks a story event as having happened (or not): sets or clears its bit, as an explicit exploration choice. The bit is not
    /// established by the scene that normally sets it, and a room already on screen only shows the change when it is entered again.
    /// </summary>
    public void UpdateStoryBit(string key, bool happened)
    {
        RequireStatusEdit();
        var bit = int.Parse(key.AsSpan(6, key.Length - 7), NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        if (bit < LoomStoryEvents.FirstBitFor(_game)) throw new ArgumentOutOfRangeException(nameof(key), "Only story bits can be edited here.");
        var word = $"var[0x{bit >> 4:X3}]";
        ushort mask = (ushort)(1 << (bit & 15));
        var value = _runtime.GlobalVariables.GetValueOrDefault(word);
        SetStatusWord(word, happened ? (ushort)(value | mask) : (ushort)(value & ~mask), mask, "Explicit user edit (story event)");
        _statusEdits[key] = ((ushort)(happened ? 1 : 0), 1, "Explicit user edit (story event)");
    }

    public void UpdateAvailableNotes(int count)
    {
        var schema = RequireStatusEdit();
        if (count is < 0 or > 8) throw new ArgumentOutOfRangeException(nameof(count));
        _browsingStaffFloor = null; // A later explicit choice supersedes the catalog adjustment.
        SetStatusWord(schema.NoteLimitKey, (ushort)count);
        RefreshStatusNotes(_runtime.GlobalVariables[schema.StaffKey] != 0);
    }

    private void BeginBrowsingStaffFloor()
    {
        _browsingStaffFloor = GameStatus.AvailableNoteCount;
        _browsingStaffQuietFrames = 0;
    }

    private void ApplyBrowsingStaffFloor()
    {
        if (_browsingStaffFloor is not int previous || StatusSchema is not { } schema) return;
        if (InCutscene || DraftPlaying) { _browsingStaffQuietFrames = 0; return; }
        var current = GameStatus;
        int floor = Math.Max(previous, current.StaffHeld == true ? schema.InitialNotes : 0);
        // This flag records the native seven-note checkpoint, not a guessed room-number range.
        if (current.StaffHeld == true && _runtime.GlobalVariables.GetValueOrDefault(schema.CheckpointKey) == 1 &&
            !_runtime.EstimatedVariables.Contains(schema.CheckpointKey) && !_runtime.EstimatedVariables.Contains("var[0x0DC]"))
            floor = Math.Max(floor, schema.CheckpointNotes);
        if (StatusEditable) { if (++_browsingStaffQuietFrames >= 32) _browsingStaffFloor = null; }
        else _browsingStaffQuietFrames = 0;
        if (current.AvailableNoteCount is not int count || count >= floor) return;
        SetStatusWord(schema.NoteLimitKey, (ushort)floor, origin: "Explorer room browsing adjustment");
        RefreshStatusNotes(current.StaffHeld == true);
    }

    public void UpdateDraftSequence(int slot, IReadOnlyList<int> notes)
    {
        var schema = RequireStatusEdit();
        if (slot < 0 || slot >= schema.DraftCount || notes.Count != 4 || notes.Any(n => n is < 1 or > 8))
            throw new ArgumentOutOfRangeException(nameof(notes), "A castable draft needs exactly four source note IDs1..8.");
        var key = $"var[0x{schema.DraftBase + 2 * slot:X3}]";
        if (!_runtime.GlobalVariables.TryGetValue(key, out var packed)) throw new InvalidOperationException("Draft state unresolved.");
        int updated = packed & 0xF000;
        for (int i = 0; i < 4; i++) updated |= (notes[i] - 1) << (i * 3);
        SetStatusWord(key, (ushort)updated, 0x0FFF);
    }

    public void UpdateDraftLearned(int slot, bool learned)
    {
        var schema = RequireStatusEdit();
        if (slot < 0 || slot >= schema.DraftCount) throw new ArgumentOutOfRangeException(nameof(slot));
        var key = $"var[0x{schema.DraftBase + 2 * slot:X3}]";
        if (!_runtime.GlobalVariables.TryGetValue(key, out var packed)) throw new InvalidOperationException("Draft state unresolved.");
        SetStatusWord(key, learned ? (ushort)(packed | 0x2000) : (ushort)(packed & ~0x2000), 0x2000);
    }
}
