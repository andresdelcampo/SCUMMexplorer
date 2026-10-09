using System.Security.Cryptography;
using System.Text.Json;
using SCUMMexplorer.Core.Discovery;

var game=new GameFolderScanner().Scan(args[0]);
var objects=new LflObjectStateReader().ReadSnapshot(Path.Combine(args[0],"00.LFL"));
var starts=JsonDocument.Parse(File.ReadAllText(args[1])).RootElement;
var results=new List<object>();
foreach(var start in starts.EnumerateArray())
{
    bool opening=start.TryGetProperty("stopAtOpening",out var boot)&&boot.GetBoolean();
    var session=new LoomExplorationSession(game,objects,stopAtOpening:opening);
    var rooms=new HashSet<int>();
    void Record(){if(session.Snapshot.RoomId is int r)rooms.Add(r);}
    int room=start.GetProperty("room").GetInt32(), obj=start.GetProperty("objectId").GetInt32(), verb=start.GetProperty("verb").GetInt32();
    int baseline=session.CutscenesBegun, trace=session.SessionTrace.Count;
    if(!opening){rooms.Add(room);session.VisitRoom(game.Rooms.Single(r=>r.ResourceId==room));baseline=session.CutscenesBegun;trace=session.SessionTrace.Count;Record();session.ActivateObjectVerb(obj,verb);}
    else room=session.Snapshot.RoomId ?? throw new InvalidOperationException("Opening has no reached start room");
    Record();
    if(start.TryGetProperty("clickX",out var clickX))
    {
        for(int f=0;session.EgoMoving && f<LoomExplorationSession.CutsceneFrameLimit;f++){Record();session.AdvanceFrame();}
        if(session.EgoMoving)throw new InvalidOperationException("Arrival walk did not finish");
        session.Click(clickX.GetInt32(),start.GetProperty("clickY").GetInt32());Record();
    }
    int frames=0,idle=0; bool started=opening || session.CutscenesBegun>baseline;
    for(;frames<LoomExplorationSession.CutsceneFrameLimit;frames++)
    {
        Record();started|=session.CutscenesBegun>baseline;
        if(started && !session.BootRunning && !session.InCutscene && session.Messages.HaveMsg==0 && !session.EgoMoving && !session.CameraMoving)
        {if(++idle>=32)break;} else idle=0;
        session.AdvanceFrame();
    }
    var sceneTrace=session.SessionTrace.Skip(trace).Where(t=>t.StartsWith("cutscene at ")||t.StartsWith("endCutscene at ")).ToArray();
    if(!started || idle<32 || frames>=LoomExplorationSession.CutsceneFrameLimit || session.Snapshot.Uncertainties.Any(t=>t.Contains("unsupported",StringComparison.OrdinalIgnoreCase)||t.Contains("unmodelled",StringComparison.OrdinalIgnoreCase)))
        throw new InvalidOperationException($"Start {start.GetProperty("id")} did not validate: {frames} frames\n"+string.Join('\n',session.Snapshot.Uncertainties));
    if(!opening && sceneTrace.Count(t=>t.StartsWith("endCutscene at "))<session.CutscenesBegun-baseline)throw new InvalidOperationException("Missing end boundary");
    var result=new{id=start.GetProperty("id").GetString(),startRoom=room,rooms=rooms.Order().ToArray(),frames,source="Validated fresh seed-0 established recipe, identical setup to App replay. Catalog arrival/direct verb/click timing and opening answer remain assumed. Membership from the start room and observed session rooms, not static load operands.",sceneTrace};
    results.Add(result);Console.WriteLine(JsonSerializer.Serialize(result));
}
var files=Directory.GetFiles(args[0],"*.LFL").OrderBy(Path.GetFileName,StringComparer.OrdinalIgnoreCase).Select(p=>new{name=Path.GetFileName(p),sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(p)))}).ToArray();
Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(args[2]))!);
File.WriteAllText(args[2],JsonSerializer.Serialize(new{sourceFiles=files,results},new JsonSerializerOptions{WriteIndented=true}));
