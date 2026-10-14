# SCUMM boot sequence by version

How each SCUMM version starts: which script the engine runs first, which engine variables it sets before that, and the order in which room, entry and exit hooks run. **One section per SCUMM version**; each statement carries its ScummVM source file and the date it was read. What a *game* puts into those variables is in the game's script map (`docs/<game>/<GAME>_SCRIPT_MAP.md`), not here.

Section format (copy for a new version):

```markdown
## SCUMM vN (games: …)
- **Boot script:** number, and where ScummVM starts it (`file`, function), date.
- **Variables set by the engine before the boot script:** table (slot, name, value, source).
- **Order on a room change:** exit hook, … entry hook, … (source).
- **Frame loop:** what runs per frame (source).
- **Differences from the previous version.**
```

## SCUMM v1 (Maniac Mansion, Zak McKracken classic)

Not documented yet.

## SCUMM v2 (Maniac Mansion Enhanced, Zak McKracken Enhanced)

Not documented yet.

## SCUMM v3 (LOOM EGA, Indiana Jones and the Last Crusade EGA)

Sources: ScummVM `engines/scumm` `scumm.cpp`, `vars.cpp`, `script.cpp`, `script_v4.cpp`, `script_v5.cpp`, `room.cpp`, `object.cpp`, `sound.cpp`, `players/player_ad.cpp`, read from the `master` branch on 14 October 2026 (S4b, Indiana Jones and the Last Crusade EGA). What a game puts into these variables: [LOOM_SCRIPT_MAP.md](../loom/LOOM_SCRIPT_MAP.md), [INDY3_SCRIPT_MAP.md](../indy3/INDY3_SCRIPT_MAP.md). The generic session that follows this sequence: [S4a record](../../.verification/session-generalisation-20261014/README.md), `ScummGameSession` (Core/Scumm/Session).

- **Boot script:** indexed script 1, started by `ScummEngine::runBootscript` (`scumm.cpp`) with argument 0 (`_bootParam`); only Maniac Mansion's demo uses 9. It runs inside the first frames of `scummLoop`.
- **Variables set by the engine before the boot script** (`vars.cpp`, `resetScummVars`, `setVideoModeVarToCurrentConfig`, `setSoundCardVarToCurrentConfig`): every variable is 0, then `VAR_SOUNDCARD` (var 48: 0 PC speaker, 1 Tandy, 2 CMS, 3 AdLib, 4 Roland), `VAR_VIDEOMODE` (var 49: 13 EGA, 19 VGA, 4 CGA), `VAR_CHARINC` (var 37) = 4; `VAR_HEAPSPACE` (var 40) = 1400 for LOOM and v4+; LOOM DOS also var 39 = 80. The slot numbers are the table in the [script reference](SCUMM_SCRIPT_MAP.md).
- **Order on a room change** (`room.cpp`, `startScene`): the current script ends if room-owned; `VAR_NEW_ROOM`; exit hook `VAR_EXIT_SCRIPT`, the room's exit script, `VAR_EXIT_SCRIPT2`; scripts and resources of the old room are killed (`killScriptsAndResources`); every actor is hidden; palettes reset; `VAR_ROOM` and the camera are set; then `VAR_ENTRY_SCRIPT`, the room's entry script, `VAR_ENTRY_SCRIPT2`. **Room 0** is a room with nothing in it: the exit sequence runs, `VAR_ROOM` is 0, there are no objects, and no entry sequence runs. Pseudo rooms (numbers from 0x80, filled by `pseudoRoom`) load the real room named in `_resourceMapper`.
- **Frame loop** (`scummLoop`): `VAR_TIMER` = jiffies, `VAR_TIMER_TOTAL` and `VAR_TMR_1..3` += jiffies (non-Macintosh Indiana Jones 3 also adds to variables 39 to 41); script delays count down by at most 15; `processInput`; `updateScummVars`; `Sound::updateMusicTimer` (`VAR_MUSIC_TIMER` = the music engine's timer times `_timerFrequency` / 240, 1193182 / 5041 = 236.7 Hz for DOS v2 to v4); `runAllScripts`; `checkExecVerbs` (calls `VAR_VERB_SCRIPT`); `checkAndRunSentenceScript` (pops the **last** sentence the scripts queued with `doSentence` and runs `VAR_SENTENCE_SCRIPT` with verb and the two objects as locals 0 to 2, unless a copy of that script runs unfrozen); then actors walk and the camera moves.
- **Opcodes the boot needed** (v3 shares `ScummEngine_v4`/`v5` code): `pickupObject` (`script_v4.cpp` `o4_pickupObject`): ignored for an object the room lacks or one already in an inventory, else the object goes into the `_inventory` array (80 slots, first free), its owner becomes `VAR_EGO`, class 24 (untouchable) is set, state 1, the draw queue is cleared and the script in `VAR_INVENTORY_SCRIPT` runs with 1. `getInventoryCount`/`findInventory` read `_inventory` (not the owner table); `setOwnerOf` with owner 0 frees the slot. `getVerbEntrypoint` finds the first verb-table entry equal to the verb or 0xFF. `doSentence 0xFE` returns before reading its objects. `saveVerbs`/`restoreVerbs`/`deleteVerbs` (`o5_saveRestoreVerbs`) move verbs under a save id that `getVerbSlot(verb, 0)` does not find. `waitForSentence` waits while a sentence is queued or the sentence script is in use. v3 Indiana Jones `waitForActor` breaks while the actor is in the room and moving.
- **Music timer by device** (`sound.cpp`, `scumm.cpp` `setupMusic`): DOS Indiana Jones 3 or LOOM with AdLib uses `Player_AD`: the engine timer goes up once every 121 steps of a counter that adds the song's tempo each 472 Hz callback and steps at 473 (LOOM, tempo x2) or 256 (other games, tempo x1); it is reset by `startMusic` only, so it keeps its value after a song ends. With PC speaker or PCjr it is `Player_V2`'s `_music_timer` (not modelled by the explorer). Roland goes through iMuse (markers, 0 when no song plays).
- **Differences from the previous version:** none (first documented v3 section). v4 adds variables 27, 39 and 50 to 53; v5 numbers 56 to 72 (`vars.cpp`).

## SCUMM v4 (LOOM CD, Monkey Island floppy, Indiana Jones 256 colours)

Not documented yet.

## SCUMM v5 (Monkey Island 1 CD, Monkey Island 2, Indiana Jones and the Fate of Atlantis)

Not documented yet.
