from pathlib import Path

base=Path('docs')
record='../.verification/archive/source-progression-20261002/validation.md'
current=f'''2 October source progression: generic old-v3 expressions and an opt-in native intro input mode are implemented (`8d12d0f`), without changing default UI/boot behavior. Actual37/603 arrival, delayed native633 and loaded slot4 input still reach090 Conditional; source204 Escape does not cure native12/11. A separate direct633/41 + actual204 override completes090 in43,12/12. Source095 Escape establishes known D7B/DA3 and completes096 in46 (146 frames,4/4; `b17c53d`). Actual16/350 sleep establishes D8F; both18/369 and371 arrivals settle20 and both390/391 responses finish,7/7 (`f18a18f`). Native challenge expressions/glyph input execute203/205, but opening certainty remains unresolved. D6F is the failed-answer flag and level4 requires0, plus used slots0/2/1; native egg input/047 skip establishes slot0 use but not1/2 or raw4. No explicit edits/browsing floors/known-state injections or catalog promotion. Fresh Core289/WPF62/doorways16/controls19/input18;363 roots, memberships, sequences and all76 current pixels unchanged; zero gaps, Release clean. [Evidence and reproduction]({record}).'''

def edit(name,change):
    path=base/name
    text=path.read_text(encoding='utf-8')
    updated=change(text)
    assert updated!=text,name
    path.write_text(updated,encoding='utf-8')

edit('LOOM_EGA_PLAN.md',lambda t:t.replace('## Current follow-up (1 October)\n\n','## Current follow-up (2 October)\n\n'+current+'\n\nThe following1 October checkpoints are historical; current source results above supersede their remaining-route descriptions.\n\n',1))
edit('LOOM_CUTSCENE_DISCOVERY.md',lambda t:t.replace('## Current replay coverage (1 October)\n\n','## Current replay coverage (2 October)\n\n'+current+'\n\nThese longer source recipes do not change fresh-entry coverage. Earlier dated checkpoints follow.\n\n',1))
def scriptmap(t):
    t=t.replace('## Follow-up findings (30 September)\n\n','## Follow-up findings (30 September)\n\n'+current+'\n\n',1)
    t=t.replace('`expression` in some forms (3 in Blockers).','nested `expression` opcodes other than the supported `getRandomNr` form. Ordinary integer postfix expressions and native201 challenge expressions are supported since2 October; seeded provenance is retained.')
    t=t.replace('the Room 69 difficulty and draft answer, the boot\'s seeded draft table','the default Room69 difficulty/draft answer (the opt-in native intro can now execute actual source input, with estimated continuation retained), the boot\'s seeded draft table')
    return t
edit('LOOM_SCRIPT_MAP.md',scriptmap)
section=f'''## 2 October: source progression

| Change | Result | Record |
| --- | --- | --- |
| Generic expressions/native intro input | Signed integer postfix stack and nested seeded random scratch semantics; native201 challenge now executes; optional input-awaiting boot preserves default UI; Core289/WPF62, all roots/sequences/pixels unchanged | [implementation](../.verification/archive/source-progression-20261002/expression-validation.md) |
| Source096 completion | Actual095 Escape override writes known flags; staff/092/639 route completes096 in46,4/4; ordinary full095 control remains incomplete | [recipe](../.verification/archive/source-progression-20261002/late096-validation.md) |
| Usable20 arrivals/both responses | Actual16 sleep writes D8F, both18 arrivals settle20 with source ego,390/391 finish in all four combinations,7/7; fresh catalog20 gaps retained | [recipes](../.verification/archive/source-progression-20261002/room20-validation.md) |
| Room41/native level4 audit | Actual37 arrival/native633 still inherits Conditional090; direct633+204 override completes43 separately; correct D6F=0 and used-slot gates recorded; native challenge and egg input advance without native opening/raw4 claims | [source evidence]({record}) |

'''
edit('CHANGELOG.md',lambda t:t.replace('## 1 October: global cutscene access',section+'## 1 October: global cutscene access',1))

