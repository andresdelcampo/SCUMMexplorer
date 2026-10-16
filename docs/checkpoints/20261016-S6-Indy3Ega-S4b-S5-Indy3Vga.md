# Checkpoint: depth masks, S6 Indy3Ega, S4b and S5 Indy3Vga (15–16 October 2026)

Run request: "keep going with the scheduled work, update the README support matrix as progress occurs, minimise my intervention" (user, 15 October), continued with "Try again" after a rate limit (16 October). Follows [S5 MonkeyFloppy](20261015-S5-MonkeyFloppy.md).

## Plan

| Item | Agent | Shared files |
|---|---|---|
| Integrate the 11 October branch `claude/practical-mendeleev-628256` (depth masks and stored objects of the browsed games, R-6.16) | one agent, own worktree | `MainWindow.xaml.cs`, v1/v2/v4/v5 room decoders |
| S6 Indy3Ega: Play through the game's verbs (Proposed R-7.7 to R-7.18) | one agent, own worktree | `ScummSessionInput`, verb/cursor rendering, App play wiring |
| S4b, then S5, Indy3Vga | one agent, own worktree, in parallel with S6 (each told which files the other owns) | `ScummProfile`, `ScummDataSources`, `Core/Scumm/V4` |

## Done

- **Depth masks** (merged, 77f59fa): masks for every browsed game (v1, v2, v4 EGA, Indy3 256, v5) and stored object images where no first visit runs; first-visit actors pass behind the foreground (Monkey Island EGA 8-disk is the new case). R-6.16 reworded to agree with R-6.12a/R-6.13. Suite on master: 999 (994 passed, 5 skipped).
- **S6 Indy3Ega** (54826d2, daee866, ac48ffb): the room view is the game's 320×200 screen; verbs, sentence line, inventory, crosshair cursor, clicks and keys through `VAR_VERB_SCRIPT`, sentences through Script 2, dialogue choices through Script 18. The R-7.18 route from Room 73 passes headless (`ScummIndy3PlayTests`) and through the real window (`.verification/indy3-play-20261015`). **Cells: Objects ◐→✔, Play –→✔** (README and REQUIREMENTS regenerated). Suite on master: 1000 (995 passed, 5 skipped).
- **S4b + S5 Indy3Vga** (eac366c, 192d59c, d696d65, 9a9437c; merge df02af1): boot to Room 73 in 1002 frames, nothing unsupported, no assumed input (no copy-protection room in this release); 87 of 87 rooms without a gap, objects in 39, actors in 67; 74 of 87 rooms match the EGA objects. Walk-box size fix for the room-file layout; `ScummCostume.ReadVga` byte commands. Cells unchanged (Objects partial until S6). Suite on master: 1007 (1002 passed, 5 skipped, 0 failed).

## Not done and why

- No ScummVM picture comparison for any of these (it cannot be driven headlessly; source-level comparison only).
- Indy3 EGA play: no sound during play; held inventory objects run no verb code of their own; the Pause/Skip bar is not shown in play (the keys work); the room list does not follow the game's room changes; the mouse-to-screen mapping was not exercised by the harness.
- Indy3Vga: Spanish copy not tried; rooms 26, 36, 38, 55, 61 differ from EGA by one object (not investigated); `actorFollowCamera` of a hero in another room not followed (Rooms 6, 18, 78, 80).

## Proposed requirements to confirm or reject

R-7.7 to R-7.18 (Play for a verb-based game) stay `Proposed`; S6 was built on them. Open questions: (1) right click: the app's Interact menu (current) or sent to the game as in ScummVM; (2) the restart and save/load keys (Indy3 F5): not delivered (current) or working; (3) R-1.5 "single click uses" met through the game's default verb ("Walk to"); (4) ScummVM's crosshair cursor.

## User checks

1. **Indy3 EGA play** (`Games/indy3 ega en`, Room 73): the room is the game screen with a crosshair; "Pick up" + the mallet shows "Pick up mallet", a second click puts it in the inventory (the game then takes it back: it is chained to the bell); "Close" the door, "Push" the switch, Esc in the dark scene brings the lights back; "Open" the door and click it to walk to the corridor; "Talk" + Marcus, pick a choice, Marcus answers. Judge the pointer mapping and the timing.
2. **Monkey Island floppy** (`Games/monkey1 ega en`) rooms 33, 58, 1, 30, 28, 35, 53, 70, 39; the 8-disk EGA (`Games/monkey1 ega es`) rooms 33, 30, 58 (actors behind the foreground).
3. **Indy3 EGA** rooms 17, 18, 21, 23, 24, 33, 72 (changed by the pseudo-room fix).
4. **Indy3 256 colours** (`Games/indy3 vga en`) rooms 73, 45, 90, 26, 36, 38, 55, 61, 82, 15 against the same EGA rooms.
5. Advanced "Show depth areas" in Monkey Island 2 room 20, Maniac Mansion, Zak McKracken.

## Cost

Masks integration about 80 tool calls; S6 about 300 (two sessions, one rate limit); S4b+S5 Indy3Vga about 155 (two sessions).

## Next run

`Run S6 for Indy3Vga and MonkeyFloppy` (both are v4-era verb interfaces on the play path S6 built), after the user answers the R-7 open questions; or `S4b MonkeyCd` (v5 needs an `IScummDataSource` first).
