# M3c playback controls: pause, play, skip and "Play a cutscene" (29 September 2026)

## What the user now sees

- In the explore mode a playback bar sits under the room, above the scene notes: **⏸ Pause / ▶ Play**, **⏭ Skip (Esc)** and **Play a cutscene ▾**. It is shown to every user, not only in the advanced mode.
- **Pause** stops the scene where it is: actors, camera, text and timers hold (the button then says ▶ Play). **Space** does the same while something plays. While paused, a room click is not sent to the game (the status says why), and **Continue ▸** in the advanced session bar plays exactly one frame. **Play** goes on from that frame.
- **Skip (Esc)** and the **Escape** key skip a cutscene as the game does (ScummVM `processKeyboard` → `abortCutscene`): the script that set the cutscene's override goes on at its skip jump, and the playback plays on from there. Escape works in the explore mode without "Keys as game input". The button is off until the cutscene has set its skip point (`beginOverride`), because until then Escape does nothing in the game either; its tooltip says so.
- **Play a cutscene ▾** lists two known cutscenes. Each starts from a stated, labelled state: a fresh session after the boot (seed 0), the room opened from the catalog (the ego's arrival there is assumed), and the object's verb run **directly**, an exploration action, not the game's click (Bobbin does not walk to the object, and object 295 is untouchable in that state, as DoorwayClickCheck shows). The menu items and the status line's tooltip say this. The current exploration state is replaced.
  - **Script 052: Room 10 → Room 11 → Room 13** (Room 10 object 295 verb 0x38). Room 10 holds while the cutscene animates actor 3 and waits its `delay 60` (+0x0041); Room 11 opens (+0x0045) with actor 3 (costume 0x51) put at (234, 112), and the cutscene starts Room 11 locals 0xD2–0xD5 and walks actor 7; the cutscene ends and `loadRoomWithEgo` (+0x017D) opens Room 13. About 33 s on the clock (with a 1 s pause in the check). Escape in Room 11 reaches Room 13 within 0.2 s.
  - **Script 046: the Elders' trial** (Room 08 object 255 verb 0x38). Its first line, "Parece que los Ancianos están ocupados." (+0x002F), shows in the game's font in the verb screen ([trial-first-text-app.png](trial-first-text-app.png)); Escape then ends it in Room 09 ([trial-after-skip-app.png](trial-after-skip-app.png)).

Screenshots from the real window: [script052-room11-actors-app.png](script052-room11-actors-app.png) (Room 11 at the skip point, actor 3 drawn at (234, 112)), [script052-room13-end-app.png](script052-room13-end-app.png) (Room 13 after the cutscene).

## Findings

- **Script 052 prints no text** (decoded: no `print` in script 052, Room 10 object 295, Room 11's locals or Room 13's entry). So "with text" in the plan's verification cannot hold for this route; text is verified with the trial instead. The only messages on the route are an empty talk from script 016 +0x0005 and script 018's drawn note letters in the verb screen in Room 13.
- **Escape before the override does nothing** (decoded, ScummVM `abortCutscene`): script 052 sets its override at +0x00C6, after the Room 11 load, so for its first ~5 s (Room 10 and the start of Room 11) Skip stays off. Script 046 sets it at +0x0020, within a frame.
- **Skipping the trial** goes to its skip jump; the cutscene ends in Room 09 about 2 s after Escape (session run).
- New session property `LoomExplorationSession.CutsceneSkippable`: `VAR_CUTSCENEEXIT_KEY` is set and the open cutscene's override script waits for a later frame, the same conditions `AbortCutscene` needs.

## Evidence

- Unit tests: 227/227 (`SCUMM_SOUND_TEST_ROOT=Games\loom`, `SCUMM_INDY3_EGA_TEST_ROOT=Games\IndyCru\EGA`). `LoomKeyInputTests.EscapeSkipsScript052ToItsOverrideJump` now also checks `CutsceneSkippable`: false before +0x00C6, true in Room 11, false after the skip.
- New harness `CutscenePlaybackCheck` ([cutscene-playback-check.txt](cutscene-playback-check.txt)), real `MainWindow`, real playback clock, **0 failures**:
  - the bar is shown in Room 02 after the boot; pause and skip are off with nothing playing;
  - script 052 played through: Room 11 at 5.9 s (the view shows Room 10 from 1.7 s, after the new session's boot), the skip point at 7.1 s, Room 13 at 33.4 s, actors drawn in Rooms 10, 11 and 13, no talk text in Rooms 10 and 11;
  - paused in Room 11: `VAR_TIMER_TOTAL` stayed at 4023 jiffies for 1 s; Continue ▸ moved it by one frame (6 jiffies), still paused; Play went on;
  - script 052 with Escape: Skip off until the override; Escape at 6.8 s, Room 13 at 7.0 s (33.4 s played through); the trace shows `abortCutscene: Script 52 continues at its skip jump`;
  - the trial: its first talk line at 9.2 s, Escape at 10.0 s, the playback ends in Room 09 at 12.0 s.
- DoorwayClickCheck on Rooms 02, 03, 04, 08 and 10 ([doorway-clicks-5-rooms.txt](doorway-clicks-5-rooms.txt)): **13/13**, the same as before (Room 04 object 177 and Room 10 object 295 through the fallback).
- Room02ExitClickCheck: **0 failures** (three clicks to Room 03 through the game's click).
- Not rerun: RoomAudit and `compare-png.ps1` (no rendering change; the session change is a read-only property), Blockers (no executor change), ActorDrawCheck, MessageAppCheck, ClickInputCheck, CameraPanCheck, PresentationCheck and VerbAreaCheck (the room-click path is unchanged except that a paused playback refuses clicks).

## Commands

```powershell
dotnet build SCUMMexplorer.slnx
dotnet test tests\SCUMMexplorer.Tests\SCUMMexplorer.Tests.csproj --no-build
dotnet build .verification\cutscene-playback\CutscenePlaybackCheck\CutscenePlaybackCheck.csproj -o <scratch>\cpc
Start-Process <scratch>\cpc\CutscenePlaybackCheck.exe -ArgumentList '<repo>\Games\loom','<out folder>' -WorkingDirectory <repo> -Wait
dotnet build .verification\doorway-clicks\DoorwayClickCheck\DoorwayClickCheck.csproj -o <scratch>\dcc
Start-Process <scratch>\dcc\DoorwayClickCheck.exe -ArgumentList '<repo>\Games\loom','<out.txt>','2','3','4','8','10' -WorkingDirectory <repo> -Wait
dotnet build .verification\room02-exit-click\Room02ExitClickCheck\Room02ExitClickCheck.csproj -o <scratch>\r02
Start-Process <scratch>\r02\Room02ExitClickCheck.exe -ArgumentList '<repo>\Games\loom','<out folder>' -WorkingDirectory <repo> -Wait
```
