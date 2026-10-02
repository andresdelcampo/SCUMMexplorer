"""Compare old/new structural accounting on one unchanged actual all-site stream."""
from pathlib import Path
import subprocess
import sys

out = Path(sys.argv[1])
if out.exists(): raise FileExistsError(out)
out.mkdir(parents=True)
template = Path('Exe/RuntimeBoundariesFinalSession3-20261002/loaded-objects-2/observer-1')
for file in template.iterdir():
    if file.is_file(): (out/file.name).write_bytes(file.read_bytes())
source = subprocess.run(['rtk','proxy','git','show','f84460f:src/SCUMMexplorer.Core/Formats/LoomCallCorrespondenceTracker.cs'],
                        check=True,capture_output=True,encoding='utf-8').stdout
source = 'namespace SCUMMexplorer.Core.Formats;\n'+source[source.index('public sealed class LoomCallCorrespondenceTracker'):]
(out/'PriorTracker.cs').write_text(source.replace('LoomCallCorrespondenceTracker','PriorCallCorrespondenceTracker'),encoding='utf-8')
(out/'ResourceIdentity.cs').write_text('''namespace SCUMMexplorer.Core.Formats;
internal static class ResourceIdentity {
    internal static bool SameResource(this LoomScriptObservationSite site,LoomScriptObservationSite? other) =>
        other is not null && site.Script==other.Script && site.Context==other.Context &&
        site.Container==other.Container && site.ResourceOffset==other.ResourceOffset;
}
''',encoding='utf-8')
p = out/'Program.cs'
source = p.read_text(encoding='utf-8')
start = source.index('    using var selectionLog=')
end = source.index('    var session=',start)
source = source[:start]+'''    using var oldLog=new StreamWriter(Path.Combine(args[2],"old.jsonl"));
    using var newLog=new StreamWriter(Path.Combine(args[2],"new.jsonl"));
    void Copy(StreamWriter log,LoomCallCorrespondence u) => log.WriteLine(JsonSerializer.Serialize(new {
        frame=elapsed,u.Event,id=u.Dispatch?.Id,site=u.Dispatch?.Site,u.Barrier,u.ChildRejection,u.CallerRejection,
        binding=u.Binding?.Initialization,child=u.ChildBoundaries.LastOrDefault(),host=u.HostReturn?.Sequence,
        continued=u.CallerContinuation?.Sequence,ended=u.CallerEnd?.Sequence,resumed=u.CallerResumption?.RestoredLocals.Sequence,
        u.ChildTerminated,triple=u.ImmediateStopCorrespondence is not null
    }));
    var prior=new PriorCallCorrespondenceTracker(u=>Copy(oldLog,u));
    var current=new LoomCallCorrespondenceTracker(u=>Copy(newLog,u));
    var observer=new LoomScriptObserver(prior.ObserveScript,callCorrespondence:current,
        receiveCall:prior.ObserveCall,receiveObjectSelection:prior.ObserveObjectSelection);
''' + source[end:]
p.write_text(source,encoding='utf-8')
print('Prior tracker only copied for diagnosis; both consume one unchanged actual stream; no execution or recipe patch.')
