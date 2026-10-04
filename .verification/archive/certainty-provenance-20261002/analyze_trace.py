"""Compare unchanged recipes and retain observed instruction provenance.

The observed build is diagnostic. No value/estimate is written by this analysis.
"""
from pathlib import Path
from collections import Counter
import hashlib
import json
import sys

base = Path(__file__).parent
output = Path(sys.argv[1])
prior = Path('Exe/CertaintyPrecisionSession-20261002')
def load(p):
    return json.loads(p.read_text(encoding='utf-8-sig'))
def digest(p):
    return dict(path=p.as_posix(), sha256=hashlib.sha256(p.read_bytes()).hexdigest())
def brief(row):
    r = row['record']
    return {k:r[k] for k in ('Run','Parent','Stage','Script','Context','Offset','Operation','Operands','PathEstimated','Resume','Stopped','Unresolved')} | dict(
        frame=row['frame'], variables={k:dict(value=r['Values'].get(k), estimated=k in r['Estimated'])
            for k in ('bit[0xD6C]','bit[0xD67]','bit[0xD70]','var[0x064]','var[0x07A]','local[0x000]',
                'var[0x062]','var[0x056]','var[0x057]','var[0x058]','var[0x059]')})

results = []
for folder in ('room41-1','egg-first-1','dye-last-1','preserved-1'):
    for p in sorted((output / folder).glob('*.json')):
        current, previous = load(p), load(prior / folder / p.name)
        assert current == previous, f'Observer changed recipe packet: {p}'
        trace = p.with_name(p.stem + '-instructions.jsonl')
        records = [json.loads(line) for line in trace.read_text(encoding='utf-8').splitlines()]
        runs = {}
        for row in records:
            runs.setdefault(row['record']['Run'], []).append(row)
        transitions, writers, completions, practice_starts = [], [], [], []
        for run in runs.values():
            for i, row in enumerate(run):
                r = row['record']
                if r['Stage'] == 'instruction' and r['Operation'] == 'move' and any(
                    r['Operands'].startswith(bit+' = ') for bit in ('bit[0xD6C]','bit[0xD67]')):
                    assert i+1 < len(run)
                    pair = dict(before=brief(row), after=brief(run[i+1]))
                    writers.append(pair)
                    if r['Script'] == 31 and r['Context'] is None and r['Offset'] in (0x243,0x31D):
                        completions.append(pair)
                    if r['Script'] == 30 and r['Offset'] == 0:
                        practice_starts.append(pair)
                if i and not run[i-1]['record']['PathEstimated'] and r['PathEstimated']:
                    transitions.append(dict(before=brief(run[i-1]), after=brief(row)))
        name=current['route']['id']
        callers=[e for e in current['events'] if e['Operation']=='startScript' and e['TargetId']==30 and e['Executed']]
        assert len(callers)==len(practice_starts), (name,len(callers),len(practice_starts))
        for practice, caller in zip(practice_starts,callers):
            practice['caller']=caller
        native = name.startswith('room41-')
        interesting = [t for t in transitions if t['before']['Script'] in (20,31)]
        completion_counts=Counter((w['before']['Offset'], w['before']['PathEstimated'],
            w['after']['variables']['bit[0xD67]']['value'], w['after']['variables']['bit[0xD67]']['estimated'])
            for w in completions)
        practice_zero=[w for w in writers if w['before']['Script']==31 and w['before']['Offset']==0x227]
        detail={}
        if name=='room41-raw4-click':
            taint=next(t for t in interesting if t['before']['Script']==31 and t['before']['Offset']==0x26)
            practice=next(w for w in practice_starts if w['before']['frame']==taint['before']['frame'])
            zero=next(w for w in practice_zero if w['after']['variables']['bit[0xD6C]']['estimated'])
            wait=next(t for t in interesting if t['before']['Script']==20)
            complete=max((w for w in completions if w['before']['frame']<wait['before']['frame']),
                key=lambda w:w['before']['frame'])
            assert wait['before']['Offset']==0x28 and wait['after']['Offset']==0x2D
            assert complete['before']['Offset']==0x31D
            assert complete['after']['variables']['bit[0xD67]']==dict(value=0,estimated=True)
            assert zero['after']['variables']['bit[0xD6C]']==dict(value=0,estimated=True)
            assert practice['caller']['EstimatedArgumentIndexes']==[0]
            assert not wait['before']['variables']['var[0x062]']['estimated']
            assert not wait['before']['variables']['var[0x056]']['estimated']
            detail=dict(firstEstimatedPractice=practice, firstPracticeNoteBranch=taint,
                firstEstimatedPracticeZero=zero,
                firstKnownNotePracticeBranch=next(t for t in interesting if t['before']['Script']==31 and t['before']['Offset']==0xA),
                completionBeforeWait=complete, firstWaitBranch=wait,
                otherTransitions=interesting[3:])
        result = dict(id=name, packet=digest(p), instructions=digest(trace), observerPacketIdentical=True,
            elapsed=current['elapsed'], room=current['snapshots'][-1]['snapshot']['RoomId'],
            instructionTransitions=len(transitions), practiceStarts=len(practice_starts),
            practiceZeroWrites=len(practice_zero), completionWrites=len(completions),
            completionGroups=[dict(offset=at,pathEstimated=path,value=value,estimated=estimate,count=n)
                for (at,path,value,estimate),n in sorted(completion_counts.items())],
            earlyStops=sum(row['record']['Script']==31 and row['record']['Context'] is None
                and row['record']['Stage']=='instruction' and row['record']['Offset']==0x19 for row in records),
            observations=detail,
            final090=[e for e in current['events'] if e['SourceScriptId']==90 and e['Operation']=='loadRoom'])
        results.append(result)
assert len(results)==13
manifest=load(output / 'observer-2/source-manifest.json')
assert all(hashlib.sha256(Path(p).read_bytes()).hexdigest()==h for p,h in manifest.items())
result=dict(scope='Read-only observer copy of the same Core sources; copied instruction values/estimates; no runtime reference or state writes. Native input recipes unchanged.',
    recipeCount=len(results), productionSourcesUnchanged=True, manifest=digest(output / 'observer-2/source-manifest.json'),
    routes=results,
    sourceEarlyStopProtocol=dict(condition='D6C0 and note > raw07A (old-v3 reversed operands); not observed on these13 recipes.',
        edges=['031 +0016 starts011;031 +0019 stops itself with D671.',
            '011 +0013 starts021;021 +0000 stops020.',
            'The source cancellation edge is different from031 +0243/+031D completion writes.'],
        limit='Decoded edges alone do not prove reached scheduling, callee completion, lack of recursive/dynamic restarts or cancellation safety.'),
    native090Completed=False)
(base / 'trace-summary.json').write_text(json.dumps(result,indent=2)+'\n',encoding='utf-8')
for r in results:
    print(r['id'], 'identical', r['room'], 'completion writes',r['completionWrites'], 'early stops',r['earlyStops'])
    if r['id']=='room41-raw4-click':
        for t in (r['observations']['firstPracticeNoteBranch'],r['observations']['firstKnownNotePracticeBranch'],r['observations']['firstWaitBranch']):
            print('transition', t['before']['Script'],hex(t['before']['Offset']), t['before']['frame'],t['before']['variables'])
        print('practice caller',r['observations']['firstEstimatedPractice']['caller'])
