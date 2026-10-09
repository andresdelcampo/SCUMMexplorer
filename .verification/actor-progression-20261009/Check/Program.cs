using System.Text.Json;
using System.Text.RegularExpressions;
using SCUMMexplorer.Core.Discovery;

var output = Path.GetFullPath(args[1]); Directory.CreateDirectory(output);
var report = new List<string>(); var states = new List<object>();
void Check(bool ok, string label) { report.Add((ok ? "PASS " : "FAIL ") + label); File.WriteAllLines(Path.Combine(output, "report.txt"), report); }
foreach (var (edition, folder) in new[] { ("ega-en", "loom ega mt32 en"), ("ega-es", "ALTERNATIVE LOOMS/loom ega mt32 es"), ("vga", "loom vga en") })
{
    var game = new GameFolderScanner().Scan(Path.Combine(Path.GetFullPath(args[0]), folder));
    var data = ScummDataSources.For(game);
    var index = game.V4Archive?.ReadIndexedObjects() ?? new LflObjectStateReader().ReadSnapshot(Path.Combine(game.FolderPath, "00.LFL"));
    var session = new LoomExplorationSession(game, index);
    void Frames(int count) { for (int f = 0; f < count; f++) session.AdvanceFrame(); }
    bool Settle(int bound = 5000)
    {
        var quiet = 0;
        for (int f = 0; f < bound && quiet < 32; f++)
        { quiet = session.GameStatus.CanEdit ? quiet + 1 : 0; session.AdvanceFrame(); }
        return quiet == 32;
    }
    void Capture(string phase) => states.Add(new { edition, phase, room = session.CurrentRoomId, session.GameStatus.StaffHeld,
        actors = session.Actors.Where(a => a.Number is 1 or 3 || a.Room == session.CurrentRoomId).Select(a => new { a.Number, a.Room, a.Visible, a.X, a.Y, a.Look.Costume, a.Look.Frame, a.Look.StandFrame }).ToArray() });
    // Prepared status choices exercise source branches. They are deliberately not earned progression.
    foreach (int room in new[] { 5, 17, 37 })
    foreach (bool held in new[] { false, true })
    {
        session.VisitRoomById(room); Check(Settle(), $"{edition} Room {room}: status editable before staff choice");
        session.UpdateStaffPossession(held); Frames(4);
        var ego = session.Actors.Single(a => a.Number == 1);
        Check(session.GameStatus.StaffHeld == held, $"{edition} Room {room}: chosen staff branch retained");
        Check(ego.Room == room && ego.Visible == true, $"{edition} Room {room}: Bobbin visible after staff refresh");
        if (game.V4Archive is null) Check(ego.Look.StandFrame == (held ? 3 : 10), $"{edition} Room {room}: own Script 17 staff stand frame");
        else
        {
            // CD uses separate large costumes and shared small costumes; Script 17 keeps frames 1..5 for a large held staff.
            var vars = session.Snapshot.KnownGlobalVariables;
            Check(ego.Look.Costume == vars.GetValueOrDefault("var[0x0AF]"), $"{edition} Room {room}: own stored costume matches actor");
            Check(ego.Look.StandFrame == (!held && vars.GetValueOrDefault("var[0x0AE]") != 0 ? 10 : 3), $"{edition} Room {room}: own Script 17 size/staff frame branch");
        }
        Capture($"staff-{room}-{held}");
    }
    var entry = data.RoomEntryScript(34)!; var code = entry.Instructions.ToArray();
    var ghostDress = code.First(i => i.Operation == "actorOps" && i.Operands.StartsWith("0x03, [costume, 0x78;"));
    var remove = code.First(i => i.Operation == "putActorInRoom" && i.Operands == "0x03, 0x00");
    string Selector(int offset) => Regex.Match(code.Last(i => i.Offset < offset && i.Operation == "notEqualZero" &&
        Regex.Match(i.Operands, @"-> 0x([0-9A-F]+)") is { Success: true } m && Convert.ToInt32(m.Groups[1].Value, 16) > offset).Operands, @"bit\[0x[0-9A-F]+\]").Value;
    var ghostKey = Selector(ghostDress.Offset); var restoredKey = Selector(remove.Offset);
    File.WriteAllText(Path.Combine(output, edition + "-selectors.txt"), $"{ghostKey}: {ghostDress.Offset:X4} {ghostDress.Operands}\n{restoredKey}: {remove.Offset:X4} {remove.Operands}");
    foreach (var (ghost, restored, expected) in new[] { (false, false, 112), (true, false, 120), (true, true, 0) })
    {
        session.VisitRoomById(5); Check(Settle(), $"{edition}: settled before prepared Rusty state");
        session.UpdateStoryBit(ghostKey, ghost); session.UpdateStoryBit(restoredKey, restored);
        session.VisitRoomById(34); Frames(40);
        var actor = session.Actors.Single(a => a.Number == 3);
        if (restored) Check(actor.Room == 0 && actor.Visible != true, $"{edition}: restored Rusty is absent as entry requires");
        else Check(actor.Look.Costume == expected && (ghost || actor.Room == 34 && actor.Visible == true), $"{edition}: {(ghost ? "ghost" : "sleeping")} Rusty uses own-source version");
        if (ghost && !restored) Check(actor.Room == 0 && actor.Visible != true, $"{edition}: ghost entry preserves its source conversation visibility gate");
        Check(session.StoryBit(ghostKey) == ghost && session.StoryBit(restoredKey) == restored, $"{edition}: catalog entry preserves chosen story selectors");
        Capture($"rusty-{ghost}-{restored}");
    }
    foreach (var (x, y, closeRoom) in new[] { (142, 125, 19), (151, 103, 20) })
    foreach (bool held in new[] { false, true })
    {
        session.VisitRoomById(18); Check(Settle(), $"{edition}: settled before close-up"); session.UpdateStaffPossession(held);
        var obj = data.ReadRoom(18).Objects.Single(o => o.WalkX == x && o.WalkY == y);
        var start = session.RoomsEntered.Count; var begun = session.CutscenesBegun; var mark = session.SessionTrace.Count;
        session.ActivateObjectVerb(obj.Id, 0x36);
        var settled = Settle(); var ego = session.Actors.Single(a => a.Number == 1);
        Check(session.RoomsEntered.Skip(start).Contains(closeRoom), $"{edition} {obj.Id}: source close-up room {closeRoom} reached");
        Check(settled && session.CutscenesBegun > begun && session.SessionTrace.Skip(mark).Count(t => t.StartsWith("endCutscene at ")) == session.CutscenesBegun - begun,
            $"{edition} {obj.Id}: close-up scopes finish balanced");
        Check(ego.Room == session.CurrentRoomId && ego.Visible == true, $"{edition} {obj.Id}: Bobbin visible after close-up");
        Check(session.GameStatus.StaffHeld == held, $"{edition} {obj.Id}: close-up retains staff branch");
        Capture($"closeup-{obj.Id}-{held}");
    }
    File.WriteAllLines(Path.Combine(output, edition + "-trace.txt"), session.SessionTrace);
}
File.WriteAllLines(Path.Combine(output, "report.txt"), report);
File.WriteAllText(Path.Combine(output, "states.json"), JsonSerializer.Serialize(states, new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"{report.Count(s => s.StartsWith("PASS "))}/{report.Count} actor-state assertions pass");
foreach (var failure in report.Where(s => s.StartsWith("FAIL "))) Console.WriteLine(failure);
return report.Any(s => s.StartsWith("FAIL ")) ? 1 : 0;
