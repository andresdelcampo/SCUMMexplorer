"""Add the superseding continuation without rewriting historical evidence."""
from pathlib import Path

docs=Path('docs')
link='../.verification/native-prerequisites-20261002/validation.md'
current=f'''2 October continuation after575d458: native loaded-note egg/047 override, dye207 and reverse-gold808 source actions now mark slots0/2/1 used and execute029(4) (recipe checkpoint `cee1bf9`). Both final-use orders finish9/9 in62/06; raw4 and slots1/2 retain estimates. This source-executed level4 replaces direct level5/6 shortcuts in a longer native41 recipe, which still ends15/14 with Conditional090/possible42; same-setup direct633+actual204 Escape completes43,15/15. The older scan's023(0/1) calls are Reached,023(2/3/4) already Conditional: earlier randomized candidate matching taints022 before slot4 selection, while loaded03B response4241 itself is not estimated. No estimate clearing, production/catalog edits or promotion. Source096/20 freshly preserved; Core289,363 exact roots/fingerprints/memberships/sequences unchanged, Blockers0, main Release clean. [Evidence and reproduction]({link}).'''

def edit(name,transform):
    p=docs/name
    text=p.read_text(encoding='utf-8')
    changed=transform(text)
    assert changed!=text,name
    p.write_text(changed,encoding='utf-8')

for name,header in (('LOOM_EGA_PLAN.md','## Current follow-up (2 October)'),('LOOM_SCRIPT_MAP.md','## Follow-up findings (30 September)'),('LOOM_CUTSCENE_DISCOVERY.md','## Current replay coverage (2 October)')):
    edit(name,lambda t,h=header:t.replace(h+'\n\n',h+'\n\n'+current+'\n\nPrevious2 October checkpoint follows; its missing-slot/raw4 statements are superseded by the continuation above.\n\n',1))

section=f'''## 2 October: native level4 prerequisites and Room41 certainty continuation

| Change | Result | Record |
| --- | --- | --- |
| Native slot-use/level4 source recipes | Loaded native egg, dye and reverse-gold actions execute034 for slots0/2/1; both final gates execute029(4),9/9; randomized/source-path estimates retained | [recipes](../.verification/native-prerequisites-20261002/level4-validation.md) |
| Room41 certainty localization | Raw4 source route replaces direct5/6 shortcuts; native15/14 remains unresolved, same-setup direct633/204 override15/15 completes43;022 is Conditional before slot4 selection; no code exception/catalog promotion | [evidence]({link}) |
| Preserved recipes/fresh regressions | 096 source override and all four20 combinations unchanged; Core289;363 roots/fingerprints/memberships/sequences unchanged; Blockers0; Release clean | [checks](../.verification/native-prerequisites-20261002/check-summary.json) |

'''
edit('CHANGELOG.md',lambda t:t.replace('## 2 October: source progression',section+'## 2 October: source progression',1))

