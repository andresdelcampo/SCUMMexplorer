# Next session

Continue SCUMMexplorer in this chat or a fresh session. Read CLAUDE.md, docs/CURRENT_WORK.md (rules and open decisions), docs/LOOM_EGA_PLAN.md "Current follow-up" and docs/READABLE_SCRIPTS_PLAN.md. LOOM Spanish DOS EGA remains the focus. Finished work is in [CHANGELOG.md](CHANGELOG.md); older hand-off sections, validation pointers and parked ideas are in [HANDOFF_HISTORY_2026-10.md](HANDOFF_HISTORY_2026-10.md).

## Current task: readable scripts, steps 4-6

Steps 1-3 (glossary, names in the views, Readable/Raw script view) and the draft verb check are done. Next, per [READABLE_SCRIPTS_PLAN.md](READABLE_SCRIPTS_PLAN.md):
4. A timeline for "Last action" built from the session's events (raw trace stays below).
5. Assumptions grouped in plain language (arrived by a shortcut, an estimate, an effect not drawn).
6. "Used by" on scripts and objects (relationship index behind "Explore links").

## Open items

- Glossary growth: story bits other than the named ones show as `bit[0x...]` (`LoomGlossary.StoryBitLabel` is not wired to the catalog scene labels yet). Actor names are context-dependent (numbers are reused by scene); extend the scoped names only with evidence.
- Presenter phrases still missing: `roomOps`, `saveLoad`, `doSentence`, strings (they keep their raw line).
- Silence (0x4E/0x4F) and Shaping (0x50/0x51) draft names follow the manual order (no object answers 0x4E/0x50); Sharpening's two verbs are not told apart.
- `.verification/WindowCheck` still looks for the removed "Show exits" checkbox; update or retire it.
- Needing the user: on-screen check of the staff scenes (Room 43), El Muerto and Room 17 sizes; Room 34 local200 +0035 (probably dead code, needs a real ScummVM save); actor 4 placement; costume leftovers need a story order the data does not give.

## Standing rules

- Name only what the script map, the game's text or play supports, and show the evidence level. Estimates are labelled; no invented story flags or one-off teleports.
- The user's state rules (docs/CURRENT_WORK.md): rooms start in the earliest game state; a late state only follows a late cutscene the user triggered; the user controls the state (Game Status) and the app raises it only to the minimum an action needs. Technical views (traces, assumptions, raw bytecode) are Advanced-only; one short subtitle per window.
- Test sparingly: one targeted harness check or filtered test per change (UiShots, SceneShots, StatusShots), never the whole suite routinely. Commit each finished task separately, no push. Ask before editing CLAUDE.md.
- Another session may work in the same tree: stage files by explicit path, never `git add -A`, and leave other uncommitted files alone.
- Tooling: in this environment backslash escapes in bash heredocs are altered; write scripts and C# with backslashes using the Write tool or build them with `chr(92)`. `.verification/**/*.txt` is git-ignored (use `git add -f` for golden files).

> Ready-to-copy prompt: Continue SCUMMexplorer from the latest commit using docs/NEXT_SESSION_PROMPT.md, docs/READABLE_SCRIPTS_PLAN.md and docs/CURRENT_WORK.md. Build step 4 (a timeline for Last action from the session events), step 5 (assumptions grouped in plain language) and step 6 ("Used by" on scripts and objects). Name only what the script map, the game's text or play supports; technical views stay Advanced-only. Verify with the UiShots harness, not the whole suite; commit each step, no push. Ask before editing CLAUDE.md.
