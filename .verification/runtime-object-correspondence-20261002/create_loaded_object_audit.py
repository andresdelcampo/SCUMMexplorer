"""Separate all-site correspondence capture of one unchanged loaded recipe."""
from pathlib import Path
import runpy
import sys

out=Path(sys.argv[1])
runpy.run_path('.verification/runtime-object-correspondence-20261002/create_audit.py',run_name='__main__')
p=out/'Program.cs'
source=p.read_text(encoding='utf-8')
a=source.index('    using var selectionLog=')
b=source.index('    var session=',a)
source=source[:a]+'''    using var selectionLog=new StreamWriter(Path.Combine(args[2],route.GetProperty("id").GetString()+"-object-selections.jsonl"));
    using var correspondenceLog=new StreamWriter(Path.Combine(args[2],route.GetProperty("id").GetString()+"-correspondence.jsonl"));
    using var callLog=new StreamWriter(Path.Combine(args[2],route.GetProperty("id").GetString()+"-calls.jsonl"));
    var correspondence=new LoomCallCorrespondenceTracker(update => {
        if(update.Dispatch?.Site.Operation=="startObject") correspondenceLog.WriteLine(JsonSerializer.Serialize(new{frame=elapsed,update}));
    });
    var observer=new LoomScriptObserver(_ => { }, callCorrespondence:correspondence,
        receiveCall:boundary => {
            if(boundary.Call?.Site.Operation=="startObject") callLog.WriteLine(JsonSerializer.Serialize(new{frame=elapsed,boundary}));
        },
        receiveObjectSelection:selection => selectionLog.WriteLine(JsonSerializer.Serialize(new{frame=elapsed,selection})));
''' + source[b:]
p.write_text(source,encoding='utf-8')
print('Separate loaded-object capture: all sites/keys;same original recipe/actions/packet;primary chronology untouched.')
