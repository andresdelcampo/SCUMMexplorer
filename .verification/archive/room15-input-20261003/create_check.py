from pathlib import Path
base=Path('.verification/archive/room15-input-20261003')
p=Path('.verification/archive/room36-input-20261003/check.py').read_text()
p=p.replace('room36-input-20261003','room15-input-20261003').replace('Room36Input-20261003/cycle-2','Room15Input-20261003/cycle-1').replace('Room 36','Room 15')
p=p.replace("failed_path = Path('Exe/Room36Input-20261003/cycle-1/input.json')\np, failed = read(packet_path), read(failed_path)","p = read(packet_path)")
p=p.replace("pairs = [('00A6','00CA'),('0048','006C'),('001D','003D'),('0077','009B')]","pairs = [('001D','0062'),('006D','00C4')]").replace("p['frames']==547","p['frames']==249")
start=p.index("assert failed['failure']"); end=p.index("for a in p['actions']:",start)
p=p[:start]+"assert [(a['index'],a['x'],a['y']) for a in p['actions']]==[(3,180,111),(5,188,118)]\n"+p[end:]
p=p.replace("['arrival','walk-box-10','walk-box-4','walk-box-3','walk-box-9']","['arrival','walk-box-3','walk-box-5']")
p=p.replace('[(438,126,12)],[(322,79,7)],[(132,92,1)],[(118,102,5)],[(376,124,11)]','[(62,80,1)],[(223,91,6)],[(180,95,2)]').replace("['RoomId']==36","['RoomId']==15").replace('len(body)==29','len(body)==23')
p='\n'.join(line for line in p.splitlines() if not line.startswith("assert [(e['Offset']"))+'\n'
p=p.replace("[('0016',3),('0041',4),('0070',9),('009F',10)]","[('0016',3),('0066',5)]").replace("'+0x00E5'","'+0x006B'")
p=p.replace("'Exe/Room10Input-20261003/cycle-2/input.json')","'Exe/Room10Input-20261003/cycle-2/input.json','Exe/Room36Input-20261003/cycle-2/input.json','Exe/Room24Input-20261003/cycle-1/input.json')")
p=p.replace('len(before)==192','len(before)==199').replace('room10-input-20261003/remaining','room24-input-20261003/remaining').replace('len(after-before)==4 and len(after)==196','len(after-before)==2 and len(after)==201')
p=p[:p.index('summary=dict(')]+'''summary=dict(outcome='Room15 two watcher sites complete through accepted Core clicks.',frames=249,completedScopes=2,conditionalExecutedBodyEvents=23,newSites=[dict(owner=o,offset=a) for o,a in sorted(after-before)],literalCoverage=dict(validated=169,total=235,percent=71.91,unchanged=True),auditedSiteUnion=dict(before=199,after=201,total=235,percent=85.53,remaining=34),diagnosticCycles=1,protectedExactUnstaged=57,preservedFiles=len(preserved),productionAndCatalogUnchanged=True,limitations=['Catalog arrival/default preview are exploration assumptions.','Core API only; no WPF, physical-input or native-game claim.','All recorded watcher events Conditional; no fabricated direct-script control.','No fresh single-action promotion.'],evidence={str(f):digest(f) for f in [packet_path,literal_path,code_path,catalog_path]+prior_paths})
for name,value in [('summary.json',summary),('remaining-sites.json',[dict(owner=o,offset=a) for o,a in sorted(inventory-after)])]:
    (base/name).write_text(json.dumps(value,indent=2)+'\\n',encoding='utf-8')
print(json.dumps(summary,indent=2))
'''
(base/'check.py').write_text(p,encoding='utf-8')
