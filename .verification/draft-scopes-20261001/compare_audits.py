import collections,hashlib,json,pathlib,sys
root=pathlib.Path(sys.argv[1]); previous=json.load(open('Exe/VerificationSession-20261001/native-replay-audit-2/audit.json',encoding='utf-8'))
current=json.load(open(root/'literal/audit.json',encoding='utf-8'))
old={r['owner']:r for r in previous['results']}; new={r['owner']:r for r in current['results']}
regressed=[r['owner'] for r in current['results'] if old[r['owner']]['status']=='Validated state preview' and r['status']!='Validated state preview']
added=[{k:r[k] for k in ('owner','status','frames','rooms','sceneTrace')} for r in current['results'] if r['status']=='Validated state preview' and old[r['owner']]['status']!='Validated state preview']
changed=[dict(owner=k,oldFrames=old[k]['frames'],frames=r['frames'],oldRooms=old[k]['rooms'],rooms=r['rooms']) for k,r in new.items() if old[k]['status']=='Validated state preview' and (old[k]['frames']!=r['frames'] or old[k]['rooms']!=r['rooms'])]
catalog=json.load(open('src/SCUMMexplorer.App/loom-replays.json',encoding='utf-8'))
membership_mismatches=[dict(id=r['id'],rooms=new[r['id']]['rooms'],catalogRooms=r['rooms']) for r in catalog['replays'] if 'steps' not in r and new[r['id']]['rooms']!=r['rooms']]
report=dict(counts=dict(collections.Counter(r['status'] for r in current['results'])),regressed=regressed,added=added,changed=changed,membershipMismatches=membership_mismatches,
    auditSha256=hashlib.sha256((root/'literal/audit.json').read_bytes()).hexdigest(),sourceFilesMatch=current['sourceFiles']==previous['sourceFiles'],
    established=json.load(open(root/'established.json',encoding='utf-8'))['results'],
    sequences=[{k:v for k,v in r.items() if k not in ('trace','uncertainties')} for r in json.load(open(root/'sequences.json',encoding='utf-8'))['results']])
pathlib.Path(sys.argv[2]).write_text(json.dumps(report,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
print(report['counts']);print('Regressions:',regressed);print('Added:',[r['owner'] for r in added]);print('Changed:',len(changed),'membership mismatches:',membership_mismatches)
