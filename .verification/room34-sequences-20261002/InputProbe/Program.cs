using System.Collections;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;
using SCUMMexplorer.Core.Rendering;

var game = new GameFolderScanner().Scan(args[0]);
var objects = new LflObjectStateReader().ReadSnapshot(Path.Combine(args[0], "00.LFL"));
Directory.CreateDirectory(args[1]);
foreach (var mode in new[] { "direct", "keyboard" })
{
    var s = new LoomExplorationSession(game, objects);
    s.VisitRoom(game.Rooms.Single(r => r.ResourceId == 34));
    int frames = 0, initialTrace = s.SessionTrace.Count;
    var records = new List<object>();
    var actions = new List<object>();
    void Advance() { s.AdvanceFrame(); frames++; }
    string[] Boundaries(int start) => s.SessionTrace.Skip(start).Where(t => t.StartsWith("cutscene at ") || t.StartsWith("endCutscene at ")).ToArray();
    void Record(string stage) => records.Add(new { stage, frames, status = s.GameStatus, snapshot = s.Snapshot, actors = s.Actors });
    void Settle(string stage, string? expected = null, int traceStart = 0)
    {
        int quiet = 0;
        for (int f = 0; f < 3000; f++)
        {
            var boundaries = Boundaries(traceStart);
            bool balanced = boundaries.Count(t => t.StartsWith("cutscene at ")) == boundaries.Count(t => t.StartsWith("endCutscene at "));
            if (s.GameStatus.CanEdit && balanced && (expected is null || boundaries.Any(t => t.StartsWith(expected))))
            { if (++quiet == 32) { Record(stage); return; } }
            else quiet = 0;
            Advance();
        }
        Record(stage + "-timeout");
        throw new InvalidOperationException($"{stage}: failed bounded settling or missing {expected}");
    }
    string? failure = null;
    try
    {
        Settle("arrival");
        var drafts = s.GameStatus.Drafts.Where(d => d.ForwardVerb is 0x43 or 0x4C || d.ReverseVerb is 0x44 or 0x4C).ToArray();
        if (drafts.Length != 2) throw new InvalidDataException("Expected two source-established drafts.");
        actions.Add(new { userStatusEdits = true, staffHeld = true, notes = drafts.Max(d => d.Notes.Max()), learnedSlots = drafts.Select(d => d.Slot).ToArray() });
        s.UpdateStaffPossession(true);
        Settle("staff-ready");
        s.UpdateAvailableNotes(drafts.Max(d => d.Notes.Max()));
        Settle("notes-ready");
        foreach (var draft in drafts) s.UpdateDraftLearned(draft.Slot, true);
        Record("status-edited");
        Settle("status-ready");
        var steps = new[] {
            ("wake-first", 0x44, "endCutscene at Script 85 +0x05CE"),
            ("change-appearances", 0x4C, "endCutscene at Room 34 local script 202 +0x015F"),
            ("wake-disguised", 0x44, "endCutscene at Room 34 local script 201 +0x012A"),
            ("wake-again", 0x44, "endCutscene at Room 34 local script 201 +0x0193"),
            ("wake-persistent", 0x44, "endCutscene at Room 34 local script 201 +0x01D9") };
        foreach (var (stage, verb, end) in steps)
        {
            if (s.Snapshot.RoomId != 34) throw new InvalidOperationException("Expected Room34");
            var target = new LoomRoomObjectDirectory().Decode(File.ReadAllBytes(game.Rooms.Single(r => r.ResourceId == 34).FilePath)).Single(o => o.Id == 561);
            actions.Add(s.Click(target.WalkX, target.WalkY));
            Settle(stage + "-approach");
            actions.Add(s.Click(target.X + target.Width / 2, target.Y + target.Height / 2));
            Settle(stage + "-target");
            var draft = s.GameStatus.Drafts.Single(d => d.ForwardVerb == verb || d.ReverseVerb == verb);
            bool reverse = draft.ReverseVerb == verb;
            if (s.GameStatus.StaffHeld != true || draft.Learned != true || s.GameStatus.AvailableNoteCount < draft.Notes.Max() || reverse && draft.Reversible != true)
                throw new InvalidOperationException("Staff, notes or learned/reverse prerequisite is missing.");
            var notes = reverse ? draft.Notes.Reverse().ToArray() : draft.Notes.ToArray();
            actions.Add(new { stage, verb, draft.Slot, reverse, notes, mode });
            Record(stage + "-before");
            int traceStart = s.SessionTrace.Count;
            if (mode == "direct") actions.Add(s.ActivateObjectVerb(561, verb));
            else foreach (var note in notes)
            {
                actions.Add(s.PressKey("qwertyui"[note - 1]));
                for (int gap = 0; gap < 8; gap++) Advance();
            }
            Settle(stage, end, traceStart);
        }
    }
    catch (Exception ex) { failure = ex.ToString(); Record("failure"); }
    var packet = new { mode, frames, failure, actions, records, boundaries = Boundaries(initialTrace), trace = s.SessionTrace.Skip(initialTrace).ToArray(),
        events = ((IEnumerable)typeof(LoomExplorationSession).GetField("_visitEvents", BindingFlags.NonPublic | BindingFlags.Instance)!.GetValue(s)!).Cast<object>().ToArray(),
        sourceFiles = Directory.GetFiles(args[0], "*.LFL").OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase).Select(p => new { name = Path.GetFileName(p), sha256 = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p))) }).ToArray() };
    File.WriteAllText(Path.Combine(args[1], mode + ".json"), JsonSerializer.Serialize(packet, new JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"{mode}: {frames} frames; {(failure is null ? "PASS" : failure.Split('\n')[0])}");
}
