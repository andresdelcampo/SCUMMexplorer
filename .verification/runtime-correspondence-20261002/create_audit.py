"""Attach structural correspondence to the unchanged recipe/call harness."""
from pathlib import Path
import runpy
import sys

out = Path(sys.argv[1])
runpy.run_path('.verification/runtime-calls-20261002/create_audit.py', run_name='__main__')
p = out/'Program.cs'
source = p.read_text(encoding='utf-8')
needle = '    using var callLog='
assert source.count(needle) == 1
source = source.replace(needle, '''    using var correspondenceLog=new StreamWriter(Path.Combine(args[2],route.GetProperty("id").GetString()+"-correspondence.jsonl"));
    var correspondence=new LoomCallCorrespondenceTracker(update => correspondenceLog.WriteLine(JsonSerializer.Serialize(new{frame=elapsed,update})));
    using var callLog=''')
needle = 'receiveCall:boundary => callLog.WriteLine(JsonSerializer.Serialize(new{frame=elapsed,boundary})))'
assert source.count(needle) == 1
source = source.replace(needle, '''receiveCall:boundary => callLog.WriteLine(JsonSerializer.Serialize(new{frame=elapsed,boundary})),
       callCorrespondence:correspondence)''')
p.write_text(source, encoding='utf-8')
print('Independent structural tracker attached; existing recipe actions and all legacy streams unchanged.')
