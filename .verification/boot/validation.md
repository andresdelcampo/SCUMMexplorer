# Validation: completing the boot past the intro (27 September 2026)

Status: **done.** Builds with 0 warnings; 156/156 tests pass. Audit: 0 gaps in 76 rooms; renders changed in Rooms 06, 38 and 62 (PNGs refreshed). DoorwayClickCheck: 75/75 ([report](doorway-clicks.txt)). Room03ActionCheck `162 4 167 38`: 46 PASS ([captures](room03-action/)). The boot takes 110 frames after script 001's first room load.

## What the decoded code shows

Script 001 (the boot, started by the engine):

| Offset | What it does |
| --- | --- |
| +0x0000–+0x014A | Screen, cursor, charset, system strings; `var[0x07B] = 1` (difficulty: standard) |
| +0x014F–+0x015B | `bit[0xD76]` if `var[0x028] > 250`; `bit[0xD62] = 1` (the "script 42 does not run in Room 03" bit) |
| +0x0160 | `loadRoom 0x45`: **Room 69**, the difficulty and draft-check screen |
| +0x0165–+0x0166 | Waits while `bit[0xD6B]` is 0 |
| +0x016B–+0x01B1 | Hooks and start state: `var[0x020] = 5`, `var[0x01D] = 14` (after-entry script), `var[0x01E] = 15` (before-exit script), `var[0x023] = 2` / `var[0x024] = 3` (cutscene start/end scripts), ego `var[0x001] = 1`, `var[0x077..079] = 1..3`, `var[0x0D5]`, `var[0x0C5]`, bits `0xD86`, `0xD89`, `0xD8A`, `0xD83` |
| +0x01B6–+0x0216 | Locks costumes 1–4, sounds and scripts 2–41 (no state) |
| +0x021D–+0x022A | `bit[0xD6E] = 1`; starts **script 044** (the opening cutscene) and waits while it runs |
| +0x022F–+0x0247 | `endOverride`, unfreeze, camera follows ego, `bit[0xD66] = 0`, `var[0x069] = 0`, `bit[0xD6E] = 0`, cursor and input on |
| +0x0249 | `chainScript 6`: the input loop takes the slot |

**How `bit[0xD6B]` gets set.** Room 69's entry clears it (+0x0029) and starts local 202 (the difficulty menu). Objects 876–878 (PRACTICA, ESTANDAR, EXPERTO) set `var[0x07B]` to 0/1/2 and start local 201. Local 201 builds a random draft into `var[0x052..0x055]` (`getRandomNr`), draws a guild symbol and chains to local 204 (the question). Objects 886–897 (the notes) start local 203 with the note index. Local 203 compares each note against the draft and counts wrong tries in `var[0x063]`. It starts local 205 after a correct draft (+0x0241, "Bienvenido a la Epoca de los Grandes Gremios.") and also after the third wrong one (+0x01D8, "Entrando en Modo Demostración."). **Local 205** clears the screen objects, stops locals 202 and 204, draws two boxes and sets `bit[0xD6B] = 1` at +0x0048. It is the only instruction in Room 69's code that sets the bit to 1. Global scripts 023 (+0x003E) and 034 (+0x0000) reuse the same bit later in play, and many rooms start them, but no Room 69 code and nothing on the boot route starts them while script 001 waits.

## What the session does

