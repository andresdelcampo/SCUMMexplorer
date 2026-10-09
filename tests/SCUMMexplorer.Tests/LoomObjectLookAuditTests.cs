using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Domain;
using Xunit.Abstractions;

namespace SCUMMexplorer.Tests;

public sealed class LoomObjectLookAuditTests(ITestOutputHelper output)
{
    [Fact(Skip = "Diagnostic trace that prints and asserts nothing; run it by hand when auditing Look verbs.")]
    public void TraceRoom17Object357DoubleClick()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, TestGames.LoomRelative));
        if (!File.Exists(Path.Combine(folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(folder);
        var session = new LoomExplorationSession(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(folder, "00.LFL")));
        session.VisitRoom(game.Rooms.Single(room => room.ResourceId == 17));
        var ego = () => session.Actors.Single(actor => actor.Number == 1);
        output.WriteLine($"START room={session.Snapshot.RoomId} ego=({ego().X},{ego().Y}) moving={session.EgoMoving} actor={ego()}");
        var before = session.SessionTrace.Count;
        var action = session.Click(44, 110, doubleClick: true);
        output.WriteLine($"CLICK {action.Evidence}; ego=({ego().X},{ego().Y}) moving={session.EgoMoving}; obj357pos={session.Snapshot.ObjectPositions.GetValueOrDefault(357)}");
        for (var frame = 0; frame < 120; frame++)
        {
            var start = session.SessionTrace.Count;
            session.AdvanceFrame();
            if (frame < 25 || frame % 10 == 9)
            {
                var recent = string.Join(" || ", session.SessionTrace.Skip(Math.Max(before, start)).TakeLast(5));
                output.WriteLine($"F{frame + 1} ego=({ego().X},{ego().Y}) moving={session.EgoMoving} actorMoving={session.ActorsMoving} pending={session.Snapshot.PendingScripts} var67={session.Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x067")} var68={session.Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x068")} talk=[{session.Messages.Talk?.Text}] trace=[{recent}]");
            }
        }
        var bytes = File.ReadAllBytes(game.Rooms.Single(room => room.ResourceId == 17).FilePath);
        var records = new ScummRoomObjectDirectory().Decode(bytes);
        var record = records.Single(item => item.Id == 357);
        var verbs = new ScummObjectVerbReader().ReadAll(bytes, records);
        foreach (var verb in verbs[357]) output.WriteLine($"VERB {verb.Label}: {new ScummObjectVerbReader().Decode(game.Rooms.Single(room => room.ResourceId == 17), record, verb.VerbId).Format()}");
    }

    [Fact(Skip = "Diagnostic trace that prints and asserts nothing; run it by hand when auditing Look verbs.")]
    public void AuditRoom02AndLaterRoomLookVerbs()
    {
        var folder = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, TestGames.LoomRelative));
        if (!File.Exists(Path.Combine(folder, "00.LFL"))) return;
        var game = new GameFolderScanner().Scan(folder);
        var directory = new ScummRoomObjectDirectory();
        var verbs = new ScummObjectVerbReader();
        foreach (var room in game.Rooms.Where(room => room.ResourceId is 2 or 3 or 4 or 10 or 17 or 22))
        {
            var bytes = File.ReadAllBytes(room.FilePath);
            var records = directory.Decode(bytes);
            var tables = verbs.ReadAll(bytes, records);
            var names = LoomTextExtractor.ReadObjectNames(LflEncoding.DecodeXorFF(bytes), records);
            var targets = records.Where(record => tables.TryGetValue(record.Id, out var table) && table.Any(item => item.VerbId == 0x36))
                .Take(8).ToArray();
            output.WriteLine($"Room {room.ResourceId}: {records.Count} records, {tables.Count} verb tables, {targets.Length} look targets in audit sample");
            foreach (var record in targets)
            {
                var session = new LoomExplorationSession(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(folder, "00.LFL")));
                session.VisitRoom(room);
                var x = record.X + Math.Max(0, record.Width / 2);
                var y = record.Y + Math.Max(0, record.Height / 2);
                var action = session.Click(x, y, doubleClick: true);
                for (var frame = 0; frame < 500 && session.Snapshot.RoomId == room.ResourceId && session.ActorsMoving; frame++)
                    session.AdvanceFrame();
                var messages = string.Join(" | ", session.Snapshot.ScreenMessages.Select(message => message.Text));
                var talks = new List<string>();
                if (session.Messages.Talk is { } firstTalk) talks.Add(firstTalk.Text);
                for (var frame = 0; frame < 300 && session.Snapshot.RoomId == room.ResourceId; frame++)
                {
                    session.AdvanceFrame();
                    if (session.Messages.Talk is { } talk && !talks.Contains(talk.Text)) talks.Add(talk.Text);
                    if (session.Messages.Talk is null && session.Snapshot.PendingScripts == 0 && !session.ActorsMoving) break;
                }
                var trace = string.Join(" | ", session.SessionTrace.Where(line => line.Contains($"object {record.Id}", StringComparison.OrdinalIgnoreCase) || line.Contains("findObject", StringComparison.OrdinalIgnoreCase)).TakeLast(5));
                var look = verbs.Decode(room with { FilePath = room.FilePath }, record, 0x36);
                output.WriteLine($"  object {record.Id} name=[{names.GetValueOrDefault(record.Id)}] ({record.X},{record.Y} {record.Width}x{record.Height}) owner={session.Snapshot.ObjectOwners.GetValueOrDefault(record.Id)} parent={record.ParentLocalIndex}/{record.ParentRequiredState} class={session.Snapshot.ObjectClasses.GetValueOrDefault(record.Id):X} action={action.Evidence} pending={session.Snapshot.PendingScripts} messages=[{messages}] talks=[{string.Join(" | ", talks)}] verbAreaImages={session.VerbArea().Items.Count} look=[{look.Format()}] trace=[{trace}]");
            }
        }

        var room2 = game.Rooms.Single(room => room.ResourceId == 2);
        var exact = new LoomExplorationSession(game, new LflObjectStateReader().ReadSnapshot(Path.Combine(folder, "00.LFL")));
        exact.VisitRoom(room2);
        var exactAction = exact.Click(700, 40, doubleClick: true);
        for (var frame = 0; frame < 12; frame++) exact.AdvanceFrame();
        output.WriteLine($"EXACT room2 object156 action={exactAction.Evidence}; messages=[{string.Join(" | ", exact.Snapshot.ScreenMessages.Select(message => message.Text))}]; last=[{string.Join(" | ", exact.SessionTrace.TakeLast(35))}]");
    }
}
