# Changelog

## 8 October: prepare the final-gap session

Prepared a [detailed EGA/VGA gap dossier](loom/FINAL_GAPS_HANDOFF.md) with verified audit results, exact resource errors, Room 34 research, residual callback classifications, original-game comparison limits, completion criteria and conditional user needs. Replaced superseded live handoff status while preserving the previous file as a dated archive. Questions and work on the gaps are deferred to the requested next session. Documentation only; checked local evidence, paths and Markdown links; no runtime changes or test rerun.

## 8 October: EGA / VGA correctness audit

Audited every script kind, the English EGA replay catalog, VGA derived groups, 1,320 isolated object callbacks and real WPF interaction/playback controls. Fixed native responses being overridden by static doorway possibilities (the grave could incorrectly jump to Room 64), generic draft reactions leaking into the VGA picker, and completing-start trials accepting unmatched cutscene scopes. VGA Room 20 now derives a labelled placement from the triggering object's own walk point. Final suite: 833 passed, five diagnostic tests skipped; clean solution build; 201 WPF assertions passed. All 328 EGA recipes and 222/223 VGA groups complete both normally and with Escape. Room 34 reachability, incomplete EGA resources and original-game fidelity remain unresolved; [audit and reproduction](../.verification/correctness-20261008/README.md). Local commit only; no push.

## 8 October: port VGA annotation findings back to EGA

Reviewed English and Spanish EGA globals against the VGA work, adding 70 EGA titles (116 valid English globals and 113 Spanish globals now titled), missing draft/note flags, pointer/buffer/animation/sound/flying-array meanings and scoped actor roles. Labels require their own EGA instruction fingerprints; invalid script entries stay unresolved. Corrected incomplete EGA 90/99 titles and the earlier comparison notes: both editions already contain the Bishop introduction and Goodmold aftermath. Refreshed readable goldens for both languages, including Spanish snapshots that had contained English data. Clean build, 42 focused Core checks and 126 real WPF assertions; [individual review and validation](../.verification/advanced-scripts/README-EGA.md). No interpreter changes, full suite or push.

## 7 October: individual VGA Advanced Scripts review

Compared all 122 CD globals with EGA and expanded the eight titled globals to 122 individually reviewed titles. Ported established input/draft flags and scoped actors, repaired the clicked-object matcher and Script 16's readable layout, and enabled CD Used by / Explore links with room-scoped local targets and actual disc source offsets. CD stubs, changed scenes and dialogue retain their edition-specific meanings. All 1,470 CD blocks still format readably; focused tests, unchanged EGA goldens and the real WPF Script 5/7/16 comparison/link navigation passed. Clean build; [review and validation](../.verification/advanced-scripts/README.md). No full suite or push.

## 7 October final clarification: all VGA Readable scripts and The Intro

The CD glossary now names source-verified difficulty, distaff, input, note/draft, costume/animation, room and sound variables throughout all script kinds. The shared presenter names established indexed arrays and explains the remaining decoded operations, including walk boxes, lighting, room effects, object renaming, unstructured branches and CD voice cues. A decoding-only check covers all 1,470 supplied CD blocks without raw-operation fallbacks; unestablished story roles retain numeric IDs and evidence levels remain visible. Focused checks passed, including three unchanged EGA script goldens and a bounded Script 28 UI comparison. Both editions call the introductory sequence **The Intro**, leaving the Opening spell unchanged. Clean solution build; [record](../.verification/loom-parity/README.md). No full suite or push.

## 7 October follow-up: shared sound rows, immediate notes, live thumbnails and dialog exit

Advanced EGA/VGA sound rows share resource, kind, initiating source and duration; full paths/precise CD timing stay in tooltips. Room 68's selected/live thumbnail now keeps the shadow palette. Both room cutscene lists identify the start room when it differs from the viewed room. VGA's intro is Opening and its scripts use the shared Readable presenter, correct CD container headers and source-verified helper names. CD browsing reads its initial three-note floor from its own opening initialization; EGA note letters no longer wait for entry camera/walk completion. Game Status has Apply and close / Cancel, with one-click exit. Four focused staff cases and bounded UI checks passed; [follow-up record](../.verification/loom-parity/README.md). No full suite or push.

## 7 October: staff and presentation parity from user feedback

Catalog room changes keep the distaff appearance and note controls together on EGA and VGA; EGA reprints its note letters, and VGA updates its large-costume default while Bobbin wears a smaller look. Room 69 suppresses the staff while preserving possession. Advanced Sounds has readable source details on EGA; Room 68's thumbnail uses the same script palette as the room view. Doorway titles use concise Reached Room labels, both pickers show verb numbers in Advanced, and VGA's intro is The Opening. VGA Room entry links now open global and room-local v4 scripts with correct DISK01.LEC source headers. Validation: six focused test cases and one bounded WPF harness, rerun only for the discovered failures; [record](../.verification/loom-parity/README.md). No push.

## 7 October: CD note heads, titles, mask planes, staff costume, Sounds lists

Image verbs on the CD sit on 8-row strips (note heads carry their own staff lines; verb area and Game Status). Play Cutscene titles name the verb as a word like EGA's ("Look at leaf", "Opening at sky", "Other draft response at myself"); the byte only in Advanced; draft verbs are separate groups (the sword's Sharpening and its reverse). A room change keeps the verb screen's strings (the note letters). The CD's rooms carry several mask planes (Rooms 23 and 25); a walk box's mask byte picks the plane. Staff verbs follow the staff bit after a catalog visit; on the CD, Game Status switches Bobbin's costume with the staff. Room 66 EGA: the Sounds button and music panel no longer blink. Sounds lists: normal and Advanced forms on both games. The CD's script scan run on EGA finds all 328 catalogued replays (`.verification/cutscene-scan-ega`).

## 6 October (late night, second pass): CD skill screen, Game Status notes, pause, titles

From the user's second list. **Skill screen (Room 69, "Play Cutscene" in Room 01):** the three button frames never showed in the intro playback because the playback's first frame in a new room did not refresh the room view, and the buttons are drawn a few frames after the room loads (`ShowPlaybackFrame` now refreshes on it for the CD). The screen also answered by itself after 3 s; with `WaitForSkillClick` (set by the intro in the app; `SCUMM_CD_INTRO_AUTOANSWER=1` restores the unattended run) it waits for a click on a button, which runs through the game's input script like any click, and the assumed answer follows 20 frames later (the draft question that comes next in the game is not simulated). **Game Status quarter notes:** the CD draws them as image verbs (script 018's `verbOps <verb>, [at...; on]`, images assigned by Room 1's local 211), where the EGA prints a musical glyph; `LoomStatusArtwork` now draws them. **Pause** now pauses the disc audio too. **Play Cutscene titles** name the verb (`verb 0x..`) only in Advanced. **Room 04's arrows:** not a bug of the CD data: Room 4's local 205 locks the camera to 480..488 while Bobbin stands at the right end (x 580), as in the game; the arrival through Room 03 puts him there, so the explorer's range collapses and Bobbin is in the visible half. **Staff height:** measured the user's reference against the CD window: the note heads sit about 3 to 4 source pixels higher than the reference; not changed (they follow script 018's rows). Share unchanged: 203 of 204 (99.5 %) enabled.

## 6 October (late night): CD doorways, Sounds button, the staff, and a VGA-versus-EGA sweep

