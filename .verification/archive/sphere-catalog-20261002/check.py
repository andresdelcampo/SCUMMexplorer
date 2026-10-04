"""Verify only the independently completed sphere preview is promoted."""
import hashlib
import json
import re
import subprocess
from pathlib import Path

def read(p):
    return json.loads(Path(p).read_text(encoding='utf-8-sig'))
def digest(p):
    return hashlib.sha256(Path(p).read_bytes()).hexdigest()

out = Path('Exe/SphereCatalog-20261002')
old = read(out/'catalog-before.json')
new = read('src/SCUMMexplorer.App/loom-replays.json')
audit_path = 'Exe/EstimatedRoomLoads-20261002/audits/literal/audit.json'
audit = read(audit_path)
fresh = read(out/'sphere-1/audit.json')
identity = 'Room 18 object 370 Verb 0x36'
sphere = next(r for r in audit['results'] if r['owner'] == identity)
assert fresh['results'] == [sphere]
assert old['sourceFiles'] == new['sourceFiles'] == audit['sourceFiles'] == fresh['sourceFiles']
for f in new['sourceFiles']:
    assert digest(Path('Games/loom')/f['name']) == f['sha256'].lower()
assert sphere['status'] == 'Validated state preview' and sphere['frames'] == 229
assert sphere['first'] == 0 and sphere['everOpen'] and sphere['begun'] == sphere['ended'] == 1
assert sphere['sceneTrace'] == ['cutscene at Script 65 +0x0000 runs script 2', 'endCutscene at Script 65 +0x0302 runs script 3']
assert sphere['rooms'] == [18,19]
assert not any(re.search('unsupported|unmodelled', u, re.I) for u in sphere['uncertainties'])
assert any('estimated execution' in u for u in sphere['uncertainties'])
for key in ('replays','membership'):
    assert [r for r in new[key] if r['id'] != identity] == old[key], key
    assert len(new[key]) == len(old[key])+1
assert len(new['replays']) == 331 and len(new['coverage']) == 49
# Status summaries may reflect the newly completed probe in shared helper011.
old_gaps = {r['title']:r for r in old['coverage']}
new_gaps = {r['title']:r for r in new['coverage']}
assert set(old_gaps)-set(new_gaps) == {'script 065'} and not set(new_gaps)-set(old_gaps)
for owner, gap in new_gaps.items():
    assert gap['status'] == old_gaps[owner]['status']
    assert gap['source'].splitlines()[0] == old_gaps[owner]['source'].splitlines()[0]
sites = set()
for result in audit['results']:
    if result['status'] == 'Validated state preview':
        sites.update(t for t in result['sceneTrace'] if t.startswith('cutscene at '))
assert len(sites) == 169
protected = read('Exe/RuntimeScriptCorrespondenceSession-20261002/protected-before.json')
assert len(protected) == 57 and all(digest(p) == h.lower() for p,h in protected.items())
assert not set(subprocess.check_output(['git','diff','--cached','--name-only'],text=True).splitlines()).intersection(protected)
names = read(out/'names.json')
assert names['entries'] == 331 and names['renamed'] == 132
build = (out/'build.txt').read_text(encoding='utf-16')
assert 'Build succeeded.' in build and '0 Warning(s)' in build and '0 Error(s)' in build
summary = dict(recipe=next(r for r in new['replays'] if r['id']==identity),
    coverage=dict(before=168,after=169,total=235,percent=round(169/235*100,2),remaining=66,outstandingOwners=49),
    starts=335,scenes=85,reactions=250,unchangedExistingRecipes=330,sequencesRetained=2,
    freshSphereExact=True,protectedExactUnstaged=57,sharedCatalogLoaded=331,releaseBuild='0 warnings,0 errors',
    evidence={p:digest(p) for p in [audit_path,str(out/'sphere-1/audit.json'),'src/SCUMMexplorer.App/loom-replays.json']})
Path('.verification/archive/sphere-catalog-20261002/summary.json').write_text(json.dumps(summary,indent=2,ensure_ascii=False)+'\n',encoding='utf-8')
print('PASS sphere229,169/235=71.91%,335 starts,330 old recipes and both sequences exact,protected57 exact/unstaged')
