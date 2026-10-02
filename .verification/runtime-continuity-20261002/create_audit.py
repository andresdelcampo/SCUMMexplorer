"""Add independent native/expression logs to the unchanged historical recipe observer."""
from pathlib import Path
import runpy
import sys

out = Path(sys.argv[1])
expanded = '--expanded' in sys.argv[2:]
sys.argv = ['create_audit.py', str(out)] + (['--expanded'] if expanded else [])
runpy.run_path('.verification/runtime-dependencies-20261002/create_audit.py', run_name='__main__')
p = out/'Program.cs'
source = p.read_text(encoding='utf-8')
needle = '    using var accessLog='
assert source.count(needle) == 1
source = source.replace(needle, '''    using var expressionLog=new StreamWriter(Path.Combine(args[2],route.GetProperty("id").GetString()+"-expressions.jsonl"));
    using var nativeLog=new StreamWriter(Path.Combine(args[2],route.GetProperty("id").GetString()+"-native.jsonl"));
    var nativeDependencies=new LoomNativeDependencyTracker(update => nativeLog.WriteLine(JsonSerializer.Serialize(new{frame=elapsed,update})));
    using var accessLog=''')
needle = 'captureOperandBindings:true, runtimeRelations:relations);'
assert source.count(needle) == 1
source = source.replace(needle, '''captureOperandBindings:true, runtimeRelations:relations,
       nativeDependencies:nativeDependencies,
       receiveExpression:step => expressionLog.WriteLine(JsonSerializer.Serialize(new{frame=elapsed,step})));''')
needle = 'match.WriterTerminated,match.TerminalRejection,'
assert source.count(needle) == 1
source = source.replace(needle, 'match.WriterTerminated,match.TerminalRejection,match.ResumedTerminalContinuity,')
p.write_text(source, encoding='utf-8')
print('Separate expression/native logs enabled; historical recipe actions and instruction stream unchanged.')
