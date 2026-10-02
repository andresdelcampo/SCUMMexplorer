"""Verify current reports, frozen work and append-only historical guidance."""
from pathlib import Path
import hashlib
import json
import re
import subprocess

base = Path(__file__).parent
def load(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest()
def git(*args):
    return subprocess.run(['rtk','proxy','git',*args],check=True,capture_output=True,encoding='utf-8').stdout.strip()
def original(name):
    return subprocess.run(['rtk','proxy','git','show','f84460f:'+name],check=True,capture_output=True,
                          encoding='utf-8').stdout.replace('\r\n','\n')

checks = load(base/'check-summary.json')
assert checks['core'] == 622 and checks['focused'] == 296
assert checks['primaryEntireLatestStreamsExact'] and checks['separateEntireLoadedObjectCallSelectionCorrespondenceExact']
assert checks['independentOwnInvocationTerminals'] == 76 and checks['actualRecipeWriterResumedTerminalChains'] == 0
assert checks['roomRenders'] == 76 and checks['remainingGaps'] == checks['changedPixels'] == 0
assert checks['releaseWarnings'] == checks['releaseErrors'] == checks['blockedRandomSites'] == 0
assert checks['replayCoverage']['validatedLiteralSites'] == 168 and checks['replayCoverage']['totalLiteralSites'] == 235
for report in checks['reports']:
    assert sha(Path(report['path'])) == report['sha256'], report['path']

protected = load(Path('Exe/RuntimeScriptCorrespondenceSession-20261002/protected-before.json'))
prior_protected = load(Path('Exe/RuntimeObjectCorrespondenceSession-20261002/protected-before.json'))
assert len(protected) == 57 and protected == prior_protected
for name, digest in protected.items(): assert sha(Path(name)) == digest, name
staged = git('diff','--cached','--name-only').splitlines()
assert not set(staged).intersection(protected)
changes = git('diff','f84460f','--name-only','--','.verification').splitlines()
assert all(name.startswith('.verification/runtime-boundaries-20261002/') or
           name == '.verification/verb-area/VerbAreaCheck/Program.cs' for name in changes)

doc_anchors = {
    'docs/LOOM_EGA_PLAN.md':'## Current follow-up (2 October)\n\n',
    'docs/LOOM_SCRIPT_MAP.md':'## Follow-up findings (30 September)\n\n',
    'docs/LOOM_CUTSCENE_DISCOVERY.md':'## Current replay coverage (2 October)\n\n',
    'docs/CHANGELOG.md':'# Changelog\n\n',
    'docs/NEXT_SESSION_PROMPT.md':'State at checkpoint (2 October 2026):\n\n'
}
doc_reports = []
for name, anchor in doc_anchors.items():
    old, current = original(name), Path(name).read_text(encoding='utf-8')
    if name.endswith('NEXT_SESSION_PROMPT.md'):
        marker = 'Next-session request (ready to copy):\n\n'
        old, current_head = old.split(marker)[0], current.split(marker)[0]
        assert 'Core622' in current.split(marker)[1] and '71.49%' in current.split(marker)[1]
    else: current_head = current
    before, after = old.split(anchor)
    assert current_head.startswith(before+anchor) and current_head.endswith(after), name
    assert '`6ad392d`' in current and '`4b7c646`' in current
    doc_reports.append(dict(path=name,sha256=sha(Path(name))))

required = {'6c06692','3ed2da7','a3a90ec','3f32e5f','32f1ecb','6ad392d','4b7c646','f84460f','aaaa755','56ffa35','729cbb9','0fda17b','00f01a8','8110900','5e2c0c4',
            '5fcc2a0','995e97e','b3ee09e','b1381ff'}
old_handoff = original('docs/NEXT_SESSION_PROMPT.md')
for candidate in re.findall(r'`([0-9a-f]{7})`',old_handoff):
    kind = subprocess.run(['rtk','proxy','git','cat-file','-t',candidate],capture_output=True,encoding='utf-8')
    if kind.returncode == 0 and kind.stdout.strip() == 'commit': required.add(candidate)
for checkpoint in sorted(required):
    git('merge-base','--is-ancestor',checkpoint,'HEAD')

app = subprocess.run(['rtk','proxy','powershell','-NoProfile','-Command',
    'Get-Process -Name SCUMMexplorer.App -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Id'],
    capture_output=True,encoding='utf-8')
assert not app.stdout.strip(), app.stdout
summary = dict(implementationHead=git('rev-parse','HEAD'),historicalCheckpointAncestors=sorted(required),
    all57ProtectedFilesExactAndUnstaged=True,previousVerificationPacketsUntouched=True,
    previousHandoffGuidanceExact=True,earlierPlanMapDiscoveryChangelogTextExact=True,
    reportHashesExact=True,appRunning=0,appLaunched=False,pushPerformed=False,
    replayCoverage=checks['replayCoverage'],reports=doc_reports)
(base/'publication-summary.json').write_text(json.dumps(summary,indent=2)+'\n',encoding='utf-8')
print(f"Publication verified: {len(required)} historical ancestors;57 frozen files;all reports exact;earlier guidance retained;App0.")
