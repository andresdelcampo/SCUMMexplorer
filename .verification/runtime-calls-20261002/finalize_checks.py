"""Require completed current-source reports before publishing the final handoff facts."""
from pathlib import Path
from collections import Counter
import hashlib
import json
import re
import sys

base = Path(__file__).parent
out = Path(sys.argv[1])
def read(p):
    b = p.read_bytes()
    return b.decode('utf-16' if b.startswith((b'\xff\xfe',b'\xfe\xff')) else 'utf-8-sig')
def load(p): return json.loads(read(p))
def digest(p): return dict(path=p.as_posix(), sha256=hashlib.sha256(p.read_bytes()).hexdigest())
prior = Path('Exe/DraftScopeSession-20261001/final-audits-1')
old, new = load(prior/'literal/audit.json'), load(out/'audits-1/literal/audit.json')
keys = ('owner','status','frames','first','last','everOpen','begun','ended','rooms','sceneTrace')
assert len(new['results']) == 363 and old['sourceFiles'] == new['sourceFiles']
assert [{k:r.get(k) for k in keys} for r in old['results']] == [{k:r.get(k) for k in keys} for r in new['results']]
for name in ('established.json','sequences.json'): assert load(prior/name) == load(out/'audits-1'/name)
assert 'blocked: 0 instruction sites' in read(out/'audits-1/blockers.txt')
assert re.search(r'Passed:\s+501', read(out/'checks-final/core-tests.txt'))
assert re.search(r'Passed:\s+175', read(out/'focused-final/tests.txt'))
for name in ('checks-final/release-build.txt','final-release-build.txt','final-evidence/observer-build.txt',
             'final-evidence/expanded-build.txt','passive-final/passive-expressions-build.txt'):
    assert '0 Warning(s)' in read(out/name) and '0 Error(s)' in read(out/name), name
assert 'Compared 76 room renders; changed: 0' in read(out/'png-comparison-2.txt')
rooms = read(out/'room-audit-2/room-audit.tsv').splitlines()[1:]
assert len(rooms) == 76 and all(int(r.split('\t')[3]) == 0 for r in rooms)
assert 'SCUMMexplorer processes: 0' in read(out/'app-state.txt')
for name in ('final-evidence/observer-1/source-manifest.json','final-evidence/expanded-1/source-manifest.json',
             'passive-final/passive-expression-source-manifest.json'):
    assert all(hashlib.sha256(Path(p).read_bytes()).hexdigest() == h for p,h in load(out/name).items()), name
call = load(base/'call-summary.json')
assert call['recipeCount'] == 13 and call['latestPacketsAndLegacyStreamsExact'] and call['independentOwnInvocationTerminals'] == 76
assert load(out/'final-evidence/numeric-fresh.json') == load(Path('Exe/CorrelatedCoreSession-20261002/numeric-clean.json'))
passive = load(out/'passive-final/passive-expressions.json')
previous_passive = load(Path('Exe/RuntimeContinuityFinalSession-20261002/passive-final/passive-expressions.json'))
assert passive == previous_passive and passive['transparent'] and passive['frames'] == 36 and len(passive['steps']) == 17
instructions = [o for o in passive['outer'] if o['Site'] is not None]
assert [o['Site']['Offset'] for o in instructions] == [0x22,0x49,0x56]
files = ('checks-final/core-tests.txt','checks-final/release-build.txt','focused-final/tests.txt','final-release-build.txt','app-state.txt',
    'final-evidence/observer-build.txt','final-evidence/expanded-build.txt','audits-1/literal/audit.json','audits-1/established.json',
    'audits-1/sequences.json','audits-1/blockers.txt','room-audit-2/room-audit.tsv','room-audit-2/room-audit.txt','png-comparison-2.txt',
    'final-evidence/numeric-fresh.json','passive-final/passive-expressions.json')
summary = dict(core=501, focused=175, newActualCallCases=13, newRemainingAccessCases=12, newNativeMutationCases=16,
    rootCount=363, comparedFields=keys, rootStatuses=dict(Counter(r['status'] for r in new['results'])),
    sourceFingerprintsUnchanged=True, literalResultsUnchanged=True, membershipsUnchanged=True, sequencesUnchanged=True,
    fullRecipePacketsUnchanged=13, latestLegacyChronologiesAndCompletionRecordsExact=True, independentOwnInvocationTerminals=76,
    actualRecipeWriterResumedTerminalChains=0, numericSourceReportUnchanged=True, numericWorlds=162, flagCases=52,
    comparisons=51, overlaps=14, fabricatedMatches=4, practiceArgumentCases=12,
    blockedRandomSites=0, roomRenders=76, remainingGaps=0, changedPixels=0, releaseWarnings=0, releaseErrors=0,
    appLaunched=False, estimatesCleared=False, native090Completed=False,
    sourceChallengeExpressions=3, sourceChallengeSteps=17, sourceChallengeEveryFrameTransparent=True,
    observedInitializationAndDispatchBoundaries=True, argumentEqualitySuppliesNoLocalOrigins=True,
    generalCalleeReturnAndEffectsRemainUnresolved=True, rawNativeSameValueMutationsStillRequireNotification=True,
    replayCoverage=dict(validatedLiteralSites=168,totalLiteralSites=235,coveredPercent=round(168/235*100,2),
        scope='validated literal cutscene sites only; unchanged, not an overall project-completion percentage'),
    callBoundaries=sum(r['callBoundaries'] for r in call['routes']), dispatches=sum(r['dispatches'] for r in call['routes']),
    reports=[digest(out/name) for name in files], sourceManifest=digest(out/'final-evidence/observer-1/source-manifest.json'))
(base/'check-summary.json').write_text(json.dumps(summary, indent=2)+'\n', encoding='utf-8')
print('Core501/focused175;13 exact packets/legacy streams;76 original terminals;363 exact roots;Blockers0;76 unchanged zero-gap renders;numeric unchanged;main Release clean;App0.')
