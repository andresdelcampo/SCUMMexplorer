using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Scumm;

var game = new GameFolderScanner().Scan(args[0]);
var data = ScummDataSources.For(game);
if (args[1] == "script")
{
    foreach (var id in args.Skip(2).Select(int.Parse))
    {
        var script = data.GlobalScript(id);
        Console.WriteLine($"Script {id}: {script?.DecodeNote}");
        foreach (var i in script?.Instructions ?? []) Console.WriteLine($"+{i.Offset:X4} {i.Operation} {i.Operands}");
    }
    return;
}
if (args[1] == "run")
{
    var session = ScummGameSession.BootForPlay(game, out _);
    var lines = new HashSet<string>();
    var frame = 0;
    void Run(int count)
    {
        for (var n = 0; n < count; n++)
        {
            foreach (var message in session.Messages.OnScreen) lines.Add(message.Text);
            session.AdvanceFrame(); frame++;
        }
    }
    void Require(bool condition, string reason) { if (!condition) throw new InvalidOperationException(reason); }
    void Until(Func<bool> condition, int bound, string reason)
    { for (var n = 0; n < bound && !condition(); n++) Run(1); Require(condition(), reason); }
    void Log(string action) => Console.WriteLine($"{action}: f={frame},room={session.CurrentRoomId},logical={session.Snapshot.KnownGlobalVariables.GetValueOrDefault(ScummVars.Room)},left={session.ViewLeft},ego={session.Actors.Single(a => a.Number == 1).X},busState={session.Snapshot.ObjectStates.GetValueOrDefault(144)},cash={session.Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x03E]")},sentence={ScummTextDecoder.DecodeGlyphs(session.PlaySentenceGlyphs())}");
    void Click(int x, int y)
    { Require(session.ClickScreen(x, y, out var refusal) is not null, refusal ?? "Native click refused"); Run(5); }
    void Verb(int id)
    { var verb = session.PlayVerbArea().Items.Single(v => v.VerbId == id && v.Mode == 1); Click(verb.X + Math.Min(4, verb.Width / 2), verb.Y + 3); }
    void Item(int id)
    {
        var inventory = session.CaptureInventoryStatus();
        var items = inventory.Items.Where(i => i.OwnerId == inventory.CurrentActorId).OrderBy(i => i.Slot).ToArray();
        var index = Array.FindIndex(items, i => i.ObjectId == id);
        Require(index is >= 0 and < 4, "Item is outside the visible native inventory: " + id);
        Click(index % 2 == 0 ? 8 : 184, session.MainScreen.Bottom + 36 + 8 * (index / 2));
    }
    void Object(int id)
    {
        var record = data.ReadRoom(session.CurrentRoomId!.Value).Objects.Single(o => o.Id == id);
        var x = record.X + record.Width / 2 - session.ViewLeft;
        Require(x is >= 0 and < 320, "Native object is off screen: " + id);
        Click(x, record.Y + Math.Max(1, record.Height / 2) + session.MainScreen.Top);
    }
    void Use(int item, int target)
    {
        Verb(13); // Clear the preceding native sentence before choosing another action.
        Verb(11); Item(item); Object(target); Log($"compose {item}/{target}");
        Click(8, session.MainScreen.Bottom + 4);
    }
    // Disclosed exploration preparation, identical to the accepted knife/card checks.
    // Only the following bus segment can establish paid transport.
    session.VisitRoom(game.Rooms.Single(r => r.ResourceId == 2));
    Until(() => session.CutsceneSkippable, 1200, "No native preparation override");
    session.PressKey(27); Until(() => session.PlayerHasControl, 1200, "No preparation control");
    session.VisitRoom(game.Rooms.Single(r => r.ResourceId == 2));
    session.ActivateObjectVerb(26, 14); Run(120);
    Require(session.Snapshot.ObjectOwners.GetValueOrDefault(26) == 1, "Knife preparation failed");
    session.VisitRoom(game.Rooms.Single(r => r.ResourceId == 1));
    Use(26, 106); Run(180);
    Require(session.Snapshot.ObjectOwners.GetValueOrDefault(18) == 1, "Native card recovery failed");
    session.VisitRoom(game.Rooms.Single(r => r.ResourceId == 3));
    Verb(13); Log("street preview");
    for (var attempt = 0; attempt < 8 && session.ViewLeft < 560; attempt++)
    { Click(300, session.MainScreen.Bottom - 4); Run(180); Log("walk along street"); }
    Require(session.ViewLeft >= 560, "Bus not reached by native floor walk");
    Use(26, 142);
    Until(() => (session.Snapshot.ObjectStates.GetValueOrDefault(144) & 8) == 0, 1200, "Driver did not wake from native knife/bus Use");
    Run(300); Log("driver awake");
    var money = session.Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x03E]");
    Use(18, 150);
    Until(() => session.Snapshot.KnownGlobalVariables.GetValueOrDefault("var[0x03E]") == money - 8, 1800, "Native reader did not charge the fare");
    Log("paid native fare");
    Until(() => session.Snapshot.KnownGlobalVariables.GetValueOrDefault(ScummVars.Room) == 145, 3000, "Native paid bus did not reach source airport145");
    Until(() => session.PlayerHasControl, 1200, "Airport control did not return");
    Log("native paid bus arrival");
    Console.WriteLine("MESSAGES " + string.Join('|', lines));
    Console.WriteLine("UNCERTAINTIES " + string.Join('|', session.Snapshot.Uncertainties));
    return;
}
throw new ArgumentException("Read-only source mode: <fixture> script <ids...>");
