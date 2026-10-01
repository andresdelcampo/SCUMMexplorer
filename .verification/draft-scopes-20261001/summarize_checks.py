import hashlib,json,pathlib,re
root=pathlib.Path('Exe/DraftScopeSession-20261001')
reports={
 'global':('regressions-1/ReplayCheck/replay-check.txt',21),
 'room':('regressions-1/RoomReplayCheck/room-replays-check.txt',20),
 'doorways':('regressions-1/controls/doorways.txt',16),
 'controls':('regressions-1/controls/controls/room-controls-check.txt',19),
 'input':('regressions-1/input/input-check.txt',18),
 'audio':('regressions-1/audio/audio-check.txt',10),
 'sequences_normal_clock':('sequence-ui-1/sequence-ui-check.txt',12),
 'room24':('ui-5/scope-ui-check.txt',2),
}
summary={}
for name,(file,count) in reports.items():
 p=root/file;t=p.read_text(encoding='utf-8-sig')
 assert (name=='doorways' or 'Failures: 0' in t) and not re.search(r'^FAIL ',t,re.M),file
 actual=len(re.findall(r'^PASS ',t,re.M));assert actual==count,(file,actual,count)
 summary[name]=dict(report=file,passed=actual,sha256=hashlib.sha256(p.read_bytes()).hexdigest())
p=root/'ui-1/scope-ui-check.txt';t=p.read_text(encoding='utf-8-sig')
assert len(re.findall(r'^PASS ',t,re.M))==7 and 'TimeoutException' in t
summary['room41_partial_report']=dict(report='ui-1/scope-ui-check.txt',passed=7,sha256=hashlib.sha256(p.read_bytes()).hexdigest(),
 note='Both Room41 responses and normal-clock/final43 checks passed; subsequent Room24 instant view wait failed. Room24 validated separately after start-room harness correction.')
assert (root/'regressions-1/blockers.txt').read_text(encoding='utf-8').startswith('blocked: 0 ')
assert (root/'final-audits-1/png-comparison.txt').read_text(encoding='utf-16').strip()=='Compared 76 room renders; changed: 0'
summary['roomAudit']=dict(rooms=76,gaps=0,changedPixels=0,refreshedPngs=0)
pathlib.Path('.verification/draft-scopes-20261001/check-summary.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')
print('Shared checks passed; Room41 partial report and corrected Room24 validation retained explicitly.')
