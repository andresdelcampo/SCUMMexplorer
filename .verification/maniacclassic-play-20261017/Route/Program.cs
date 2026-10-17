// Continuous native-input S6 route. No story edits, catalog visits or direct verbs.
var folder = args.FirstOrDefault() ?? @"D:\Juegos\Lucas\mmansion ega en";
var game = new GameFolderScanner().Scan(folder);
var data = new ScummV1DataSource(game);
var session = new ScummGameSession(game, data.ReadIndexedObjects(), data: data);
var frames = 0;
Check(session.CurrentRoomId == 45, "native party menu");
foreach (var (x, y) in new[] { (76, 104), (196, 104), (292, 60) })
{
    Click(x, y + session.MainScreen.Top);
    Run(100);
}
Until(() => session.CurrentRoomId == 44 && session.CutsceneSkippable);
Check(session.Snapshot.KnownGlobalVariables["var[0x030]"] == 1 &&
      session.Snapshot.KnownGlobalVariables["var[0x031]"] == 5, "Syd and Bernard chosen");
session.PressKey(27);
Until(() => session.PlayerHasControl);
Check(session.SessionTrace.Any(l => l.Contains("abortCutscene: Script 131")), "source override skip");
Click(8, 120 + session.MainScreen.Top);
Until(() => session.CurrentRoomId == 1);
Run(120);
for (var step = 0; step < 4; step++)
{
    Click(8, 120 + session.MainScreen.Top);
    Run(200);
}
Check(session.ViewLeft == 352, "walked camera to front door");
Key('d');
Click(464 - session.ViewLeft, 92 + session.MainScreen.Top);
Check(session.Snapshot.KnownGlobalVariables["var[0x01A]"] == 14 &&
      session.Snapshot.KnownGlobalVariables["var[0x01B]"] == 54, "Pick up mat composed");
Console.WriteLine("Sentence: " + ScummTextDecoder.DecodeGlyphs(session.PlaySentenceGlyphs()));
Click(464 - session.ViewLeft, 92 + session.MainScreen.Top);
Until(() => session.Snapshot.ObjectStates[54] == 8);
Key('d');
Click(504 - session.ViewLeft, 92 + session.MainScreen.Top);
Click(504 - session.ViewLeft, 92 + session.MainScreen.Top);
Until(() => session.Snapshot.ObjectOwners[55] == 3);
Check(true, "key picked up by Dave");
var area = session.PlayVerbArea();
Console.WriteLine("Inventory painted pixels: " + Enumerable.Range(184,8).Sum(y => area.Pixels.Skip((y-ScummVerbArea.Top)*320).Take(144).Count(p=>p!=0)));
Click(8,188);
Console.WriteLine("Inventory input: " + session.DeliveredInput.Last());
Console.WriteLine("Sentence: " + ScummTextDecoder.DecodeGlyphs(session.PlaySentenceGlyphs()));
Console.WriteLine($"PASS: Room {session.CurrentRoomId}, key owner 3, {frames} frames after constructor, {session.DeliveredInput.Count} queued clicks/keys.");
foreach (var line in session.SessionTrace.Where(l => l.Contains("startObject") || l.Contains("sentence script") || l.Contains("abortCutscene")))
    Console.WriteLine(line);

void Run(int count)
{
    for (var frame = 0; frame < count; frame++) { session.AdvanceFrame(); frames++; }
}
void Until(Func<bool> ready, int limit = 2500)
{
    for (var frame = 0; frame < limit && !ready(); frame++) Run(1);
    if (!ready()) throw new InvalidOperationException("Route did not reach its next condition:\n" + string.Join('\n', session.SessionTrace.TakeLast(15)));
}
void Check(bool condition, string description)
{
    if (!condition) throw new InvalidOperationException(description);
    Console.WriteLine($"PASS {description} (frame {frames})");
}
void Click(int x, int y)
{
    var input = session.ClickScreen(x, y, out var refused);
    if (input is null) throw new InvalidOperationException(refused);
    Console.WriteLine(input.Description);
    Run(1);
}
void Key(int key)
{
    var input = session.PressPlayKey(key, out var refused);
    if (input is null) throw new InvalidOperationException(refused);
    Console.WriteLine(input.Description);
    Run(1);
}
