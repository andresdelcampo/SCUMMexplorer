# Placer audit: costume changes and story-bit room entries (3 October 2026, after 5fc0e1e)

Raw data: [audit-raw.txt](audit-raw.txt) (dump from `LoomSceneSetupPlannerTests.DumpCostumeAndEntryAudit`, run by hand with `SCUMM_AUDIT_DUMP`).
New scanner queries (Core, no behaviour change in the app): `FindCostumeChanges`, `FindWriters(variable)`, `FindStoryBitMoves(value)`, `ScanRoomEntry(room)`.

## Correction to the first placer
The scanner looked for `costume ` (with a space) in `actorOps`, but the disassembler prints `costume, 0x11`. Its `CostumeChanges` list was always empty, so the earlier
"the scan found no costume dependency" was not evidence. The match is fixed (`costume,`); a test pins it. The planner itself never used that list, so no setup changed.

## Costume (actor 1) after the re-scan
- **Room entries own their costume and a catalog visit runs the entry.** Fresh visits show: Room 12 → 0x11, Room 13 → 0x37, Room 46 → 0x7B, Room 47 → 0x7C, Room 66 → 0x7C,
  every other room 0x01 (Bobbin by size; the small costumes 0x02/0x03 come from script 017's size argument). Rooms 20 (0x58, entry +0x0040) and 38 (0x70, entry +0x0015) set theirs only on a
  branch the fresh state does not take (Room 38 reads D9E, which script 087 sets), so they start as 0x01 (session run).
- **Costumes left by scenes, not entries** (the only ones that could carry into a later scene): Room 12/322 verb 0x38 (0x13), Room 49/704 verb 0x38 (0x7C), Room 23 local 210 (0x4C),
  Room 28 local 215 (0x60), scripts 054 (0x3E), 082 (0x64), 087 (0x70), 102 (0x7C), 106 (0x7D, ending 0x35), 107 (0x12). Scripts 053, 091 (0x01 after 0x9D), 093 (0x01 after 0x91) and 096 (0x01 after 0x85) restore Bobbin themselves.
- **The saved costume `var[0x07D]`** is written only by Room 10 local 203 (0x11), Room 34 object 557 (0x68), Room 34 local 202 (0x01 / 0x67) and script 017 (size costume); read by Room 10's entry, Room 34 local 202 (+0x00EF restores it) and the scenes Room 02/156 and Room 13/324 (through Room 10's entry).
- **Result: no costume setup is derived.** No scene's own code reads the ego costume except those three; the leftovers above matter only when a scene runs in a room *after* one of them in the story,
  and the story order between rooms is not in the data (it would be an invented sequence). Left as an open decision, see below.

## Story-bit room entries
- 35 rooms' own entry or local scripts read story bits (≥ D72). Over the 228 catalog entries, 136 start in a room whose entry branches on a bit that some other scene sets; a note for each was tried and dropped: most are the room's own once-only toggles
  (a verb reads and writes its own bit, for example Room 06 D86–D89, Room 08 D7D–D7F, Room 22 DB5/DB9, Room 28 D94) or bits whose only setter is a local script of the same room, so the first-visit value is the only one the scene can show.
  The product rule (first-visit look) already covers them, and a long unhelpful note on 60% of the scenes would hide the real ones.
- **Scenes that read bits an earlier real scene sets** (the real candidates; each runs from a fresh start and reaches its own end, but with the first-visit branch):
  - The Room 43–45 chain: Room 41/633 (script 090), Room 44/652 and Room 45/655 (script 092) read D96, D97, DA3, DB6 (setters: scripts 091, 093, 095; Room 43 local 200) and DC0 (090, 091); Room 43/643 (091) reads DA3 and DB6, Room 43/650 (093) reads DB8 (set by 091).
    The verified click route (global-scripts validation: 650 → 639 → 44, 653 + Escape, 656 staff, 655, 639) shows the real order 093 → 095 → 092 → 096. The two cage scenes read each other's bits, so the order between them is a story choice.
  - The ghost chain: Room 33/543, Room 46/684 (script 097) read D72, D7B, D98, D9D, DC7, DC8, DD4 (setters: script 106, Room 34 local 202/207/209, script 087, script 098). `sequence-086-087` and `sequence-087-098` already run the real earlier scenes.
  - Room 02/156 (script 049) reads D7A, DBD, DC3, DD3; Room 13/324 (107) reads DBD, DCA, DD3; Room 09/270 and 268 read the Room 08 cave bits D7C–D7F (script 046, Room 08 locals); Room 49/707 (script 099) reads D90 (Room 20 local 207); Room 46/682 (110) reads DC2 (script 099); Room 47/697 (100) reads DC5 (Room 46 local 204).
  - Bits no code sets: D8F (read by Room 18/369 and 371, Room 49/707) stays 0 in the whole game.
- **Result: no new setup is derived.** For none of these does the scene's own code *clear* the bit to presume it (the staff rule), so the evidence only says "a later branch exists". Clears of story bits in scene scripts
  (086 → D83; 087 → D98, D9E, D9B; 090/091 → DC0; 093 → DB7) are initialisations at the start of a scene, not presumptions.

## For the user to decide (not done)
1. Chain scenes (Room 44/652, Room 45/655, Room 41/633) after the cage scenes, using the verified route order 093 → 095 → 092, as unseen real setup steps? It would show the later branch; the fresh branch stays the default otherwise.
2. A story order between rooms, so a costume left by 054/082/087/102/106/107 can be carried into the next scenes. Without it any pick is a guess; the product rule says label a random pick.
3. On-screen check of the earlier setups (staff scenes in Room 43, El Muerto, Room 17 sizes) needs the App launched by the user.
