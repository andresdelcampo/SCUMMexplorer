using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;

var game = new GameFolderScanner().Scan(args[0]);
var objects = new LflObjectStateReader().ReadSnapshot(Path.Combine(args[0],"00.LFL"));
var input = JsonDocument.Parse(File.ReadAllText(args[1])).RootElement;
var results = new List<object>(); var clock = Stopwatch.StartNew();
Directory.CreateDirectory(args[2]);
int n=0;
foreach (var root in input.GetProperty("roots").EnumerateArray())
{
    var owner = root.GetProperty("owner").GetString()!;
    int roomId=root.GetProperty("room").GetInt32(), obj=root.GetProperty("objectId").GetInt32(), verb=root.GetProperty("verb").GetInt32();
    var s = new LoomExplorationSession(game,objects);
    var baseline=s.CutscenesBegun; int trace=s.SessionTrace.Count;
    s.VisitRoom(game.Rooms.Single(r => r.ResourceId==roomId));
    if(obj!=0) { baseline=s.CutscenesBegun; trace=s.SessionTrace.Count; s.ActivateObjectVerb(obj,verb); }
    var lines=new HashSet<string>(); var rooms=new HashSet<int>(); int first=-1, last=-1, frames=0; bool everOpen=false;
    int audioBaseline=s.SoundEvents.Count; int puts=s.EgoPuts; int idle=0;
    for (;frames<3000;frames++)
    {
        if(s.Snapshot.RoomId is int id) rooms.Add(id);
        foreach(var m in s.Snapshot.ScreenMessages) if(!string.IsNullOrWhiteSpace(m.Text)) lines.Add(m.Text);
        if(s.CutscenesBegun>baseline && first<0) first=frames;
        everOpen |= s.InCutscene;
        if(first<0 && frames>=32) break;
        if(first>=0 && !s.InCutscene && s.Messages.HaveMsg==0 && !s.EgoMoving && !s.CameraMoving)
        { last=frames; if(++idle>=32) break; }
        else idle=0;
        s.AdvanceFrame();
    }
    var sceneTrace=s.SessionTrace.Skip(trace).Where(t=>t.StartsWith("cutscene at ") || t.StartsWith("endCutscene at ")).ToArray();
    int begun=s.CutscenesBegun-baseline;
    int ended=sceneTrace.Count(t=>t.StartsWith("endCutscene at "));
    var uncertainties=s.Snapshot.Uncertainties;
    bool unsupported=uncertainties.Any(t=>t.Contains("unsupported",StringComparison.OrdinalIgnoreCase) || t.Contains("unmodelled",StringComparison.OrdinalIgnoreCase));
    string status=first<0 ? "No cutscene reached from this start" : frames>=3000 || s.InCutscene || ended<begun ? "Incomplete: prerequisites, waits or executor require review" : unsupported ? "Completed preview with unsupported execution: review required" : !everOpen ? "Brief input-suppression scope: not an independent replay" : "Validated state preview";
    var result=new {owner,room=roomId,objectId=obj,verb,status,frames,first,last,everOpen,begun,ended,
        rooms=rooms.Order().ToArray(),lines=lines.ToArray(),soundCommands=s.SoundEvents.Count-audioBaseline,egoPuts=s.EgoPuts-puts,
        candidates=root.GetProperty("candidates").Deserialize<string[]>(),source=root.GetProperty("source").Clone(),sceneTrace,uncertainties};
    results.Add(result);
    File.WriteAllText(Path.Combine(args[2],$"probe-{++n:000}.json"),JsonSerializer.Serialize(result,new JsonSerializerOptions{WriteIndented=true}));
    Console.WriteLine($"{n}: {owner}: {status}; {frames} frames ({clock.Elapsed.TotalSeconds:F1}s)");
}
var files=Directory.GetFiles(args[0],"*.LFL").OrderBy(Path.GetFileName,StringComparer.OrdinalIgnoreCase).Select(p=>new {name=Path.GetFileName(p),sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)))}).ToArray();
File.WriteAllText(Path.Combine(args[2],"audit.json"),JsonSerializer.Serialize(new{sourceFiles=files,inputSha256=input.GetProperty("inputSha256").GetString(),results},new JsonSerializerOptions{WriteIndented=true}));
Console.WriteLine($"Done: {n} recipes, {clock.Elapsed}");