1. **Global scripts across loads** (from the [previous task](../cutscene-continuation/validation.md)) now also apply during the boot: script 001 enters Room 69 and carries on.
2. **Input assumption.** `CompleteBoot` finds, from decoded code and without room numbers in the code, that script 001 waits for a bit that exactly one local script of the current room sets, and that the room's own code starts that script (local 203 +0x01D8 and +0x0241). It starts local 205 as the player's answer would, and labels this: "Player input assumed: … Choices made on that screen were not simulated." The difficulty stays at script 001's default (standard), and `var[0x063]` stays 0 (no wrong tries).
3. **Frames** then run until script 001 has chained on and no cutscene is open (at most `BootFrameLimit` = 5000). Script 044 plays: Room 01 (+0x00C2), Room 02 (+0x023F), Room 01 (+0x0248), Room 02 (+0x027D), Room 53 (+0x03BA, "Levántate, Hijo de Cygna."), Room 02 (+0x0536). Then script 001 finishes and chains to script 006. **The session starts in Room 02.**
4. Pieces modelled for this:
   - `chainScript` (`o5_chainScript`): the current script ends and the target starts in its place with the arguments and its freeze resistance.
   - `drawBox` is presentation only.
   - **Camera:** `setCameraAt`/`panCameraTo` set `var[0x002]` (`VAR_CAMERA_POS_X`), clamped to half a screen from the room's edges. The pan arrives at once (labelled). Room entry sets it to 160. Script 044 +0x0361 waits for the camera to reach 0x2E8.
   - Repeated identical uncertainties within one step (visit, action, run of frames, boot) are kept once. A looping script had added one per frame (4 994 copies of the same label).
5. `completeBoot: false` keeps the old behaviour (stop at the Room 69 load); tests that need a bare session use it.

Trace: [probe-boot.txt](probe-boot.txt).

## Effects

- **Hooks are real now.** Cutscenes run scripts 002 (freeze, `bit[0xD6E] = 1`) and 003 (unfreeze, clear it). Every room entry runs script 014 after the room's entry code, and every exit runs script 015 first (it stops script 42, resets `bit[0xD7A]`, starts scripts 008 and 028).
- **Renders** ([audit before](room-audit-before.txt), [after](room-audit-after.txt)): each change follows from a bit script 001 sets after the intro wait.
  - Room 06: `bit[0xD86]` (+0x01A2) makes the entry draw 225–228 instead of 221–224 (entry +0x0063).
  - Room 38: `bit[0xD83]` (+0x01B1): 618 is set to 1 and 612 is drawn, instead of 614.
  - Room 62: `bit[0xD8A]` (+0x01AC): 812 instead of 814 (entry +0x0091).
  - Room 01: the intro passes through it, so 152 has a known state and needs **no stored-image guess. No room uses the first-stored-image guess any more.**
- Every room has one more pending script: script 039, the intro's sound loop, which waits while sound 25 plays (a labelled estimate, since sound length is not simulated). The music row therefore shows "Started: Sound 25 … from this session's route" in rooms without a cue of their own.
- **Blockers** ([before](blockers-before.txt), [after](blockers-after.txt)):
  - `chainScript` 10 → 0, `drawBox` 3 → 0; "room transition awaits" 141 → 66; `getActorWalkBox` 215 → 184.
  - Each of the 76 visits also carries the boot's three stops: `findObject` at script 006 +0x0096 (the input loop, with no mouse), `verbOps` at Room 01 local 211 +0x000C, and one dynamic `startScript` target. That is why `findObject` shows 76 and `verbOps` 127.
- WPF captures: [WindowCheck](window-check/window-check.txt) of Rooms 01, 02, 06, 38 and 62.

## Tests

`LoomBootTests`: the boot reaches Room 02 and sets the hooks and bits listed above, and it labels the input assumption; `completeBoot: false` stops at the intro load. `LoomCutsceneTests` now checks that the hooks come from the boot. Updated for the new start: the Room 04 pick test counts only the visit's picks; Room 01 has no estimate; Room 38 draws 612.

## Open

- **Input loop.** Script 006 stops at `findObject` on its first frame. Modelling it needs a mouse position (`var[0x014]`/`var[0x015]`) and ScummVM `findObject`; with no mouse it would idle. This matters for sentences (script 007) and hover (verb 0x37).
- **Sound 25** keeps "playing" after the intro, because sound length is not simulated. Consider limiting the room music row to cues started since the room was entered.
- Actor state (`getActorWalkBox`, `getActorX`) is still the largest blocker. Script 045 (Room 02's walk-box watcher) stops at `getActorWalkBox` on the first frame.
