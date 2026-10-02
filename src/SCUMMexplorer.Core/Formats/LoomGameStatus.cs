using System.Globalization;
using System.Text.RegularExpressions;
using SCUMMexplorer.Core.Domain;

namespace SCUMMexplorer.Core.Formats;

public sealed record LoomDraftStatus(int Slot, ushort? Packed, int[] Notes, bool? Learned,
    bool? Reversible, int? ForwardVerb, int? ReverseVerb, string Origin);
public sealed record LoomGameStatus(bool Supported, string Evidence, bool CanEdit, bool? StaffHeld,
    string StaffOrigin, int? AvailableNoteCount, string NotesOrigin, IReadOnlyList<LoomDraftStatus> Drafts);

/// <summary>Verify the loaded LOOM script contract before interpreting or editing its fields.</summary>
internal sealed record LoomStatusSchema(string StaffKey, string NoteLimitKey, int DraftBase, int DraftCount, int InitialNotes, string CheckpointKey, int CheckpointNotes, string Evidence)
{
    public static LoomStatusSchema Read(GameLibrary game)
    {
        if (game.Id != GameId.Loom || game.Version != ScummVersion.V3) throw new InvalidDataException("Game status fields have only been established for LOOM old-v3.");
        var reader = new LoomScriptReader(ScummScriptDialects.For(game.Id));
        var scripts = new Dictionary<int, LoomScriptDisassembly>();
        LoomScriptInstruction At(int id, int offset, string operation, string? operands = null)
        {
            if (!scripts.TryGetValue(id, out var script)) scripts[id] = script = reader.Decode(game.Resources.Single(r => r.Kind == ResourceKind.Script && r.ResourceId == id));
            var instruction = script.Instructions.SingleOrDefault(i => i.Offset == offset);
            if (instruction is null || instruction.Operation != operation || operands is not null && instruction.Operands != operands)
                throw new InvalidDataException($"Loaded script {id:000} +0x{offset:X4} does not establish the status contract.");
            return instruction;
        }
        string Key(string text, string pattern) => Regex.Match(text, pattern) is { Success: true } match ? match.Value : throw new InvalidDataException("Status operand could not be resolved.");
        var staff = Key(At(17, 0x7D, "equalZero").Operands, @"bit\[0x[0-9A-F]+\]");
        At(17, 0x82, "move", "var[0x072] = 0x0008"); // Zero selects the without-staff animations.
        var noteLimit = Key(At(10, 0x20, "isLess").Operands, @"var\[0x[0-9A-F]+\]");
        At(10, 0x27, "chainScript", "0x0B, []"); // Old-v3 tests limit < ID; IDs above the limit are refused.
        var table = Regex.Match(At(23, 5, "move").Operands, @"var\[0x([0-9A-F]+)\+local\[0x000\]\]");
        if (!table.Success) throw new InvalidDataException("Draft table base is unresolved.");
        At(23, 0, "multiply", "local[0x000] *= 0x0002");
        At(23, 0x48, "and", "local[0x001] &= 0x0007");
        At(23, 0x5C, "and", "local[0x001] &= 0x0038");
        At(23, 0x75, "and", "local[0x001] &= 0x01C0");
        At(23, 0x8E, "and", "local[0x001] &= 0x0E00");
        At(23, 0x1B, "and", "local[0x001] &= 0x2000");
        At(23, 0x2A, "and", "local[0x001] &= 0x1000");
        At(23, 0x20, "move", "bit[0xD69] = local[0x001]");
        At(23, 0x2F, "move", "bit[0xD6A] = local[0x001]");
        At(26, 0, "equalZero", "bit[0xD69], branch +0x004B -> 0x0050");
        At(22, 0xCC, "notEqualZero", "bit[0xD6A], branch +0x005D -> 0x012E");
        At(25, 0x16, "or", "local[0x002] |= 0x2000");
        At(34, 0x16, "or", "local[0x002] |= 0x4000");
        At(22, 0x131, "isLess", "local[0x000], 0x000F, branch +0xFF31 -> 0x0069");
        At(18, 0x6A, "verbOps", "0x08, [at, 0x00C0, local[0x004]; image, 0x008E; on]");
        var init = reader.DecodeRoomLocalScript(game.Rooms.Single(r => r.ResourceId == 1), 211, game.Resources.Count(r => r.Kind == ResourceKind.Script));
        var checkpoint = reader.DecodeRoomLocalScript(game.Rooms.Single(r => r.ResourceId == 46), 204, game.Resources.Count(r => r.Kind == ResourceKind.Script));
        if (init?.Instructions.SingleOrDefault(i => i.Offset == 0x2C3) is not { Operation: "move", Operands: "var[0x07A] = 0x0003" } ||
            checkpoint?.Instructions.SingleOrDefault(i => i.Offset == 0x24) is not { Operation: "move", Operands: "bit[0xDC5] = 0x0001" } ||
            checkpoint.Instructions.SingleOrDefault(i => i.Offset == 0x29) is not { Operation: "startScript", Operands: "0x1D, [0x0007, 0x000C]" })
            throw new InvalidDataException("Loaded staff progression checkpoints are unresolved.");
        return new(staff, noteLimit, int.Parse(table.Groups[1].Value, NumberStyles.HexNumber), 16, 3, "bit[0xDC5]", 7,
            $"Loaded scripts017 +0x007D/+0x0082: {staff}=1 means held;010 +0x0020/+0x0027: note IDs1..8 up to and including {noteLimit} are available. " +
            "023: two words per draft, four 3-bit notes +1; flags0x2000 learned/0x1000 reverse/0x4000 last-note knowledge. " +
            "022 searches slots0..15 inclusively, including Transcend.026 refuses matched unlearned drafts before target dispatch;022 skips reverse matching without0x1000. " +
            "Sequence edits preserve flags and response verbs; Learned edits change only0x2000. " +
            "Staff edits refresh the existing normal Bobbin appearance through017 and note controls through018/019; special room costumes remain under room code. " +
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
                try { _statusSchema = LoomStatusSchema.Read(_game); }
                catch (Exception ex) when (ex is InvalidDataException or InvalidOperationException or IOException) { _statusSchemaFailure = ex.Message; }
            return _statusSchema;
        }
    }
    private bool StatusEditable => _currentRoom is not null && !BootRunning && !InCutscene && !EgoMoving && !CameraMoving &&
        Messages.HaveMsg == 0 && _pending.All(p => p.Frame.FreezeCount == 0) &&
        _runtime.GlobalVariables.GetValueOrDefault("bit[0xD66]") == 0 && _runtime.GlobalVariables.GetValueOrDefault("bit[0xD67]") == 0;

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
        // Only the normal appearance managed by017 can be refreshed through017.
        // A boat/disguise/close-up costume is left to its source room scripts.
        var ego = Actors.FirstOrDefault(a => a.Number == 1);
        if (ego?.Look.Costume == _runtime.GlobalVariables.GetValueOrDefault("var[0x07D]") && ego?.Look.Costume > 0)
            RunStartedScript(17, 0, new Dictionary<string, ushort> { ["local[0x000]"] = _runtime.GlobalVariables["var[0x07C]"], ["local[0x001]"] = (ushort)ego.Look.Costume }, 0);
        else RefreshStatusNotes(held);
    }

    private void RefreshStatusNotes(bool held)
    {
        RunStartedScript(19, 0, null, 0); // Source clears any partial input as part of this explicit edit.
        if (held) RunStartedScript(18, 0, new Dictionary<string, ushort> { ["local[0x000]"] = 2 }, 0);
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
        if (!StatusEditable) { _browsingStaffQuietFrames = 0; return; }
        var current = GameStatus;
        int floor = Math.Max(previous, current.StaffHeld == true ? schema.InitialNotes : 0);
        // This flag records the native seven-note checkpoint, not a guessed room-number range.
        if (current.StaffHeld == true && _runtime.GlobalVariables.GetValueOrDefault(schema.CheckpointKey) == 1 &&
            !_runtime.EstimatedVariables.Contains(schema.CheckpointKey) && !_runtime.EstimatedVariables.Contains("var[0x0DC]"))
            floor = Math.Max(floor, schema.CheckpointNotes);
        if (++_browsingStaffQuietFrames >= 32) _browsingStaffFloor = null;
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
