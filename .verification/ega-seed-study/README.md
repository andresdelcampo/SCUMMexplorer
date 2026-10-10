# EGA seed study (10 October 2026) - read-only measurement, nothing shipped

`dotnet run --project .verification/ega-seed-study -- "<EGA folder>" <out.json>` times the live derivation the app does for an EGA edition with no catalog
(phase 1: one fresh headless run per scene, parallel on half the cores; phase 2: the planner's derived starts and runs, one thread) and dumps one row per group.
Debug build, 32 logical cores (16 workers), SCUMM_ROOM_CACHE_DIR not involved (the harness never touches the cache), %LOCALAPPDATA% untouched.

| Edition | groups | phase 1 | phase 2 | total | no start found |
|---|---|---|---|---|---|
| mt32 en (reference, scanned only for comparison) | 218 | 61 s | 139 s | 201 s | 1 (Room 34 entry) |
| en (plain) | 216 | 49 s | 320 s | 370 s | 14 |
| es | 216 | 58 s | 131 s | 190 s | 1 |
| mt32 es | 216 | 58 s | 132 s | 191 s | 1 |
| fr | 216 | 58 s | 132 s | 192 s | 1 |
| de | 216 | 58 s | 133 s | 193 s | 1 |
| v1.2 es | 216 | 59 s | 143 s | 203 s | 1 |
| it | 216 | 120 s | 413 s | 535 s | 15 |

Scan itself: 1.2 s. Scenes by the static rule: 94 in every edition.
Edition differences (matched by room + verb + script set, not by id): see the report in the session; summary: es, mt32 es, fr, de, v1.2 es are identical
in group count, scene split and rooms per scene (same object ids too); the Roland edition has 2 extra groups (Room 31 local script 203, Opening at pool and
Closing at dry hole) and object ids shifted by one from Room 34 on; plain en and it match the Spanish groups but 13-14 planner starts fail there.

## Why plain English and Italian had 14/15 scenes with no start (found the same evening)
Every one of them needs a `box` or `place` step, and `LoomExplorationSession.PlaceEgo` throws unless `StatusEditable` (which needs the Game Status schema, `LoomStatusSchema.Read`). The schema did not resolve:
- **Plain English** (`loom ega en`): `LoomStatusSchema` requires Room 01 local 211 to contain `var[0x07A] = 0x0003`; this release's script assigns `0x0008` at the same offset (+0x02C3), everything else matches. Tried the one-line relaxation (`StartsWith("var[0x07A] = ")`): Room 2 entry then plays, and a full run gives 1 no-start (Room 34 entry), 187 s, rooms identical to Spanish for all 216 groups. Reverted: the fix should also take `InitialNotes` from that literal (the record hardcodes 3). It also fixes Game Status for this edition ("Loaded staff progression checkpoints are unresolved").
- **Italian** (`loom ega it`): the folder has no `LOOM.EXE` (and lowercase `00.lfl`, `01.lfl`, `02.lfl`), so `GameFolderScanner` gives `GameId.Unknown`; in the app the edition is not treated as LOOM at all (no scan, no Game Status). The harness ran its scripts anyway. Its Room 01 script matches Spanish (`= 0x0003`), so detection is probably the only blocker. Not tested.
Modes added for this: `probe <room> <box>`, `schema`, `writers <var> <value>`, `grep <text>`.

## After the plain English fix (10 October, same build)
`LoomStatusSchema.Read` now takes the initial note limit from the opening's own literal (`InitialNotes`). Current numbers: plain en 59 s + 111 s = 172 s, 1 no-start (Room 34 entry), rooms and categories identical to Spanish for all 216 groups. Italian on a scratch copy of the folder with `LOOM.EXE` from the Spanish copy added (the game was only unrecognised because of the missing exe name): 78 s + 140 s = 219 s, 1 no-start, rooms and categories identical to Spanish. All other editions unchanged (the schema change gives them the same value, 3).
