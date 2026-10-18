using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Maniac;
using SCUMMexplorer.Core.Scumm;

// Family S7 census and three fresh prepared proofs; game data remains read-only.
var game = new GameFolderScanner().Scan(args[0]);
var data = ScummDataSources.For(game);
var planner = ManiacCutsceneSetup.Planner(game);
Console.WriteLine(ManiacCutsceneSetup.PreparationNote);
Console.WriteLine($"triggers={planner.Triggers.Count}; groups={planner.Groups.Count}; scenes={planner.Groups.Count(g => g.IsScene)}; dialogue={planner.Groups.Count(g => !g.IsScene)}");
Console.WriteLine($"verbs: {string.Join(", ", Enumerable.Range(1,15).Select(v => $"{v}:{planner.Verbs.WordFor(v)}"))}");
if (args.ElementAtOrDefault(1) == "proof")
{
    foreach (var (room, obj, verb, global) in new[] { (1,394,9,87), (8,157,5,47), (17,190,4,112) })
    {
        var trigger = planner.Triggers.Single(t => t.Room == room && t.ObjectId == obj && t.VerbId == verb);
        var result = planner.Run(trigger);
        Console.WriteLine($"PROOF {room}/{obj}/{verb}: {ScummCutsceneTitles.Title(trigger)}; {result}");
        foreach (var i in data.GlobalScript(global)!.Instructions.Where(i => i.Operation is "cutscene" or "endCutscene" or "print" or "printEgo" or "loadRoom" or "startScript"))
            Console.WriteLine($"SOURCE Script{global} +0x{i.Offset:X4}: {i.Operation} {i.Operands}");
        var source = data.ReadObjectVerb(room, data.ReadRoom(room).Objects.First(o => o.Id == obj), verb, out _)!;
        foreach (var i in source.Instructions) Console.WriteLine($"OBJECT +0x{i.Offset:X4}: {i.Operation} {i.Operands}");
        var session = ManiacCutsceneSetup.NewSession(game);
        session.VisitRoom(game.Rooms.Single(r => r.ResourceId == room));
        var trace = session.SessionTrace.Count; var messages = session.Messages.Log.Count; var begun = session.CutscenesBegun;
        session.ActivateObjectVerb(obj, verb);
        Console.WriteLine($"CONTEXT {session.Snapshot.KnownGlobalVariables[ScummVars.V2.ActiveVerb]}/{session.Snapshot.KnownGlobalVariables[ScummVars.V2.ActiveObject1]}/{session.Snapshot.KnownGlobalVariables[ScummVars.V2.ActiveObject2]}");
        for (var frame = 0; frame < 6000; frame++)
        {
            foreach (var line in session.SessionTrace.Skip(trace).Where(t => t.StartsWith("cutscene at ") || t.StartsWith("endCutscene at "))) Console.WriteLine($"FRAME{frame}: {line}");
            trace = session.SessionTrace.Count;
            foreach (var line in session.Messages.Log.Skip(messages)) Console.WriteLine($"FRAME{frame} ROOM{session.CurrentRoomId}: {line.Source}: {line.Text.Replace('\n',' ')}");
            messages = session.Messages.Log.Count;
            if (session.CutscenesBegun > begun && !session.InCutscene && session.Messages.HaveMsg == 0) { Console.WriteLine($"END frame{frame} room{session.CurrentRoomId} control{session.PlayerHasControl}"); break; }
            session.AdvanceFrame();
        }
    }
    return;
}
var runs = planner.Groups.Select(g => (Group:g, Run:planner.Run(g.First))).ToArray();
foreach (var (g,r) in runs)
    Console.WriteLine($"{g.First.Room}/{g.First.ObjectId}/{g.First.VerbId}: {ScummCutsceneTitles.Title(g.First)} | scripts={string.Join(';',g.First.Scripts)} | texts={g.First.Texts} | begun={r.Begun} frames={r.Frames} rooms=[{string.Join(',',r.RoomsEntered)}] failure={r.Failure}");
Console.WriteLine($"TOTAL runs={runs.Length}; begun={runs.Count(p=>p.Run.Begun)}; failed={runs.Count(p=>p.Run.Failure is not null)}; limit={runs.Count(p=>p.Run.Frames>=ScummCutsceneRun.EndFrames)}");
