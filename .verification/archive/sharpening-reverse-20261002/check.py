"""Evaluate the single bounded reproduction; never alter its capture or estimates."""
import hashlib
import json
import subprocess
from pathlib import Path

def read(p):
    return json.loads(Path(p).read_text(encoding='utf-8-sig'))
def digest(p):
    return hashlib.sha256(Path(p).read_bytes()).hexdigest()

base = Path('.verification/archive/sharpening-reverse-20261002')
packet = Path('Exe/SharpeningReverse-20261002/capture-1/sharpening-reverse-user-status.json')
p = read(packet)
assert p['route'] == read(base/'response18.json')[0]
before, after, precast = [next(s for s in p['snapshots'] if s['stage']==stage)
    for stage in ['user-status-before','user-status-after','draft-9-before']]
spell = lambda s: next(d for d in s['status']['Drafts'] if d['Slot']==9)
original = spell(before)
for s in (after,precast):
    draft = spell(s)
    assert s['status']['StaffHeld'] and s['status']['AvailableNoteCount'] == 6
    assert draft['Learned'] and draft['Reversible'] and draft['ReverseVerb'] == 0x4B
    assert draft['Notes'] == original['Notes'] == [6,6,6,5]
    assert draft['Packed'] == original['Packed'] | 0x2000
    assert 'Explicit user edit' in draft['Origin'] and 'estimated' in draft['Origin']
events = p['events'][precast['eventCount']:]
def one(label,offset,operation):
    found = [e for e in events if e['SourceLabel']==label and e['Offset']==offset and e['Operation']==operation]
    assert len(found)==1, (label,offset,operation,len(found))
    assert found[0]['Executed'] and found[0]['Certainty']==1
    return found[0]
dispatch = one('Room 41 Object 633 Verb 0x4B',0,'startScript')
assert dispatch['TargetId']==204 and dispatch['Operands']=='0xCC, [0x0000]'
end204 = one('Room 41 local script 204',0x31E,'endCutscene')
chain = one('Room 41 local script 204',0x31F,'chainScript')
assert chain['TargetId']==90
loads = [one('Script 90',offset,'loadRoom') for offset in [5,0xCF,0x1F5,0x379]]
assert [e['TargetId'] for e in loads]==[42,43,60,43]
end090 = one('Script 90',0x730,'endCutscene')
assert events.index(dispatch)<events.index(end204)<events.index(chain)<events.index(loads[0])<events.index(end090)
assert sum(t.startswith('cutscene at ') for t in p['boundaries']) == sum(t.startswith('endCutscene at ') for t in p['boundaries']) == 15
final = p['snapshots'][-1]
assert final['snapshot']['RoomId']==43 and not final['InCutscene'] and not final['EgoMoving'] and not final['CameraMoving']
assert not final['snapshot']['IsFaithful'] and p['elapsed']==6342
assert final['snapshot']['ObjectStates']['645']==0 and 645 in final['snapshot']['EstimatedStates']
assert 645 not in final['snapshot']['StatesSetByCode']
assert [c['room'] for c in p['roomChanges'][-4:]]==[42,43,60,43]
assert not any('unsupported' in u.lower() or 'unmodelled' in u.lower() for u in p['uncertainties'])
# Only the named confirmed edit step differs from the old source-input recipe.
old = read('.verification/archive/estimated-room-loads-20261002/response18.json')[0]
assert [a for a in p['route']['actions'] if a['kind']!='user-status'] == old['actions']
assert sum(a['kind']=='user-status' for a in p['route']['actions'])==1
protected = read('Exe/RuntimeScriptCorrespondenceSession-20261002/protected-before.json')
assert len(protected)==57 and all(digest(k)==v.lower() for k,v in protected.items())
assert not set(subprocess.check_output(['git','diff','--cached','--name-only'],text=True).splitlines()).intersection(protected)
for r in read('.verification/archive/room41-route-20261002/route-summary.json')['reports']:
    assert digest(r['path'])==r['sha256']
for path in ['.verification/archive/room41-certainty-20261002/room41.json',
             '.verification/archive/room41-certainty-20261002/ProgressionProbe/Program.cs',
             '.verification/archive/estimated-room-loads-20261002/response18.json']:
    historic = subprocess.check_output(['git','show','0318792:'+path]).replace(b'\r\n',b'\n')
    assert Path(path).read_bytes().replace(b'\r\n',b'\n') == historic
catalog = read('src/SCUMMexplorer.App/loom-replays.json')
assert len(catalog['replays'])==331 and len(catalog['coverage'])==49
summary = dict(
    userEvidence='Confirmed Game Status staff, enough notes and learned Sharpening; note-key input already established.',
    setup=dict(staffHeld=True,availableNotes=6,noteCountChoice='minimum sufficient for unchanged seeded notes; exact user count unspecified',
               learned=True,sourceNotes=original['Notes'],reverseKeys='tyyy',unchangedReverseFlag=True,
               precedingHistory='existing source-input setup retained; not a captured user save'),
    outcome=dict(room=43,frames=p['elapsed'],begun=15,ended=15,postCastSettlingFrame=1742,rooms=p['roomChanges'][-4:],faithful=False),
    executedEstimatedEvidence=[dispatch,end204,chain,*loads,end090],
    productionChanges=0,reproductionRuns=1,observerExpansion=False,coverage='169/235 (71.91%), unchanged by this longer input recipe',
    protectedExactUnstaged=57,raw4AndHistoricalEvidenceUnchanged=True,
    evidence={str(packet):digest(packet),str(base/'response18.json'):digest(base/'response18.json')},
    limitation='Core keyboard-input reproduction with confirmed kinds of user edits; no App interaction, exact user-history capture or physical-input claim.')
(base/'summary.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')
print('PASS one keyboard reproduction:633/4B ->204(0) ->090,42/43/60/43,15/15,6342 frames; estimates retained;protected57/raw4/history preserved')
