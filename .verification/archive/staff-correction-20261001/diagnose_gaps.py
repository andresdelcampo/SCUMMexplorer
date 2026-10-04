from pathlib import Path
import json
audit=json.loads(Path('Exe/StaffCorrectionSession-20261001/audits-1/literal/audit.json').read_text(encoding='utf-8'))
for r in audit['results']:
    if 'Incomplete' in r['status'] or 'incomplete' in r['status']:
        print(r['owner'],r['frames'],r['rooms'],r['sceneTrace'])
        for line in r['uncertainties'][-8:]: print(line)
data=json.loads(Path('Exe/StaffCorrectionSession-20261001/routes-1/late-096-after095-staff-fresh.json').read_text(encoding='utf-8'))
print('Late certainty')
last=data['snapshots'][-1]
print('snapshot fields', list(last['snapshot']))
actor=next(a for a in last['actors'] if a['Number']==4)
print('actor4', {k:actor[k] for k in ('Room','X','Y','IgnoreBoxes','Estimate','Moving')})
for t in data['uncertainties']:
    if any(s in t for s in ('actor 4','Script 96','639')): print(t)
print('source095 flag writes')
for l in Path('Exe/VerificationSession-20261001/native-code-1/code.tsv').read_text(encoding='utf-8').splitlines():
    if l.startswith('script 095') and ('bit[' in l or '\tloadRoom\t' in l): print(l)
