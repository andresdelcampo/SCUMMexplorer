# M3: script 086, the firewood scene, played in the app (29 September 2026)

## What the user now sees

- **Play a cutscene ▾** has a new entry, **Script 086: the firewood scene (Room 37)**. Game Texts also offers "Play this cutscene" on its 13 lines ([game-texts-cutscene-check.txt](game-texts-cutscene-check.txt)).
- The stated start (in the menu and status tooltips): a fresh session after the boot (seed 0); Room 36 opened from the catalog (assumed arrival); object 599's verb 0x38 run directly (exploration action), which is the doorway into Room 37 (`loadRoomWithEgo`, walk to (24, 101)); Bobbin's arrival walk is run before the view shows the room (about 1 s); then **the game's click** at (109, 102) in box 8 (assumed input).
- Bobbin walks right; as he enters box 8, Room 37's own local 200 chains to script 086 and the cutscene plays on the playback clock, about 90 s:
  - **Room 37**: Stoke turns from the forge, walks up and talks: "Vaya, vaya, vaya." … "¿Leña para el fuego?" ([firewood-room37-stoke-app.png](firewood-room37-stoke-app.png)).
  - **Room 58** (from about 31 s): Stoke's close-up with his three long lines in the game's font ([firewood-room58-closeup-app.png](firewood-room58-closeup-app.png)).
  - **Room 37** again (from about 70 s): "Dame ese palo." and "Ya se lo que voy a hacer contigo." ([firewood-room37-stick-app.png](firewood-room37-stick-app.png)).
  - **Room 38** (from about 80 s): the cell door, "Ya volveré, Nailbender." and "Y no vendré sólo." ([firewood-room38-app.png](firewood-room38-app.png)). It ends in Room 38 with Bobbin at (196, 132) ([firewood-end-app.png](firewood-end-app.png)).
- **Skip (Esc)** turns on at the skip point (+0x0023, with the first line). Escape then ends it in Room 38 within about 1.5 s, Bobbin at the same place (script 086 +0x0504–0x0531; [firewood-after-skip-app.png](firewood-after-skip-app.png)).

## Findings (decoded; session runs)

- **The route.** Room 37's entry starts local 200 (+0x0097) and Stoke's local 203 only while `bit[0xD9A]` is 0; script 086 sets it at +0x0005, so the scene plays once. On that first visit the entry makes objects 602 and 603 (the ways to Rooms 38 and 41) class 0x98, and Room 37 is entered from Room 36 (object 599 verb 0x38: `loadRoomWithEgo 601, 37`, walk to (24, 101), which is box 0). Box 8 (x 50–168, y 101–103) is next to box 0, so the player's first walk to the right starts the scene.
- **Script 086 checks no costume and no distaff.** It waits for Stoke's hammer cycle (`bit[0xD99]` is 0, set by local 203) before its override. At +0x03E9, just after "Dame ese palo.", it clears `bit[0xD61]` (also +0x0504 on the skip path); the reading of that bit ("no distaff") stays open.
- **Why a catalog visit to Room 37 is not the route.** A catalog visit picks an assumed arrival among Room 37's doorways (seed 0 picks Room 38 object 623). Bobbin's position is then an estimate, local 200's `getActorWalkBox` reads it, and script 086's Room 58 load is refused as following a seeded pick: the cutscene stopped in Room 37 after its fifth line. From Room 36's doorway verb, Bobbin's position is reached (`Estimate` is empty), and every room load of script 086 is entered.
- **Not explained yet:** the game's click on Room 36's exit (object 599) from the catalog arrival in Room 36 did not leave the room in a session probe; the app's labelled direct-verb fallback covers it, as for other doorways.

## Verification

- `FirewoodPlaybackCheck` (new, [firewood-playback-check.txt](firewood-playback-check.txt)): 0 failures. The real `MainWindow`; `PlayKnownCutscene` and `SkipCutscene` called through reflection; the view shows Rooms 37, 58, 37, 38; all 13 lines; local 200 (not the explorer) chains to 086; Escape ends it in Room 38.
- New Core test `AWalkIntoRoom37sBox8PlaysTheFirewoodScene`: the same route in the session. Tests: 230/230. `dotnet build SCUMMexplorer.slnx`: 0 warnings.
- DoorwayClickCheck on Rooms 02, 03, 04, 08, 10 and 37 ([doorway-clicks-6-rooms.txt](doorway-clicks-6-rooms.txt)): 16/16 (11 through the game's click, 5 through the fallback, among them Room 37's three).
- GameTextsCutsceneCheck: 0 failures; 086: 13 texts offer the button.
- Not rerun: RoomAudit, `compare-png.ps1` and Blockers (no Core change), the other WPF checks (no change to room clicks or loading outside the new entry).
- Not compared with ScummVM by eye.
