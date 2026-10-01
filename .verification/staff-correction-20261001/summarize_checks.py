from pathlib import Path
import json, hashlib
base=Path('Exe/StaffCorrectionSession-20261001')
reports=[base/'ui-3/staff-check.txt',base/'tests-final-2.txt',base/'audits-1/comparison.txt']
reports+=list((base/'regressions-1').rglob('*check.txt'))
reports+=list((base/'regressions-1').rglob('*clicks.txt'))
reports+=list((base/'regressions-1').rglob('blockers.txt'))
items=[]
for path in sorted(set(reports)):
    raw=path.read_bytes()
    text=raw.decode('utf-16') if raw.startswith(b'\xff\xfe') else raw.decode('utf-8-sig')
    items.append(dict(path=str(path).replace('\\','/'),sha256=hashlib.sha256(raw).hexdigest(),passes=sum(l.startswith('PASS') for l in text.splitlines()),failures=sum(l.startswith('FAIL') for l in text.splitlines()),summary=[l for l in text.splitlines() if any(x in l for x in ('Passed!','Failures:','changed:','roots:','unchanged.','blocked'))]))
    print(items[-1])
Path('.verification/staff-correction-20261001/check-summary.json').write_text(json.dumps(items,indent=2)+'\n',encoding='utf-8')
