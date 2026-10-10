# Documentation

Updated 9 October 2026. CURRENT_WORK is the current plan and handoff; CHANGELOG holds completed work. References and dated evidence are separate.

- [CURRENT_WORK.md](CURRENT_WORK.md): current state, ordered tasks, rules and session handoff.
- [FINAL_GAPS_HANDOFF.md](loom/FINAL_GAPS_HANDOFF.md): exact original-game comparison recipes and evidence limits.
- [REQUIREMENTS.md](REQUIREMENTS.md): what the app must do, tagged by scope (SCUMM, SCUMM v3/v4, LOOM, LOOM EGA/CD), with exact title and script-view formats.
- [REQUIREMENTS_FINDINGS.md](archive/REQUIREMENTS_FINDINGS.md): faults, inconsistencies, risks and improvement ideas found while drafting the requirements (static reading, nothing run).
- [CHANGELOG.md](CHANGELOG.md): finished work, with links to its validation.
- [CLEANUP_REVIEW.md](archive/CLEANUP_REVIEW.md): historical cleanup review; other deletion proposals remain parked.

Subfolders:

- `loom/`: LOOM reference. [LOOM_SCRIPT_MAP.md](loom/LOOM_SCRIPT_MAP.md) (decoded scripts, variables, verbs, rooms), [LOOM_CUTSCENE_DISCOVERY.md](loom/LOOM_CUTSCENE_DISCOVERY.md) (how the cutscene catalog is generated), [ENGLISH_COMPAT_2026-10-04.md](loom/ENGLISH_COMPAT_2026-10-04.md) (historical English/Spanish migration audit).
- `indy3/`: implemented Indy EGA conversation export and its source limits. [INDY3_CONVERSATIONS_PLAN.md](indy3/INDY3_CONVERSATIONS_PLAN.md).
- `engine/`: generic SCUMM knowledge. [SCUMM_OBJECT_STATES.md](engine/SCUMM_OBJECT_STATES.md), [RESEARCH.md](engine/RESEARCH.md).
- `archive/`: finished plans, audits and dated history. Records, not current guidance.

Put new reference by game (`loom/`, `indy3/`, later others) or in `engine/` when it holds for every SCUMM game; keep current state in CURRENT_WORK and completed results in CHANGELOG.
