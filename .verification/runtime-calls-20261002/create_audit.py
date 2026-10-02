"""Add separate actual call-boundary logs to the unchanged continuity harness."""
from pathlib import Path
import runpy
import sys

out = Path(sys.argv[1])
runpy.run_path('.verification/runtime-continuity-20261002/create_audit.py', run_name='__main__')
p = out/'Program.cs'
source = p.read_text(encoding='utf-8')
needle = '    using var expressionLog='
assert source.count(needle) == 1
source = source.replace(needle, '''    using var callLog=new StreamWriter(Path.Combine(args[2],route.GetProperty("id").GetString()+"-calls.jsonl"));
    using var expressionLog=''')
needle = 'receiveExpression:step => expressionLog.WriteLine(JsonSerializer.Serialize(new{frame=elapsed,step})))'
assert source.count(needle) == 1
source = source.replace(needle, '''receiveExpression:step => expressionLog.WriteLine(JsonSerializer.Serialize(new{frame=elapsed,step})),
       receiveCall:boundary => callLog.WriteLine(JsonSerializer.Serialize(new{frame=elapsed,boundary})))''')
p.write_text(source, encoding='utf-8')
print('Actual separate call boundaries enabled; historical source actions and all legacy streams unchanged.')
