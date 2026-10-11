# Documentation

## Engine references

- [SCUMM script reference](engine/SCUMM_SCRIPT_MAP.md): engine variables and script behavior, with explicit SCUMM version scope.
- [Object states](engine/SCUMM_OBJECT_STATES.md): object state, visibility and rendering behavior.
- [Research](engine/RESEARCH.md): format references, tools and parser findings.

## Game references

- [LOOM script map](loom/LOOM_SCRIPT_MAP.md): decoded game variables, bits, scripts, verbs and rooms. EGA findings retain their edition and audit dates; CD/VGA roles require evidence from that edition's own scripts.
- [LOOM cutscene discovery](loom/LOOM_CUTSCENE_DISCOVERY.md): how the cutscene catalog is generated.
- [LOOM English/Spanish compatibility audit](loom/ENGLISH_COMPAT_2026-10-04.md): dated edition comparisons.
- [Indy3 conversations](indy3/INDY3_CONVERSATIONS_PLAN.md): conversation export and its source limits.

## Project references

- [Requirements](REQUIREMENTS.md): what the app must do, tagged by engine, version, game and edition scope.
- `archive/`: historical plans, audits and evidence records.

Put engine definitions in `engine/` and label the versions and formats they apply to. Put script IDs, story meanings, game-selected values and edition-specific findings in the relevant game folder (`loom/`, `indy3/`, or later games). Explorer implementation choices and dated validation must be identified separately from engine rules and decoded game facts.
