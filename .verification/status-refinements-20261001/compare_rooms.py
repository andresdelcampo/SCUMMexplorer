from pathlib import Path
import hashlib,json
root=Path('Exe/StatusRefinementsSession-20261001')
before=root/'room-before'
after=root/'room-after-1'
changes=[]
for path in sorted(after.glob('room-*.bmp')):
    if path.read_bytes() != (before/path.name).read_bytes():
        changes.append(path.name)
report={'rooms':len(list(after.glob('room-*.bmp'))),'changed':changes,
        'before20':hashlib.sha256((before/'room-20.bmp').read_bytes()).hexdigest(),
        'after20':hashlib.sha256((after/'room-20.bmp').read_bytes()).hexdigest()}
Path('.verification/status-refinements-20261001/room-comparison.json').write_text(json.dumps(report,indent=2)+'\n')
print(json.dumps(report))
assert report['rooms']==76
assert changes==['room-20.bmp']
