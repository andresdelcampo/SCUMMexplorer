"""Attach actual object selections without changing any original recipe/legacy stream."""
from pathlib import Path
import runpy
import sys

out = Path(sys.argv[1])
runpy.run_path('.verification/runtime-correspondence-20261002/create_audit.py', run_name='__main__')
p = out/'Program.cs'
source = p.read_text(encoding='utf-8')
needle = '    using var correspondenceLog='
assert source.count(needle) == 1
source = source.replace(needle, '''    using var selectionLog=new StreamWriter(Path.Combine(args[2],route.GetProperty("id").GetString()+"-object-selections.jsonl"));
    using var correspondenceLog=''')
needle = 'callCorrespondence:correspondence)'
assert source.count(needle) == 1
source = source.replace(needle, '''callCorrespondence:correspondence,
       receiveObjectSelection:selection => selectionLog.WriteLine(JsonSerializer.Serialize(new{frame=elapsed,selection})))''')
p.write_text(source, encoding='utf-8')
print('Actual decoder selections attached separately; original recipes/calls/instructions unchanged.')
