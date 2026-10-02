"""Read-only comparison of the bounded estimated-room-load correction captures."""
import hashlib
import json
import subprocess
import sys
from pathlib import Path

root = Path(sys.argv[1] if len(sys.argv) > 1 else 'Exe/EstimatedRoomLoads-20261002')
baseline = Path('Exe/Room43CageSession-20261002')
def read(path):
    return json.loads(path.read_text(encoding='utf-8-sig'))
def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()

before_path = Path('Exe/Room41RouteSession-20261002/raw4-1/room41-raw4-click.json')
after_path = root / 'native-1/room41-raw4-click.json'
before, after = read(before_path), read(after_path)
assert before['route'] == after['route']
for report in read(Path('.verification/room41-route-20261002/route-summary.json'))['reports']:
    assert digest(Path(report['path'])) == report['sha256']
assert after['events'][:len(before['events'])] == before['events']
end = [e for e in after['events'] if e['SourceScriptId'] == 90 and e['Offset'] == 0x730 and e['Operation'] == 'endCutscene']
assert len(end) == 1 and end[0]['Executed'] and end[0]['Certainty'] == 1
def outcome(packet):
    return dict(room=packet['snapshots'][-1]['snapshot']['RoomId'], frames=packet['elapsed'],
                begun=sum(s.startswith('cutscene at ') for s in packet['boundaries']),
                ended=sum(s.startswith('endCutscene at ') for s in packet['boundaries']))
assert outcome(before) == dict(room=41, frames=7659, begun=15, ended=14)
assert outcome(after) == dict(room=43, frames=5886, begun=15, ended=15)
snapshot = after['snapshots'][-1]['snapshot']
assert not snapshot['IsFaithful']
assert snapshot['ObjectStates']['645'] == 0 and 645 in snapshot['EstimatedStates']
assert 645 not in snapshot['StatesSetByCode']

old, new = [read(p / 'audits/literal/audit.json') for p in [baseline, root]]
assert old['sourceFiles'] == new['sourceFiles'] and old['inputSha256'] == new['inputSha256']
assert len(old['results']) == len(new['results']) == 363
changed = [(a,b) for a,b in zip(old['results'],new['results']) if a != b]
assert len(changed) == 1
a,b = changed[0]
assert a['owner'] == b['owner'] == 'Room 18 object 370 Verb 0x36'
assert a['ended'] == 0 and b['ended'] == 1 and b['status'] == 'Validated state preview'
assert 'endCutscene at Script 65 +0x0302 runs script 3' in b['sceneTrace']
for name in ['established.json', 'sequences.json']:
    assert read(baseline/'audits'/name) == read(root/'audits'/name)
assert (root/'audits/blockers.txt').read_text(encoding='utf-8-sig').startswith('blocked: 0 instruction sites')

response18 = read(root/'response18-1/room41-sharpening-reverse-full.json')
precast = next(s for s in response18['snapshots'] if s['stage'] == 'draft-9-before')
draft = next(d for d in precast['status']['Drafts'] if d['Slot'] == 9)
assert precast['status']['AvailableNoteCount'] == 4 and not draft['Learned'] and draft['Notes'] == [6,6,6,5]
assert not any(e['SourceScriptId'] == 90 for e in response18['events'][precast['eventCount']:])

def identical_files(old_folder, new_folder, pattern):
    old_files = {p.relative_to(old_folder): p for p in old_folder.glob(pattern)}
    new_files = {p.relative_to(new_folder): p for p in new_folder.glob(pattern)}
    assert old_files.keys() == new_files.keys()
    differences = [str(k) for k,p in old_files.items() if p.read_bytes() != new_files[k].read_bytes()]
    return len(old_files), differences
renders, render_changes = identical_files(baseline/'room-audit',root/'room-audit','*.bmp')
assert renders == 76 and not render_changes
controls, control_changes = identical_files(baseline/'capture-final',root/'cage-controls','*.*')
assert controls and not control_changes
protected = read(Path('Exe/RuntimeScriptCorrespondenceSession-20261002/protected-before.json'))
assert len(protected) == 57
assert all(digest(Path(p)) == value.lower() for p,value in protected.items())
staged = subprocess.check_output(['git','diff','--cached','--name-only'], text=True).splitlines()
assert not set(staged).intersection(protected)
trx = (root/'core-final.trx').read_text(encoding='utf-8-sig')
assert 'total="662"' in trx and 'passed="662"' in trx and 'failed="0"' in trx
build_bytes = (root/'build.txt').read_bytes()
build = build_bytes.decode('utf-16' if build_bytes.startswith(b'\xff\xfe') else 'utf-8-sig')
assert 'Build succeeded.' in build and '0 Error(s)' in build and '0 Warning(s)' in build
summary = dict(
    native=dict(before=outcome(before),after=outcome(after),recipeIdentical=True,
                oldEventsExactPrefix=len(before['events']),end=end[0],roomsAfterOldStop=after['roomChanges'][-4:],
                beforeSha256=digest(before_path),afterSha256=digest(after_path)),
    literal=dict(total=363,unchanged=362,changedOwner=b['owner'],beforeStatus=a['status'],afterStatus=b['status'],
                 beforeFrames=a['frames'],afterFrames=b['frames'],end=b['sceneTrace'][-1]),
    membershipsExact=4,sequencesExact=2,blockedSites=0,rendersExact=renders,cageControlFilesExact=controls,
    corePassed=662,releaseBuild='0 warnings, 0 errors',protectedExactUnstaged=57,
    response18SetupMismatch=dict(outcome=outcome(response18),availableNotes=4,learned=False,sequence=draft['Notes'],captureDispatched=False),
    limitation='No WPF/manual response18 check; no catalog or coverage promotion; historical observer captures not rerun.')
destination = Path('.verification/estimated-room-loads-20261002/summary.json')
destination.write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')
print(json.dumps(summary,indent=2))
