"""Observe new native fields on the unchanged native challenge input, in a separate stream."""
from pathlib import Path
import runpy
import sys

out=Path(sys.argv[1])
runpy.run_path('.verification/runtime-continuity-20261002/create_expression_audit.py',run_name='__main__')
p=out/'Program.cs'
source=p.read_text(encoding='utf-8')
needle='var baseline = new LoomExplorationSession'
assert source.count(needle)==1
source=source.replace(needle,'''var native = new List<LoomNativeDependencyUpdate>();
var tracker = new LoomNativeDependencyTracker(update => {
    if(update.Reads.Any(r => r.Dependency.Domain is "user-input" or "object-position-x" or "object-position-y") ||
       update.Observation is null && update.Invalidated.Count > 0) native.Add(update);
});
var baseline = new LoomExplorationSession''')
needle='observer:new(outer.Add, includeSite:site => site.Operation == "expression", receiveExpression:steps.Add)'
assert source.count(needle)==1
source=source.replace(needle,'''observer:new(outer.Add, includeSite:site => site.Operation is "expression" or "drawObject" or "getActorX" or "getActorY" ||
        site.Operation == "cursorCommand" && site.Operands?.StartsWith("userInput",StringComparison.Ordinal)==true,
        receiveExpression:steps.Add, captureSourceAccess:true, nativeDependencies:tracker)''')
needle='transparent=true, packet=JsonDocument.Parse(Packet(observed)).RootElement, outer, steps'
assert source.count(needle)==1
source=source.replace(needle,'transparent=true, packet=JsonDocument.Parse(Packet(observed)).RootElement, outer, steps, native')
p.write_text(source,encoding='utf-8')
print('Separate native field audit follows the existing challenge input; primary expression/call streams unchanged.')
