# Checkpoint: Indy 4 play fixes, MI2 talkie speech, sound polish, faster cutscene lists, MI2 play start (19 to 20 October 2026)

Run request: "Keep going with the next suggested runs in NEXT_SESSION_PROMPT.md, in parallel where they don't share files." (user, 19 October). Four agents ran in parallel (each stopped once by a rate limit and resumed); a fifth investigated a slow MI2 play start found on the way. Follows [19 October](20261019-S9-v5-snapshot-languages.md).

| Item | Commits | Result | Record |
|---|---|---|---|
| Sound polish | 59574dee, 49718df6 | MI1 floppy music on iMUSE's exact clock (the half-tempo estimate, ~6 % fast, removed); Indy3 EGA and 256 colours use the non-LOOM `Player_AD` constants (R-13.9 updated; songs about twice as long as before); PC-speaker songs that loop after 3 minutes render through their loop; with the PC speaker in play, `isSoundRunning` and the music timer follow `Player_V2` (labelled estimates listed in the record) | `.verification/sound-polish-20261019/` |
| MI2 talkie speech and mouth sync | c2d5f167, e0a52ef9, 42a8141f, 7417ce43 | The MI2 fan patch's `monster.sou` has Indy 4's layout: its lines already played through the shared path (the earlier "not played" notes were wrong). Mouth sync from the `VCTL` times as ScummVM's `sound.cpp` (Indy 4: real times; MI2: one placeholder word, mouth open until the voice ends); a talkie mouth closes when the voice ends | `.verification/talkie-speech-20261019/` |
| Indy 4 credits and magazine | 245b4b4c, cbad47a9, merges, a6c0082c | A drawn object now covers drawn strings under its strips (ScummVM `Gdi::drawBitmap`/`copy8Col`): the credits headings are erased by Script 94's `drawObject` as in the game. A held object's code runs from the room it was taken from (`whereIsObject` WIO_INVENTORY): the magazine's icon (Room 98, object 1260) appears. v4+ `print` SO_LEFT modelled | addendum in `.verification/indy4-play-20261017/` |
| Global-variable change tracking | bd93919b, cd2177b5, c0b8ef9f, merges | `ScummGlobalVariables` (Dictionary API and order) and `ScummRunVariables` track writes instead of copying 800–1400 globals per run; prints read a detached view. All twelve cutscene lists byte-identical. First launch in the window: Indy 4 34 s (was 45), MI2 21 s; all twelve lists in parallel 70 s (was 123) | `.verification/globals-tracking-20261019/` |
| MI2 play start | dafa717f, 291c3316 | Not a regression of this run: since `273cc399` (19 October, R-8.5b/R-8.5c, the user's rules) a picked room whose own entry leaves it (MI2's campfire, Room 4) is a static preview, not played. Guards added: `ScummPlayStartTests` (play reaches a live room within 20 s for MI2, Indy 4, MI1 CD, Indy3), `PlayStartWindowCheck`, and the MI2 cutscene window check now opens Woodtick | `.verification/mi2-play-start-20261020/` |

Suite on master at the end: 1735 tests (1730 passed, 5 skipped, 0 failed), 0 build warnings, about 8 minutes.

**Open for the user:** R-5.3a says the first playable room is the boot's own state, while R-8.5b turns MI2's boot room (the campfire) into a static preview: a Normal-mode user who picks Room 4 sees no play and no reason (the reason is an Advanced tooltip only). Options: a short Normal-mode hint on a static preview pointing to **Play Game Intro**, or let a boot room play its settled boot state.

**Not done:** ScummVM comparison; MonkeyCd Rooms 88, 89, 99; cutscene start setups (R-8.33); the `printEgo` first-text offer; Indy3Vga one-object differences; `actorFollowCamera` of a hero in another room; server GC (user's call); a fixed-point stop for long cutscene runs (proposal in the globals record).

**User checks:**
1. Listening: Indy3 EGA AdLib songs 29, 35, 39 (about twice as long); MI1 floppy 101, 103, 109 (slightly slower); PC speaker LOOM sound 11 past 3 minutes; play in the SCUMM Bar past 30 s (music carries on).
2. MI2 talkie: the barkeeper (Room 9) and LeChuck (Room 74) scenes from the list — voices match, mouths close when the voice ends; `mi2 talkie es` the same with English voices.
3. Indy 4: from Room 4 click the statue; Room 5's credits show one heading at a time; after Esc the first New York slot shows the magazine; Smith's mouths move with the words.
4. First-launch cutscene line for Indy 4 and MI2 (about 35 s and 20 s).
