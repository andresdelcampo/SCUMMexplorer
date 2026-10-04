from pathlib import Path

summary = ('2 October Room41 certainty layers after `e285e08`: mouse/key pairs have identical downstream events, state, timing and rooms. '
    'Native source-level4 remains41,15/14 and enters022 already Conditional; the older raw6 control remains41,12/11 and first taints022 at +007D while rejecting slot1. '
    'Before final raw4 casting, mode/target/index/notes and D70 already carry estimates. Earlier dye input reads estimatedD6C in031 +000A; conditional palette actorOps +0254 adds an actor estimate, read by010 getDist for the next note. '
    'Loaded211 tables establish51 disjoint slot4/earlier-candidate comparisons, zero collisions; this is source-domain research, not known runtime state or native090 completion. '
    'Same-setup direct633/204 Escape still completes43,15/15. Both native level4 orders and096/all four20 recipes freshly preserved. '
    'Production/catalog unchanged, no estimate clearing or promotion; Core289,363 exact roots/fingerprints/memberships/sequences unchanged, Blockers0, Release clean. '
    '[Evidence and reproduction](../.verification/archive/room41-certainty-20261002/validation.md).')

if summary in Path('docs/LOOM_EGA_PLAN.md').read_text(encoding='utf-8-sig'):
    print('This checkpoint is already recorded; no repeated history insertion.')
    raise SystemExit(0)

for file, heading in (('LOOM_EGA_PLAN.md','## Current follow-up (2 October)'),
                      ('LOOM_SCRIPT_MAP.md','## Follow-up findings (30 September)'),
                      ('LOOM_CUTSCENE_DISCOVERY.md','## Current replay coverage (2 October)')):
    p=Path('docs')/file
    text=p.read_text(encoding='utf-8-sig')
    assert heading+'\n\n' in text
    text=text.replace(heading+'\n\n',heading+'\n\n'+summary+'\n\nPrevious native-prerequisite checkpoint follows. Its023(0/1) Reached observation describes the older raw6 control, not the longer raw4 route.\n\n',1)
    p.write_text(text,encoding='utf-8')

p=Path('docs/CHANGELOG.md')
text=p.read_text(encoding='utf-8-sig')
heading='## 2 October: native level4 prerequisites and Room41 certainty continuation'
entry='''## 2 October: Room41 certainty layers and source-domain proof

| Change | Result | Record |
| --- | --- | --- |
| Native input certainty audit | Mouse/key pairs agree downstream; raw4 enters022 Conditional, older raw6 first taints at007D; inherited input/practice/actor dependencies localized without runtime writes | [evidence](../.verification/archive/room41-certainty-20261002/validation.md) |
| Source boot alternatives | 51 slot4/earlier-candidate comparisons exclude collisions; estimates and native090 gap retained, no engine/catalog changes | [derived source domains](../.verification/archive/room41-certainty-20261002/source-domains.json) |
| Preserved source recipes and checks | Both level4 orders,096 and four20 combinations unchanged; Core289,363 exact roots/fingerprints/memberships/sequences unchanged; Blockers0; Release clean | [checks](../.verification/archive/room41-certainty-20261002/check-summary.json) |

'''
assert heading in text
p.write_text(text.replace(heading,entry+heading,1),encoding='utf-8')

p=Path('docs/NEXT_SESSION_PROMPT.md')
text=p.read_text(encoding='utf-8-sig')
marker='State at checkpoint (2 October 2026):\n\n'
assert marker in text
text=text.replace(marker,marker+'* '+summary+'\n* New diagnostic packet: .verification/archive/room41-certainty-20261002/. Read per-note route-summary.json, isolated CertaintyAudit and source-domains.json. Native4 first estimated010/022 branch0000; older raw6 first022 branch007D. The native4 block is not explained solely by randomized candidate rejection or raw4 limit. No new WPF/pixel run; no App launched or running; no push.\n\n',1)
old='1. Inspect git/preserve unrelated work.'
new='1. Inspect git/preserve unrelated work. Read .verification/archive/room41-certainty-20261002/validation.md, route-summary.json, source-domains.json and both diagnostic harnesses first.'
assert old in text
text=text.replace(old,new,1)
start=text.index('2. Room41 native certainty remains priority.')
end=text.index('\n\n3. Native source-executed level4',start)
text=text[:start]+'''2. Room41 native certainty remains priority. Reuse new room41.json paired mouse/key recipes: raw4 native15/14 in41, older raw6 native12/11 in41, direct633/actual204 Escape15/15 in43. Preserve both native level4 orders and direct control separation. Longer raw4 enters022 already Conditional: estimatedD6C after the dye lesson taints031 at000A; palette0254 adds an actor estimate read by the next010 getDist. Final mode064/target067/index062/notes/raw4 andD70 retain estimates;010/022 first conditions0000 already read them. Older raw6 entered notes remain known and023(0/1) Reached;022 first estimated branch007D rejects slot1, then023(2/3/4) Conditional. Loaded03B response4241 remains known; index/path taint065/026.51 source-derived slot4/earlier forward/capable-reverse comparisons exclude collisions, but do not make current runtime state known. Next generic precision research: source finite domains/masks and appearance versus placement dependencies, preserving uncertain movement, randomized notes and earlier-path provenance. No blanket join/loop resets, estimate clearing, injected known state, helper shortcuts, room exceptions or catalog promotion. Native090 and native intro continuation remain unresolved.''' + text[end:]
start=text.index('Continue native LOOM after the2 October native-prerequisite continuation (git log).')
text=text[:start]+'''Continue native LOOM after the2 October Room41 certainty-layer continuation (git log). Read .verification/archive/room41-certainty-20261002/validation.md, per-note summaries/isolated CertaintyAudit and source-domains.json, then native-prerequisite and previous source-progression guidance. Native level4 recipes remain9/9 with estimates; raw4 native mouse/key15/14 starts022 Conditional, older raw6 native12/11 first taints022 at007D; direct633/204 override15/15 completes43. Source211 domains exclude51 earlier-candidate collisions. Prioritize generic certainty precision across input/practice/actor dependencies and randomized candidate matching; do not clear estimates or lose earlier path/movement provenance. Preserve096/20 recipes, status/costume/scythe fixes and all fresh-root gaps. No known-state/flag/placement/note injection, status-edit prerequisites, browsing floors, arbitrary helper shortcuts, room hacks or catalog promotion. Native090/native challenge opening unresolved. Fresh appropriate regressions, final main Release rebuild, separate task commits and updated handoff; no push. No app should be running; user authorized stopping stray SCUMMexplorer instances. Session token-usage tool unavailable. Finish with clickable continuation chip; no separate chat.\n'''
p.write_text(text,encoding='utf-8')
print('Updated plan, script map, discovery, changelog and handoff.')
