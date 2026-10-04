from pathlib import Path
p=Path('.verification/archive/room08-input-20261003/check.py').read_text()
p=p.replace("base=Path('.verification/archive/room08-input-20261003')","base=Path('.verification/archive/room62-input-20261003')").replace("packet_path=Path('Exe/Room08Input-20261003/cycle-1/input.json')","packet_path=Path('Exe/Room62Input-20261003/cycle-1/input.json')").replace('Room 08','Room 62')
p=p.replace("[(212,'000B','0062'),(211,'0008','0066'),(213,'0008','0066'),(214,'000B','0062')]","[(201,'0000','007B'),(202,'0000','007B'),(204,'0000','007B')]").replace('((p,1024),(d,820))','((p,692),(d,539))').replace("['RoomId']==8","['RoomId']==62")
p=p.replace('D7D','D8A').replace('D7E','D8B')
p=p.replace("('arrival','bleach-left','restore-left','dye-right','restore-right')","('arrival','left-to-straw','left-to-gold','right-to-straw')").replace("[('arrival',None,None),('bleach-left',1,None),('restore-left',0,None),('dye-right',0,1),('restore-right',0,0)]","[('arrival',1,None),('left-to-straw',0,None),('left-to-gold',1,1),('right-to-straw',0,0)]")
p=p.replace('notes=2,learnedSlot=2','notes=3,learnedSlot=1').replace("')])==16","')])==12")
p=p.replace("[(63,2,True,[2,1,1,1]),(62,2,False,[1,1,1,2]),(62,2,False,[1,1,1,2]),(63,2,True,[2,1,1,1])]","[(61,1,True,[3,1,1,1]),(60,1,False,[1,1,1,3]),(61,1,True,[3,1,1,1])]")
p=p.replace("[(256,'3F'),(256,'3E'),(257,'3E'),(257,'3F')]","[(808,'3D'),(808,'3C'),(809,'3D')]")
start=p.index('labels='); end=p.index('body=lambda',start)
p=p[:start]+"labels={f'Room 62 local script {n}' for n in (201,202,204,206)}|{f'Room 62 Object {o} Verb 0x{v}' for o,v in [(808,'3D'),(808,'3C'),(809,'3D')]}\n"+p[end:]
p=p.replace('len(a)==len(b)==79','len(a)==len(b)==63').replace('len(differences)==76','len(differences)==63')
start=p.index('for owner,offset'); end=p.index('def sites',start)
p=p[:start]+'''for owner,offset,variable,value in [(201,'001B','D8A',0),(202,'001B','D8A',1),(202,'0045','D8B',1),(204,'001B','D8B',0),(204,'0045','D8A',0)]:
    assert any(r[:3]==[f'Room 62 local {owner}','+0x'+offset,'move'] and f'bit[0x{variable}] = 0x{value:04X}' in r[3] for r in code)
'''+p[end:]
p=p.replace("'Exe/Room22Input-20261003/cycle-2/input.json')]","'Exe/Room22Input-20261003/cycle-2/input.json','Exe/Room08Input-20261003/cycle-1/input.json','Exe/Room28Input-20261003/cycle-1/input.json')]").replace('len(before)==205','len(before)==210').replace('room22-input-20261003/remaining','room28-input-20261003/remaining').replace('len(after)==207','len(after)==212')
p=p.replace('Two Room08 tapestry restoration sites complete via four accepted keyboard casts.','Room62 two gold/straw sites complete via three accepted keyboard casts.').replace('inputFrames=1024,directFrames=820,completedScopes=4,conditionalExecutedBodyEvents=79,certaintyDifferences=76','inputFrames=692,directFrames=539,completedScopes=3,conditionalExecutedBodyEvents=63,certaintyDifferences=63').replace('before=205,after=207','before=210,after=212').replace('percent=88.09,remaining=28','percent=90.21,remaining=23')
Path('.verification/archive/room62-input-20261003/check.py').write_text(p,encoding='utf-8')
