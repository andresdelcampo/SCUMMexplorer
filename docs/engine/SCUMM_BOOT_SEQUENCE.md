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

Not documented yet (LOOM's boot as its game uses it is in [LOOM_SCRIPT_MAP.md](../loom/LOOM_SCRIPT_MAP.md); the generic session is described in the [S4a record](../../.verification/session-generalisation-20261014/README.md)).

## SCUMM v4 (LOOM CD, Monkey Island floppy, Indiana Jones 256 colours)

Not documented yet.

## SCUMM v5 (Monkey Island 1 CD, Monkey Island 2, Indiana Jones and the Fate of Atlantis)

Not documented yet.
