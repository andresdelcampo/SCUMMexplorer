"""Native-boot chronology/transparency audit using the existing source input recipe."""
from pathlib import Path
import hashlib
import json
import sys

out = Path(sys.argv[1])
assert not out.exists(), 'Use fresh output.'
out.mkdir(parents=True)
root = Path.cwd()
(out/'ExpressionAudit.csproj').write_text(f'''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
  <ItemGroup><ProjectReference Include="{(root/'src/SCUMMexplorer.Core/SCUMMexplorer.Core.csproj').as_posix()}" />
    <Compile Include="{(root/'.verification/HarnessFailurePolicy.cs').as_posix()}" Link="HarnessFailurePolicy.cs" /></ItemGroup>
</Project>
''', encoding='utf-8')
(out/'Program.cs').write_text('''using System.Text.Json;
using SCUMMexplorer.Core.Discovery;
using SCUMMexplorer.Core.Formats;
using SCUMMexplorer.Core.Rendering;
var game = new GameFolderScanner().Scan(args[0]);
var objects = new LflObjectStateReader().ReadSnapshot(Path.Combine(args[0], "00.LFL"));
var steps = new List<LoomExpressionObservation>();
var outer = new List<LoomScriptObservation>();
var baseline = new LoomExplorationSession(game, objects, stopAtBootInput:true);
var observed = new LoomExplorationSession(game, objects, stopAtBootInput:true,
    observer:new(outer.Add, includeSite:site => site.Operation == "expression", receiveExpression:steps.Add));
string Packet(LoomExplorationSession session) => JsonSerializer.Serialize(new {
    session.Snapshot, session.SessionTrace, session.GameStatus, session.Actors,
    messages=new { session.Messages.HaveMsg, session.Messages.TalkDelay, session.Messages.TalkingActor,
        session.Messages.Talk, session.Messages.Drawn, session.Messages.Log },
    session.CutscenesBegun, session.InCutscene, session.EgoMoving, session.CameraMoving});
if(Packet(baseline) != Packet(observed)) throw new InvalidDataException("Native boot observation changed the packet.");
var recipe=JsonDocument.Parse(File.ReadAllText(args[2])).RootElement[0];
int frames=0;
foreach(var action in recipe.GetProperty("actions").EnumerateArray()) {
    var kind=action.GetProperty("kind").GetString();
    if(kind == "boot-answer") break; // Preserve the unresolved opening; audit only the existing challenge input.
    if(kind == "inspect") continue;
    if(kind == "frames") {
        for(int frame=0; frame<action.GetProperty("count").GetInt32(); frame++) {
            baseline.AdvanceFrame(); observed.AdvanceFrame(); frames++;
            if(Packet(baseline) != Packet(observed)) throw new InvalidDataException($"Observation changed native boot frame {frames}.");
        }
    } else if(kind == "target") {
        var room=game.Rooms.Single(r => r.ResourceId == baseline.Snapshot.RoomId);
        var target=new LoomRoomObjectDirectory().Decode(File.ReadAllBytes(room.FilePath)).Single(o => o.Id == action.GetProperty("objectId").GetInt32());
        baseline.Click(target.X+target.Width/2,target.Y+target.Height/2);
        observed.Click(target.X+target.Width/2,target.Y+target.Height/2);
        if(Packet(baseline) != Packet(observed)) throw new InvalidDataException("Observation changed the existing challenge input.");
    } else throw new InvalidDataException($"Unsupported historical recipe action {kind}.");
}
if(steps.Count == 0) throw new InvalidDataException("No loaded native expression was observed.");
File.WriteAllText(args[1],JsonSerializer.Serialize(new { nativeBoot=true, frames, sourceRecipe=args[2], sourceInputOnly=true,
    transparent=true, packet=JsonDocument.Parse(Packet(observed)).RootElement, outer, steps },new JsonSerializerOptions { WriteIndented=true }));
Console.WriteLine($"Transparent native boot source input: {outer.Count(o => o.Site?.Operation == "expression")} loaded expressions, {steps.Count} actual steps; no state writes.");
''', encoding='utf-8')
manifest = {p.as_posix(): hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(Path('src/SCUMMexplorer.Core').rglob('*.cs')) if 'obj' not in p.parts}
(out/'source-manifest.json').write_text(json.dumps(manifest, indent=2)+'\n', encoding='utf-8')
print('Source-executed native boot expression audit generated; existing challenge recipe, no prerequisites or state writes.')
