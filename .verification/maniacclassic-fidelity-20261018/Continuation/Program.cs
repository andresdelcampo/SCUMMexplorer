using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Scumm;
using SCUMMexplorer.Tests;

var folder = @"D:\Juegos\Lucas\mmansion ega en";
var game = new GameFolderScanner().Scan(folder);
var data = ScummDataSources.For(game);
var observations = new List<ScummScriptObservation>();
var observer = new ScummScriptObserver(observations.Add, site => site.Script == 174,
    key => key is "var[0x0C8]" or "var[0x0C9]");
var session = ManiacClassicStateRoute.AcquireKey(folder, observer);
var bell = data.ReadRoom(1).Objects.Single(obj => obj.Id == 394);
Console.WriteLine($"Doorbell {bell.X},{bell.Y} {bell.Width}x{bell.Height}; view left {session.ViewLeft}");
void Frames(int count) { for (var frame = 0; frame < count; frame++) session.AdvanceFrame(); }
void Until(Func<bool> ready, int limit)
{ for (var frame = 0; frame < limit && !ready(); frame++) session.AdvanceFrame(); if (!ready()) throw new Exception("Native continuation frame bound exceeded."); }
Until(() => session.PlayerHasControl, 1200);
var push = session.Verbs.Single(verb => verb.VerbId == 9 && verb.Mode == 1);
if (session.ClickScreen((push.X ?? throw new Exception("Push X unknown")) + 4,
    (push.Y ?? throw new Exception("Push Y unknown")) + 4, out var refused) is null) throw new Exception(refused);
Frames(1);
Console.WriteLine("Selected verb " + session.Snapshot.KnownGlobalVariables[ScummVars.V2.SentenceVerb]);
if (session.Snapshot.KnownGlobalVariables[ScummVars.V2.SentenceVerb] != 9) throw new Exception("Push key did not select native Push.");
var x = bell.X + bell.Width / 2 - session.ViewLeft;
var y = bell.Y + bell.Height / 2 + session.MainScreen.Top;
if (x < 0 || x >= 320) throw new Exception("Doorbell outside reached native camera; no direct activation used.");
for (var click = 0; click < 2; click++)
{ if (session.ClickScreen(x, y, out refused) is null) throw new Exception(refused); Frames(1); }
var before = session.CutscenesBegun;
var messages = new HashSet<string>();
for (var frame = 0; frame < 3000; frame++)
{
    foreach (var message in session.Messages.OnScreen)
        if (messages.Add(message.Text)) Console.WriteLine($"Frame {frame}, room {session.CurrentRoomId}: {message.Text}");
    session.AdvanceFrame();
}
Console.WriteLine($"After continuation: room {session.CurrentRoomId}, control {session.PlayerHasControl}, scenes {session.CutscenesBegun - before}");
Console.WriteLine($"Script 174 queries {observations.Count(observation => observation.Site?.Operation == "getClosestObjActor")}; observed post-query values {string.Join(',', observations.Where(observation => observation.Site?.Offset == 3).Select(observation => observation.Values.GetValueOrDefault("var[0x0C9]")).Distinct())}");
foreach (var observation in observations.Where(observation => observation.Site is not null).Take(45))
    Console.WriteLine($"Script 174 {observation.Site?.Stage} +{observation.Site?.Offset:X4} {observation.Site?.Operation}: C8={observation.Values.GetValueOrDefault("var[0x0C8]")}, C9={observation.Values.GetValueOrDefault("var[0x0C9]")}; {observation.UnresolvedReason}");
foreach (var actor in session.Actors.Where(actor => actor.Number is 3 or 11)) Console.WriteLine(actor);
foreach (var line in session.Snapshot.Uncertainties.Where(line => line.Contains("unsupported v2"))) Console.WriteLine("Limitation: " + line);
if (!messages.Any(message => message.Contains("Oh, the doorbell!"))) throw new Exception("Native scene line was not reached.");
if (!observations.Any(observation => observation.Site?.Operation == "getClosestObjActor")) throw new Exception("Script 174 query was not reached.");
if (session.Snapshot.Uncertainties.Any(line => line.Contains("unsupported v2"))) throw new Exception("Native route retains unsupported opcode.");
Console.WriteLine("PASS: genuine boot/party/exit/key route, native Push sentence, doorbell scene and 3000-frame continuation.");
