"""Require completed current reports and preserve all inherited check assertions."""
from pathlib import Path
import hashlib
import json
import re
import sys

base = Path(__file__).parent
source = Path('.verification/archive/runtime-dependencies-20261002/finalize_checks.py').read_text(encoding='utf-8')
source = source.replace('.verification/archive/runtime-dependencies-20261002', '.verification/archive/runtime-continuity-20261002').replace('401', '460')
exec(compile(source, 'preserved-check-analysis', 'exec'), {'__file__': __file__, '__name__': '__main__'})
summary = json.loads((base/'check-summary.json').read_text(encoding='utf-8'))
continuity = json.loads((base/'continuity-summary.json').read_text(encoding='utf-8'))
assert continuity['latestWholePacketsChronologiesAndCompletionExact'] and continuity['expandedChronologyExact']
assert summary['core'] == 460
out = Path(sys.argv[1])
passive = out/'passive-final'
report = json.loads((passive/'passive-expressions.json').read_text(encoding='utf-8'))
assert report['transparent'] and report['nativeBoot'] and report['sourceInputOnly'] and report['frames'] == 36
instructions = [o for o in report['outer'] if o['Site'] is not None]
assert [o['Site']['Offset'] for o in instructions] == [0x22, 0x49, 0x56]
assert all(o['Site']['Context'] == 'Room 69 local script 201' and o['Site']['Container'] == 69 and o['Site']['ResourceOffset'] == 4600 for o in instructions)
steps = report['steps']
assert len(steps) == 17
for instruction in instructions:
    current = [s for s in steps if s['InstructionSequence'] == instruction['Sequence']]
    assert [s['Step'] for s in current] == list(range(1, len(current)+1))
    assert all(s['Site'] == instruction['Site'] and s['Slot'] == instruction['Slot'] and s['Invocation'] == instruction['Invocation']
        and s['ParentSlot'] == instruction['ParentSlot'] and s['PathEstimated'] == instruction['PathEstimated'] for s in current)
    assert current[0]['Stage'] == 'destination-selected' and current[-1]['Stage'] == 'final-write'
    if instruction['Site']['Offset'] in (0x49, 0x56):
        assert [s['Stage'] for s in current] == ['destination-selected', 'nested-before', 'nested-write', 'scratch-read', 'read', 'final-write']
        assert current[2]['Bindings'][0]['Value'] == current[3]['Bindings'][0]['Value']
        assert current[2]['Bindings'][0]['ValueEstimated'] and current[3]['Bindings'][0]['ValueEstimated']
        assert current[-1]['Bindings'][0]['ValueEstimated']
        assert current[-1]['Bindings'][0]['Value'] == current[3]['Bindings'][0]['Value'] + 1
assert report['packet']['Snapshot']['RoomId'] == 69
manifest = json.loads((passive/'passive-expression-source-manifest.json').read_text(encoding='utf-8'))
assert all(hashlib.sha256(Path(p).read_bytes()).hexdigest() == h for p,h in manifest.items())
def digest(p): return dict(path=p.as_posix(), sha256=hashlib.sha256(p.read_bytes()).hexdigest())
def log(p):
    raw = p.read_bytes()
    return raw.decode('utf-16' if raw.startswith((b'\xff\xfe', b'\xfe\xff')) else 'utf-8-sig')
assert re.search(r'Passed:\s+134', log(out/'focused-final/tests.txt'))
assert '0 Warning(s)' in log(passive/'passive-expressions-build.txt') and '0 Error(s)' in log(passive/'passive-expressions-build.txt')
expression_summary = dict(scope='Existing source-progression native challenge actions through 36 frames, stopping before boot-answer; no state/prerequisite writes or native opening completion claim.',
    sourceRecipe=report['sourceRecipe'], transparentAtEveryFrameAndInput=True, frames=36, loadedExpressions=3, actualSteps=17,
    sourceOffsets=[0x22,0x49,0x56], room=69, inheritedEstimatesRetained=True, numericWriteOriginsNotInferred=True,
    reports=[digest(passive/name) for name in ('passive-expressions.json','passive-expression-source-manifest.json','passive-expressions-build.txt','passive-expressions-run.txt')])
(base/'expression-summary.json').write_text(json.dumps(expression_summary, indent=2)+'\n', encoding='utf-8')
summary.update(newImplicitAccessCases=22, newExpressionChronologyCases=7, newNativeLifetimeCases=15, newResumedTerminalCases=15,
    actualExpressionChronologyOptional=True, nativeDependencyTrackerIndependent=True,
    rawNativeSameValueWritesStillRequireNotification=True, boundedObservedResumeChainsAvailable=True,
    resumedTerminalContinuityUnresolved=True, fullyObservedResumeChainCanEstablishSelectedTerminal=True,
    missingResumeContinuityStillUnresolved=True,
    calleeArgumentBindingAndReturnUnresolved=True,
    latestCompletionChronologyExact=True, actualRecipeResumedTerminalChains=0,
    additionalAccessRecords=sum(sum(r['additionalAccessRecords'].values()) for r in continuity['routes']),
    independentNativeReads=sum(r['independentNativeReads'] for r in continuity['routes']),
    actualSelectedExpressionSteps=sum(r['expressionSteps'] for r in continuity['routes']),
    sourceChallengeExpressions=3, sourceChallengeExpressionSteps=17, sourceChallengeEveryFrameTransparent=True)
summary['reports'].extend(expression_summary['reports'])
summary['focused'] = 134
summary['reports'].append(digest(out/'focused-final/tests.txt'))
(base/'check-summary.json').write_text(json.dumps(summary, indent=2)+'\n', encoding='utf-8')
print('Core460;59 new cases;13 exact packets/chronologies;76 separate own-invocation terminals;all preservation reports complete.')
