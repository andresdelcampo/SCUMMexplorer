# startScript with a local target (28 September 2026)

## What was blocked

Blockers counted 80 stops labelled "A started script has a dynamic target; the parent continues without its effects." `LoomScriptEvent.FromInstruction` only parses a literal script number, so a `startScript` whose number is a variable or local had no target and the session skipped it.

Every such site in the data (`code.tsv`, `startScript` with a `var`, `local` or `bit` target) has a local target:

| Site | Target | Value |
| --- | --- | --- |
| Script 044 +0x01C8, +0x0215 (the boot's opening cutscene) | `local[0x001]` | 0xC8..0xD0, a literal loop (+0x01B5 `move local[0x001] = 0x00C8`, +0x01D9 `isLess local[0x001], 0x00D1`) |
| Room 02 local 203 +0x0019 (the ending) | `local[0x000]` | 0xCD..0xDD, a literal loop |
| Room 10 local 205 +0x0015 | `local[0x000]` | its argument: objects 297–300 verb 0x36 start it with 0xCE..0xD1 |
| Room 23 local 210 +0x0107 | `local[0x001]` | its argument: objects 422–425 verb 0x49 start it with 0xD7..0xDA |

## Change

ScummVM `o5_startScript`, `o5_chainScript` and `o5_stopScript` read the script number with `getVarOrDirectByte`, so the value is whatever the variable holds when the instruction runs. The executor (`LoomBoundedScriptExecutor`) now fills the event's target from the known variables for `startScript`, `chainScript` and `stopScript`, as it already did for sounds. A target that reads a seeded pick makes the event a possible outcome (`Conditional`). A `stopScript` whose value is 0 (which would stop the current script) is left unresolved. A target that reads an unknown variable still stops with the old label.

## Result

- Blockers (`blockers-after.txt` against `../key-input/blockers-after.txt`): 5994 → 5912 over 136 → 135 reasons. "A started script has a dynamic target" 80 → 0; "presentation operation not applied: animateActor" 417 → 415. No other line changed: the newly started scripts (Room 01 locals 200–208) contain only `putActorInRoom`, `putActor`, `animateActor` and a frame loop.
- The boot now starts Room 01 locals 200–208 from script 044, twice (the second loop restarts each copy: "script 208 started again: runScript ended its running copy first"), and script 044 +0x041B stops local 200's pending run. The boot still ends in Room 02 with the ego (actor 1, which local 200 puts in and takes out of Room 01) at (828, 60), box 0, camera 800 (VisitProbe `02`).
- Tests: 196/196 (new: `LoomBootTests.OpeningCutsceneStartsRoom01LocalsThroughALocalTarget`).
- Room audit: 0 gaps in 76 rooms; `compare-png.ps1`: 0 changed renders.
- WPF checks (`wpf-run1`): ClickInputCheck 11/11, DoorwayClickCheck 75/75, Room03ActionCheck 46, PointerInputCheck 13/13, ReverseHoverCheck 22/22, HighlightCheck 19/19, MusicRowCheck 5/5, CameraPanCheck 11/11, VerbAreaCheck 15/15, KeyInputCheck 10/10, PresentationCheck 39/39.

## Script findings (recorded in the script map)

- Room 01 locals 200–208: each puts actor n (1–9) in Room 01 at the argument point, `animateActor n, 6`, waits 11 frames, then `putActorInRoom n, 0`. Decoded; reading: the intro's flashing points of light.
- Room 10 local 205: inside a cutscene, starts the given local (0xCE..0xD1) with the draft's first note (`var[0x052]`), waits for it, then starts each of the other three locals in order with the next notes (`var[0x053..]`); the `isNotEqual local[0x000], …` tests skip the one already played. Decoded; reading for the role.
- Room 23 local 210: objects 422–425 pass their own local (0xD7..0xDA) to start at +0x0107. Decoded.
