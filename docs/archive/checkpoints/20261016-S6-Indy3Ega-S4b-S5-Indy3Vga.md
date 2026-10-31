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

## Continued (16 October): S6 Indy3Vga and S6 MonkeyFloppy, in parallel

- **S6 Indy3Vga** (963c406, bba19b4, be9c339): play on by dialect; the R-7.18 route from Room 73 passes headless (`ScummIndy3VgaPlayTests`, sharing the route with EGA) and in the real window; the shadow palette dims the whole screen as ScummVM's `updatePalette` does. **Cells: Objects ◐→✔, Play –→✔.** Suite on master: 1011 (1006 passed, 5 skipped).
- **S6 MonkeyFloppy** (df24f80, 2a10f91, daba5b0, c5c9acf): the play path generalised to v4 (`IScummTextCharset` over v3 charset files and v4 charset blocks; `VAR_CURSORSTATE`/`VAR_USERPUT` published from v4; `VAR_MAINMENU_KEY` refused). Route from Room 33: "Look at poster" composed and run, its cutscene skipped, the door to the SCUMM Bar, Talk to the important-looking pirates with a dialogue choice, the rubber chicken taken in Room 29; real-window check. The cursor is now ScummVM's 16×16 v3/v4 cross-hair (hotspot 8,7) for Indy3 too; R-7.9 wording corrected. **Cells: Objects ◐→✔, Play –→✔.** Suite on master: 1014 (1009 passed, 5 skipped, 0 failed).
- Not done: sound during play (all three games); right click is not delivered (R-7.16), so Monkey's two-button default verb is not available (the game's `)` key switches to one-button mode); a verb's click area is its text box; no ScummVM runtime comparison; no S9 edition checks.
- **User checks (added):** `Games/monkey1 ega en` Room 33: wait for Guybrush to walk in; "Look at" + the poster (line reads "Look at poster"), click, Esc during the speech; "Open" + the door, click the door again; in the bar "Talk to" the important-looking pirates and pick a line. `Games/indy3 vga en` Room 73: the EGA steps, and the switch dims the whole screen. Check the mouse mapping, the timing and the cursor look in all three.
- **Next run:** the R-7 open questions (right click above all, now that Monkey's default verb depends on it); then `S7 Cutscenes` for the three played games, or `S4b MonkeyCd` (v5 needs an `IScummDataSource`), or sound during play.

## Continued (16 to 17 October): right click and sound during play

- **Right click** (9faa03b requirements, f984ee0, c2cd5dc, d9b51eb): user decision "Right click goes to the game in verb-based games, as in ScummVM; keep the Interact menu elsewhere", narrowed by "why not use right click only in the games that need it? -instead of everywhere?". `ScummProfile.RightClickReachesGame` (verb-based and engine version 4 or later; ScummVM turns a v3 right press into Esc): in Monkey Island floppy a right click is input (2, 0, 2) and Script 4 runs the hovered object's default verb (`var[0x0B5]`, set by Script 23); Indy3, LOOM and the browsed games keep the Interact menu. R-7.16 confirmed; R-6.9 and R-7.10 adjusted. Suite on master: 1016 (1011 passed, 5 skipped).
- **Sound during play** (aa22a37, 31459e3, 8f954ff, 5759e7f): the scripts' sounds play during play through the device picked for browsing (AdLib, PC speaker, No Sound); pause, room/resource changes and Skip handled as in LOOM; `ScummProfile.MusicStart` (Indy3 stops the previous music, Monkey stops every sound first, from ScummVM's players). Sound ids pinned on the play routes, each tied to script and instruction. Window check: Indy3 door sound and Monkey bar music started on AdLib. Suite on master: 1021 (1016 passed, 5 skipped, 0 failed). No cell changes.
- **Listening checks for the user:** see the sound addendum of [the Monkey play record](../../../.verification/monkey1-play-20261016/README.md): Indy3 Room 73 door, light switch and door sounds (EGA and 256 colours); Monkey Island dock to the SCUMM Bar (door, bar music, pause with Space, music stops on leaving, the next scene's piece, Room 29's music); switching to PC speaker and No Sound during play; right click on the poster in Monkey Island.
- **Known limits:** Monkey's music is the AD block at the estimated half tempo, not iMuse; Indy3 AdLib uses LOOM's driver constants; loops end after 3 minutes; the scripts always see an AdLib card.
