import collections, json, pathlib
catalog=json.load(open('src/SCUMMexplorer.App/loom-replays.json',encoding='utf-8'))
audit=json.load(open('Exe/VerificationSession-20261001/replay-audit/audit.json',encoding='utf-8'))
established=json.load(open('Exe/VerificationSession-20261001/established-membership-1.json',encoding='utf-8'))
assert catalog['sourceFiles']==audit['sourceFiles']==established['sourceFiles']
members={m['id']:m for m in catalog['membership']}
assert len(members)==291
for r in catalog['replays']:
    assert set(members[r['id']]['rooms'])==set(r['rooms'])|{r['room']}
    assert members[r['id']]['startRoom']==r['room']
for r in established['results']:
    assert members[r['id']]['rooms']==r['rooms'] and members[r['id']]['frames']==r['frames']
pathlib.Path('.verification/room-replays-20261001/established-validation.json').write_text(json.dumps(established['results'],indent=2),encoding='utf-8')
counts=collections.Counter(room for m in members.values() for room in set(m['rooms'])|{m['startRoom']})
print('PASS 291 unique memberships, same exact fingerprints; every link equals validated start/reached rooms')
print('Single-match rooms:',[room for room,count in sorted(counts.items()) if count==1])
print('Room counts:',dict(sorted(counts.items())))