ordered='''Ordered next work:

1. Inspect git/preserve unrelated work. Read .verification/archive/source-progression-20261002/validation.md and its expression/096/20 validations and compact summaries. The latest native code is8d12d0f;096 recipe b17c53d;20 recipes f18a18f. Current handoff/evidence commit is in latest git log.334 starts/168 of235 sites/50 outstanding owners/123 unresolved edges stay unchanged. No promotion was attempted.

2. Prioritize actual Room41 casting certainty. Reuse native41.json/native41-skip.json:9/271use→10/295use/052→native325 learning→direct13/323 and28/470 for raw5/6→catalog37/direct603use source arrival41→wait632/native look→active633→source walk point/aim→loaded slot4 note inputs.026 +0071/+007F already Conditional (response argument1 estimated);633/41 starts204 Conditional;090 opens and rejects possible42 at0005. Native12/11 remains after3000, including actual204 override. Separate direct41-skip.json completes090 +0730 in43,12/12 after1227 settling frames. Keep that direct-control success separate from native casting. Establish source prerequisites/provenance rather than clearing estimates or substituting status edits/browsing floors.

3. Native level4: D6F is the failed challenge flag; gates require0, not1. They additionally require used bit4000 in slots0,2,1 (023/034; learned2000 is separate). level4-skip.json executes08/255use/046, native egg270 walk/look/aim and loaded slot0 keyboard draft, waits actual047 override22 frames then Escape. It reaches270/3A +0006/+0011/+0017 (023(0)/034(0)/023(2)), but slots1/2 use remains missing and raw3 stays. Full047 changes rooms and ends the frozen room-owned270 caller. Establish additional learning/use through source06/local200,08/local217 and slot1 owners/input; never inject flags or drafts.

4. Native intro is now available with stopAtBootInput:true, without CompleteBoot's assumed answer. boot.json clicks876 and generated glyphs0..11 (886..897);201 expressions generate0,4,9,1 at seed0;203/205 welcome and D6B=1 execute, D6F=0. Seed/timer/pointer provenance leaves001/044 Conditional and final69 at5000; a reached opening from native challenge is unestablished. Keep the default challenge-bypassing app boot labelled; do not treat either preview as original-game proof.

5. Completed096/20 recipes must be preserved. late.json: source43/639→44/653look→2frames/Escape095 actual override0720→45/staff656→655use/092→43/639→096 ends00E7 then loads46 at00EF,146frames,4/4. Full095 control still4/3. room20.json:16/350response47 sleep117frames writesD8F1;catalog18/369look062 or371look056 settle20 at888/967;require20 then390/391response48 finishes206/getActorX002D/end00A9 and return18,7/7. All four combinations pass. Source placements/flags are executed, not injected. These longer recipes do not remove fresh roots:18/370look065218frames1/0;20/390 and39178frames1/0 with missing ego;38/610look0873000frames1/0. Existing086/087 longer recipe remains distinct.

6. Later: Room13 watcher/firewood native source prerequisites and matching ScummVM02 mountain-mask frames remain. Keep class24/previous-room/box starts source-driven; no guessed placement or pixel shifts. Earlier1 October packets retain early misses and controls; read superseding2 October evidence first.

'''
def handoff(t):
    t=t.replace('State at checkpoint (1 October 2026):','State at checkpoint (2 October 2026):\n\n* '+current+'\n* Main Release rebuilt at the main cwd with zero warnings/errors; all reports hashed in .verification/archive/source-progression-20261002/check-summary.json. User app untouched; no push. Completed status/costumes/scythe and unrelated untracked work preserved.\n\nHistorical1 October checkpoint notes follow:',1)
    start=t.index('Ordered next work:')
    end=t.index('Authoritative earlier completed work:',start)
    t=t[:start]+ordered+t[end:]
    marker='Next-session request:'
    start=t.index(marker)
    t=t[:start]+'''Next-session request:

Continue after2 October generic expressions/native intro8d12d0f, source096 b17c53d, usable20 f18a18f and the latest Room41/level4 handoff/evidence commit (git log). Read .verification/archive/source-progression-20261002/validation.md and referenced records. Prioritize native Room41/090 inherited draft certainty, source-established slots1/2 use for raw4, and the native challenge's opening continuation. Preserve completed096/20 source recipes and status/costume/scythe fixes. No flags/placements/known-state injection, status-edit prerequisites or catalog browsing policy in source progression.334 replay starts retain all fresh gaps/123 unresolved edges. Fresh regressions, main Release rebuild, separate task commits and updated handoff; no push or user-app stop. Session token-usage tool unavailable. Finish with continuation chip; no separate chat.
'''
    return t
edit('NEXT_SESSION_PROMPT.md',handoff)
print('Updated plan, source map, discovery, changelog and handoff.')
