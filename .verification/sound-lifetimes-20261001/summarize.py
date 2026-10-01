"""Compare old incomplete roots with fresh post-clock snapshots; no catalog writes."""
import collections, json, pathlib, sys

gaps = json.loads(pathlib.Path(sys.argv[1]).read_text(encoding='utf-8'))
rows = []
for root in gaps:
    final = root['checkpoints'][-1]
    begun = sum(t.startswith('cutscene at ') for t in root['sceneTrace'])
    ended = sum(t.startswith('endCutscene at ') for t in root['sceneTrace'])
    unsupported = [u for u in root['uncertainties'] if 'unsupported' in u.lower() or 'unmodelled' in u.lower()]
    complete = not final['InCutscene'] and begun == ended and not final['EgoMoving'] and not final['CameraMoving'] and not unsupported
    rows.append(dict(owner=root['owner'], completeBoundaries=complete, begun=begun, ended=ended, room=final['room'],
        inCutscene=final['InCutscene'], sounds=final['sounds'], flag=final['variables'].get('var[0x0A4]'),
        pending=[dict(label=p['label'],offset=p['offset'],number=p['number'],freeze=p['freeze']) for p in final['pending']],
        sceneTrace=root['sceneTrace'],unsupported=unsupported))
pathlib.Path(sys.argv[2]).write_text(json.dumps(rows,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
lines=['# Native sound completion follow-up','',
    '45 formerly incomplete roots rerun with the same fresh seed-0 catalog/direct-verb setup and 3000-frame bound. This diagnostic records scopes and idle state at frame 3000; promotion requires the ordinary ReplayAudit and visible WPF validation. No state injections.','',
    '| Root | Matching ended scopes and idle | Begun/end | Final room | Sound flag |','|---|---|---|---|---|']
lines += [f"| {r['owner']} | {r['completeBoundaries']} | {r['begun']}/{r['ended']} | {r['room']} | {r['flag']} |" for r in rows]
pathlib.Path(sys.argv[3]).write_text('\n'.join(lines)+'\n',encoding='utf-8')
print(collections.Counter(r['completeBoundaries'] for r in rows))
