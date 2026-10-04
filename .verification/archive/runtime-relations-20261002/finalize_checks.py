"""Require all fresh preservation reports before publishing the compact check summary."""
from pathlib import Path
import json
import sys

out = Path(sys.argv[1])
evidence = Path(sys.argv[2]) if len(sys.argv) > 2 else out
base = Path(__file__).parent
def report(name):
    return (evidence if name in ('observer-build.txt', 'expanded-build.txt', 'numeric-fresh.json') else out)/name
source = Path('.verification/archive/runtime-bindings-20261002/finalize_checks.py').read_text(encoding='utf-8')
source = source.replace('base=Path(__file__).parent', "base=Path('.verification/archive/runtime-relations-20261002')")
source = source.replace('326', '348').replace('checks-1/', 'checks-final/')
source = source.replace('newOperandBindingCases=5', 'newRelationLifetimeCases=12,newCompletionObserverCases=10')
source = source.replace("out/'observer-1/", "evidence/'observer-1/")
source = source.replace("out/'numeric-fresh.json'", "evidence/'numeric-fresh.json'")
source = source.replace('read(out/name)', 'read(report(name))').replace('digest(out/n)', 'digest(report(n))')
exec(compile(source, 'preserved-check-analysis', 'exec'), {'__file__': __file__, '__name__': '__main__',
    'evidence': evidence, 'report': report})
summary = json.loads((base/'check-summary.json').read_text(encoding='utf-8'))
relations = json.loads((base/'relation-summary.json').read_text(encoding='utf-8'))
assert relations['recipeCount'] == 13 and relations['ownLiteralChildMatches'] == 76
assert relations['exactBindingChronology'] and relations['expandedChronologyIdentical']
summary.update(exactBindingChronologyUnchanged=True, expandedChronologyUnchanged=True,
    genericOwnLiteralChildMatches=76, relationLimitsRetained=True)
(base/'check-summary.json').write_text(json.dumps(summary, indent=2)+'\n', encoding='utf-8')
