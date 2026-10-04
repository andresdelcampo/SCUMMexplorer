from pathlib import Path
p=Path('.verification/archive/room15-input-20261003/check.py').read_text()
p=p.replace("base = Path('.verification/archive/room15-input-20261003')","base = Path('.verification/archive/room17-input-20261003')").replace("packet_path = Path('Exe/Room15Input-20261003/cycle-1/input.json')","packet_path = Path('Exe/Room17Input-20261003/cycle-2/input.json')")
p=p.replace('Room 15','Room 17').replace("[('001D','0062'),('006D','00C4')]","[('0022','0042'),('004D','006D')]").replace("p['frames']==249","p['frames']==241")
start=p.index("assert [(a['index']"); end=p.index("assert [r['stage']",start)
p=p[:start]+'''failed_path=Path('Exe/Room17Input-20261003/cycle-1/input.json')
failed=read(failed_path)
assert failed['failure'] and failed['frames']==3041 and failed['boundaries']==[]
assert 'not sent, because the point is outside the room picture' in failed['actions'][0]['result']['Uncertainties'][0]
assert [(a['x'],a['y']) for a in p['actions']]==[(312,72),(300,90)]
exit=next(o for o in p['directory'] if o['Id']==353)
assert (exit['X']+exit['Width']//2,exit['Y']+exit['Height']//2)==(312,72)
assert (exit['WalkX'],exit['WalkY'])==(336,141)
box=next(b for b in p['geometry']['Boxes'] if b['Index']==3)
assert tuple(box['UpperLeft'][k]+(box['LowerLeft'][k]-box['UpperLeft'][k])//3 for k in ('X','Y'))==(300,90)
for a in p['actions']:
    assert a['result']['IsGameInput'] and not any('not sent, because' in u for u in a['result']['Uncertainties'])
    assert any(f"a single click at ({a['x']}, {a['y']}) runs script 5" in t for t in p['trace'])
assert not any('startObject runs object 353 verb 0x38' in t for t in p['trace'])
''' + p[end:]
p=p.replace("['arrival','walk-box-3','walk-box-5']","['arrival','toward-exit353','return-upper-path']").replace('[(62,80,1)],[(223,91,6)],[(180,95,2)]','[(161,82,0)],[(269,111,6)],[(311,94,3)]').replace("['RoomId']==15","['RoomId']==17").replace('len(body)==23','len(body)==15').replace("[('0016',3),('0066',5)]","[('001B',4),('0046',5)]").replace("'+0x006B'","'+0x0080'")
p=p.replace("'Exe/Room24Input-20261003/cycle-1/input.json')]","'Exe/Room24Input-20261003/cycle-1/input.json','Exe/Room15Input-20261003/cycle-1/input.json')]")
p=p.replace('len(before)==199','len(before)==201').replace('room24-input-20261003/remaining','room15-input-20261003/remaining').replace('len(after)==201','len(after)==203')
p=p.replace("outcome='Room15 two watcher sites complete through accepted Core clicks.'","outcome='Room17 watcher pair completes through visible input points; off-picture first attempt retained.'").replace('frames=249,completedScopes=2,conditionalExecutedBodyEvents=23','frames=241,completedScopes=2,conditionalExecutedBodyEvents=15').replace('before=199,after=201','before=201,after=203').replace('percent=85.53,remaining=34','percent=86.38,remaining=32').replace('diagnosticCycles=1','diagnosticCycles=2').replace('[packet_path,literal_path','[packet_path,failed_path,literal_path')
Path('.verification/archive/room17-input-20261003/check.py').write_text(p,encoding='utf-8')
