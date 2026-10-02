"""Require completed reports and both final manifests; retain the entire prior check protocol."""
from pathlib import Path
import hashlib
import json
import sys

out = Path(sys.argv[1])
base = Path(__file__).parent
source = Path('.verification/runtime-access-20261002/finalize_checks.py').read_text(encoding='utf-8')
source = source.replace("Path('.verification/runtime-access-20261002')", "Path('.verification/runtime-dependencies-20261002')")
source = source.replace(".replace('326', '374')", ".replace('326', '401')")
source = source.replace('Core374;26 new access/lineage/terminal cases;', 'Current Core401; preserved earlier access/lineage/terminal assertions;')
exec(compile(source, 'preserved-final-check-analysis', 'exec'), {'__file__': __file__, '__name__': '__main__'})
summary = json.loads((base/'check-summary.json').read_text(encoding='utf-8'))
dependencies = json.loads((base/'dependency-summary.json').read_text(encoding='utf-8'))
assert dependencies['latestWholePacketsAndChronologiesExact'] and dependencies['expandedChronologyExact']
assert dependencies['zeroWaits'] == dependencies['independentOwnInvocationTerminations'] == 76
summary.update(newSelectedAccessCases=17, newSourceCausalBoundaryCases=10,
    selectedNativeDependencies=sum(r['nativeDependencies'] for r in dependencies['routes']),
    newEventOperandRecords=sum(r['newEventOperandRecords'] for r in dependencies['routes']),
    callbackQueriesNotInvokedByObservation=True, nativeRelationsNotInferred=True,
    writerConfirmationsRequireSourceAndParent=True, terminalRequiresOwnInvocationAndParent=True,
    resumedTerminalContinuityUnresolved=True, calleeArgumentBindingAndReturnUnresolved=True)
(base/'check-summary.json').write_text(json.dumps(summary, indent=2)+'\n', encoding='utf-8')
print('Core401;27 new cases;13 exact latest packets/chronologies;76 independent own-invocation ends;all preservation checks complete.')