ordered='''Ordered next work:

1. Inspect git/preserve unrelated work. Read .verification/native-prerequisites-20261002/validation.md, level4-validation.md and compact summaries, then the previous2 October source-progression record. New tasks are committed separately; identify their exact hashes from git log. Production implementation8d12d0f,096 recipe b17c53d and20 recipes f18a18f remain unchanged.334 starts/168 of235 sites/50 outstanding owners/123 unresolved edges retain every fresh gap.

2. Room41 native certainty remains priority. Reuse room41-level4.json/room41-level4-skip.json from the NEW packet: native egg/047 override→dye207→reverse-gold808 executes raw4→10/295/052→native325→37/603 actual41 arrival→delayed632look/active633→walk point/aim→loaded slot4 input. Both15/14 remain41 after3000; same-setup room41-level4-direct.json directly invokes633/41 + actual204 Escape, completes43,15/15. Keep direct control separate. Older raw5/6 native12/11/direct12/12 controls freshly repeat.022 already calls023(2/3/4) Conditional while rejecting earlier randomized candidates;023(0/1) starts Reached.03B response4241 itself is known, but inherited path/index makes065/026 response estimated. Mask-only learned-bit precision cannot cure022 path provenance. Preserve randomized notes, earlier branch provenance and generic certainty; never clear estimates or add room/script exceptions.

3. Native source-executed level4 is now established in the seeded explorer, with estimates retained. level4-egg-first.json (egg→dye→reverse gold) and level4-dye-last.json (egg→reverse gold→dye) both end9/9 in62/06, raw4 estimated, D6F0. Slots0/1/2 packed7282/7400/7200 carry learned2000/used4000. Final gate62/local205+002C→029(4,5) or06/local200+0111→029(4,3), Conditional. Forward808 alone does not mark use; reverse808/3D does. The clean046 trial must precede estimated casts in these recipes: opposite-order control leaves046 open6/5 and misses047 override. Starting rooms/direct teaching calls remain exploration choices, not full-game route proof. No status edits, browsing floors, injected flags/notes/placements or known-state writes.

4. Native intro opening continuation remains unresolved. stopAtBootInput:true, native challenge201 expressions and203/205 welcome execute at seed0; source-progression-20261002/boot.json still ends69/5000 under seeded/timer/pointer provenance. Default challenge bypass remains labelled. No native opening success claimed. Investigate source certainty rather than manufacture boot gates.

5. Completed096/20 recipes freshly repeated and must remain preserved. New preserved-recipes.json contains exact earlier recipes.095 actual2-frame/Escape override0720 writes knownD7B/DA3→45/staff656→655/092→43/639→096 ends00E7/loads46 at00EF,146frames,4/4. Full095 control43,4/3.16/350response47 sleep117frames writesD8F1;18/369 or371 look settle20 at888/967 with source ego;390/391response48 all finish7/7/return18. Do not remove literal-root gaps or change prior086/087 sequence proof.

6. Later: Room13 watcher/firewood native prerequisites and matching ScummVM02 mountain-mask frames remain. Keep class24/previous-room/box starts source-driven; no guessed placement or pixel shifts.

'''
def handoff(t):
    marker='State at checkpoint (2 October 2026):\n\n'
    t=t.replace(marker,marker+'* '+current+'\n* User clarified no app should be running and authorized stopping any stray SCUMMexplorer instance; this continuation found none. No push. No new WPF/RoomAudit/pixel run claimed for this recipes/docs-only task; earlier UI/pixel checks stay dated.\n\nPrevious2 October checkpoint:\n\n',1)
    start=t.index('Ordered next work:');end=t.index('Authoritative earlier completed work:',start)
    t=t[:start]+ordered+t[end:]
    t=t.replace('Final main Release build; never stop user app.', 'Final main Release build. User now authorizes stopping stray SCUMMexplorer instances; the continuation process check found none.')
    start=t.index('Next-session request:')
    t=t[:start]+'''Next-session request:

Continue native LOOM after the2 October native-prerequisite continuation (git log). Read .verification/native-prerequisites-20261002/validation.md and level4-validation.md, then previous source-progression guidance. Source native egg/dye/reverse-gold routes now execute raw4 with estimates retained,9/9. Prioritize native Room41/090 certainty: new raw4-native route remains15/14, same-setup direct633/204 override15/15 in43;022 becomes Conditional before slot4 selection. Preserve randomized notes/path provenance, completed096/20 recipes and status/costume/scythe fixes. No estimate clearing, room hacks, known-state/flag/placement injection, status-edit prerequisites, browsing floors or catalog promotion. Native challenge opening remains unresolved. Fresh regressions, main Release rebuild, separate task commits and updated handoff; no push. User says no app should be running and authorizes stopping stray SCUMMexplorer instances. Session token-usage tool unavailable. Finish with clickable continuation chip; no separate chat.
'''
    return t
edit('NEXT_SESSION_PROMPT.md',handoff)
print('Updated plan, script map, discovery, changelog and handoff; historical evidence retained.')
