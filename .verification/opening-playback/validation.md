# M3: the boot's opening cutscene (script 044) played in the app (29 September 2026)

## What the user now sees

- **Play a cutscene ▾** has a new first entry, **The opening (script 044)**. It starts a fresh session (seed 0) whose boot stops on the frame script 001 starts script 044 (+0x0222), just after the intro screen's assumed answer (Room 69: difficulty and draft assumed, not simulated, as in every boot). The menu item's and the status line's tooltips say this. The current exploration state is replaced.
- The rest of the boot then plays on the playback clock at the game's 10 frames a second, about 71 s:
  - **Room 01**, the Lucasfilm Games sky: Room 01's locals 200–208 put their points of light (actors 1–9) in the sky one after another, up to 9 at once ([opening-room01-lights-app.png](opening-room01-lights-app.png)). The copyright lines are part of the room's EGA artwork.
  - **Room 02** (the cliff at dawn), **Room 01** again, **Room 02** again (from 22 s), then **Room 53** (from 34 s): the fairy's four lines, "Levántate, Hijo de Cygna." to "El Gran Consejo espera tu presencia en el Santuario.", each for its talk delay, in the game's font ([opening-room53-text-app.png](opening-room53-text-app.png)).
  - **Room 02** (from 54 s): Bobbin's four lines, "¡Un hada mensajera!" to "Será mejor que baje al pueblo.", while the fairy flies off. The playback ends at 71 s with Bobbin under the tree at (828, 60) in left profile, as after the app's own boot and as in ScummVM ([opening-end-room02-app.png](opening-end-room02-app.png)).
- **Skip (Esc)** turns on once script 044 sets its skip point (+0x0124, about 0.2 s after Room 01 opens). **Escape** then ends the opening at once: the view shows Room 02 within 0.2 s, and Bobbin faces the screen, as ScummVM shows after a skip (script 044 +0x063E; [opening-after-skip-app.png](opening-after-skip-app.png)).
- A room click during the opening is not sent to the game (input is off, script 044 +0x0002), as for any cutscene.

## Findings

- **Script 044 opens no cutscene** (decoded: no `cutscene`/`beginCutscene` in script 044). Script 001 sets the in-a-cutscene bit (`bit[0xD6E]`, +0x021D) and script 044 turns the cursor and input off (+0x0000, +0x0002). So the session's `InCutscene` is false during the opening; the app treats "the boot still runs" (`BootRunning`) like a cutscene for playback, Skip and Escape. Its skip point is an override at cutscene depth 0 (ScummVM `beginOverride` uses `cutSceneStackPointer` 0 then), which `abortCutscene` honours.
- **Two skip points, one on this route** (decoded): +0x005A is on the `var[0x030] == 4` (Roland) path only; with the session's sound card 0 (and ScummVM's AdLib 3) script 044 jumps from +0x0046 to +0x00C2, and the one override is +0x0124, whose jump goes to +0x062F.
- **The copyright print is CGA-only** (decoded, ScummVM): +0x0129 `isEqual var[0x031], 4` skips the print at +0x0130 unless `VAR_VIDEOMODE` is 4, which ScummVM sets only for CGA rendering (`setVideoModeVarToCurrentConfig`: EGA 13, VGA 19). On EGA the copyright is in Room 01's artwork, as the screenshot shows. The session leaves `var[0x031]` at 0; both 0 and 13 take the same branches (`== 4` tests only).
- **The play-through ends exactly as the constructor's boot**: 775 frames after script 001's first room load (3 before the stop, 772 played), ego (828, 60) in Room 02 facing 270, `var[0x071]` = 0, camera 800, the same uncertainty list (unit test and harness).

## How it works

- `LoomExplorationSession(…, stopAtOpening: true)`: `CompleteBoot` stops when script 044 is pending (`OpeningScript`), with `BootRunning` true. `AdvanceFrame` plays the rest frame by frame and ends the boot where the constructor's loop would (script 001 ended or chained and no cutscene open), recording `BootFrames` and the trace line. The constructor's own boot is unchanged (same frames, same end state).
- The app's `PlayOpening` builds that session and starts the playback clock; `SessionInScene` (cutscene or boot running) keeps the playback going through the opening's room changes, counts its frames as cutscene frames, and enables Skip and Escape.

## Evidence

- Unit tests: 229/229 (`SCUMM_SOUND_TEST_ROOT=Games\loom`, `SCUMM_INDY3_EGA_TEST_ROOT=Games\IndyCru\EGA`). New: `LoomBootTests.OpeningPlayedFrameByFrameEndsAsTheBootDoes` (rooms 01, 02, 01, 02, 53, 02; at least 5 of actors 1–9 in Room 01; the end state equals the constructor's boot) and `EscapeSkipsTheOpeningAndBobbinFacesTheScreen` (facing 180 after Escape).
- New harness `OpeningPlaybackCheck` ([opening-playback-check.txt](opening-playback-check.txt)), real `MainWindow`, real playback clock, **0 failures**: the view shows Rooms 01, 02, 01, 02, 53, 02 in order; up to 9 points of light in Room 01; talk text in Rooms 53 and 02; Skip off until the skip point; the end equals the app's boot after 72.8 s; Escape 1 s after the skip point ends it in Room 02 after 3.2 s with Bobbin facing 180, and the trace shows `abortCutscene: Script 44 continues at its skip jump`.
- DoorwayClickCheck on Rooms 02, 03, 04, 08 and 10 ([doorway-clicks-5-rooms.txt](doorway-clicks-5-rooms.txt)): **13/13**, 11 through the game's click, the same as before.
- Room02ExitClickCheck: 0 failures. CutscenePlaybackCheck: 0 failures. ActorDrawCheck: 0 failures.
- Not rerun: RoomAudit and `compare-png.ps1` (no Core rendering change; the constructor's boot runs the same frames to the same state), Blockers, VerbAreaCheck (task 2), the other WPF checks.
- Not compared with ScummVM by eye in this task: the opening's timing frame by frame. The end facing (left profile) and the skip facing (the screen) were compared on 28 September.

## Commands

```powershell
dotnet build .verification\opening-playback\OpeningPlaybackCheck\OpeningPlaybackCheck.csproj -o <scratch>\opening
Start-Process -FilePath <scratch>\opening\OpeningPlaybackCheck.exe -ArgumentList "<repo>\Games\loom","<out folder>" -WorkingDirectory <repo> -Wait
```
