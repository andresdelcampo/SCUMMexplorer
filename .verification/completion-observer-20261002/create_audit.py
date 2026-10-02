"""Use the production opt-in observer with the unchanged13 native recipes."""
from pathlib import Path
import hashlib
import json
import sys

repo=Path.cwd()
out=Path(sys.argv[1]).resolve()
assert out.is_relative_to((repo/'Exe').resolve())
out.mkdir(parents=True,exist_ok=False)
source=(repo/'.verification/room41-certainty-20261002/ProgressionProbe/Program.cs').read_text(encoding='utf-8-sig')
needle='    var session=new LoomExplorationSession'
assert source.count(needle)==1
source=source.replace(needle,'''    int elapsed=0;
    using var instructionLog=new StreamWriter(Path.Combine(args[2],route.GetProperty("id").GetString()+"-instructions.jsonl"));
    using var cancellationLog=new StreamWriter(Path.Combine(args[2],route.GetProperty("id").GetString()+"-cancellations.jsonl"));
    var keys=new HashSet<string>{"bit[0xD6C]","bit[0xD67]","bit[0xD70]","bit[0xD61]","bit[0xD66]","var[0x0D6]","var[0x004]","var[0x064]","var[0x067]","var[0x07A]","var[0x07B]","var[0x062]"};
    for(int i=0x32;i<0x5A;i++)keys.Add($"var[0x{i:X3}]");
    var observer=new LoomScriptObserver(observation => {
        if(observation.Site is not { } site) {
            cancellationLog.WriteLine(JsonSerializer.Serialize(new{frame=elapsed,record=observation})); return;
        }
        instructionLog.WriteLine(JsonSerializer.Serialize(new{frame=elapsed,record=new{
            Run=observation.Invocation,observation.Sequence,observation.Slot,observation.ParentSlot,
            Stage=site.Stage,Script=site.Script,site.Context,site.Container,site.ResourceOffset,site.Offset,site.Operation,site.Operands,
            observation.PathEstimated,Values=observation.Values,observation.Estimated,Resume=observation.ResumeOffset,
            Stopped=observation.ReachedStop,Unresolved=observation.UnresolvedReason}}));
    }, site => site.Script is 10 or 20 or 22 or 23 or 30 or 31 || site.Context=="Room 06 local script 201" ||
        site.Operands?.Contains("bit[0xD6C]")==true || site.Operands?.Contains("bit[0xD67]")==true ||
        site.Operands?.Contains("var[0x0D6]")==true,
       key => keys.Contains(key)||key.StartsWith("local["));
    var session=new LoomExplorationSession''')
source=source.replace('stopAtBootInput:route.TryGetProperty("nativeBoot",out var nativeBoot)&&nativeBoot.GetBoolean())',
    'stopAtBootInput:route.TryGetProperty("nativeBoot",out var nativeBoot)&&nativeBoot.GetBoolean(),observer:observer)')
assert source.count('    int elapsed=0; var roomChanges=')==1
source=source.replace('    int elapsed=0; var roomChanges=','    var roomChanges=')
(out/'Program.cs').write_text(source,encoding='utf-8')
(out/'CompletionAudit.csproj').write_text(f'''<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup><OutputType>Exe</OutputType><TargetFramework>net10.0</TargetFramework><ImplicitUsings>enable</ImplicitUsings><Nullable>enable</Nullable></PropertyGroup>
  <ItemGroup><ProjectReference Include="{(repo/'src/SCUMMexplorer.Core/SCUMMexplorer.Core.csproj').as_posix()}" />
    <Compile Include="{(repo/'.verification/HarnessFailurePolicy.cs').as_posix()}" Link="HarnessFailurePolicy.cs" /></ItemGroup>
</Project>''',encoding='utf-8')
manifest={p.relative_to(repo).as_posix():hashlib.sha256(p.read_bytes()).hexdigest()
    for p in sorted((repo/'src/SCUMMexplorer.Core').rglob('*')) if p.is_file() and 'obj' not in p.parts}
(out/'source-manifest.json').write_text(json.dumps(manifest,indent=2)+'\n',encoding='utf-8')
print('Unchanged recipe harness with production observer; no copied-Core patches, inputs or estimate writes.')