The user's list after the first CD pass: much of the CD game should have been the same as EGA. Found and fixed: (1) **Doorways**: `LoomDoorwayFinder` and the room's verb decoding used the EGA loaders (LFL scripts, `_verbReader`), so the CD found none; both now read through the CD data source (Room 04: doorways to 03 and 64; the Doorways option and the DOORWAYS panel appear). (2) **Sounds button** next to Play Cutscene: the disc audio the room's own scripts ask for (`LoomCdCueIndex.ScanRoom`), music/narration first, played on demand through `CdVoicePlayer.PlayNow`; hidden when the dropdown is on No Sound. (3) **The staff**: the CD session never ran Room 01's entry (the boot's room load is dropped), so local 211, which creates the 61 verb slots, never ran when another room was visited first: no staff drawn, and Game Status's staff edit changed variables but showed nothing. The session now enters Room 01 once at the start of the boot continuation; the staff layout variable (0x0AD, found in script 018) takes script 001's own pre-intro value (estimate: standard skill). A staff set through the status call now shows the distaff with its note letters (CdCheck `CD_LOOK_STAFF=1`). Tests: LoomVga and LoomCdCutscene filters pass (50). **Open, not fixed:** Room 69's skill screen after the Room 1 cutscene (frames missing, not waiting for a press; not reproduced), Room 04's missing scroll arrows (the assumed arrival through Room 03 local 200 leaves the camera limits at 480..480 where EGA keeps 160..480; the room is 640 wide in both), the note heads' height on the staff (follows script 018's rows; unverified against the real game), and the VGA/EGA gaps listed in the README step 24. Share unchanged: 203 of 204 (99.5 %) enabled.

## 6 October (night): CD sound dropdown, Bobbin's size in the CD's Room 02

The CD game's "Voices" checkbox is now a sound dropdown like the EGA one with two choices, "No Sound" and "CD Audio" (a disc icon; the voices and what the audio track carries; saved as before in the view options, locked during a cutscene like the EGA box). A catalog visit to a CD room estimated no size for Bobbin because the size logic was keyed to the EGA variable `var[0x07C]`; script 017's size variable is now found in the code (the variable most room entries pass: `var[0x0AE]` on the CD) and the estimate runs for the CD too, so Room 02 shows the small Bobbin on the path (CdCheck screenshot; the user's report). Actors can also be drawn scaled (actorOps `scale`, a walk box's scale word or scale slot from the SA block; no CD room uses them, so it is only a safeguard). Room 69's skill screen: a fresh visit shows the three framed buttons in the window; the user's case (after the Room 1 cutscene) was not reproduced, see the README step 23. Test `ACatalogVisitToTheCdPathRoomEstimatesBobbinsSizeFromTheCodesOwnVariable`. Share unchanged: 203 of 204 (99.5 %) enabled.

## 6 October (later): the room's CD cutscene button no longer waits for the background runs

The room's "Play Cutscene" button for the CD game appeared only after the background runs (many seconds, reported by the user) had listed every scene's rooms. Now `CdRoomScenesCache` (App) keeps those lists (rooms per trigger and the intro's rooms) as JSON under `%LOCALAPPDATA%SCUMMexplorer
oom-cache`, one file per game folder, stamped with the Core assembly's build time and the game folder's file sizes and times; reopening the last game uses it at once, while opening a folder with the button derives the lists again (as the room thumbnails do) and overwrites it. On a first launch (no saved lists) the button shows disabled with a tooltip ("Looking for the scenes that happen in this room…") until the runs finish, then settles (hidden in the rooms with none). The user's older exe (main checkout on master) was why the button was missing earlier: master now holds the branch. Checked in the real window through `CdCheck` (`CD_USECACHE`, `CD_FIRSTLOOK`, private `SCUMM_ROOM_CACHE_DIR`): a first launch shows Room 1's button disabled with the tooltip, then enabled; a reopen with the saved lists shows it enabled at once ("Play The intro"). Two fixes found on the way: the saved-lists stamp now covers the files beside a `.cue` (it was empty for a disc image opened through its `.cue`), and the disabled placeholder reads "Play Cutscene" without the menu arrow. Looked at by screenshot only, not by hand. Not heard by ear. Share unchanged: 203 of 204 (99.5 %).

## 6 October: CD scenes listed in the rooms they play out in, the intro in Rooms 01 and 02

The room's "Play Cutscene" list for the CD game also holds scenes that play out in the room (a headless run of each scene from its derived setup records the rooms it loads; "(played from Room NN)") and "The intro" in the rooms it is in (69, 2, 53, 1). The button is now in 60 of 78 rooms (was 44; checked in the real window). The user said no room showed it; it shows in this build, so an older build is the likely cause (estimate). 203 of 204 scenes (99.5 %) still enabled. Record: `.verification/loom-vga/README.md` step 21.

## 5 October (night, the shepherd): Room 25 object 372 enabled, 203 of 204 CD scenes (99.5 %)

Room 25 object 372 verb 0x37 (the shepherds' "Go forth, Wizard!" scene) needs `bit[0x032]`, `bit[0x033]` and `bit[0x046]` set and `bit[0x05B]` clear; the room's walk-box monitor (local 200) sets them as the ego enters boxes 4, 3, 2 in that order. `LoomCdSceneSetupPlanner` now finds such monitors (a local script that reads the ego's box and compares it with constants) and tries the orders of their boxes, settling between boxes; a trial for an object verb counts only scenes begun after the setup. Seen in the real window (the scene prints, then Room 24 is entered; box placement and order are estimates). Room 34's entry stays without a start: the only script that puts the ego at box 6's line (global 97) leaves him in box 5, and a walk cannot skip box 7. The user's check (walk-triggered scenes, CD music in Advanced) found nothing wrong; the per-room "Play Cutscene" button is in the build (44 of 78 CD rooms have scenes of their own; 34 show none), the user said they missed it: open question. Record: `.verification/loom-vga/README.md` step 20. Not heard by ear.

## 5 October (night, the sword): CD scenes that begin but never end, the disguise and the doors

The sword scene (Room 41 object 559 verb 0x41) stalled from a fresh start: the room's hammer loop (script 206, freeze-resistant) cues the disc every 27 frames and every cue zeroes the CD timer that the scene's waits read, while the scene itself stops the talk loop 207 and so expects it. The planner now notices a scene that begins but does not end (6,000 frames), finds the story bits its scripts clear that other code sets, and plays the code that sets them first (the foreman scene, object 557's look); the scene then runs all 50 lines and ends in Room 42 (real window, not heard). How the real game gets into that state was not found; the picker note says it is an estimate. Room 34 object 485 (the disguise door) is enabled by running object 487's draft verb twice (the first run is script 85, the second the toggle script 202); the Room 34 entry needs a one-frame change from box 6 to box 8, which are not neighbours, and says so. Share of the 204 CD scenes enabled: 202 (99.0 %): 185 from a fresh start, 17 from a derived setup, 2 without a start (Room 25 object 372 verb 0x37, Room 34 entry). Record: `.verification/loom-vga/README.md` step 19.

## 5 October (night, the next session): LOOM CD start setups derived from the scripts, three engine fixes

Two scenes that never ended (Room 9 object 199 verb 0x38, Room 24 object 359 verb 0x3E) waited for the CD music timer (`var[0x00E]`), which only a disc cue starts; a session that skipped the intro had none, so the timer now counts from the session's start (an estimate, labelled). Looking for why 24 scenes opened no cutscene from a fresh state found two more engine gaps: ScummVM aliases a bit variable onto a variable word only up to version 3, so the CD game's bits are a separate array (the aliasing let `var[0]` and the camera change `bit[0x00E]` and `bit[0x025]`); and the skipped-intro boot dropped script 001's tail (it clears the start-up lock `bit[0x00E]` that room entries test before they start their walk-box monitors), now run from its `endOverride`. `LoomCdSceneGuards` reads, from the disassembly, every test the code makes between a trigger and a `cutscene`; `LoomCdSceneSetupPlanner` meets the tests a fresh start fails with what the game's own object verbs, room entries and earlier scenes do (run unseen, as exploration actions), or a walk-box monitor with the ego (placed in the box, or walked across a doorway line), and keeps a candidate only when a headless trial begins a cutscene. Of the 24 scenes, 6 now begin by themselves (the engine fixes), 16 begin from a derived setup, 2 say why not (Room 34 entry needs a crossing from box 6 into box 8 that no route here gives; Room 34 object 485 verb 0x38 needs `bit[0x038]`, written only by its own script and by script 120, which nothing starts). The picker note of every CD scene says whether a fresh start begins it or lists the setup; `PlayCdCutscene` runs the setup on a fresh session. Unset script variables from 0x100 up read 0 (the engine starts them at 0), and `bit[...]` arguments of `startScript` and `chainScript` are read. One scene got worse: Room 41 object 559 verb 0x41 (the sword) now stalls after its first line (its ambient script 206 re-issues a voice cue every few dozen frames, which resets the CD timer its waits depend on; Escape ends it; in the game something keeps this from happening and it was not found). Tests: `LoomCdCutsceneTests` (84 with the CD and status ones), record `.verification/loom-vga/README.md` step 18.

## 5 October (night, very very very last): LOOM CD cutscenes from the scripts, Escape, Game Status with the staff held

`LoomCdCutsceneIndex` finds every trigger (a room's entry script, an object's verb) whose code, or a script it starts or chains by a literal number (three levels), holds a `cutscene` ... `endCutscene` span: 404 triggers, grouped by the scripts they reach into 204 scenes after leaving out one shared helper (global 41, the generic reaction, reached from 100 objects). Play a cutscene lists them for the CD game after "The intro" (79 "Scenes": entry scripts, spans with room loads or three or more lines; 125 "Dialogue and reactions"), the room's cutscene button lists the room's own. Each plays from a stated start (the running session, the room visited from the catalog, the entry script or the object's verb run directly: an exploration action); the note says what the code reaches and that the branches are possible, not certain, and the status line says so when no cutscene began (24 of the 204 do not from a fresh state: a story state or item is missing). Escape (checked headless and in the real window) skips: the intro's first skip point only skips the opening narration (script 001's own override, +0x0337) and the opening plays on, the overture's override skips the rest; all 37 scenes that set a skip point end much sooner on Escape (for example Room 8's tribunal 1,440 frames, 93 with Escape). CD Game Status with the staff held draws the held staff, the note strip and learned drafts. Record: `.verification/loom-vga/README.md` step 17. Not heard by ear.

## 5 October (night, very last): LOOM CD music timer, the intro as a scene, voice timing

`var[0x0E]` follows the CD cue as ScummVM's CD timer does (zero at a cue's start, one per jiffy, a stop sets the variable to 0 but the timer goes on), so scripts 001 and 044 run on. "The intro" is in the CD game's Play a cutscene list: the skill screen (answer assumed after 3 s, labelled), the 83.9 s overture cue, the logo and copyright line, the fairy and Bobbin's lines, ending in Room 02 (978 frames; checked in the real window; the company line in charset 4 is replaced by the copyright line in the same frame, as ScummVM's order would). The CD playback tick makes up late frames so the scene stays with the audio. A click's voice lines start at the game time their prints ran (gaps are estimates); `sayVoice` operands are unsigned. Not heard by ear. Record: `.verification/loom-vga/README.md` step 16.

## 5 October (night, last): LOOM CD sounds and CD audio list, Game Status artwork

The CD's 30 `SO` sounds (AdLib and PC speaker parts) are read and playable on demand; no CD script starts them (script 39 is a stub). The tree has a "CD audio" list of the 313 voice and music cues the scripts ask for, played from the disc's audio track. CD Game Status shows the staff and note artwork and named drafts. Build warnings fixed (0 now). The intro is blocked on the CD music timer `var[0x0E]` (not modelled). Record: `.verification/loom-vga/README.md` step 15.

## 5 October (night, final): LOOM CD voices and status

CD audio: `DiscImage.ReadAudio`/`ReadAudioWave` cut the audio track (44.1 kHz, 16-bit, stereo); a voice cue (`LoomVoiceCue`) maps to track frames as ScummVM does (offset x 7.5 minus the first track's 22,500 frames and the 150-frame lead-in; run time x 7.5 + 5), and `LoomMessageTable.VoiceEvents` lists every `sayVoice` a reached print issued. The app's `CdVoicePlayer` queues a click's lines one after the other, a stop alone silences the voice, cues a room visit issued are skipped, and a Voices checkbox (saved with the view options) turns it off. The CD status schema (`LoomStatusSchema.Read` with a data source) is derived from scripts 010, 017, 018, 022, 023, 025, 026 and 034, so Game Status works for the CD game and its staff and note edits show the draft verbs. Record: `.verification/loom-vga/README.md` step 14.

## 5 October (night, last): LOOM CD verb strip, per-message charset, voice cues

The verb strip and the one-screen layout now serve the CD game (the draft verbs draw from room 1's images once script 018 turns them on; the CD staff/draft state needs its own schema, not done). A message keeps the charset set when its print ran (script 044 sets 4 for one intro line) and `print` sub 8 (`sayVoice`) is read as a voice cue (disc offset and length in tenths of a second) with no "not modelled" estimate. Double-click checks in rooms 3 to 20 draw their descriptions. Record: `.verification/loom-vga/README.md` step 13. CD audio is the remaining CD item.

## 5 October (night, later): LOOM CD text on screen

The CD charsets (`901` to `904.LFL`, 1 and 2 bits per pixel, per-glyph offsets) decode (`LoomVgaCharset`, `LoomVgaArchive.ReadCharset`) and `LoomVgaMessageRenderer` draws the session's talk and drawn strings in the app (charset 2, black outline from the colour table). The CD room screen is 144 rows, as on EGA. Record: `.verification/loom-vga/README.md` step 12.

## 5 October (night): LOOM CD click check, no EGA artwork estimate, matrixOps

`CdCheck` clicks several rooms: Bobbin walks (room 4's cutscene, room 16), room loads are reached (rooms 5 to 8). The purple artifact at the top left of room 4 after its cutscene was the EGA artwork estimate (`FillGaps`) painting a stored 48 x 40 image over a live CD room; the CD game takes no estimate now (`CreateSessionRoomSelection`). `matrixOps` sub 1 runs in the executor as `setBoxFlags`; scale sub-opcodes and `createBoxMatrix` are presentation operations (test `MatrixOpsRunsInTheExecutorAsAWalkBoxFlagChange`). Record: `.verification/loom-vga/README.md` steps 10 and 11. Texts: see the next entry.

## 5 October (late night, last): LOOM CD live session and clicks

The CD game gets a live `LoomExplorationSession` in the app (`MainWindow.OpenGameFolder`, `IsLoomGame` gate for keys, talk-stop and the playback bar; EGA-only readers such as Game Status, glossary, catalog and sound stay on `GameId.Loom`). A click reaches scripts 005 to 007 as on EGA (verb 0x37 walk-to, 0x38, double click 0x36); the CD boot now turns user input on (labelled estimate, the intro that does it is skipped). Room 4's click starts its cutscene (script 002) in the real window (`CdCheck`: `click-04.png`). Record: `.verification/loom-vga/README.md` step 9.

## 5 October (late night, later): LOOM CD depth masks and camera

The CD data carries the depth mask (z-plane 1) after the strips of each `BM` and `OI` body: the size field counts the strips, then a length word, one offset word per strip and the EGA mask's run coding. `LoomVgaArchive` reads it (`RoomImage.DepthMask`, `RoomObjectImage.Mask`); the room view hides an actor in a masked walk box behind it, with the drawn objects' masks as on EGA (rooms 4, 16, 18, 22, 35, 66 and others clip a Bobbin or actor, tests `LoomVgaTests`). The CD room view starts at the camera the entry script left (room 4's Bobbin stands in view). Record: `.verification/loom-vga/README.md` step 8.

## 5 October (late night): LOOM CD walk boxes, Bobbin on screen

`BX` walk boxes read for the CD game (`LoomWalkBoxReader.ReadBlockBody`); `LoomEgoArrivalIndex` and `LoomSceneStateScanner` read through the data-source seam (`GlobalScriptIds`, `ReadCostume`); the CD costumes drive the actor table (`LoomCostume.ReadVga`, 16-entry colour table into the room palette, `LoomActorLayer.IsDrawn` because Bobbin uses palette index 255); the CD boot continues past the intro so the ego exists (`var[0x001] = 1` from script 001's own code), and an assumed arrival places Bobbin as in EGA catalog visits (labelled). Found and fixed: the CD data source gave global scripts a context text, which the session reads as "room-owned", so script 001 never survived its room load. The CD room view draws Bobbin and the entry script's actors (rooms 35, 16 checked in the real window). Record: [.verification/loom-vga](../.verification/loom-vga/README.md), step 7. Tests: `LoomVgaTests` (walk boxes, costumes, Bobbin in room 35); 113 filtered tests passed on `loom ega mt32 en`.

## 5 October (night): data-source seam; LOOM CD first-visit object states

`IScummDataSource` (Core) is what the exploration session and `LoomSceneDrawBatches.Build` ask for: global, entry, exit and local scripts, a room's object records, names, walk boxes and size, object images and verb code. Two implementations: `LoomEgaDataSource` (the old `GameResource.FilePath` reads, behaviour unchanged; the filtered session, scene, first-visit and demo tests pass on `loom ega mt32 en`) and `LoomCdDataSource` over `LoomVgaArchive` (new there: `ReadGlobalScript` through the `0S` directory, `ReadObjectImages` for the `OI` blocks, `ReadRoomSize`, `ReadIndexedObjects` for `0O`). The session now accepts the CD game, runs its boot script and each room's entry script, and `LoomCdRoomScene.FirstVisit` builds the room as that script leaves it; the app's CD room view and room cards use it (no artwork gap filling: the CD backgrounds carry the permanent scenery, and where an entry script branches on a random value the session's seeded pick is labelled). `OI` images decode exactly: the images the entry scripts draw in rooms 13 and 17 equal the background pixels under them. Not yet read for the CD game: walk boxes, Bobbin and costumes inside the session, verbs in the UI. Tests in `LoomVgaTests` (source reads, a session visit of all 78 rooms, first-visit pictures). Record: [.verification/loom-vga](../.verification/loom-vga/README.md), step 6.

## 5 October (evening): LOOM CD costumes

The 165 costumes of the CD game decode (`LoomVgaCostumeRenderer`: the 16-colour classic format with block-relative offsets) and show in the app's Costumes tab and pose gallery (new real-window harness `.verification/loom-vga/CdCheck`). Test `EveryCostumeOfTheCdGameDrawsAtLeastOnePose`. Record: [.verification/loom-vga](../.verification/loom-vga/README.md), step 5.

## 5 October (later): LOOM CD scripts, objects and texts

`ScummScriptDialect.LoomCd` (the v5 operand forms: `roomOps` with its sub-opcode first, `matrixOps`, `getActorScale`, `soundKludge`, the line-height-free print and the charset colours) reads all 793 scripts of the CD game without an issue (it was 742 as EGA). `LoomVgaArchive` now returns each room's scripts (`ReadScripts`) and objects (`ReadObjects`: position, parent, walk point, name, verb table). The text extractor has a CD branch: 1,196 texts (1,042 spoken lines, 134 object names), and the Game Texts window opens for the CD game. `TextGalleryCheck` no longer searches for a hard-coded Spanish word. Record: [.verification/loom-vga](../.verification/loom-vga/README.md).

## 5 October: LOOM CD (VGA) opens, room backgrounds only

The app opens `Games/loom vga en` (a folder holding the `.cue`, the `.cue` or `.bin` itself, or a folder with `000.LFL` and `DISK01.LEC` copied off the disc) and shows all 78 room backgrounds with their 256-colour palettes in the room picker and the scene view (`GameId.LoomVga`, a new game id, so the EGA features stay off). New in Core: `Storage/` (`IGameFileSource` with a folder and a disc-image implementation; `DiscImage`: cue sheet, MODE1/2352 or plain ISO sectors, ISO 9660 listing) and `Formats/LoomVgaArchive` (the `.LEC` XOR 0x69 block tree, the `RN` room names, the strip decoder). Record: [.verification/loom-vga](../.verification/loom-vga/README.md). The kixx dump carries identical rooms. Rooms 61, 65 and 69 have an all-black background (carrier rooms). No objects, scripts, actors or sound for the CD game yet; the sound device menu is hidden for it. Tests: `LoomVgaTests` (do nothing without the folders).

## 5 October: Games folders renamed (the Roland copy says so)

`Games/loom ega en`/`es` (Roland upgrade installed) became `loom ega mt32 en`/`es`; the former `loom ega ibm en`/`es` (plain game) became `loom ega en`/`es`. `loom ega mt32 en` stays the reference edition: `TestGames`, `run-verify.cmd`, the harness scripts, the README, CLAUDE.md and the golden folders `.verification/readable-scripts/loom ega mt32 en|es` use the new names. Older entries below name the folders as they were called then.

## 5 October: AdLib notes re-attack (the chip's register write latency)

Some AdLib instruments sounded low or different from ScummVM in the title music, and the spell effect with the stars (sound 23) faded away. The cause was ours: the driver writes a note's registers between its key-off and key-on, and the Nuked chip received them all at one instant, so the key-off never registered and nothing re-attacked. `NukedOplChip` now uses Nuked's buffered writes (the real chip's write latency); the result sounds almost the same as ScummVM's MAME OPL core. A C# port of that core and a second "AdLib (ScummVM)" device were built and verified, then shelved: [archive](../.verification/archive/audio-accuracy-20261003/mame-opl-port/README.md), [validation](../.verification/archive/audio-accuracy-20261003/validation.md).

## 5 October: Roland MT-32 device (the LOOM Roland upgrade's music)

Sounds 33–64 of the EGA folders with the Roland upgrade are tagless MIDI-like streams (`F0/F1` waits, 120 ticks per quarter at 500000 us, program numbers only, MIDI channels 1–9), and the game's scripts choose them when the sound card `var[0x030]` is 4 (script 001, 039, 040, 044, 046, 095, 104; Roland id = other id + 32). New: `LoomRolandSong`, `LoomMt32Audio` + `Mt32RomSet` (Munt through Mt32emu.net 1.0.0, LGPL-2.1+; the user's own ROMs), `LoomExplorationSession.SoundCard`, and a fourth sound device "Roland MT-32" that sets the card to 4 so the scripts pick the Roland sounds (the opening then starts the overture, sound 56). Tests `LoomRolandAudioTests` (10); tools `Mt32Probe`, `RolandCheck`; record [.verification/mt32/README.md](../.verification/mt32/README.md). Not yet listened to by the user. Same day: the game holds while a track is still rendering (the overture's first seconds were being skipped), and the sound device is remembered between runs (`view-options.txt`, `sound=`). The Roland opening no longer hangs (the music timer `var[0x00E]` now follows the song's markers), and opening a scene shows the wait cursor and the scene's own text ("Scene / Loading the scene…" replaces the welcome "Choose a room" at once), and preparing music a text over the room. `roomOps roomPalette` (the shadow palette) is now applied, so the opening title dims in colour and fades as in ScummVM.

## 4 October (late): the two LOOM demos open

`Games/loom demo short ega en` and `loom demo long ega en` (`LOOMDEMO.EXE`) are another build of the game, not a subset: their `00.LFL` lists resources the folders lack, and rooms 72, 80 (long: also 73, 74, 76, 77, 81) exist only in them. The scanner keeps only index entries that start a resource and read as their kind (short: 19 scripts, 36 costumes, 20 sounds; long: 20, 52, 27) and sets `GameLibrary.IsDemo`; a demo's glossary leaves out script, bit, actor and sound names (script 045 there is the preview's director); the Game Status schema failure no longer stops the window from opening. A demo session keeps the director stopped so the rooms can be explored; "The preview" plays it on the playback clock from Room 01 through the existing Play Cutscene launchers (Room 01, Advanced picker) until another room is opened. Tools: `DemoProbe` (`index`, `texts`, `play`, `idle`), `DemoPreviewCheck`; tests `LoomDemoTests`. Record: [.verification/demos-20261004/](../.verification/demos-20261004/README.md).

## 4 October (night, 3): an interaction brings in the actors it involves

One rule replaces the El Muerto special case: an actor the scene or interaction uses (lines, walks, turns, animations) without placing him, or an actor that is the named object being interacted with, is brought into the room and dressed as the game dresses him. "Is the object" is read from the game's own placements: `putActorAtObject` on the object, or a `putActor` by the room's own code to a point inside its rectangle (Room 09: Dead One 275 = actor 4, Hetchel 273 = actor 5, egg 270 = actor 9, distaff 271 = actor 11). The dressing is the literal `actorOps` costume, elevation and talk colour before the game's `putActorInRoom` and the stand frame after it (`LoomActorPlacer.Dress`). The Advanced interaction menu and the picker both use it. Also fixed: the planner passed the actor's position in the wrong step fields (placements fell back to (240, 120)); the scanner now walks the game's code once per room for all actors (it was once per actor, 80 s on a first start). Tests: `AnObjectThatIsAnActorBringsHimIntoTheRoomWithAnyInteraction`.

## 4 October (night, 2): glossary and story flags checked on English

New tool `.verification/GlossaryCheck` compares every glossary entry across the editions ([ENGLISH_COMPAT_2026-10-04.md](loom/ENGLISH_COMPAT_2026-10-04.md), report in `.verification/glossary-english-20261004/`). Bits, variables, scripts, rooms and sounds hold; the actor names did not: the Room 43 cage scene's speakers are Mandible and Cob (not Elders), script 098's actor 3 is Rusty, script 086's actor 5 Mr. Stoke, Room 09's actor 4 the Dead One. Scoped glossary entries and scene-aware speaker names in the Game Texts browser; presenter goldens are now one set per edition.

## 4 October (night): El Muerto is drawn, room animation runs between inputs, build clean

El Muerto (Room 09, actor 4) was placed but kept the costume the last room gave his actor slot (Room 02's costume 141, a 32x13 floor shimmer), so only a few pixels showed while he spoke. `PlaceActorInRoom` now runs the game's own room-local script when the placer is one (Room 09 local 217: costume 0x82, elevation, walk/talk animations, talk colour, put at (68, 130)); other placers keep the plain placement. Tested by `ElMuertosLinesAreSpokenBecauseTheDerivedSetupBringsActor4IntoRoom09` (costume and place asserted); seen in SceneShots for Room 09 objects 275/0x56 and 274/0x36 (`.verification/el-muerto-20261004/run8`, `run9`). The main window now has an idle frame clock: with no playback running, one game frame runs per frame interval (10 a second), so ambient animation (Room 09 loom, swan, waves) no longer waits for the pointer; it pauses while another window is open or the app is inactive. The Options expander always starts collapsed (its open state is no longer restored). The three build warnings are fixed. New tool `.verification/CostumeSheet` draws every animation of a costume.

## 4 October (evening): migration to the English release

Unit tests, harness drivers and the replay catalog use `Games/loom ega en`; all 706 tests pass (5 skipped diagnostics). Catalog regenerated with the unchanged pipeline: 332 starts, 169/235 cutscene sites. Corrected bit layout (the Spanish 0xD62 is absent in English; bits from 0xD63 shift by one) and the object-id shift (+1 from Room 34). Details in [ENGLISH_COMPAT_2026-10-04.md](loom/ENGLISH_COMPAT_2026-10-04.md), validation in `.verification/english-catalog-20261004/`.

## 4 October (later): English LOOM EGA, first compatibility step

Game Status finds its draft contract by instruction pattern (not byte offset or Spanish bit numbers), the "draft playing" and story-bit ranges and the glossary's bit names follow the edition's bit numbering (English lacks the Spanish bit 0xD62 and numbers bits from 0xD63 on one lower); 42 of the 63 English failures remain and are triaged in [ENGLISH_COMPAT_2026-10-04.md](loom/ENGLISH_COMPAT_2026-10-04.md) (almost all test-only: text, bit, offset and object-id expectations). New tool `.verification/ScriptDump`; UiShots on the English folder in `.verification/english-compat-20261004/`.

## 4 October: sphere rooms, El Muerto, button row, English data

Rooms 19 and 86 (the sphere visions) start as a plain sphere: the entry script's explicit reset to state 0 now wins over a later script's draw batch when the background already fills the position (only these two of the 76 first-visit renders change; new test `SphereVisionRoomsStartAsAPlainSphere`). The Advanced interaction menu applies the planner's actor placements before a direct verb (El Muerto's lines in Room 09 show). The Advanced button rows share one height and font and a 5 px gap. The tests default to the English data in `Games/loom ega en` (63 of 710 fail: [ENGLISH_COMPAT_2026-10-04.md](loom/ENGLISH_COMPAT_2026-10-04.md)). The harness review was corrected: exit code 0 hides FAIL lines in WPF harnesses.

## 4 October: harness clean-up and `Exe` compaction

Decided on effort, regression risk, rebuild cost, whether the code is still touched and use for other versions: 35 obsolete harness projects removed (20 stale UI checks, 15 probes needing an old saved input; source stays in git history), `StatusShots`, `TextGalleryCheck` and `SpellAudioCheck` repaired and passing, the rest kept (see [HARNESS_REVIEW_2026-10-04.md](archive/HARNESS_REVIEW_2026-10-04.md)). `Exe/` went from 27.6 GB to about 1.4 GB: the 75 evidence folders of 2–3 October are packed unchanged in `Exe/_evidence-archive/*.tar.gz` (each verified against its file list before removal). M5 parked; the other-versions order agreed (LOOM-specific vs generic, English LOOM/Indy3, Indy3 as second game).

## 4 October: full test and harness review, documentation rewritten

Build clean, 703 unit tests pass (5 skipped diagnostic dumps), DoorwayClickCheck 16/16, RoomAudit zero gaps, Blockers 0 blocked sites. All 129 verification harnesses were run: 76 run clean; the other 53 are stale checks (UI changed on purpose), one-off probes needing an old saved input, documented failed investigations or deliberate crash probes, with no game-behaviour regression found. Nothing deleted: the list for review is [HARNESS_REVIEW_2026-10-04.md](archive/HARNESS_REVIEW_2026-10-04.md). Docs: the 90 KB plan was rewritten to its current state and open items, its dated checkpoints moved unchanged to [PLAN_HISTORY_2026-10.md](archive/PLAN_HISTORY_2026-10.md); CURRENT_WORK and NEXT_SESSION_PROMPT rewritten (RTK block removed, plan for other versions proposed). Records: `.verification/harness-review-20261004/`.

## 3 October: costumes that do not show

17 gallery costumes (20, 21, 42, 66, 70, 74, 75, 89-91, 136, 171-176) have index entries that point at no costume data and no script uses them; not a renderer bug. Record: [costume-leftovers-20261003](../.verification/archive/costume-leftovers-20261003/validation.md).

## 3 October: WindowCheck harness repaired

It looked for the removed "Show exits" checkbox; those two checks are gone and the rest still runs (Rooms 03 and 17, exit 0). Record: [window-check-20261003](../.verification/archive/window-check-20261003/validation.md).

## 3 October: presenter phrases for roomOps, strings, doSentence and save/load

The readable script view no longer shows a raw line for them: camera limits, screen rows, shake, colour/palette settings, string operations, the sentence queue (verb 0xFE cancels it). Colour and palette phrases keep their numbers and claim no on-screen meaning. Test: `LoomScriptPresenterTests.RoomStringAndSentenceOperationsGetPlainPhrases` (operand shapes read from every LOOM script).

## 3 October: story bits named by catalog scenes

Advanced views only. `LoomGlossary.StoryBitLabel` is wired to the catalog scene titles (a background scan after a game opens), so the readable script view and the Assumptions name a story bit as "set by the scene: ..." with `reading` evidence; bits no catalog scene sets stay raw. Validation: [.verification/readable-scripts/validation.md](../.verification/readable-scripts/validation.md).

## 3 October: readable scripts, steps 4-6 (timeline, grouped assumptions, "Used by")

Advanced inspector only. **Last action** shows "what happened" as numbered steps built from the bounded run's events (`LoomActionTimeline`; each step reuses the presenter's phrases, a step from an untaken branch is marked "maybe", the raw trace stays below). **Assumptions** are grouped as "Arrived by a shortcut", "Estimates" and "Effects not drawn", each with one sentence on what it means for the picture (`LoomAssumptionGroups`; the original lines stay as the evidence in each group). **Used by** on every global script (an expander on the script page) and on the selected object (a line under its actions): `LoomUsageIndex` scans all code once, in the background, for literal `startScript`/`chainScript`/`startObject`/`loadRoom`/sound references. Calls through a variable and the player's clicks are not listed, and the text says so. Actor names in the glossary can be scoped to a script or a room (numbers are reused: script 42 actor 2 is the flying actor, script 44 actor 3 the fairy, Room 02 actor 5 the last leaf), so the Elder names apply only where the council speaks.

## 3 October: readable scripts (glossary, names, Readable/Raw view) and a draft verb check

`LoomGlossary` (Core, data file `loom-glossary.json`) gives variables, bits, scripts, actors, rooms, sounds and objects a name with its evidence (decoded, engine, reading, play); unknown things stay raw. The Advanced views use it: tree labels, the script header, the "starts Script N" rows and the Assumptions list. `LoomScriptPresenter` renders a script in plain language (one phrase per opcode family, text with its speaker, `if / else / while / repeat-until` recovered from the jumps, ScummVM's jump-if-false semantics) and ends with the list of names used and their evidence; the script view has a Readable checkbox (default on, remembered), Raw stays one click away. Golden outputs for scripts 91, 17 and 6: [validation](../.verification/readable-scripts/validation.md). Draft verbs checked against the scenes: 0x43/0x44 is Sleep and 0x47/0x48 Invisibility (they were swapped), the rest stands; evidence in the script map ("Draft verb names").

## 3 October: interface pass with the user (scene size, options, inspector, texts, interactions)

Scene area 508 -> about 620 px high at 1400x900 (the reserved music row, the title and hint lines are gone; the playback row holds Restart/Step only in Advanced). Options section with Never / On hover / Always for doorways and object hotspots, remembered with framing, Options state and the bytecode choice. Full screen (F11, corner button on the verb strip, Esc). Interact button and right-click menu in normal mode (Look at, Use / go, drafts by name); Advanced shows the same as an ACTIONS list in the right panel. Advanced inspector: Room entry (grouped rows, raw disassembly collapsed), Last action, Assumptions (this room only). Back-to-room button on script, sound and costume views; script view: bytecode toggle (default off) and Play as cutscene. Game Texts: first-visit room picture (cached per room), speaker names (`LoomSpeakerNames`) in a column, compact layout. One compact dark ComboBox and ContextMenu style. Verb names (`LoomVerbNames`); draft verbs 0x4C/0x4D corrected after play (Reflection / Healing). Interaction picks now start the playback clock (frames used to move only with the mouse). Cutscene picker: one short subtitle, details Advanced-only. Plan for the next step: [READABLE_SCRIPTS_PLAN.md](archive/READABLE_SCRIPTS_PLAN.md).

## 3 October: menu cutscenes keep the running session; LOOM flags split from the generic window

`PlayKnownCutscene` keeps the running session for a single scene (a late state stays) and `LoomSceneSetupPlanner.Adapt` skips grants the session already has (the staff pickup); stated sequences and the boot still start fresh. The story-flag naming moved from MainWindow into `LoomEgaStateFlags`, so Game Status only knows generic `StateFlag`s. Checked with `.verification/SceneShots` ([keep](../.verification/archive/setup-placer-20261003/keep/report.txt): pickup, then Room 43 scene in the same session, staff kept, D96 set by the scene).

## 3 October: Game Status lists the story events as checkboxes

`LoomStoryEvents` (Core) finds the 90 story bits (D72 and up) that the game's code sets to 1; 27 are set by a catalog scene (global script or object verb) and are named by its title, the others by the room script that sets them. `LoomExplorationSession.StoryEvents`, `StoryBit` and `UpdateStoryBit` read and edit them like the other status edits (exploration choices, applied with the window's Apply). The window's list is generic (`StateFlag`, `FlagSource`: name, detail, read, write); only the search and the naming are LOOM-specific. Checked in the real window with `.verification/StatusShots` ([captures](../.verification/archive/setup-placer-20261003/status/report.txt)). Design intent (user): the user controls the state; the app only raises it to the minimum an action needs.

## 3 October: placer audit of costume changes and story-bit room entries (no setup change)

Re-scan of the game's code for the two remaining audit cases. The scanner missed every costume change (it looked for `costume ` where the disassembler prints `costume,`); fixed and pinned by a test. New Core queries `FindCostumeChanges`, `FindWriters`, `FindStoryBitMoves`, `ScanRoomEntry`. Result: room entries already give the ego the costume they own on a catalog visit, no scene's own code presumes a leftover costume, and no scene clears a story bit other than D61 to presume it, so no further start setup is derived; the candidate chains (Room 43-45 cage scenes, ghost chain) and the open story-order decisions are in the [audit](../.verification/archive/setup-placer-20261003/audit-costume-entries.md).

## 3 October: scenes bring in the actors who speak (Room 09 El Muerto and others)

Room 09 object 275 verb 0x56 ("Other draft response at El Muerto") ran but showed only a half-second flash of the ghost: its two lines are printed for actor 4, who is not in Room 9 in a catalog start, and `actorTalk` shows nothing for an actor outside the room (ScummVM). The setup planner now also scans the object verb's own code (`Scan(..., objectId, verbId)`) for lines printed for constant actors the scene does not place itself, finds the game's own `putActorInRoom` + `putActor`/`putActorAtObject` into that room (`FindActorPlacers`), and applies the first one in the start room's code, else the first in code order, through the new labelled replay step `actor` (`PlaceActorInRoom`; a no-op when the actor is already there). It is listed as an estimate in the picker note with the other placements. Effect on the 29 catalog entries it touches (probe, 3000 frames): Room 09 obj 275 0→2 lines, Room 09 obj 274 3→6, Room 23 objs 422-425 0→1 each, Room 51 obj 742 0→1; the rest had the actor already and are unchanged.

## 3 October: catalog arrival takes the size of its depth (Room 17 from Room 22)

An arrival with no size of its own took the size carried back through earlier rooms (Room 16's entry: small), whatever depth it stands at. Room 17's Room 22 door arrives at (252, 141), near the viewer, and showed a tiny Bobbin. `EstimateEgoSize` now takes, for such arrivals, the nearest sample in depth from the room's own code (other arrivals that set a size and walk to a point; the watcher jumps in local scripts that start script 017 with a literal and `putActor` the ego): size 0 here (Room 16 object 349, y 134). Labelled as an estimate in the uncertainties. Arrivals with their own size (Room 16 doors) are unchanged. The Room 17 watcher cutscene (local 200 +0x0022, box 4/5 jump to the centre with size 0/1) already plays on a click toward the centre.

## 3 October: generic per-scene start setup (staff pickup derived from the game's code)

`LoomSceneStateScanner` scans a scene's scripts (plus the scripts and room entries they reach) for reads and writes of globals and story bits; `LoomSceneSetupPlanner` derives the least labelled start from that. Scenes whose own global script clears bit D61 (091, 093) now start after the game's real pickup (Room 09 object 271 verb 0x38, found by `FindConstantWriters`) run unseen, then a catalog visit; no `staff` edit and no story flag. Reads of D61, var[0x07D] and story bits keep their first-visit value and are listed in the picker note. Costume, visit-chain and `place` derivations found no scanned dependency and are not applied. [Validation](../.verification/archive/setup-placer-20261003/validation.md).

## 3 October: Forge lookout starts at the cliff end (new `place` setup step)

`PlaceEgo` / step `place`: a labelled, estimated ego placement with the camera put on it. The lookout scene now starts with Bobbin at (378,134) and camera 376 (the user's reference), not at (64,74) with camera 160. The 6→8 trigger is still unreachable (matrix 6→8 = 7; camera follow gives 320 at that end); no trigger claim. [Validation](../.verification/archive/cliff-scene-20261003/validation.md).

## 3 October: ghost scene arrives from Room 35 (head no longer clipped)

The "headless Bobbin" was the depth mask of object 565 (state 1 after scene 087) hiding the head at Room 33's arrival spot (64,73), not a limb/animation fault. The ghost sequence now hops 35→34 first, so the arrival is Room 35's (the route that is open after 087). [Validation](../.verification/archive/ghost-headless-20261003/validation.md).

## 3 October: lookout is a scene, ghost scene camera, staff for the firewood scene

"First sight of the Forge" is an ordinary scene (no Bonus label or filter). Playback now scrolls to the game's camera when a scene starts. The firewood sequences hold the staff (new `staff` step) as well as wearing Rusty's look. [Validation](../.verification/archive/bonus-room34-20261003/validation.md).

## 3 October: Rusty scenes separated, Rusty's look, Forge lookout named

The ghost scene (098) now plays alone: the earlier scenes it needs run unseen (new `RunSilently`/`silentSteps`). The firewood/dream sequences start with Bobbin in Rusty's look (Awakening and costume change run unseen first). The bonus scene is "first sight of the Forge (cliff lookout)", once-only per the user's memory. [Validation](../.verification/archive/bonus-room34-20261003/validation.md).

## 3 October: Room 34 Rusty ghost replay fixed

Three duplicate "Walk response, Reached Room 57" entries (objects 559, 560, 586 verb 0x37) started script 098 from the fresh state, where Rusty is still the sleeping living boy (Room 34's entry needs bit D9D from scene 087 to show the ghost). Replaced by one sequence after the firewood/dream scenes with a catalog visit step (`visit`); Game Texts can now offer it for script 098. [Validation](../.verification/archive/bonus-room34-20261003/validation.md).

## 3 October: bonus scene for Room 34 local200 +0035

The unreachable Room 34 watcher scene is playable from the cutscene menu as a labelled bonus preview ("Bonus scenes" filter): new `RunLocalScriptAt` and sequence step `script` start the scene body directly (132 frames, balanced). Not audited coverage (234/235 and fresh 169/235 unchanged). [Validation](../.verification/archive/bonus-room34-20261003/validation.md).

## 3 October: verify Room34 local207 +0079 through 097 after scene 087

Local207 +0079/end0100 ("¡De eso nada!") completes via actual clicks: route3 prefix to scene 087 (D9D), catalog hop to Room43, the 096 route, Look 684 (097) into Room34 layout A, then two clicks on 559 (098, then 207). Audited233→234/235 (99.57%),1 remaining (Room34 local200 +0035); fresh single-action169/235 unchanged. Labelled staff/notes/learned setup and the catalog hops only; no production/catalog change; protected57/history/App untouched. [Validation](../.verification/archive/global-scripts-input-20261003/validation.md).

## 3 October: verify script009 +002C through native boot and practice mode

Room69's "stall" is the copy-protection question: four glyph clicks (886,890,895,887) release the boot into Room02 with practice mode (876). Two notes then show verb 0x35 briefly; its click runs script009 +002C/end0079. Audited232→233/235 (99.15%),2 remaining; fresh single-action169/235 unchanged. Labelled staff/7-notes setup only; no production/catalog change; protected57/history/App untouched. [Validation](../.verification/archive/global-scripts-input-20261003/validation.md).

## 3 October: verify script022 +0005 through Room41 input

Script022 +0005/end001E ("too noisy to weave") completes in Room41 (D70=1): click 632 aims (631/633 are untouchable), then wrong notes 1,2,1,2 with labelled staff/7-notes setup. Audited231→232/235 (98.72%),3 remaining; fresh single-action169/235 unchanged. No production/catalog change; protected57/history/App untouched. [Validation](../.verification/archive/global-scripts-input-20261003/validation.md).

## 3 October: verify script096 through Room43 input

Script096 +0000/end00E7 completes through actual clicks (650 cage scene 093, 639, 653 look + Escape for 095, 656, 655 for 092, then 639) and loads Room46; 639 stays untouchable until 093 sets DB6. Audited230→231/235 (98.30%),4 remaining; fresh single-action169/235 (71.91%) unchanged. No status edits, production/catalog change; protected57/history/App untouched. [Validation](../.verification/archive/global-scripts-input-20261003/validation.md).

## 3 October: verify script066 through Room17 box8 input

Script066 +0000/end0152 completes through actual Room17 clicks on box8 (after the box4 scene 200 +0022/+0042 interrupts the first walk), then loads Room56. Audited229→230/235 (97.87%),5 remaining; fresh single-action169/235 (71.91%),66 gaps/49 owners/335 starts unchanged. No status edits, production/catalog change; protected57/history/App untouched. [Validation](../.verification/archive/global-scripts-input-20261003/validation.md).

## 3 October: verify global-script sites through input

Six global-script sites complete through accepted input: note keys with no aim/too-high/too-far give 013,011,012 (+0000); four wrong notes give 022 +0138; Escape during playback gives 005 +01C0; F5 plus the Cancel click gives 004 +0000/end0206 (session remains in Room70, return unverified). Audited223→229/235 (97.45%),6 remaining; fresh single-action169/235 (71.91%),66 gaps/49 owners/335 starts unchanged. Labelled staff/notes status edits only; no production/catalog change; protected57/history/App untouched. [Validation](../.verification/archive/global-scripts-input-20261003/validation.md).

## 3 October: verify Room02 script045 scenes through input

Room02 script045 box-change scenes complete through accepted clicks on boxes4,5,10 (twice, after an interrupted walk) and 11: +001D/end003E,+0049/end006A,+0075/end0092,+009D/end00BA, 535 frames,5 balanced scopes. Audited219→223/235 (94.89%),12 remaining; fresh single-action169/235 (71.91%),66 gaps/49 owners/335 starts unchanged. Two cycles (first interrupted before +009D, retained/excluded); no production/catalog change; protected57/history/App untouched. [Validation](../.verification/archive/room02-input-20261003/validation.md).

## 3 October: verify Room09 late-game casts through input

Room09 corrected full route (Look 272→209/101, then 273/4D, 4F, 51 keyboard casts with supported staff/7 notes/learned 11-13 setup) completes 273/4D +000D, local223/224 +0000 and script101 +0000. Input3300/direct3147frames,6/6 scopes;252 Executed recorded events,3 certainty-only differences. Audited215→219/235 (93.19%),16 remaining; fresh single-action169/235 (71.91%),66 gaps/49 owners/335 starts unchanged. One cycle, no production/catalog change; protected57/history/App untouched. [Validation](../.verification/archive/room09-fix-20261003/validation.md).

## 3 October: park after Room34 prerequisite investigation

Room34 watcher prerequisite investigation stopped after two failed cycles: fresh38 Look610 reproduces incomplete087 in37; fresh36 exit599 is hidden by class24 and never dispatches. Four failed captures retained/excluded; no coverage gain or production defect established. Next source step is Room36 box9 watcher200 +0077/end009B, which clears599 class24 at+0094. Audited215/235 (91.49%),20 remaining; fresh single-action169/235 (71.91%),66 gaps/49 owners/335 starts unchanged. Protected57/history/App untouched. [Validation and precise next experiment](../.verification/archive/room34-watcher-20261003/validation.md). User explicitly requested parking and a next prompt after compactions; session gain23 audited sites from84da7df. No exact200K claim.

## 3 October: verify Room20 Waking through continuous input

Room20 remaining206 +0000 completes via Sleep350 keys, exit346, Room18 conversation372 then revealed crystal371, and Waking390 keys. SourceD8F missing→1→0; final18 via207/058/059. Input3263/direct3161frames,8/8 scopes;154 Executed/Conditional selected events,153 certainty-only differences plus one retaining argument-index uncertainty. Audited214→215/235 (91.49%),20 remaining; fresh single-action169/235 (71.91%),66 gaps/49 owners/335 starts unchanged. First hidden-bell attempt retained/excluded; second cycle passes. No production/catalog change; protected57/history/App untouched. [Validation](../.verification/archive/room20-input-20261003/validation.md).

## 3 October: verify Room47 return response

Room47 Cygna697 Look and actual exits696→46→681→47 complete205 +01B7/end03F1. Source100 removes703 class24; DC5/DC6 remain missing. Input/direct2423frames,3/3 scopes;117 Executed/Reached selected events match exactly. Audited213→214/235 (91.06%),21 remaining; fresh single-action169/235 (71.91%),66 gaps/49 owners/335 starts unchanged. One cycle; no production/catalog change, protected57/history/App untouched. [Validation](../.verification/archive/room47-input-20261003/validation.md).

## 3 October: verify Room33 Untwist/Twist input

Room33 Untwist/Twist keys on544 complete remaining202 +0006 after source201 establishesD81. Input706/direct604frames,4/4 scopes;63 Executed recorded events (entry Reached,62 Conditional),60 certainty-only differences plus two retaining argument-index uncertainty. Audited212→213/235 (90.64%),22 remaining; fresh single-action169/235 (71.91%),66 gaps/49 owners/335 starts unchanged. One cycle, no production/catalog change; protected57/history/App untouched. [Validation](../.verification/archive/room33-input-20261003/validation.md).

## 3 October: verify Room62 gold/straw input

Room62 three keyboard casts808/3D,808/3C,809/3D establish source gold/straw states and complete remaining202/204 +0000. Input692/direct539frames,3/3 scopes;63 Executed/Conditional recorded events,63 certainty-only differences. Audited210→212/235 (90.21%),23 remaining; fresh single-action169/235 (71.91%),66 gaps/49 owners/335 starts unchanged. One cycle, no production/catalog change; protected57/history/App untouched. [Validation](../.verification/archive/room62-input-20261003/validation.md).

## 3 October: verify Room28 camera, timed fire and continuation

Room28 actual camera click and470/3D then466/43 keys complete2030000, timed2120064 and0760000 through27/28. SourceCA0→1→2,D94missing→1→0; no story edits. Input949/direct847frames,9/9 scopes;207 selected Executed/Conditional body events,199 certainty-only differences. Audited207→210/235 (89.36%),25 remaining; fresh single-action169/235 (71.91%),66 gaps/49 owners/335 starts unchanged. One cycle, no production/catalog change; protected57/history/App untouched. [Validation](../.verification/archive/room28-input-20261003/validation.md).

## 3 October: verify Room08 tapestry restoration input

Room08 four keyboard casts256/3F,256/3E,257/3E,257/3F complete both tapestry restoration sites2110008/214000B with sourceD7D/D7E setup. Input1024/direct820frames,4/4 scopes,79 Executed/Conditional recorded events,76 certainty-only differences. Audited205→207/235 (88.09%),28 remaining; fresh single-action169/235 (71.91%),66 gaps/49 owners/335 starts unchanged. One cycle; no production/catalog change. Protected57/history/App untouched. [Validation](../.verification/archive/room08-input-20261003/validation.md).

## 3 October: retain bounded Room09 prerequisite investigation

Room09 two bounded cycles add no coverage. Source50→721/089→7→252→9 plus actual272 Look completes209/101,3/3 prefix scopes at1768frames; then probe incorrectly requires4D as reverse and stops before edits/casts. Slot11 is forward4D; slots12/13 reverse4F/51. Failed pairs retained/excluded; no engine defect established. Audited205/235 and fresh single-action169/235 unchanged. [Validation/next experiment](../.verification/archive/room09-input-20261003/validation.md).

## 3 October: verify Room22 watcher input

Room22 box11→10 clicks complete200 +0083/end00D3 then002F/end0078,235frames,2/2 scopes,23 Executed/Conditional body events. Audited union203→205/235 (87.23%),30 remaining; fresh single-action169/235 (71.91%),66 gaps/49 owners/335 starts unchanged. First wrong-order capture retained/excluded, second cycle passes. No production/catalog change; protected57/history/App untouched. [Validation](../.verification/archive/room22-input-20261003/validation.md).

## 3 October: verify Room17 watcher input

Room17 visible exit353 center312,72 then upper-path300,90 clicks complete200 +0022/end0042 and004D/end006D.241frames,2/2 scopes,15 Executed/Conditional body events. Audited union201→203/235 (86.38%),32 remaining; fresh single-action169/235 (71.91%),66 gaps/49 owners/335 starts unchanged. First off-picture click rejected/excluded; second cycle passes. No production/catalog change; protected57/history/App untouched. [Validation](../.verification/archive/room17-input-20261003/validation.md).

## 3 October: verify Room15 watcher input

Room15 actual box3/5 clicks complete200 +001D/end0062 and006D/end00C4 in249frames,2/2 scopes,23 Executed/Conditional body events. Audited union199→201/235 (85.53%),34 remaining; fresh single-action169/235 (71.91%),66 gaps/49 owners/335 starts unchanged. One cycle, no production/catalog change. Protected57/prior evidence and running App untouched. [Validation](../.verification/archive/room15-input-20261003/validation.md).

## 3 October: verify Room24 terrain and repeated dye input

Room24 box3/4 clicks and two dye keyboard casts complete all three remaining sites:200 +001D/+0089 and211 +0018. Source first dye changesC5=444→445 through211/213; repeat completes the refusal. Four balanced scopes,792 input/690 direct frames;115 recorded body events equal except93 retained certainty fields and two argument-estimate fields. Audited union196→199/235 (84.68%),36 remaining; fresh single-action169/235 (71.91%),66 gaps/49 owners/335 starts unchanged. One cycle, no production/catalog change; protected57/prior evidence exact. [Validation](../.verification/archive/room24-input-20261003/validation.md).

## 3 October: verify Room36 terrain watcher input

Room36 accepted box10→4→3→9 clicks complete all four watcher200 sites001D/0048/0077/00A6,547frames,4/4 scopes,29 Executed/Conditional body events. Audited union192→196/235 (83.40%),39 remaining; separate fresh single-action169/235 (71.91%),66 gaps/49 owners/335 starts unchanged. Two bounded cycles; failed first long walk retained/excluded. No production/catalog change. Protected57 and prior evidence exact; running App untouched. [Validation](../.verification/archive/room36-input-20261003/validation.md).

## 3 October: verify all four remaining Room10 sites through game input

Two terrain transitions, the clam's gull-feeding refusal and its later closing scene complete through clicks and note keys, with an actual Room5 exit/return supplying source state. Five balanced scopes,1266 input/1113 direct frames;86 recorded body events differ only in30 retained certainty fields. Audited union188→192/235 (81.70%); fresh single-action169/235 unchanged. No production fix, catalog change, App interaction or push; protected57 exact/unstaged. [Validation](../.verification/archive/room10-input-20261003/validation.md).

## 2 October: verify Room23 shepherd and terrain watcher input

Six actual walking inputs complete202/214 and four200 sites,6/6 scopes in1393frames;139 recorded body events remain Conditional/Executed. Audited union182→188/235 (80.00%), session+12; fresh single-action169/235 unchanged. Two bounded cycles, failed centroid route retained. No production change, App interaction or push; protected57 exact/unstaged. [Validation](../.verification/archive/room23-input-20261002/validation.md).

## 2 October: verify Room25 walking and repeated lamb input

Actual box4/box3 clicks, three lamb double-clicks and an exit click complete six previously unvalidated sites, returning24.208 recorded scene-body events exactly match the direct-object control; estimates preserved. Audited union176→182/235 (77.45%); fresh single-action169/235 unchanged. No production fix, catalog change, App interaction or push; protected57 exact/unstaged. [Validation](../.verification/archive/room25-input-20261002/validation.md).

## 2 October: verify Room34 spells through game input

Five keyboard casts, with staff/six notes/learned slots5 and10, trigger the same nine completed scopes as direct responses. Four new qualified source sites;127 scene events differ only in retained certainty labels. No production fix or catalog change. Audited site union172→176/235 (74.89%); fresh single-action coverage stays169/235 (71.91%). All57 protected files exact/unstaged; no App interaction or push. [Input evidence and coverage definition](../.verification/archive/room34-sequences-20261002/validation.md).

## 2 October: verify Sharpening reverse with the user's confirmed setup

One bounded Core keyboard reproduction uses the confirmed Game Status staff/enough-notes/learned edits and completes633/4B ->204(0) ->090, through42/43/60/43,15/15 scopes,6342 total frames. Full204 conversation; no skip of204/090. Source notes/reverse flag unchanged and estimates retained. No additional gameplay fix needed after0318792, no observer expansion; literal coverage stays169/235=71.91%. Raw4/history/protected57 unchanged/unstaged, no App launch/close/interaction or push. [Validation and limits](../.verification/archive/sharpening-reverse-20261002/validation.md).

## 2 October: catalogue the completed sphere Look preview

Room18 sphere370/36 independently completes065 and meets the existing fresh literal preview criteria in229 frames. Added shared Play Cutscene recipe; coverage **169/235=71.91%**,335 starts (85 scenes/250 reactions),66 sites/49 owners outstanding. All330 existing recipes/memberships and both sequences unchanged; direct-verb/estimated-state labels retained. Fresh Core-only audit and actual catalog/name loader; no App launch/close/interaction or push. Prior Core662/broad runtime baseline remains dated. [Review](../.verification/archive/sphere-catalog-20261002/validation.md).

## 2 October: let executed estimated room loads continue

The session no longer stops an executed room load merely because its path carries estimates. Known destinations proceed with uncertainty preserved through room entry/exit; unknown destinations and unresolved branches remain blocked. The unchanged saved spell-input capture now completes090 through42/43/60/43, and looking at the Room18 sphere completes065. Other362 literal roots, four memberships, two sequences,76 room renders and15 direct-cage files remain exact. Core662,Blockers0,clean Release,protected57 exact/unstaged; no App launch/close/interaction or push. Original recipes/history and catalog coverage unchanged. A separate response18 experiment lacks the user's learned spell/available notes and does not establish their setup. [Validation](../.verification/archive/estimated-room-loads-20261002/validation.md).

## 2 October: preserve the source-closed cage and name all draft responses

From627b682: both catalog capture previews retain the closed Room43 background instead of borrowing the open-door image from later091. Generic fallback respects source-selected hidden regions, including overlapping images, while preserving the established entry/opening/unconditional-reset and gap policies. Room08's equivalent future-overlay guess is also corrected; other74 renders exact, all76 zero-gap. No native route improved; raw4 and all execution barriers stay unchanged. Core657,363 exact roots/fingerprints/four memberships/two sequences,Blockers0, clean normal main Release. No App launch/close/interaction or push;57 protected files exact. [Validation](../.verification/archive/room43-cage-20261002/validation.md).

All132 numbered draft-response titles now use the existing Game Status spell associations in the shared catalog, including reverse directions. Global picker, room picker and Game Texts agree; original recipes, IDs, generated JSON and evidence are untouched. [Naming validation](../.verification/archive/room43-cage-20261002/names-validation.md). The user clarifies that Play Cutscene works, while manual18 remains blocked before42; its exact native setup is separate from unchanged raw4.

## 2 October: bound the raw4 failure and separate the reported cage issue

Continued `55cb5ec`; no route improved. Rebuilt the existing Core probe and replayed only the unchanged Room41 raw4 recipe: whole packet byte-exact, Room41,15/14,7659frames. Two diagnostic cycles confirm the conditional Room42 rejection leaves no090 resumption and that uncertainty predates204-to090; no execution, observer, catalog or recipe change. The user clarified that Play Cutscene reaches the cage but shows it open rather than expected closed; preserve this separate report for a bounded source state/image check of the exact selected entry. All57 protected files remain exact/unstaged, prior runtime suite/build remain dated, no App launch or push. A stray App was stopped before the user opened another and explicitly requested it be left open. [Validation, limits and reproduction](../.verification/archive/room41-route-20261002/validation.md).

## 2 October: prioritize a concrete blocked route

After `9b5fdc3`, the user requested a new continuation prompt and working rules focused on route results. Room41 native090 with the unchanged raw4 recipe is the first target. The plan now bounds diagnostic detours to two focused cycles without a route-relevant implementation decision, defers general observer expansion, and requires plain-language before/after results. Existing evidence and constraints remain; genuine execution fixes must explain fresh differences without rewriting historical packets. CLAUDE.md and the handoff carry the same priority. Documentation-only change: no new runtime validation, route completion or coverage increase;168/235=71.49% remains. See [current plan](LOOM_EGA_PLAN.md#current-priority-make-one-blocked-route-work) and [next-session prompt](NEXT_SESSION_PROMPT.md).

2 October continuation from `f649ff1`: `02c9fc1` rejects stop/resume result payloads on actual dispatch/restoration/continuation instructions; `b65cb4e` retires previous native lifetimes across omitted observation sequences. Twenty call-phase cases reproduce15 false correspondences;12 native cases reproduce5 stale-lifetime failures. Complete incoming-dispatch initialization/continuity and every boundary/barrier remain;opaque caller-end effects stay independent of actual child assignment. String contents/length/lifetime and exhaustive raw/native effects remain unresolved. No equality-derived origin,successful return,domain/effect transport or completion promotion. Fresh Core654/focused328;363 exact roots/fingerprints/memberships/sequences;Blockers0;76 unchanged zero-gap renders;13 exact primary packets/chronologies;primary366/55/0 and76 original writer terminals/0 new original writer resumed chains;15 unknown message fields/camera waits0. Separate raw4 object16/14/9/3/195/rejection1,fallback0 and chronology IDs/estimates exact;separate camera55 waits/220 unknown fields with no lifetime/completion claim. Numeric report and native challenge3 expressions/17 steps/36 exact frames/11 reads/11 invalidations unchanged. Clean final main Release;all57 protected files exact/unstaged;App0/no launch/no push. Literal-site coverage168/235=71.49%;broader plan has no fixed denominator. Evidence:`Exe/RuntimePhasePayloadFinalSession2-20261002`. [Validation](../.verification/archive/runtime-phase-payloads-20261002/validation.md),[checks](../.verification/archive/runtime-phase-payloads-20261002/check-summary.json). Continue remaining bounded callee/caller correspondence and implicit/string/native access/mutations under every retained constraint.

2 October continuation from `f84460f`: `6ad392d`, `3ed2da7` and `6c06692` require actual initialization/host/caller/child phases, including a terminated chaining caller before deferred initialization; `4b7c646` copies implicit ego plus independently unknown owner/inventory membership and notifies executed inventory mutations. Preserves `3f32e5f`, `32f1ecb`, `a3a90ec` and every earlier checkpoint. Nineteen boundary tests reproduce twelve former failures; fifteen owner/inventory/string controls preserve independent provenance and opaque string storage. Entire incoming-dispatch initialization/continuity remains required for caller restoration. No argument-derived origin, successful return, domain/effect transport or completion follows. Fresh Core622/focused296;363 exact roots/fingerprints/memberships/sequences;Blockers0;76 unchanged zero-gap renders;13 exact primary packets/chronologies;primary366 bindings/55 triples/0 caller resumptions/76 original writer terminals/0 new original writer resumed chains;15 unknown message fields and0 camera waits. Separate raw4 objects remain16/14/9/3/195 with one per-call128 rejection,loaded fallback0;separate camera55 waits/220 unknown fields. Numeric report and native challenge3 expressions/17 steps/36 exact frames/11 reads/11 invalidations remain exact. Clean final main Release,all57 protected files exact/unstaged,App0/no launch/no push. Literal coverage168/235=71.49%;broader plan has no fixed denominator. [Validation](../.verification/archive/runtime-boundaries-20261002/validation.md),[checks](../.verification/archive/runtime-boundaries-20261002/check-summary.json). Evidence:`Exe/RuntimeBoundariesFinalSession4-20261002`. Continue remaining actual callee/return/yield/resumed correspondence and implicit/string/native access/mutations under every retained bound and barrier.

## 2 October: observed caller incoming identity and independent camera access

| Change | Result | Record |
| --- | --- | --- |
| `3f32e5f` | Actual incoming dispatch initialization/continuity required for caller restoration;8 cases,5 prior false resumptions reproduced;all omitted/native/cancellation/replacement barriers retained | [identity](../.verification/archive/runtime-restoration-20261002/identity-validation.md) |
| `32f1ecb` | Four unknown-provenance native camera wait fields retain global/opaque barriers;independent same-value/no-op camera mutation notifications;12 cases | [camera](../.verification/archive/runtime-restoration-20261002/camera-validation.md) |
| Final preservation | Core588/focused262;13 entire latest streams exact;primary366/55/0;separate raw4 object16/terminal14/triple9/resumption3/restoration195/rejection1 exact;separate camera55 waits/220 unknown-provenance fields;363 roots/fingerprints/memberships/sequences;Blockers0;76 exact zero-gap Core renders;numeric/challenge exact;clean final main Release;App0;57 protected files unchanged;168/235=71.49% unchanged | [validation](../.verification/archive/runtime-restoration-20261002/validation.md) |

## 2 October: object/message preservation and separate loaded caller resumption

| Change | Result | Record |
| --- | --- | --- |
| `aaaa755` | Actual object selected-entry binding;fallback/defect controls;independent child/host/caller/restoration boundaries | [object](../.verification/archive/runtime-object-correspondence-20261002/object-validation.md) |
| `56ffa35` | Native message wait fields retain unknown provenance;executed message/no-op notifications | [message](../.verification/archive/runtime-object-correspondence-20261002/message-validation.md) |
| `729cbb9` | Callee begin-site filtering suppresses selection metadata and preserves omitted initialization barriers | [filter](../.verification/archive/runtime-object-correspondence-20261002/filter-validation.md) |
| Final preservation | Core568/focused242;13 exact primary streams;366 original bindings/55 triples/0 resumptions;separate unchanged raw4 capture16 object bindings/9 triples/3 caller resumptions;76 original writer terminals;363 exact roots/fingerprints/memberships/sequences;Blockers0;76 unchanged zero-gap renders;numeric/challenge exact;clean main Release;App0;57 protected files unchanged;71.49% literal-site coverage unchanged | [validation](../.verification/archive/runtime-object-correspondence-20261002/validation.md) |

## 2 October: filtered callee selection metadata

| Change | Result | Record |
| --- | --- | --- |
| Callee-site filter | Selected object-entry metadata honors the omitted callee begin site;1 new case/call-focused54;original initialization/restoration barriers retained | [filter](../.verification/archive/runtime-object-correspondence-20261002/filter-validation.md) |

## 2 October: native message wait fields and notifications

| Change | Result | Record |
| --- | --- | --- |
| Message waits | Copied native status/delay/speaker retain unknown field provenance and original timing/effect barrier;executed same-value/no-op message notifications;12 new cases/focused45 | [message](../.verification/archive/runtime-object-correspondence-20261002/message-validation.md) |

## 2 October: actual object callee selection

| Change | Result | Record |
| --- | --- | --- |
| Object selection | Actual exact/fallback verb table entry and resource bind startObject independently of host return/caller continuation;20 new cases/focused53;no origins or completion promotion | [object](../.verification/archive/runtime-object-correspondence-20261002/object-validation.md) |

## 2 October: bounded actual callee/caller correspondence and native fields

| Change | Result | Record |
| --- | --- | --- |
| `00f01a8` | Actual callee assignments and independent child/host/caller/restoration correspondence;no numeric origins or successful-return/completion proof | [calls](../.verification/archive/runtime-correspondence-20261002/call-validation.md) |
| `8110900` | Copied drawObject/stored drawing positions/native signed _userPut;explicit identical/absent position/input notifications;native/effect barriers retained | [native](../.verification/archive/runtime-correspondence-20261002/native-validation.md) |
| Final preservation | Core535/focused209;13 exact packets/legacy/call/completion streams;76 original writer terminals;363 exact roots/fingerprints/memberships/sequences;Blockers0;76 unchanged zero-gap renders;numeric/native challenge exact;clean final main Release;App0/no launch/no push;57 protected files unchanged;71.49% literal-site coverage unchanged | [validation](../.verification/archive/runtime-correspondence-20261002/validation.md) |

## 2 October: actual callee/caller boundaries and notified native mutations

| Change | Result | Record |
| --- | --- | --- |
| `5fcc2a0` | Actual supplied/default/restored locals,dispatch,child end/yield,host return and caller continuation;no equality-derived numeric origins | [calls](../.verification/archive/runtime-calls-20261002/call-validation.md) |
| `995e97e` | Copied loadRoomWithEgo/implicit ego/string/owner/wait dependencies;native/effect barriers and estimates intact | [access](../.verification/archive/runtime-calls-20261002/access-validation.md) |
| `b3ee09e` | Executed object/actor native mutation notifications,including identical/attempted no-op writes;palette lifetime separate | [native](../.verification/archive/runtime-calls-20261002/native-validation.md) |
| Final preservation | Core501/focused175;13 exact packets/legacy chronologies/completion records;76 original terminals;363 exact roots/fingerprints/memberships/sequences;Blockers0;76 unchanged zero-gap renders;numeric/native challenge exact;clean final main Release;App0/no launch/no push/promotion;71.49% literal-site coverage unchanged | [validation](../.verification/archive/runtime-calls-20261002/validation.md) |


## 2 October: implicit chronology, native lifetimes and observed resumption chains

| Change | Result | Record |
| --- | --- | --- |
| `ec9a525` | Additional copied subcommand/implicit access and actual separate expression reads/writes/scratch chronology; barriers/estimates intact | [access](../.verification/archive/runtime-continuity-20261002/access-validation.md) |
| `75d9cd7` | Independent copied native field/selector lifetimes and explicit raw native same-value notification | [native](../.verification/archive/runtime-continuity-20261002/native-validation.md) |
| `9593435` | Fully observed bounded yield/begin/end continuity for resumed terminals; original invocation checks and completion limits intact | [terminal](../.verification/archive/runtime-continuity-20261002/terminal-validation.md) |
| Final preservation | Core460;13 exact packets/chronologies/completion records;76 own-child matches/separate own-invocation terminals;363 exact roots/fingerprints/memberships/sequences;Blockers0;76 unchanged zero-gap renders;numeric unchanged;3 source challenge expressions/17 actual steps;clean main Release;App0/no launch/no push/promotion | [validation](../.verification/archive/runtime-continuity-20261002/validation.md) |


## 2 October: selected native dependencies and observation boundaries

| Change | Result | Record |
| --- | --- | --- |
| Selected access `6442e4e` | Decoded expressions, nested pre-values, selected event/object-call/actor/print operands, independent native fields and explicit raw notifications; barriers/estimates intact | [API](../.verification/archive/runtime-dependencies-20261002/api-validation.md) |
| Source/causal boundaries `133b4d4` | Resource/parent/stage confirmation; own-invocation/parent termination; no callee-local argument binding or resumed continuity inference | [boundaries](../.verification/archive/runtime-dependencies-20261002/boundary-validation.md) |
| Final preservation | Core401;13 exact packets/chronologies;76 own-child matches/separate terminal observations;363 exact roots/fingerprints/memberships/sequences;Blockers0;76 unchanged zero-gap renders;numeric unchanged;clean main Release;App0/no launch/no push/promotion | [validation](../.verification/archive/runtime-dependencies-20261002/validation.md) |


## 2 October: access/lineage evidence and fresh preservation

| Change | Result | Record |
| --- | --- | --- |
| Copied access and lineage | Numeric operands, same-value runtime notifications, observed arithmetic/index/arguments; explicit unresolved/raw external/native-state/callee limits and all estimates retained | [validation](../.verification/archive/runtime-access-20261002/validation.md) |
| Independent completion precision | All13 whole packets/prior chronologies exact;76 own-literal-child zero waits and76 separate terminal observations; primary3036/4552→4553→4556→4557 unchanged;090 unresolved | [access evidence](../.verification/archive/runtime-access-20261002/access-summary.json) |
| Fresh checks | Core374;363 exact roots/fingerprints/four memberships/both sequences;Blockers0;76 unchanged zero-gap renders;numeric report unchanged;final main Release zero warnings/errors;App0/no launch/no push/promotion | [checks](../.verification/archive/runtime-access-20261002/check-summary.json) |


## 2 October: observed arithmetic/index lineage and separate writer termination

| Change | Result | Record |
| --- | --- | --- |
| Arithmetic/index/arguments | Captured before-values and recursive index origins; unsigned/wrapped numeric candidates require snapshot confirmation; no domain transport or callee binding | [lineage validation](../.verification/archive/runtime-access-20261002/lineage-validation.md) |
| Writer termination | Separate from literal-child matching; source identity/order and terminal status required; arithmetic zero, yields and stale/foreign ends do not become protocol proof | [lineage validation](../.verification/archive/runtime-access-20261002/lineage-validation.md) |
| API checks | Twelve lineage/five terminal cases; Core374, clean main Release; final preservation completed in the follow-up above; no App/push/promotion | [lineage validation](../.verification/archive/runtime-access-20261002/lineage-validation.md) |

## 2 October: numeric source access and notified runtime mutations

| Change | Result | Record |
| --- | --- | --- |
| Optional access records | Query/expression/range destinations and debug/delay reads; legacy bindings and instruction sequence preserved; unknown/filtered access stays a barrier | [API validation](../.verification/archive/runtime-access-20261002/api-validation.md) |
| Runtime mutations | Known session/message/status writes and removals retire relations even for unchanged values; raw external dictionary writes require explicit notification | [API validation](../.verification/archive/runtime-access-20261002/api-validation.md) |
| API checks | Nine new cases, Core357, clean main Release; native preservation follows the separate lineage task; no App/push/promotion | [API validation](../.verification/archive/runtime-access-20261002/api-validation.md) |

## 2 October: generic completion correspondence and fresh preservation

| Change | Result | Record |
| --- | --- | --- |
| Optional completion observer | Surviving literal writer, exact child/reader identity and sequence; independent estimates/terminal observations; stale/cancelled/aliased/external evidence rejected | [validation](../.verification/archive/runtime-relations-20261002/validation.md) |
| Native evidence | All13 packets and entire prior binding/cancellation/expanded chronology exact;76 own-child zero reads, primary3036/4552→4553→4556→4557 preserved;090 unresolved | [relations](../.verification/archive/runtime-relations-20261002/relation-summary.json) |
| Fresh checks | Core348;363 exact roots/fingerprints/four memberships/both sequences;Blockers0;76 unchanged zero-gap renders;numeric report unchanged;clean main Release;App0/no push/promotion | [checks](../.verification/archive/runtime-relations-20261002/check-summary.json) |

## 2 October: optional runtime relation lifetimes

| Change | Result | Record |
| --- | --- | --- |
| Runtime source relations | Historical read/move/argument lineage separated from live correspondence; writes/indexes/aliases/omissions/external effects/cancellation invalidate without changing estimates | [API validation](../.verification/archive/runtime-relations-20261002/api-validation.md) |
| Focused validation | Twelve lifetime cases; unchanged executor decisions and observation sequence; numeric domains remain source-only | [Core tests](../tests/SCUMMexplorer.Tests/LoomRuntimeRelationTests.cs) |

## 2 October: selected Room41 input/index provenance audit

| Change | Result | Record |
| --- | --- | --- |
| Selected operand chain | Boot1018/live3018 →whole native input[1,4,1,1] →056..059/indexes0..3 →actual023 slot4/index8,response/index9 →026(4,0); practice index0/estimated note1 bound; estimates intact | [validation](../.verification/archive/runtime-bindings-20261002/validation.md) |
| Completion/preservation | 13 exact whole packets/prior records; original3036/4552→4553→4556→4557 ordering and76 own-child literal zero waits retained; native090 unresolved; numeric source report unchanged | [bindings](../.verification/archive/runtime-bindings-20261002/binding-summary.json) |
| Fresh checks | Core326;363 exact roots/fingerprints/memberships/sequences;Blockers0;76 unchanged zero-gap renders;clean final main Release;App0/no push/promotion | [checks](../.verification/archive/runtime-bindings-20261002/check-summary.json) |

## 2 October: optional operand binding observations

| Change | Result | Record |
| --- | --- | --- |
| Core operand snapshots | Exact selected cells, separate value/index/path estimates, bit aliases and ordered arguments; unknown/filtered indexes unresolved; no execution policy change | [API validation](../.verification/archive/runtime-bindings-20261002/api-validation.md) |
| Fresh preservation | Five new cases/Core326;13 exact packets/prior sequences;363 exact roots/fingerprints/memberships/sequences;Blockers0;76 unchanged zero-gap renders;clean main Release;App0/no push | [checks](../.verification/archive/runtime-bindings-20261002/check-summary.json) |

## 2 October: production source/slot completion observations

| Change | Result | Record |
| --- | --- | --- |
| Optional Core observer | Copied read-only values/estimates, ordered instructions, stable causal slots and cancellation identities; no copied-Core patch or estimate change | [validation](../.verification/archive/completion-observer-20261002/validation.md) |
| Completion precision evidence | 13 exact whole packets/all prior instruction values;76 own-child zero waits; native write/wait both3036; limits retained | [observer](../.verification/archive/completion-observer-20261002/observer-summary.json) |
| Fresh preservation | Core321;363 exact roots/fingerprints/memberships/sequences;Blockers0;76 unchanged zero-gap renders;clean main Release;no App/promotion/push | [checks](../.verification/archive/completion-observer-20261002/check-summary.json) |

## 2 October: generic Core correlated numeric analysis

| Change | Result | Record |
| --- | --- | --- |
| Bounded source-analysis API | Whole correlated worlds, masks/indexes/arguments, opaque responses and explicit effect/callee boundaries; no runtime estimates changed | [validation](../.verification/archive/correlated-core-20261002/validation.md) |
| Fresh loaded-source reproduction | 162 worlds,52 flags,51 comparisons/14 overlaps,four Cartesian witnesses,12 practice argument cases; Core316 and clean main Release | [summary](../.verification/archive/correlated-core-20261002/numeric-summary.json) |

## 2 October: per-slot cancellation and completion writer identity

| Change | Result | Record |
| --- | --- | --- |
| Generic script cancellation | Executing/nested/upcoming copies stop; replacements remain distinct; seven new cases, Core305 | [validation](../.verification/archive/certainty-protocol-20261002/validation.md) |
| Stable slot observer | Native031 completion and020 wait both frame3036; prior frame pairing corrected;13 whole packets and all prior instruction records identical | [slot evidence](../.verification/archive/certainty-protocol-20261002/protocol-summary.json) |
| Fresh preservation | 363 exact roots/fingerprints, unchanged memberships/sequences, Blockers0,76 unchanged zero-gap renders; clean main Release; no App/promotion/push | [checks](../.verification/archive/certainty-protocol-20261002/check-summary.json) |
| User-approved existing cleanup | Pre-existing tracked deletions and plan trimming included with the completed task | [scope](../.verification/archive/certainty-protocol-20261002/validation.md) |

Coverage checkpoint: all 45 incomplete starts diagnosed individually; 42 sound/helper waits, one actor-box wait, two unmatched scopes. Reached Room13 watcher recorded without promotion. [Research](../.verification/archive/replay-gaps-20261001/validation.md).

## 2 October: complete correlated draft predicate

| Change | Result | Record |
| --- | --- | --- |
| Source-only022/023 numeric interpretation | All162 forward/reverse worlds select slot4 before026;52 flag projections;51 previous comparisons/14 overlaps reproduced; four Cartesian input artifacts exposed | [research](../.verification/archive/correlated-drafts-20261002/validation.md) |
| Certainty limits retained | No selected runtime input/index, path, scheduler/cancellation, movement, dispatch or native090 proof; production and palette implementation unchanged | [summary](../.verification/archive/correlated-drafts-20261002/predicate-summary.json) |

## 2 October: Room41 instruction-level completion provenance

| Change | Result | Record |
| --- | --- | --- |
| Isolated source observer | Direct031 practice-note +0026 →D6C0 →031 completion +031D →020 wait +0028 provenance; all13 recipe packets identical, production unchanged | [evidence](../.verification/archive/certainty-provenance-20261002/validation.md) |
| Fresh Core preservation checks | Core298;363 exact roots/fingerprints; memberships/sequences unchanged; Blockers0;76 unchanged zero-gap renders; clean main Release; no App or push | [checks](../.verification/archive/certainty-provenance-20261002/check-summary.json) |

## 2 October: Room41 completion protocol and correlated-domain research

| Change | Result | Record |
| --- | --- | --- |
| Source completion/input audit | Four dye notes now known; gap snapshots bound020 D67 path taint; D6C terminal projection and D67 early-stop counterexample recorded without production resets | [evidence](../.verification/archive/certainty-precision-20261002/validation.md) |
| Packed-domain precision | Seven loaded023 masks,51 disjoint tuple comparisons and14 first-component overlaps; no native090 completion/promotion | [summary](../.verification/archive/certainty-precision-20261002/precision-summary.json) |

## 2 October: generic palette certainty precision

| Change | Result | Record |
| --- | --- | --- |
| Palette versus placement provenance | Palette-only actor commands retain color uncertainty separately; dye input notes/count stay known, existing movement estimates retained; no room exceptions or estimate clearing | [implementation](../.verification/archive/certainty-precision-20261002/palette-validation.md) |
| Fresh preservation checks | Core298 including nine palette cases;363 exact roots/fingerprints, memberships/sequences unchanged; Blockers0;76 unchanged zero-gap renders; main Release clean; native090 still Conditional | [checks](../.verification/archive/certainty-precision-20261002/check-summary.json) |

## 2 October: Room41 certainty layers and source-domain proof

| Change | Result | Record |
| --- | --- | --- |
| Native input certainty audit | Mouse/key pairs agree downstream; raw4 enters022 Conditional, older raw6 first taints at007D; inherited input/practice/actor dependencies localized without runtime writes | [evidence](../.verification/archive/room41-certainty-20261002/validation.md) |
| Source boot alternatives | 51 slot4/earlier-candidate comparisons exclude collisions; estimates and native090 gap retained, no engine/catalog changes | [derived source domains](../.verification/archive/room41-certainty-20261002/source-domains.json) |
| Preserved source recipes and checks | Both level4 orders,096 and four20 combinations unchanged; Core289,363 exact roots/fingerprints/memberships/sequences unchanged; Blockers0; Release clean | [checks](../.verification/archive/room41-certainty-20261002/check-summary.json) |

## 2 October: native level4 prerequisites and Room41 certainty continuation

| Change | Result | Record |
| --- | --- | --- |
| Native slot-use/level4 source recipes | Loaded native egg, dye and reverse-gold actions execute034 for slots0/2/1; both final gates execute029(4),9/9; randomized/source-path estimates retained | [recipes](../.verification/archive/native-prerequisites-20261002/level4-validation.md) |
| Room41 certainty localization | Raw4 source route replaces direct5/6 shortcuts; native15/14 remains unresolved, same-setup direct633/204 override15/15 completes43;022 is Conditional before slot4 selection; no code exception/catalog promotion | [evidence](../.verification/archive/native-prerequisites-20261002/validation.md) |
| Preserved recipes/fresh regressions | 096 source override and all four20 combinations unchanged; Core289;363 roots/fingerprints/memberships/sequences unchanged; Blockers0; Release clean | [checks](../.verification/archive/native-prerequisites-20261002/check-summary.json) |

## 2 October: source progression

| Change | Result | Record |
| --- | --- | --- |
| Generic expressions/native intro input | Signed integer postfix stack and nested seeded random scratch semantics; native201 challenge now executes; optional input-awaiting boot preserves default UI; Core289/WPF62, all roots/sequences/pixels unchanged | [implementation](../.verification/archive/source-progression-20261002/expression-validation.md) |
| Source096 completion | Actual095 Escape override writes known flags; staff/092/639 route completes096 in46,4/4; ordinary full095 control remains incomplete | [recipe](../.verification/archive/source-progression-20261002/late096-validation.md) |
| Usable20 arrivals/both responses | Actual16 sleep writes D8F, both18 arrivals settle20 with source ego,390/391 finish in all four combinations,7/7; fresh catalog20 gaps retained | [recipes](../.verification/archive/source-progression-20261002/room20-validation.md) |
| Room41/native level4 audit | Actual37 arrival/native633 still inherits Conditional090; direct633+204 override completes43 separately; correct D6F=0 and used-slot gates recorded; native challenge and egg input advance without native opening/raw4 claims | [source evidence](../.verification/archive/source-progression-20261002/validation.md) |

## 1 October: global cutscene access

| Change | Result | Record |
| --- | --- | --- |
| Status controls and normal costumes | Single-click Learned, read-only reverse,38px native notes, staff/note coupling with C/D/E edit minimum,16 source-associated names, stable toolbar position and graphical costume cards; WPF62/62, Core277/277, doorways16/16 and controls19/19 | [validation](../.verification/archive/status-refinements-20261001/validation.md) |
| Scythe background preservation | Generic fallback avoids index-hidden later erasing overlays; polishing scene indexed20 restored; only20 of76 room renders changes, zero gaps; source/replay results unchanged | [source and validation](../.verification/archive/status-refinements-20261001/scythe-validation.md) |
| Native draft progression and source-route correction | Native325 learning, source levels5/6, delayed active633 and native633/41 casting established;090/096 certainty and four fresh failures retained; transient20 entry corrected; Core275/275, main Release clean, no production/catalog changes | [validation](../.verification/archive/source-progression-20261001/validation.md) |
| Compact spell rows | Height reduced from32 to26; spell text, note selectors, checkboxes and Advanced provenance vertically centered; visible WPF47/47, final Release build clean | [validation](../.verification/archive/spell-row-layout-20261001/validation.md) |
| Staff bound, native score and Transcend | Inclusive levels1..8/no None, native combined artwork, sixteen loaded drafts, catalog-only higher-level preservation and verified limited floors; Core275/275, WPF47/47, all363 roots/sequences/pixels unchanged | [validation](../.verification/archive/staff-correction-20261001/validation.md) |
| Continued source dependency audit | Native634 is a valid Room41 aim target but high-note refusal remains; late096 actor movement finishes but certainty blocks load45; both fresh20 arrivals remain unestablished; all four fresh incomplete owners retained | [source findings](../.verification/archive/staff-correction-20261001/source-routes.md) |
| Requested UI refinements | Completed toolbar/Advanced visibility, uniform original names, ready-to-edit Apply/Close, musical labels, separate Learned/Can reverse, two verified name pairs and native graphical staff/contiguous notes; Core265/265, WPF42/42, all363 roots/sequences/pixels unchanged | [validation](../.verification/archive/ui-refinements-20261001/validation.md), [next prompt](NEXT_SESSION_PROMPT.md) |
| Game status | Live source-validated staff, available-note identities/count and fifteen draft sequences; explicit edits preserve flags/targets, busy/paused/stale guards; Core260/260, WPF16/16 | [status validation](../.verification/archive/game-status-20261001/validation.md) |
| Nested scope scheduling and three promotions | Frozen callers suspend; upcoming slots stay accessible; Room24/432 and Room41/63341/4B complete;334 starts,168/235 literal sites; late096/native Room41 input retained unresolved | [scope validation](../.verification/archive/draft-scopes-20261001/validation.md) |
| Generic sequence replays | 052/input/053 and086/look/087 promoted with audited per-stage waits, actual memberships and pause/cancellation guards;331 starts; literal-root gaps retained | [sequence validation](../.verification/archive/sequence-replays-20261001/validation.md) |
| Continued source routes | 052/input completes053 and086/look completes087, normal WPF8/8; Room41/096 remain unmatched, all site gaps retained; main Release rebuilt | [route investigation](../.verification/archive/coverage-routes-20261001/validation.md) |
| Sound-unblocked catalog promotion | 38 additional validated previews: 329 starts, 163/235 sites, 55 unresolved owners; all 38 WPF replays verified, global21/21 and room20/20 | [promotion](../.verification/archive/native-replay-coverage-20261001/validation.md) |
| Native sound completion waits | Finite AdLib clocks release script040 naturally; loops/unknowns retain status; 38 former failures now complete, seven unresolved; 253 Core tests and normal-clock WPF8/8 | [sound lifetimes](../.verification/archive/sound-lifetimes-20261001/validation.md) |
| Top-level Play Cutscene | Existing replay starts available from every room; playback/pause/departure guards preserved | [picker](../.verification/archive/global-cutscene-picker-20261001/validation.md) |
| Advanced Game Texts | Game Texts appears only in Advanced; normal view returns to interactive input | [visibility](../.verification/archive/global-cutscene-picker-20261001/validation.md) |
| Source-derived replay coverage | 291 global replay starts, grouped/searchable; 139/235 sites reached, 79 owners with explicit unresolved coverage | [replay audit](../.verification/replay-coverage-20261001/validation.md) |
| Unattended harness crashes | All rebuilt verification executables log fatal failures and exit 1; four deliberate crash probes pass without user input | [failure policy](../.verification/archive/harness-failures-20261001/validation.md) |
| Automatic interactive hover | Pointer checkbox removed; native hover in normal/Advanced, inspection preserved, room/note clicks survive both round trips, paused scenes do not advance | [input checks](../.verification/archive/automatic-pointer-20261001/validation.md) |
| Shared source-backed replay titles | Stored object names retained; 39 unnamed targets have action/reached-room descriptors and explicit naming gaps; 9/9 WPF checks | [names](../.verification/archive/replay-names-20261001/validation.md) |
| Room Play Cutscene | One match plays, multiple use shared filtered picker, none hides button; all 291 memberships from validated start/observed rooms including original four; 20/20 WPF checks | [room access](../.verification/room-replays-20261001/validation.md) |

Finished work, oldest first. Each entry links to the validation record that holds the details, the commands and the counts. Reverse-engineering findings live in the [script map](loom/LOOM_SCRIPT_MAP.md); priorities live in the [plan](LOOM_EGA_PLAN.md). This file replaces `WIP_2026-09-27.md` and the history table that was in the plan (28 September 2026).

Unless noted, "no physical input" applies: the WPF harnesses drive the real window's handlers with synthetic events.

## 23–26 September: groundwork

| Change | Result | Record |
| --- | --- | --- |
| Research and format readers | LFL index and room readers, EGA room decoder, costume poses, PC speaker and AdLib audio | [research](engine/RESEARCH.md) |
| Audit of exploration | Room state and object clicks were unresolved; direct verb calls labelled as exploration actions | [exploration audit](archive/EXPLORATION_AUDIT.md) |
| Parent-state correction | Code-only parents keep their states for image selection | [object states](engine/SCUMM_OBJECT_STATES.md) |
| Room 03 local scripts | Scripts 200–202 decode from Room 03's directory; object hotspots are focusable | [room03-local-action](../.verification/room03-local-action/validation.md) |
| Game Texts browser | Searchable lines, object names and stored strings with room context, then object context and conversation groups (719 entries at the time) | [game-texts](../.verification/game-texts/validation.md), [refinement](../.verification/game-text-refinement/validation.md) |
| Room viewport | 320 source px wide by default, scaled up, pan controls, costume pose gallery | [room-viewport](../.verification/room-viewport/validation.md) |

## 27 September: rendering, navigation and the boot

| Change | Result | Record |
| --- | --- | --- |
| Indy3 EGA opcode dialect and dialogue choices | `ScummScriptDialects.For(game.Id)`; choice sets grouped with their replies | [audit](archive/AUDIT_2026-09-27.md) |
| Call-order renderer, draw batches, gap filling, doorways through started scripts | SCUMM paint order (reverse directory, then the `drawObject` queue); 0 gaps in 76 rooms | [room-audit](../.verification/room-audit/validation.md) |
| Script-backed fallback for rooms with nothing visible | Script draw batches first, then a labelled first stored image | [fallback-batches](../.verification/fallback-batches/validation.md) |
| Global scripts 0, 51, 55, 116 | Leftover index entries pointing inside other resources; listed as such in Game Texts | [global-script-index](../.verification/global-script-index/validation.md), [leftovers](../.verification/game-texts-leftovers/validation.md) |
| One doorway button per target room | | [doorway-buttons](../.verification/doorway-buttons/validation.md) |
| Verb-area images kept off the room; reached and unavoidable entry states | First-stored-image rooms 24 → 4 | [fallback-evidence](../.verification/fallback-evidence/validation.md) |
| Room 03 exit | Object 162 → Room 04 reached; `stopScript` modelled; the `0xD7A` wait was read inverted before | [room03-exit](../.verification/room03-exit/validation.md) |
| `getRandomNr` | Labelled seeded pick; later steps are estimates | [random-picks](../.verification/random-picks/validation.md) |
| Doorway clicks and object classes | `findObject` order, class 24 untouchable; 75/75 doorway boxes navigate | [doorway-clicks](../.verification/doorway-clicks/validation.md) |
| Compact room panel | | [room-panel](../.verification/room-panel/validation.md) |
| Local-array draws, `setVarRange` | Room 04 rest frame | [local-array-draws](../.verification/local-array-draws/validation.md) |
| Cutscene, freeze, waits, `isSoundRunning` | Hooks `var[0x023]`/`var[0x024]`; waits continue next frame (timing labelled) | [opcodes](../.verification/opcodes/validation.md) |
| Global scripts across room loads, `beginOverride`, `isScriptRunning` | Cutscene bodies decoded (spoken lines 580 → 1086) | [cutscene-continuation](../.verification/cutscene-continuation/validation.md) |
| Boot past the intro | Room 69 answer assumed; opening cutscene 044; session starts in Room 02 with the real hooks | [boot](../.verification/boot/validation.md) |
| Walk boxes | Old-v3 BOXD reader and ScummVM box geometry; overlay checked by eye | [walk-boxes](../.verification/walk-boxes/validation.md) |
| Actor positions and queries | `LoomActorTable`; walks end at once (estimate) | [actor-positions](../.verification/actor-positions/validation.md), [analysis](../.verification/actor-queries/analysis.md) |
| Hotspot highlight and reverse hover | | [hotspot-highlight](../.verification/hotspot-highlight/validation.md) |
| Room music row | Reached cues only since entry, or the room's own and not stopped | [room-music](../.verification/room-music/validation.md) |
| Camera after `loadRoomWithEgo`, `actorFollowCamera`; optional entry pan | | [camera-ego](../.verification/camera-ego/validation.md) |
| Camera limits (`roomScroll`) | Room 04 opens on the half its entry picks | [camera-limits](../.verification/camera-limits/validation.md) |
| `findObject` and the input loop | Script 006 idles with an assumed pointer below the room | [input-loop](../.verification/input-loop/validation.md) |

## 28 September: input and arguments

| Change | Result | Record |
| --- | --- | --- |
| Catalog arrivals | A catalog visit takes a decoded `loadRoomWithEgo` as an assumed arrival | [catalog-arrivals](../.verification/catalog-arrivals/validation.md) |
| `loadRoom` arrivals | Rooms 12, 28, 31, 32, 43, 49, 65, 66 | [loadroom-arrivals](../.verification/loadroom-arrivals/validation.md) |
| Pointer as game input, `verbOps`, `startObject` | Optional hover through script 006; close-up from script 008 | [pointer-input](../.verification/pointer-input/validation.md) |
| A click as game input | `Click` through scripts 005–007 (optional in the app) | [click-input](../.verification/click-input/validation.md) |
| Verb area (presentation) and verb-area clicks | `LoomVerbArea`, `findVerbAtPos`; notes reach script 010 | [verb-area](../.verification/verb-area/validation.md), [verb-click](../.verification/verb-click/validation.md) |
| Keys as game input | Script 005 area 4 decoded; Escape's draft cancel corrected | [key-input](../.verification/key-input/validation.md) |
| `startScript` with a local target | Boot runs Room 01 locals 200–208 | [dynamic-scripts](../.verification/dynamic-scripts/validation.md) |
| VisitProbe input steps | `v:`, `k:`, `s:` | [probe-input](../.verification/probe-input/validation.md) |
| Escape's `abortCutscene` | Script 052 skips from Room 11 to Room 13 | [abort-cutscene](../.verification/abort-cutscene/validation.md) |
| Estimated script arguments | Draft notes passed as labelled estimates (commit 06fc19a) | [estimated-arguments](../.verification/estimated-arguments/validation.md) |
| Room loads by room-owned scripts end the script | ScummVM `startScene`; Blockers 5905 → 5839 (commit 3b15611) | [room-owned-loads](../.verification/room-owned-loads/validation.md) |
| Visual-first UI refresh | Room-art picker, larger scene, advanced details collapsed (commit 92c2e3c). CameraPanCheck 2 FAIL and VerbAreaCheck 1 FAIL since then | [ui-refresh-complete](../.verification/ui-refresh-complete/validation.md) |
| Replan | Priorities moved to visible behaviour: interactions, on-screen text, actors, playback. Plan and changelog cleaned up; no code changed | [plan](LOOM_EGA_PLAN.md) |
| M1e: the game's click by default | Explore-mode clicks go through scripts 005–007; double click adds verb 0x36; doorways fall back to their verb, labelled (75/75: 29 by the game's click); inspection mode cycles artwork | [game-click](../.verification/game-click/validation.md) |
| M1a/M1b: messages and the game's font (Core) | `print` → text slots, talk message, talk delay, `VAR_HAVE_MSG`, `waitFor message`; object names in text (code 6); charset 0 from `99.LFL` drawn as `CharsetRendererV3`. Blockers 5839 → 4979; cutscene cap 1000 → 20,000 frames | [messages](../.verification/messages/validation.md) |
| M1c/M1d: messages on screen and clicks that play forward (app) | Messages drawn in charset 0 over the room and in the verb screen (room row = screen y − 16); a click plays at 10 frames a second until the talk ends and scripts wait; clicking text skips it (assumed input). MessageAppCheck 0 failures; DoorwayClickCheck 75/75, identical to M1e; ClickInputCheck 0 failures; tests 206/206 | [messages-app](../.verification/messages-app/validation.md) |
| Room view checks fixed | The first room opened starts at the game's camera (it started at x 0); VerbAreaCheck's "off again" updated to the refreshed layout and the M1c verb screen. CameraPanCheck 2 → 0 failures, VerbAreaCheck 1 → 0 | [view checks](../.verification/view-checks/validation.md) |
| Object names under their close-up | `drawBox` (script 008) now covers earlier names, so one name shows at a time; the verb screen draws the verb slots with its text, so the close-up is above the name. Tests 207/207; MessageAppCheck, VerbAreaCheck, ClickInputCheck 0 failures | [names](../.verification/messages-app/names/validation.md) |
| M2a costume animation (Core) | `LoomCostume` starts, steps (loop or hold) and draws each limb as ScummVM's `ClassicCostumeLoader`; Bobbin's costume 1 stand, walk and talk frames rendered to PNG. Tests 212/212; no UI change yet | [costume animation](../.verification/costume-anim/validation.md) |
| M2b actor state (Core) | The actor table keeps costume, elevation, walk speed, frames, palette, facing and limb state; `animateActor`, `faceActor`, talk frames and the per-frame limb step apply them; `getActorCostume`/`getActorElevation`/`getActorFacing` answer. After the boot the ego has costume 1 without the distaff (frames 8–15). Tests 216/216; Blockers: only `expression` (3) stops; RoomAudit 0 gaps, renders unchanged | [actor state](../.verification/actor-state/validation.md) |
| M2c actors drawn in the room (app) | `LoomActorRenderer` (ScummVM `processActors` y-sort, drawn at (x, y − elevation)) and the app's actor layer, redrawn on each playback frame; `setClass` class 22 sets an actor's ignoreBoxes; box direction flags remap a standing actor's turn. Compared with ScummVM by eye: Room 02 after the opening matches except Bobbin's facing (walk estimate, M3a). Tests 220/220; ActorDrawCheck 0 failures; audit and Blockers unchanged | [actor-draw](../.verification/actor-draw/validation.md) |
| M3a walking in steps | Ports ScummVM `Actor_v3` walks (`startWalkActor`, `walkActor`, `calcMovementFactor`, `actorWalkStep`, `findPathTowardsOld`/`getGates`), turns over frames and `moveCamera` (new `LoomCamera`); `waitFor actor`/`camera` wait for the real arrival; `getActorMoving` answers; the app plays on while actors walk. Bobbin ends the boot in left profile (matches ScummVM). Boot 421 → 725 frames, script 052 74 → 267, the Elders' trial 2,119 → 2,784. Tests 223/223; ActorDrawCheck 0 failures; DoorwayClickCheck (5 rooms) 13/13; audit unchanged; Blockers: only `expression` (3) | [walk-steps](../.verification/walk-steps/validation.md) |
| M3a ScummVM comparison | The user drove ScummVM: one click on Room 02's path exit walks Bobbin right along the rock, the box-4 scene change puts him on the path in the smaller costume and he waits for another click; the camera follows. Matches the session. Skipping the opening with Escape leaves him facing the screen, as script 044 +0x063E/+0x0678 says | [walk-steps](../.verification/walk-steps/validation.md) |
| M3b frame timing | `delay`/`delayVariable` pause their slot for their jiffies (ScummVM `o5_delay`, `decreaseScriptDelay`: runs again once below 0); `VAR_TIMER`, `VAR_TIMER_TOTAL` and `VAR_TMR_1..3` count every frame from 0. Room 02's exit cutscene now holds the panned view 1.1 s before Room 03. Boot 725 → 775 frames, script 052 267 → 277, the Elders' trial 2,784 → 2,824. Tests 227/227; ActorDrawCheck 0 failures (new: the exit's delay is 66 jiffies, 1.25 s on the playback clock); DoorwayClickCheck (5 rooms) 13/13; audit unchanged; Blockers: the "delay … continues on the next frame" labels are gone, only `expression` (3) | [frame-timing](../.verification/frame-timing/validation.md) |
| Room 02's exit click | The doorway fallback does not run when the game's click changed the scene (a cutscene began and a script put the ego: `CutscenesBegun`, `EgoPuts`). Room 02's exit from the cliff now stops at script 045's box-4 scene and waits for another click, as in the game; 3 clicks reach Room 03 by the game's click. Room 37 object 601's refusal (a cutscene without a `putActor`) still falls back. Tests 227/227; new Room02ExitClickCheck 0 failures; ActorDrawCheck 0 failures; DoorwayClickCheck 13/13 on 5 rooms (11 by the game's click, was 10) and 75/75 on all (8 doorways need 2–3 clicks) | [room02-exit-click](../.verification/room02-exit-click/validation.md) |
| The game's 128 room rows | The room view shows rows 0–127, as ScummVM draws them (`initScreens(16, 144)`, `redrawBGStrip`); rows 128–143 (artwork in 67 of 76 rooms) and Room 02's actor 4 at (433, 129) are hidden. Room 18 object 368 lies wholly below; its "Open Room 22" button stays. PresentationCheck updated (0 failures); CameraPanCheck 0; VerbAreaCheck 1, the same as before the crop | [room-crop](../.verification/room-crop/validation.md) |
| M3c playback controls | A playback bar in the explore mode: pause/play (space), skip (Escape: `abortCutscene`, off until the cutscene's `beginOverride`), and "Play a cutscene" for script 052 (Room 10 object 295) and the Elders' trial (Room 08 object 255) from a stated start (fresh boot, catalog visit, the verb run directly, labelled). Script 052 plays through Rooms 10, 11 and 13 with actors and its delay (it prints no text, decoded); Escape in Room 11 reaches Room 13 at once; the trial shows its first line, then skips to Room 09. CutscenePlaybackCheck 0 failures; DoorwayClickCheck 13/13; Room02ExitClickCheck 0 | [cutscene-playback](../.verification/cutscene-playback/validation.md) |
| M3: the opening played (script 044) | "Play a cutscene" → "The opening (script 044)": a session whose boot stops where script 001 starts script 044 (`stopAtOpening`, `BootRunning`), then the rest of the boot on the playback clock: Rooms 01 (the points of light), 02, 01, 02, 53 (the fairy's four lines) and 02 (Bobbin's four lines) in about 71 s, ending exactly as the app's boot (775 frames; ego (828, 60) facing 270). Escape after the skip point (+0x0124) ends it in Room 02 with Bobbin facing the screen. Finding: the copyright print is CGA-only (`VAR_VIDEOMODE` 4). Tests 229/229; new OpeningPlaybackCheck 0 failures; DoorwayClickCheck 13/13; Room02ExitClickCheck, CutscenePlaybackCheck, ActorDrawCheck 0 | [opening-playback](../.verification/opening-playback/validation.md) |
| VerbAreaCheck "off again" | The check was stale: it expected 144 room rows and a scroll bar; since the crop the view shows 128 rows with pan buttons. The verb screen rightly stays for drawn strings ("cielo", the note letters), which have no talk delay. VerbAreaCheck 1 → 0 failures (twice); no app change | [verb-area-off-again](../.verification/verb-area-off-again/validation.md) |
| M3c: play from Game Texts | Game Texts shows "Play this cutscene" on a text printed by a known cutscene's script (044: 9 texts, 046: 40; script 052 prints none); the click closes the window, turns the interactive mode on if needed and calls `PlayOpening`/`PlayKnownCutscene`. The trial's first line is on screen after about 8.5 s; the opening shows Room 01's points of light within 0.5 s. Tests 229/229; new GameTextsCutsceneCheck 0 failures | [game-texts-cutscene](../.verification/game-texts-cutscene/validation.md) |
| M3: the firewood scene (script 086) | "Play a cutscene" → "Script 086": a catalog visit to Room 36, object 599's verb 0x38 run directly into Room 37, the arrival walk, then the game's click at (109, 102) in box 8; Room 37's local 200 chains to 086, which plays Rooms 37, 58, 37 and 38 with its 13 lines in about 90 s; Escape ends it in Room 38. Game Texts offers it on 086's lines. Finding: a catalog visit to Room 37 leaves the ego's box an estimate, so 086's Room 58 load is refused. Tests 230/230; new FirewoodPlaybackCheck 0 failures; DoorwayClickCheck 16/16 on Rooms 02, 03, 04, 08, 10, 37 | [firewood-scene](../.verification/firewood-scene/validation.md) |
| Cutscene layout (user report) | In the interactive mode the room keeps one size and position through a cutscene: the verb screen is always under the room (black when empty), the scroll-arrow row keeps its space and the arrows are hidden while the player has no control, and the controls below keep a minimum height. Disabled buttons are dark. New CutsceneLayoutCheck: 6 failures before, 0 after (one size in the opening and the trial); VerbAreaCheck 0 (two checks updated for the kept verb screen); PresentationCheck and CameraPanCheck 0 | [cutscene-layout](../.verification/cutscene-layout/validation.md) |
| Sound in cutscenes (user report) | While the playback clock runs, each sound a script starts plays from the frame that reached it, `stopSound`/`stopMusic` stop them, pause pauses them. Music as AdLib, effects as the PC speaker rendition (AdLib effects not decoded; labelled). New SceneSoundCheck: 0 failures (opening 1/1, trial 28/28 starts and stops on the same frames; the opening's music audible). Tests 230/230 | [cutscene-sound](../.verification/cutscene-sound/validation.md) |
| The text line (user report) | Checked against ScummVM first: messages are placed as the scripts say (the trial's at y 152, in the verb screen as in the game). The interactive mode now keeps the game's text line (rows 0–15) above the room and draws text there (Room 41's lines, y 12); text line, room and verb screen touch as one screen. New TextLineCheck 0; CutsceneLayoutCheck, VerbAreaCheck (expected scale updated), PresentationCheck, CameraPanCheck 0. Tests 230/230 | [text-line](../.verification/text-line/validation.md) |
| Whole first lines on the text line (user report) | Room 41's two-line messages (the sword) lost their first line: ScummVM clips a line starting at y 12 at the text screen's row 15. The app now draws such a line whole into the room's top rows (reading of the original, labelled; the user chose it). TextLineCheck 0 with a new sword step (742 lit pixels in room rows 0–3). Tests 230/230 | [text-line](../.verification/text-line/validation.md#follow-up-whole-first-lines-29-september) |
| Depth masks (user report) | Actors in a walk box with mask 1 are hidden where the room's depth mask, or a drawn object's own mask, is set (ScummVM `getMaskFromBox`, `decodeMask`, `byleRLEDecode`). Object masks decode in all 76 rooms. Bobbin walks behind Room 02's foliage. ActorDrawCheck 0. Tests 233/233 | [depth-masks](../.verification/depth-masks/validation.md) |
| Swans in the trial's flight (user report) | `actorOps` on a variable actor (`local[0x000], [costume, …]`) lost its first item, the costume, in all 41 such sites (ScummVM `o5_actorOps` takes a variable actor). The trial's Room 11 flight shows nine small swans (costume 80), not Hetchel and the elders as people; Room 03 shows its campfires (costume 9), not Room 02's leaf. ActorDrawCheck 0; RoomAudit 0 gaps. Tests 234/234 | [swan-flight](../.verification/swan-flight/validation.md) |
| LOOM's 144 room rows (user report) | The room's bottom was cut (Room 09's egg hidden). LOOM's boot runs `roomOps setScreen 0, 144` (script 001 +0x0000), ScummVM `initScreens(0, 144)`: the room screen is all 144 rows from screen row 0, with no text screen. The view shows 144 rows again and text is drawn over the room; the text line is removed. TextLineCheck, VerbAreaCheck, PresentationCheck 0. Tests 234/234 | [room-rows](../.verification/room-rows/validation.md) |
| Leaving a scene from the catalog (user report) | Picking a room while a conversation played left its cutscene open and input off (script 002), so no click was read there. The catalog visit now stops the scene's script and runs the cutscene end script (script 003), labelled as an explorer action. SceneLeaveCheck 0 (2 failures before); DoorwayClickCheck 16/16. Tests 235/235 | [scene-leave](../.verification/scene-leave/validation.md) |
| The ego's size after a catalog visit (user report A) | Rooms 02, 15, 17, 18, 22, 23, 24 and 36 keep the ego's size from the room before (`var[0x07C]`, script 017). A catalog visit now estimates it from the assumed arrival's route instead of keeping the size of the room shown before, and ends a left cutscene before the room's entry. Room 02 shows the small Bobbin from any room. Follow-up: a door's own size (Room 18 from 16) and the arrival's own `setClass`es before its load (Room 12's boat) are applied. SceneLeaveCheck, ActorDrawCheck 0; DoorwayClickCheck 16/16. Tests 239/239 | [ego-size](../.verification/ego-size/validation.md) |
| Actor clip classes (user report B) | ScummVM `drawActorCostume` honours class 21 (AlwaysClip, `_forceClip`) and class 20 (NeverClip) before the walk box's mask; the app ignored them, so ignore-boxes actors (box 255) were masked. Room 43's flames (actors 5–9) are drawn whole now; class-21 actors are masked in any box. ActorDrawCheck 0. Tests: `LoomActorRendererTests` 6/6 | [actor-clip](../.verification/actor-clip/validation.md) |
| The manual pan keeps to the game's camera limits (user report C) | Room 41 (336 px, `roomScroll` 176..176) could be panned to x 0, a black strip the game never shows. The pan, scroll bar and wheel now keep to the camera limits in the interactive mode; Rooms 04, 34 and 47 show only the part the game allows. CameraPanCheck 0 (Rooms 02, 03, 41, 04, 08) | [manual-pan-limits](../.verification/manual-pan-limits/validation.md) |
| The verb screen's note row removed (user report D) | The explanatory line under the verb screen is gone, so the room and the verb screen are larger; the text is the verb screen's tooltip. The user confirmed B: the "sheep" were Room 24's and are drawn right since the clip classes. VerbAreaCheck (updated), CutsceneLayoutCheck 0 | [verb-screen-note](../.verification/verb-screen-note/validation.md) |
| The distaff's notes return to plain after they play (user report E) | Script 031 leaves five glow verbs on over a played note and draws the plain note last; ScummVM draws each verb when its `verbOps` runs, the app drew them in slot order, so the brightest glow stayed. The verb screen is now composed in draw order, and verbs turned off clear their rectangle and cover printed letters. Tests 244/244; VerbAreaCheck 0 | [verb-draw-order](../.verification/verb-draw-order/validation.md) |
| The game's keys and '.' to skip text (user reports F and G) | Keys go to the game in the interactive LOOM mode with no option (the user's choice): the note rows cast spells, and the other keys script 005 reads work. '.', ScummVM's talk-stop key, ends the text shown, also in cutscenes. KeyInputCheck 0 | [game-keys](../.verification/game-keys/validation.md) |
| The cutscene menu lists the room's cutscenes (user report H) | "Play a cutscene" lists only the known cutscenes that start in the room shown or whose script loads it (the scripts' `loadRoom` operands, decoded); elsewhere one disabled line names the rooms that have one. CutsceneMenuCheck 0. Tests 244/244 | [cutscene-menu-rooms](../.verification/cutscene-menu-rooms/validation.md) |
| The room media player hidden in the interactive mode (user item I) | Analysis: in the interactive mode the MUSIC panel duplicated the game's music, called reached music "not reached", and played over a cutscene's own copy of the same track. With the user's choice, it is hidden in the interactive LOOM mode, and a sound the game starts stops the app's other player. The inspection mode is unchanged. MusicPanelCheck 0 | [room-music-analysis](../.verification/room-music-analysis/validation.md) |
| Integrate the interrupted H/I worktree onto master | Reviewed and fast-forwarded the three completed commits; rebuilt the main Release app with zero warnings. Tests 244/244; fresh CutsceneMenuCheck and MusicPanelCheck 0 failures. Separate room-controls commit left untouched | [integration-hi-20260930](../.verification/archive/integration-hi-20260930/validation.md) |
| Discard obsolete room-controls work | With the user's instruction, removed the clean detached worktree without integrating its old UI commit 8878cd9; updated the handoff so it is no longer pending | [drop-room-controls-20260930](../.verification/archive/drop-room-controls-20260930/validation.md) |
| Room cutscene buttons and Skip line | One scene plays directly; several use simple menu titles; no scene hides the control. Skip line shares '.' and continues the current scene. ControlsCheck 10 PASS; tests 244/244 | [cutscene-controls-20260930](../.verification/archive/cutscene-controls-20260930/validation.md) |
| Remove obsolete Advanced controls | Visible verb notes accept clicks without Show verb area. Removed the entry-pan option, timer and CameraPanCheck; source-driven camera remains. VerbAreaCheck and six required doorway rooms pass | [advanced-controls-20260930](../.verification/archive/advanced-controls-20260930/validation.md) |
| Spell notes and result audio | Draft playback runs through its completion; audio follows interactive and Advanced actions. Executed estimated sounds remain labelled previews, unexecuted possibilities stay silent. Four note tracks and result sound 19 open; SceneSoundCheck 0; tests 246/246 | [spell-audio-20260930](../.verification/archive/spell-audio-20260930/validation.md) |

## State at the end of 28 September

- `dotnet build SCUMMexplorer.slnx`: 0 warnings. Tests: 198/198 with `SCUMM_SOUND_TEST_ROOT=Games\loom` and `SCUMM_INDY3_EGA_TEST_ROOT=Games\IndyCru\EGA`.
- RoomAudit: 0 gaps in all 76 rooms.
- WPF checks: all pass except CameraPanCheck (2) and VerbAreaCheck (1), see above.
- Blockers baseline: `.verification/room-owned-loads/blockers-after.txt` (5839 over 134 reasons). The real stops left are `expression` (3), `getActorCostume` (1) and `getActorElevation` (1). The rest are labels.
# 1 October: source-backed replay titles

Stored names resolved across source room definitions; 39 genuinely unnamed targets use decoded action/reached-room descriptors with explicit gaps. Shared picker/Game Texts titles, 9/9 WPF checks; replay actions and coverage unchanged. [Validation](../.verification/archive/replay-names-20261001/validation.md).

# 1 October 2026: requested replay/status control placement

Top-right Game Status, Advanced-only global Play Cutscene and room replay beside Skip line; shared memberships and playback guards retained. [WPF11/11 and screenshots](../.verification/archive/ui-refinements-20261001/validation.md).

# 1 October 2026: uniform original replay names

Original opening/trial/transition/firewood use the shared room-and-description formatter in all replay views and Game Texts. Opening label01 verified against stopped boot69; recipes/memberships retained. [WPF15/15](../.verification/archive/ui-refinements-20261001/validation.md).
