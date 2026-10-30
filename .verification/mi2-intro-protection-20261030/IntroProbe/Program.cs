using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Scumm;

// Usage: IntroProbe <game folder> [frames]
// 30 October 2026: runs the explicit native intro (ScummGameSession.StartIntro, the path of the header's Play Game Intro) with no input and logs,
// whenever it changes, the room, user input, cursor, pending scripts and the texts on screen. Nothing here edits session state.
var game = new GameFolderScanner().Scan(args[0]);
var limit = args.Length > 1 ? int.Parse(args[1]) : 6000;
var session = ScummGameSession.StartIntro(game, out _);
string? last = null;
for (var frame = 0; frame < limit; frame++)
{
    session.AdvanceFrame();
    var texts = string.Join(" / ", session.Messages.OnScreen.SelectMany(m => m.Lines).Where(l => l.Length > 0).Take(4));
    var state = $"room {session.CurrentRoomId} input {(session.UserInputOn ? "on" : "off")} cutscene {session.InCutscene} boot {session.BootRunning} | {string.Join(", ", session.PendingScriptSummary.Take(6))} | {texts}";
    if (state != last) Console.WriteLine($"{frame,5} {state}");
    last = state;
}
Console.WriteLine($"verbs on: {string.Join(", ", session.Verbs.Where(v => v.Mode == 1).Select(v => $"{v.VerbId}:{v.Name}"))}");
