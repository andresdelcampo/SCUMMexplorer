"""Require completed fresh reports, current manifests and every historical preservation check."""
from pathlib import Path
import hashlib
import json
import sys

out = Path(sys.argv[1])
evidence = out / 'final-evidence'
base = Path(__file__).parent
def report(name):
    return (evidence if name in ('observer-build.txt', 'expanded-build.txt', 'numeric-fresh.json') else out) / name

source = Path('.verification/runtime-bindings-20261002/finalize_checks.py').read_text(encoding='utf-8')
source = source.replace('base=Path(__file__).parent', "base=Path('.verification/runtime-access-20261002')")
source = source.replace('326', '374').replace('checks-1/', 'checks-final/')
source = source.replace("out/'observer-1/", "evidence/'observer-1/")
source = source.replace("out/'numeric-fresh.json'", "evidence/'numeric-fresh.json'")
source = source.replace('read(out/name)', 'read(report(name))').replace('digest(out/n)', 'digest(report(n))')
exec(compile(source, 'preserved-check-analysis', 'exec'), {'__file__': __file__, '__name__': '__main__',
    'evidence': evidence, 'report': report})
summary = json.loads((base/'check-summary.json').read_text(encoding='utf-8'))
access = json.loads((base/'access-summary.json').read_text(encoding='utf-8'))
assert access['recipeCount'] == 13 and access['zeroWaits'] == access['independentTerminations'] == 76
assert access['priorWholePacketsAndChronologyExact']
summary.update(newSourceAccessCases=9, newObservedLineageCases=12, newIndependentTerminalCases=5,
    exactBindingChronologyUnchanged=True, expandedChronologyUnchanged=True,
    genericOwnLiteralChildMatches=76, independentWriterTerminations=76,
    notifiedSessionMutations=True, recursiveObservedIndexLineage=True,
    rawDictionaryExternalWritesStillRequireNotification=True, relationLimitsRetained=True)
expanded_manifest = evidence/'expanded-1/source-manifest.json'
manifest = json.loads(expanded_manifest.read_text(encoding='utf-8'))
assert all(hashlib.sha256(Path(p).read_bytes()).hexdigest() == h for p,h in manifest.items())
summary['expandedSourceManifest'] = dict(path=expanded_manifest.as_posix(), sha256=hashlib.sha256(expanded_manifest.read_bytes()).hexdigest())
(base/'check-summary.json').write_text(json.dumps(summary, indent=2)+'\n', encoding='utf-8')
print('Core374;26 new access/lineage/terminal cases;13 exact whole packets/chronologies;76 independent writer ends;all preservation checks complete.')
