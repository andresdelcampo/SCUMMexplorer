using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Reflection;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;

var output = Path.GetFullPath(args[0]);
Directory.CreateDirectory(output);
var game = new GameFolderScanner().Scan("Games/loom vga en");
var data = ScummDataSources.For(game);
var reference = Path.GetFullPath(".verification/native-walk-20261008/Reference/reference.exe");
var results = new List<object>();
if(args.Contains("arrival") || args.Contains("monitor"))
{
    var frame=0;
    LoomExplorationSession? session=null;
    var samples=new List<object>();
    var monitorContext=data.RoomLocalScript(34,200)!.Context;
    var entryContext=data.RoomEntryScript(34)!.Context;
    var observer=new LoomScriptObserver(o=> {
        if(o.Site?.Stage!="instruction" || session is null) return;
        var ego=session.Actors.Single(a=>a.Number==1);
        samples.Add(new { frame,o.Sequence,o.Site,o.Values,o.SourceAccess,
            ego.X,ego.Y,ego.WalkBox,ego.Moving,session.CameraX,session.InCutscene });
    },includeSite:s=>s.Script==97 || s.Context==monitorContext || s.Context==entryContext,
        captureSourceAccess:true);
    session=new LoomExplorationSession(game,game.Vga!.ReadIndexedObjects(),completeBoot:false,observer:observer);
    var monitorRun=args.Contains("monitor");
    session.VisitRoomById(monitorRun ? 34 : 46);
    for(;frame<3000 && !session.GameStatus.CanEdit;frame++) session.AdvanceFrame();
    var baseline=session.SessionTrace.Count;
    if(monitorRun) {
        for(var n=0;n<32;n++,frame++) session.AdvanceFrame();
        // Public walking to the source-established doorway walk point; no placement or box injection.
        var doorway=data.ReadRoom(34).Objects.Single(o=>o.Id==485);
        session.WalkEgoTo(doorway.WalkX,doorway.WalkY);
    }
    else session.ActivateObjectVerb(610,0x36);
    var frames=new List<object>();
    var runtime=typeof(LoomExplorationSession).GetField("_runtime",BindingFlags.NonPublic|BindingFlags.Instance)!.GetValue(session)!;
    for(var n=0;n<600;n++,frame++) {
        if(monitorRun && n==200 && session.GameStatus.CanEdit) {
            var end=data.ReadRoom(34).WalkBoxes!.Coordinates(8)!.LowerRight;
            session.WalkEgoTo(end.X,end.Y);
        }
        var ego=session.Actors.Single(a=>a.Number==1);
        frames.Add(new { frame,room=session.CurrentRoomId,ego.X,ego.Y,ego.WalkBox,ego.Moving,
            session.CameraX,session.InCutscene,
            userPut=runtime.GetType().GetProperty("UserPut")!.GetValue(runtime),
            flags6=session.CurrentWalkBoxes?.GetFlags(6) });
        session.AdvanceFrame();
    }
    File.WriteAllText(Path.Combine(output,monitorRun ? "monitor.json" : "arrival.json"),JsonSerializer.Serialize(new { frames,samples,
        trace=session.SessionTrace.Skip(baseline).ToArray(),session.LastAction },new JsonSerializerOptions { WriteIndented=true }));
    Console.WriteLine($"{(monitorRun ? "Fresh Room 34 public walks" : "Room 46/610 look return")}: room {session.CurrentRoomId}, ego {session.Actors[0].X},{session.Actors[0].Y}, box {session.Actors[0].WalkBox}; {samples.Count} source samples");
    return;
}
foreach (var (room, startX, startY, destX, destY) in new[] {
    (34,246,64,246,80), (34,246,64,311,90), (34,246,80,311,90),
    (34,246,82,378,134), (34,311,90,246,64), (34,246,64,333,79), (34,246,80,333,79), (2,828,60,860,118),
    (20,153,102,250,100), (41,150,100,250,100), (43,150,100,250,100) })
{
    var boxes = data.ReadRoom(room).WalkBoxes!;
    // Decoded resource defaults, with box 6 enabled as Room 34's entry does.
    // This is an isolated geometry comparison; no story state is manufactured.
    if (room == 34) boxes.SetFlags(6,0);
    var actors = new LoomActorTable(useV4Walking: args.Contains("v4"));
    var place = new LoomActorTable.Placement(room, boxes, null, null);
    actors.Put(1,startX,startY,room,place,1);
    actors.Walk(1,destX,destY,place,"diagnostic geometry walk");
    var before = actors[1];
    var end = boxes.Adjust(destX,destY,false,0,0);
    // startWalkActor keeps its previous destination box when it contains the point.
    if (boxes.Contains(before.WalkBox!.Value,destX,destY))
        end = end with { X=destX, Y=destY, Box=before.WalkBox.Value };
    var input = new StringBuilder().AppendLine(boxes.Count.ToString());
    foreach (var box in boxes.Boxes)
        input.AppendLine(string.Join(" ",box.Corners.SelectMany(p=>new[]{p.X,p.Y}).Append(box.Scale)));
    for(var from=0;from<boxes.Count;from++)
        input.AppendLine(string.Join(" ",Enumerable.Range(0,boxes.Count).Select(to=>boxes.GetNextBox(from,to))));
    input.AppendLine($"{before.X} {before.Y} {before.WalkBox} {end.X} {end.Y} {end.Box} {before.Look.WalkSpeedX} {before.Look.WalkSpeedY} {before.Look.Scale} 1000");
    var process = new Process { StartInfo = new ProcessStartInfo {
        FileName = @"C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe",
        RedirectStandardInput=true, RedirectStandardOutput=true, RedirectStandardError=true, UseShellExecute=false,
        CreateNoWindow=true, WorkingDirectory=Path.GetDirectoryName(reference)! } };
    process.StartInfo.ArgumentList.Add("proxy"); process.StartInfo.ArgumentList.Add("./reference.exe");
    process.Start(); process.StandardInput.Write(input); process.StandardInput.Close();
    var native = process.StandardOutput.ReadToEnd().Split('\n',StringSplitOptions.RemoveEmptyEntries)
        .Select(l=>l.Split(' ',StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray()).ToArray();
    var error = process.StandardError.ReadToEnd(); process.WaitForExit();
    if(process.ExitCode!=0) throw new InvalidOperationException(error);
    var explorer = new List<int[]>();
    for(var f=0;f<=1000;f++) {
        var a=actors[1]; explorer.Add([f,a.X!.Value,a.Y!.Value,a.WalkBox!.Value,a.Moving]);
        if(a.Moving==0) break;
        actors.StepWalks(place);
    }
    var nativeChanges = native.Select(a=>a[3]).Distinct().ToArray();
    var explorerChanges = explorer.Select(a=>a[3]).Distinct().ToArray();
    var firstDifference = Enumerable.Range(0,Math.Min(native.Length,explorer.Count))
        .FirstOrDefault(i=>!native[i].SequenceEqual(explorer[i]),-1);
    if(firstDifference<0 && native.Length!=explorer.Count) firstDifference=Math.Min(native.Length,explorer.Count);
    var name=$"room{room}-{startX}-{startY}-to-{destX}-{destY}";
    File.WriteAllText(Path.Combine(output,name+".json"),JsonSerializer.Serialize(new { room,startX,startY,destX,destY,
        initial=before, destination=end, native, explorer, firstDifference },new JsonSerializerOptions { WriteIndented=true }));
    results.Add(new { name,firstDifference,nativeFrames=native.Length-1,explorerFrames=explorer.Count-1,
        nativeChanges,explorerChanges,nativeEnd=native[^1],explorerEnd=explorer[^1] });
    Console.WriteLine($"{name}: first difference {firstDifference}; native {native.Length-1} frames {string.Join('/',nativeChanges)}, explorer {explorer.Count-1} frames {string.Join('/',explorerChanges)}");
    if(args.Contains("v4") && (firstDifference>=0 || explorer[^1][4]!=0))
        throw new InvalidDataException($"Native comparison failed for {name}");
}
File.WriteAllText(Path.Combine(output,"summary.json"),JsonSerializer.Serialize(results,new JsonSerializerOptions { WriteIndented=true }));
