# Keys as game input (28 September 2026)

## What ScummVM does (ScummVM `input.cpp` `processKeyboard`, `verbs.cpp` `checkExecVerbs`, `vars.cpp` v3)

- `processKeyboard` turns a key into `_mouseAndKeyboardStat`: printable characters as their ASCII code, F1–F9 as 315–323 (F5 = 0x13F … F8 = 0x142), Ctrl+A–Z as 1–26 (Ctrl+V = 0x16). Before that it keeps some keys for the engine: the restart key (`VAR_RESTART_KEY`), the pause key (`VAR_PAUSE_KEY`), '.' (talk stop; v3 has no `VAR_TALKSTOP_KEY`, so it is always on), and Escape, which runs `abortCutscene` and then passes `VAR(VAR_CUTSCENEEXIT_KEY)` on to the scripts.
- v3 variable numbers: `VAR_CUTSCENEEXIT_KEY` = 24 (`var[0x018]`), `VAR_CHARINC` = 37 (`var[0x025]`), `VAR_RESTART_KEY` = 42 (`var[0x02A]`), `VAR_PAUSE_KEY` = 43 (`var[0x02B]`). The boot sets them to 0x1B, 0x142 (F8) and 0x20 (space) (script 001 +0x012C, +0x0131, +0x0136, decoded). Script 004 clears 24 and 43 on the save screen; Room 70's exit sets them back (decoded).
- `checkExecVerbs` with `_userPut` > 0: a verb that is on whose `key` matches is clicked (`runInputScript(1, verb, 1)`); otherwise `runInputScript(4, key, 1)`. No decoded `verbOps` in the release sets a key, so the first path never applies to LOOM EGA (checked over `.verification/fallback-evidence/code.tsv`).

## Script 005 area 4 (decoded)

| Key | Offset | Effect |
| --- | --- | --- |
| q w e r t y u i (0x71 …) | +0x00B4–0x0129 | notes 1–8 → script 010 at +0x0418 |
| c d e f g a b C (0x63 …, 'C' = 0x43) | +0x012C–0x01A1 | notes 1–8 → script 010 ('e' is caught by the first row; both give 3) |
| Escape (0x1B) | +0x01A4 | only outside a cutscene (`bit[0xD6E]` = 0): `var[0x067]` = 0, script 008 [0] (close-up off); then, **only while `bit[0xD66]` is set**, a cutscene waits for `bit[0xD67]` to clear and runs scripts 028 and 021. The script map's earlier reading ("when `bit[0xD66]` is 0") had the `notEqualZero` branch inverted. |
| '<' (0x3C), '>' (0x3E) | +0x01DF, +0x01F6 | `var[0x0A5]` − 1 / + 1 (0–9), then `var[0x025]` (VAR_CHARINC) = 9 − `var[0x0A5]`, and prints the speed |
| F5 (0x13F) | +0x0237 | `bit[0xD6F]` set: script 043 (the demo's "cannot save" line); else script 004 (reading: the save screen; it loads Room 70 inside a cutscene and makes Room 70 local 200 the input script) |
| F6 (0x140) | +0x024F | `var[0x0A2]` 0 → 1 (music off) → 2 (music and effects off) → 0, stopping `var[0x0A4]`'s sound |
| F7 (0x141) | +0x0313 | unless `bit[0xD76]` is set ("invalid function"), toggles `bit[0xD62]` (full or fast animation) |
| F8 (0x142) | +0x03BD | `systemOps restart` (the engine takes F8 first as the restart key) |
| Ctrl+V (0x16) | +0x03C9 | prints the version line, waits for it, toggles `var[0x0A8]` between 0x16 and 0 (0x68 → 0x16; reading: purpose unknown) |

Reading (from the decoded code): script 020 sets `bit[0xD66]` while it plays the recorded draft back through script 031 (which holds `bit[0xD67]` while a note sounds) and times the next note with `var[0x00B]`; script 021 stops script 020, clears `bit[0xD65]`, `bit[0xD66]` and `var[0x062]`; script 028 clears the draft buffer `var[0x05A..0x05D]` and `var[0x063]`, and turns verb 0x35 off in practice mode. So Escape cancels a draft being played.

## What the session does

`LoomExplorationSession.PressKey(key)` (with `DescribeKey` for labels): refuses, with a label, the restart key and the pause key (as the boot set them), '.', Escape while a cutscene is open (`abortCutscene` is not modelled), user input off, and no input script. Otherwise it runs `var[0x020]`'s script with `[4, key, 1]` as assumed input and dispatches loads as clicks do. The app's "Keys as game input" (off by default, interactive mode, LOOM only) sends keys typed in the window, except in a text box: Escape, space, F1–F9 and Ctrl+letters on key down, printable characters as text.

## Checks

- Tests (`LoomKeyInputTests`, 4 new; 195/195 pass): after script 018 [2] and a room click on Room 02 object 156, 'e' is recorded as note 3 in `var[0x056]`, 'c' as note 1 in `var[0x057]` (`var[0x062]` = 2), `bit[0xD66]` = 1; Escape then runs script 005's cutscene (+0x01C0) and leaves `var[0x067]`, `var[0x062]` and `bit[0xD66]` at 0. '>' and '<' set `var[0x0A5]`/`var[0x025]` to 1/8 and 0/9; F6 cycles `var[0x0A2]` 1, 2, 0. F5 runs script 004 and reaches Room 70 within 12 frames. F8, space and '.' are refused with labels and do not run script 5; with input off (script 002) a key is refused.
- KeyInputCheck 10/10 ([report](wpf-run1/KeyInputCheck/key-input-check.txt)) raises the real window's PreviewKeyDown/PreviewTextInput: off by default and nothing sent while off; '>' and F6; F8 and space refused, and space does not toggle the focused check box; 'e' recorded after the aim; Escape cancels the draft; F5 opens Room 70 in the app. The text-box exclusion was not exercised (the trace boxes are collapsed and cannot take focus; logged as INFO).
- Every other WPF check passes (11 in `wpf-checks.ps1` now). RoomAudit: 0 gaps, 0 changed renders. Blockers unchanged ([after](blockers-after.txt), 5994 over 136 reasons): it sends no input.
