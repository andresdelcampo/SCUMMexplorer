"""Record the completed input route without rewriting historical outcomes."""
from pathlib import Path

link = '../.verification/room34-sequences-20261002/validation.md'
entries = {
    'docs/NEXT_SESSION_PROMPT.md': f'''## Latest: Room34 input progression verified; select remaining work by practical coverage

After7ddda08, the user delegates route selection by highest legitimate coverage value and prioritizes **interactive triggers**, since scenes generally already play. Staff, sufficient notes and learned spell are the established setup for casting; use supported Game Status edits as explicitly labelled exploration choices when appropriate. Do not ask those prerequisites again or use a direct preview as proof of keyboard dispatch.

Fresh paired Room34 runs: five note-key casts execute561/44,4C,44,44,44 via actual007 +002E; complete9/9 scopes and the same18 boundaries as direct responses.127 recorded local201/local202/global085 events differ only in retained Certainty (direct Reached, keyboard Conditional); every event Executed. Four newly qualified sites:201 +0046/+0134/+0197 and202 +0000. Direct2199frames, keyboard2454; final34,D1/D98/DCC=2/1/1. No production fix. [Validation]({link}), [summary](../.verification/room34-sequences-20261002/summary.json).

Keep the metrics distinct: fresh literal169/235=71.91%,66 sites/49 owners and335 catalog starts are unchanged. The explicit union of qualified literal previews, two established sequences and this input route rises**172→176/235=74.89%**,59 remaining sites. This is bounded explorer execution coverage, not native full-game certainty. No catalog promotion, fabricated prerequisite, cleared estimate, observer expansion or historical rewrite. All57 protected files exact/unstaged; Core/catalog unchanged; no App launch/close/interaction or push. Fresh Core-only probe/checker pass; Core662/broad audits/main Release remain dated.

Next highest-value candidate: Room25's six-site cluster, using source walk-box transitions and actual repeated object clicks before any required spell inputs. Keep Room34's successful pair, sphere and Sharpening tasks complete. Room34's remaining local207 requires D9D from087; its camera watcher requires box6→8/camera conditions. Use existing tools, two bounded diagnostic cycles at most without a route-relevant implementation decision, commit completed tasks separately, and report working input routes before regression counts. A new UI test still requires authorization; leave the user's running App untouched.

Ready-to-copy continuation: Continue SCUMMexplorer from the Room34 interactive-input checkpoint using the latest handoff. Prioritize remaining actual game-input routes by expected legitimate coverage gain; start with Room25's six-site cluster. Staff/notes/learned spell setup is established; use supported status edits with explicit provenance when needed. Preserve every constraint, historical capture and protected57 file, no App launch/close/interaction or push. Current audited union176/235 (74.89%), fresh-literal169/235 (71.91%); do not conflate them or repeat completed Room34/sphere/Sharpening work.

''',
    'docs/CHANGELOG.md': f'''## 2 October: verify Room34 spells through game input

Five keyboard casts, with staff/six notes/learned slots5 and10, trigger the same nine completed scopes as direct responses. Four new qualified source sites;127 scene events differ only in retained certainty labels. No production fix or catalog change. Audited site union172→176/235 (74.89%); fresh single-action coverage stays169/235 (71.91%). All57 protected files exact/unstaged; no App interaction or push. [Input evidence and coverage definition]({link}).

''',
    'docs/LOOM_EGA_PLAN.md': f'''**Current priority: verify interactive triggers by legitimate coverage gain (user delegation).** Room34 complete: five note-key casts with supported staff/notes/learned setup match direct scene boundaries and recorded body events except retained certainty. Four new sites; audited union176/235=74.89%, fresh literal169/235 unchanged. No execution fix. Next: Room25's six-site cluster through actual clicks/source walking; do not repeat successful direct scene playback as a substitute. Existing bounds, estimates, protected57, no App interaction/push remain. [Validation]({link}).

''',
    'docs/LOOM_SCRIPT_MAP.md': f'''Latest Room34 input evidence,2 October: supported staff/six notes/learned5+10, source notes/reverse flags retained. Note keys actually dispatch561/44,4C,44,44,44 at007 +002E. Local201 +0046/+0134/+0197 and202 +0000 complete;9/9 scopes, final34, D1/D98/DCC=2/1/1.127 recorded scene-body events match direct responses except retained Reached/Conditional provenance. No source certainty or full-game claim. [Paired evidence]({link}).

''',
    'docs/LOOM_CUTSCENE_DISCOVERY.md': f'''**Interactive evidence supplement (2 October):** Room34 adds four completed source sites through actual note-key dispatch with supported staff/notes/learned setup. Qualified literal+existing-sequence+new-input site union is176/235=74.89% (previous172);59 remain. The fresh single-action metric remains169/235=71.91%,66 literal gaps/49 owners and335 catalog starts, all unchanged. Input completion is not a new fresh replay recipe or native progression proof. [Definition, full comparison and remaining-site ledger]({link}).

'''
}
for name, text in entries.items():
    p = Path(name)
    old = p.read_text(encoding='utf-8')
    if name.endswith('LOOM_EGA_PLAN.md'):
        anchor = '## Current follow-up (2 October)\n\n'
    elif name.endswith('LOOM_CUTSCENE_DISCOVERY.md'):
        anchor = '## Current replay coverage (2 October)\n\n'
    else:
        anchor = old.split('\n\n', 1)[0] + '\n\n'
    assert anchor in old
    p.write_text(old.replace(anchor, anchor+text, 1),encoding='utf-8')
print('Updated five current handoff documents; earlier outcomes retained.')
