"""Export only completed preview recipes, exact data fingerprints and honest gaps.
Usage: export_catalog.py roots.json audit.json Games/loom catalog.json report.md code.tsv established-membership.json
"""
import collections, json, pathlib, re, sys
from replay_names import ReplayNames
roots=json.load(open(sys.argv[1],encoding='utf-8'))
audit=json.load(open(sys.argv[2],encoding='utf-8'))
names=ReplayNames(sys.argv[3], sys.argv[6])

def site(trace):
    m=re.match(r'cutscene at (.*?) (\+0x[\dA-Fa-f]+) runs script',trace)
    if not m: return None
    owner=m[1]
    owner=re.sub(r'^Script (\d+)$',lambda m:f'script {int(m[1]):03d}',owner)
    owner=owner.replace(' Object ',' object ').replace(' local script ',' local ').replace(' entry script',' entry')
    return owner,m[2].upper().replace('+0X','+0x')

replays=[]; reached=set()
for r in audit['results']:
    if r['status']!='Validated state preview': continue
    actual={s for t in r['sceneTrace'] if (s:=site(t))}
    reached |= actual
    scripts=sorted({int(s[0].split()[1]) for s in actual if s[0].startswith('script ')})
    category='Scenes' if r['verb']==0x38 or r['objectId']==0 or len(r['rooms'])>1 or any(44<=s<112 for s in scripts) else 'Dialogue and reactions'
    source=f"{r['owner']}. Fresh post-boot seed-0 session; catalog arrival assumed; " + ('room entry' if not r['objectId'] else 'direct object verb, not game input') + f". Completed in {r['frames']} audit frames, including the final 32 idle frames. Reached scene boundaries:\n"+'\n'.join(r['sceneTrace'])
    title, name_evidence=names.describe(r)
    replays.append(dict(id=r['owner'],title=title,category=category,room=r['room'],objectId=r['objectId'],verb=r['verb'],scriptIds=scripts,rooms=r['rooms'],frames=r['frames'],source=source+'\n'+name_evidence))

# Existing hand-validated starts keep their descriptors in the app, and are counted once.
manual={(8,255,0x38),(10,295,0x38)}
replays=[r for r in replays if (r['room'],r['objectId'],r['verb']) not in manual]
coverage=[]
for c in roots['candidateOwners']:
    missing=[s for s in c['sites'] if (c['owner'],s['offset']) not in reached]
    if not missing: continue
    probes=[r for r in audit['results'] if c['owner'] in r['candidates']]
    statuses=collections.Counter(r['status'] for r in probes)
    status='Some branches need validation' if len(missing)<len(c['sites']) else 'Start needs validation'
    if c['owner'] in ('script 004','script 005','script 009','script 011','script 012','script 013','script 022','script 029','script 041'):
        status='Shared input/draft helper; not a standalone story scene'
    source=c['owner']+'; unvalidated cutscene sites: '+', '.join(s['offset'] for s in missing)+'.\n'
    source+='Literal trigger probes: '+('; '.join(f'{k}: {v}' for k,v in statuses.items()) if probes else 'none resolved; boot/watcher, dynamic calls or earlier state require individual review')+'.\n'
    source+='Possible roots (not proof of reachability): '+', '.join(c['roots'])
    coverage.append(dict(title=c['owner'],status=status,source=source))

counts=collections.Counter(r['category'] for r in replays)
summary=f"{counts['Scenes']+4} scene starts and {counts['Dialogue and reactions']} dialogue/reaction previews. Coverage remains incomplete; Needs validation lists unresolved scene branches and shared helpers."
established=json.load(open(sys.argv[7],encoding='utf-8'))
assert established['sourceFiles']==audit['sourceFiles'], 'Membership must validate the same exact game files'
memberships=[dict(id=r['id'],startRoom=r['room'],rooms=sorted(set(r['rooms'])|{r['room']}),frames=r['frames'],source='Validated start room plus rooms observed during completed playback.') for r in replays]
for r in established['results']:
    memberships.append({k:r[k] for k in ('id','startRoom','rooms','frames','source')})
assert len({r['id'] for r in memberships})==len(memberships), 'Duplicate membership ID'
out=dict(sourceFiles=audit['sourceFiles'],replays=replays,coverage=coverage,summary=summary,membership=memberships)
with open(sys.argv[4],'w',encoding='utf-8') as f: json.dump(out,f,indent=2,ensure_ascii=False)
with open(sys.argv[5],'w',encoding='utf-8') as f:
    f.write('# Broad replay audit\n\n'+summary+'\n\n')
    f.write(f"Decoded input SHA256: `{roots['inputSha256']}`. {len(audit['results'])} roots tested; {len(roots['candidateOwners'])} candidate owners, {sum(len(c['sites']) for c in roots['candidateOwners'])} cutscene sites; {len(reached)} distinct sites reached by completed previews; {len(roots['unresolved'])} unresolved static call edges. These are starts/sites, not a count of unique narrative scenes.\n\n")
    f.write('Fresh boot seed 0; catalog visit; direct source verb or entry; no variable, inventory, class or position writes. Probe limit 3000 frames; missing starts excluded after the same ordinary 32-frame arrival/input window used by the app. A successful preview must open a scene, execute matching end boundaries, settle without an open scene/movement/talk and report no unsupported execution. Completion proves this explorer preview, not full original-game prerequisites or ScummVM parity. Full source traces and uncertainties remain in '+str(pathlib.Path(sys.argv[2]).parent).replace('\\','/')+'.\n\n')
    f.write('| Status | Recipes |\n|---|---|\n')
    for k,v in collections.Counter(r['status'] for r in audit['results']).items(): f.write(f'| {k} | {v} |\n')
    f.write('\n| Trigger | Result | Frames | Reached scene boundaries |\n|---|---|---|---|\n')
    for r in audit['results']:
        f.write('| '+r['owner']+' | '+r['status']+' | '+str(r['frames'])+' | '+'; '.join(r['sceneTrace'])+' |\n')
print(summary, f'{len(reached)} of 235 sites reached; {len(coverage)} owners with outstanding sites')
