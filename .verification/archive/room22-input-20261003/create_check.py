from pathlib import Path
p=Path('.verification/archive/room15-input-20261003/check.py').read_text()
p=p.replace("base = Path('.verification/archive/room15-input-20261003')","base = Path('.verification/archive/room22-input-20261003')").replace("packet_path = Path('Exe/Room15Input-20261003/cycle-1/input.json')","packet_path = Path('Exe/Room22Input-20261003/cycle-2/input.json')")
p=p.replace('Room 15','Room 22').replace("[('001D','0062'),('006D','00C4')]","[('0083','00D3'),('002F','0078')]").replace("p['frames']==249","p['frames']==235")
p=p.replace('[(3,180,111),(5,188,118)]','[(11,97,69),(10,90,64)]').replace("['arrival','walk-box-3','walk-box-5']","['arrival','walk-box-11','walk-box-10']").replace('[(62,80,1)],[(223,91,6)],[(180,95,2)]','[(194,66,13)],[(108,56,9)],[(120,66,12)]').replace("['RoomId']==15","['RoomId']==22").replace("[('0016',3),('0066',5)]","[('0028',10),('007C',11)]").replace("'+0x006B'","'+0x0101'")
p=p.replace("'Exe/Room24Input-20261003/cycle-1/input.json')]","'Exe/Room24Input-20261003/cycle-1/input.json','Exe/Room15Input-20261003/cycle-1/input.json','Exe/Room17Input-20261003/cycle-2/input.json')]")
p=p.replace('len(before)==199','len(before)==203').replace('room24-input-20261003/remaining','room17-input-20261003/remaining').replace('len(after)==201','len(after)==205')
p=p.replace('code_path=Path(','''failed_path=Path('Exe/Room22Input-20261003/cycle-1/input.json')
failed=read(failed_path)
assert failed['failure'] and failed['frames']==3041 and failed['boundaries']==boundaries[:2]
assert [(a['index'],a['x'],a['y']) for a in failed['actions']]==[(10,90,64)]
code_path=Path(''')
p=p.replace("outcome='Room15 two watcher sites complete through accepted Core clicks.'","outcome='Room22 watcher pair completes from incoming box11 then box10; first failed order retained.'").replace('frames=249','frames=235').replace('before=199,after=201','before=203,after=205').replace('percent=85.53,remaining=34','percent=87.23,remaining=30').replace('diagnosticCycles=1','diagnosticCycles=2').replace('[packet_path,literal_path','[packet_path,failed_path,literal_path')
Path('.verification/archive/room22-input-20261003/check.py').write_text(p,encoding='utf-8')
