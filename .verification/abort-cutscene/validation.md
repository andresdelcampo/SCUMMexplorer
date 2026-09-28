# Escape's abortCutscene (28 September 2026)

## ScummVM behaviour (`script.cpp`, `input.cpp`)

- `beginOverride` records, for the open cutscene depth (`vm.cutSceneStackPointer`), the running script and the offset of the jump that follows it (`vm.cutScenePtr`, `vm.cutSceneScript`), then steps over the jump. `endOverride` clears both. `beginCutscene` clears the pointer for the new depth, and `endCutscene` clears it before popping.
- `processKeyboard`: Escape, while `VAR_CUTSCENEEXIT_KEY` (v3 var 24, `var[0x018]`, 0x1B after the boot) is nonzero, runs `abortCutscene`. It then sets `_mouseAndKeyboardStat` to that variable's value, so the key goes on to `checkExecVerbs`, which reads it only while `_userPut` > 0.
- `abortCutscene`: when the open depth has a pointer, that script slot continues at the recorded offset (the skip jump), running, with `freezeCount` = 0; `VAR_OVERRIDE` (`var[0x005]`) = 1; the pointer is cleared. With no pointer it does nothing.

## Model

- The executor tells the session about `beginOverride` (the jump's offset) and `endOverride` through `LoomScriptRuntimeState.OverrideChanged`. The session keeps one override per cutscene depth (0 = none open), tied to the running script's slot.
- `PressKey(0x1B)` while `var[0x018]` ≠ 0 runs `AbortCutscene`: the pending run of the slot that set the override resumes at the skip jump on the next frame, unfrozen, with `var[0x005]` = 1. The key then goes on as `var[0x018]`'s value; inside a cutscene script 002 has turned user input off, so it stops there with the label "the key did not reach the input script after abortCutscene, because user input is off". Outside a cutscene with no override, abort does nothing and Escape reaches script 005 as before.
- It is assumed input, like every key. Not modelled: `cutsceneOverride` counting (only used by ScummVM for error checks) and an override whose script is not waiting for a later frame (labelled, nothing changes).
- The earlier refusal ("Escape during a cutscene … abortCutscene is not modelled") is gone.

## Evidence: script 052 (Room 10 object 295 → Room 11 → Room 13)

Script 052: `cutscene` at +0x0000, `loadRoom 11` at +0x0045, `beginOverride` at +0x00C6 with its jump at +0x00C8 → +0x0157. At +0x0157, `notEqualZero var[0x005]` continues only when the override is set (the disassembly's branch is taken when the condition is false): it stops Room 11 locals 210–213 and removes actors 3–6, then `endCutscene` (+0x017C) and `loadRoomWithEgo 324, 13, walkTo=(280, 120)`.

Probe (VisitProbe, `s:34` starts script 052 through the test seam):

- `10 s:34 +6 k:0x1B`: still in Room 10, before `beginOverride`: "the open cutscene has no override set (beginOverride), so it does nothing"; `var[0x005]` stays 0.
- `10 s:34 +40 k:0x1B +60`: in Room 11 after +0x00C6: `abortCutscene: Script 52 continues at its skip jump +0x00C8, var[0x005] = 1`. On the next frames: `stopScript 210..213 at Script 52 +0x015C..+0x0162`, `endCutscene at Script 52 +0x017C runs script 3`, then Room 13 with the ego at (280, 117), box 3 (walk end, estimate); `var[0x005]` is 0 again (`endCutscene`).

Test: `LoomKeyInputTests.EscapeSkipsScript052ToItsOverrideJump` checks both cases.

## Other change

A room entered after a key said "The click went through the game's input scripts (005, 006, 007)". It now says "The explorer's input (assumed) went on through the game's own scripts". The app's click-only note is unchanged.

## Verification

- Tests: 197/197.
- Blockers (`blockers-after.txt` against `../dynamic-scripts/blockers-after.txt`): unchanged, 5912 over 135 reasons (Blockers sends no keys).
- Room audit: 0 gaps in 76 rooms; `compare-png.ps1`: 0 changed renders.
- WPF checks (`wpf-run1`): ClickInputCheck 11/11, DoorwayClickCheck 75/75, Room03ActionCheck 46, PointerInputCheck 13/13, ReverseHoverCheck 22/22, HighlightCheck 19/19, MusicRowCheck 5/5, CameraPanCheck 11/11, VerbAreaCheck 15/15, KeyInputCheck 10/10, PresentationCheck 39/39.
