# SCUMMexplorer requirements

Drafted 10 October 2026 from the project notes, `README.md`, `docs/` and the source under `src/` (read, not run). Reviewed 14 October 2026 ([review](REQUIREMENTS_REVIEW_2026-10-14.md)): the support matrix (§3.1) is now generated from the code, the rows about browsed games were brought up to date with the 11–12 October work, and duplicate ids carry a suffix (`R-5.5a`, `R-6.13a`). Problems found on the way are **not** mixed in here; they are in [REQUIREMENTS_FINDINGS.md](archive/REQUIREMENTS_FINDINGS.md).

## 0. How to read this document

**Scope tags.** Every requirement carries one tag that says how widely it holds and therefore where its code belongs (layering rule: put code in the lowest layer that is true for it; `Scumm` cannot see `Loom`, and `Domain`/`Discovery` are shared by both).

| Tag | Meaning | Code home |
|---|---|---|
| `SCUMM` | Any SCUMM game and version | `Core/Scumm/` |
| `SCUMM v1` | Only the classic v1 games (fixed-size index, tile rooms): Maniac Mansion, Zak McKracken | `Core/Scumm/V1/` |
| `SCUMM v2` | Only the enhanced v2 games (one-byte object index, one-stream rooms): Maniac Mansion Enhanced, Zak McKracken Enhanced | `Core/Scumm/V2/` |
| `SCUMM v3` | Only old-v3 (LFL files, XOR 0xFF rooms): LOOM EGA, Indy3 EGA | `Core/Scumm/V3/` |
| `SCUMM v4` | Only v4 (`DISK01.LEC` archives, or per-room files for Indy3 256 colours): LOOM CD, Monkey Island, Indy3 256 colours | `Core/Scumm/V4/` |
| `SCUMM v5` | Only v5 (`NAME.000` index and `NAME.001` resources, XOR 0x69): Indy 4, Monkey Island 1 CD, Monkey Island 2 | `Core/Scumm/V5/` |
| `BROWSED` | A game that is only browsed (rooms, costumes where readable, sounds where readable): Monkey Island, Indy3 256 colours, Maniac Mansion and Zak McKracken (classic and enhanced). Always `GameId.Unknown`: no LOOM or Indy3 feature switches on | `Core/Scumm/` + `Discovery` |
| `LOOM` | Both LOOM editions (EGA and CD) | `Core/Loom/` (Session, Text, Scenes, Relationships) |
| `LOOM EGA` | DOS LOOM EGA, any language edition | `Core/Loom/Ega/` or v3 data |
| `LOOM EGA ref` | Only the English Roland edition (`Games/loom ega mt32 en`), the reference | catalog data + App |
| `LOOM CD` | LOOM CD/VGA | `Core/Loom/Cd/` |
| `LOOM demo` | `LOOMDEMO.EXE` previews (short, long) | `Core/Loom/Ega/` + App |
| `INDY3 EGA` | Indiana Jones and the Last Crusade EGA | `Core/Scumm/` dialect `V3Indy3` |
| `APP` | The WPF shell, independent of which game is open | `src/SCUMMexplorer.App` |

A requirement tagged `SCUMM` that today only one game exercises is still `SCUMM`: the tag states the *intended* reach.

**Requirement vs implementation.** The tag belongs to the *requirement*, not to the code that happens to meet it today. A requirement that should hold for every game (or for the whole app) keeps the wide tag even when today's implementation is LOOM-specific or needs per-game or per-version data; such a row names the implementation after `Implementation:` and may say what another game would have to supply. Only a requirement that is true of one game or edition by nature carries a narrow tag.

**Normal vs Advanced** is the product's central display rule (R-1.13): Normal mode is strictly exploratory and playful and never shows status, validation or evidence information; all of that belongs in Advanced only.

**Basis column.** `Rule` = a product rule the user decided (with date where the docs give one); `Code` = behaviour implemented today and worth keeping, taken from the source (file named); `Doc` = taken from a reference document; `Target` = stated by docs or code comments as intent but not fully met today (see the findings).

**Words that are normative.** *Must* = required. *Should* = wanted, may be partial. Exact on-screen strings are in `code font`.

---

## 1. Purpose and product rules

| ID | Tag | Requirement | Basis |
|---|---|---|---|
| R-1.1 | APP | The app is an audio-visual, interactive **explorer**, not a technical SCUMM viewer: users walk through rooms, trigger cutscenes, change objects and discover secrets. Technical evidence exists, but only behind **Advanced**. | Rule |
| R-1.2 | APP | Game data is never shipped or committed. The user opens a folder (or disc image) they own. `Games/` and `Games/mt32` stay out of git. | Rule |
| R-1.3 | LOOM | Reference quality is **LOOM EGA (DOS)**. LOOM CD/VGA has live interaction, actors, scripts, scene playback, Game Status and CD audio. Indy3 EGA is limited to backgrounds, costumes, sounds, scripts and texts, including dialogue choices; the other games are browse-only (§3.1). | Rule |
| R-1.4 | APP | **Rooms show no gaps and show their first-visit state.** Where the data does not decide, pick at random and label the pick. Implementation: LOOM today (first-visit state from the room entry scripts and the session). | Rule |
| R-1.5 | APP | **Objects respond as in the game, with the game's hero on screen** (28 Sep). Single click walks the hero to the object and uses it (exits lead on). Double click also looks at it (the description). Implementation: LOOM, Bobbin, input scripts 005–007, use verb 0x38, look verb 0x36; another game supplies its own verbs and input script. Cycling an object's stored images belongs only to the inspection mode, and the mode's text says so. There is no hidden modifier gesture (no Alt+click). Hiding an object is allowed only if the background behind it has no gap. | Rule |
| R-1.6 | APP | **Room hotspots navigate between rooms.** Implementation: LOOM doorways from source responses (R-7.2). | Rule |
| R-1.7 | APP | Rooms begin in their earliest state; the running session carries changes caused by real scenes. A scene adds only its minimum prerequisites and preserves later state. Single picks keep the session; explicit sequences start fresh; Restart returns to boot. Implementation: `LoomExplorationSession`, on the generic `ScummSession`. | Rule |
| R-1.8 | APP | **Game state shown to the user (for LOOM, Game Status) is under the user's control**; the app never silently edits it as a shortcut. The approved exception (8 Oct) is reviewed *actual source* prerequisites run for a late-room catalog start (English Room 46 recipe); it is not generalised by guessing story bits. | Rule |
| R-1.9 | SCUMM | **No room-specific engine or renderer hacks, no invented states, no forced costumes/flags/boxes, no one-off teleports.** Actor look, placement and scene starts come from the edition's own scripts. | Rule |
| R-1.10 | APP | **Normal mode** shows artwork, game text and concise controls. Technical details belong in Advanced. Preserve edition differences, palettes and verified source glitches. Window subtitles stay short. See R-1.13 for the intent behind the two modes. | Rule |
| R-1.11 | APP | **Estimates never change gameplay.** Where the app must assume (a click point, a random pick, a skill answer), the assumption is labelled and does not alter game state. | Rule |
| R-1.12 | APP | Build only what adds real value beyond cutscenes and inspections (the "M5 secrets guide" is shelved). | Rule (memory) |
| R-1.13 | APP | **Intent of Normal vs Advanced.** **Normal mode is strictly exploratory and playful**: the user plays with the game, and sees only the game's own art, text, sound and the controls to play with them. It never shows development-status or confidence information (for example "needs validation", "not yet playable", evidence levels, unverified/partial notices, counts of unfinished items) and never explains why something is absent: content that is not ready is simply not offered. **Advanced mode is for the developer/researcher**: evidence, validation status, bytecode, traces, assumptions and coverage live there, and only there. When a new feature has a status or caveat to report, it goes in Advanced; a Normal-mode note about it needs the user's explicit approval. | Rule (10 Oct) |

## 2. Evidence vocabulary (shown to the user, used everywhere)

| ID | Tag | Requirement | Basis |
|---|---|---|---|
| R-2.1 | SCUMM | Every exit, action, state change, cue, scene or secret is tied to a resource, script and instruction (`Script 85 +0x05A8`-style offsets, `Room 34 local script 202`, `Room 36 object 600 verb 0x38`). | Rule |
| R-2.2 | SCUMM | A decoded branch or room load is a **possible** outcome; calling a verb directly is an **exploration action**; a partial session is a **state preview**; estimates are labelled **estimate**. These exact words are used in notes and tooltips. | Rule |
| R-2.3 | SCUMM | Glossary names always carry one evidence level: `decoded` (read from instructions or game data), `engine` (ScummVM behaviour), `reading` (interpretation, not verified in a run), `play` (seen in play). A name never exists without one. Unnamed things keep their raw form (`var[0x0D1]`), never a guess. | Code (LoomGlossary) |
| R-2.4 | APP | Source execution, prepared Explorer/Core/WPF checks and original-game observation are distinct evidence; a scene completing does not prove correct actors, prerequisites, event delivery or timing. | Rule |
| R-2.5 | SCUMM | Names already judged unreliable stay out: **sounds are plain `Sound NN`** in the tree (4 Oct). Uncertain names stay uncertain (Shaping's canonical label, the reverse of Silence, unidentified story roles). | Rule |

## 3. Games and editions

### 3.1 Support matrix

The matrix is **generated** from the registry `GameSupport.Families` (`Core/Domain/GameSupport.cs`, one row per `GameFamily`) and is the same table as in the README. Edit the registry, not the table: a test (`SupportMatrixTests`) fails when either copy differs, and running the tests with `SCUMM_UPDATE_SUPPORT_MATRIX=1` rewrites both. A level is raised only when the feature works for that family and its conformance check passes (`SupportConformanceTests`, run for every family whose data folder is present).

<!-- support-matrix:start -->
| Game | Rooms | Objects | Walk boxes | Costumes | Sound | Texts | Scripts | Play | Cutscenes | Status |
|---|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|:-:|
| **LOOM** EGA English (reference) | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ |
| **LOOM** EGA other editions | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ◐ | ✔ |
| **LOOM** demos | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ◐ | ✔ |
| **LOOM** CD | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ✔ | ◐ | ✔ |
| **Indiana Jones 3** EGA | ✔ | ◐ | ✔ | ✔ | ✔ | ✔ | ✔ | – | – | – |
| **Indiana Jones 3** 256 colours | ✔ | ◐ | ✔ | ✔ | ✔ | ✔ | ✔ | – | – | – |
| **Monkey Island 1** floppy | ✔ | ◐ | ✔ | ✔ | ✔ | ✔ | ✔ | – | – | – |
| **Monkey Island 1** CD | ✔ | ◐ | ✔ | ✔ | ◐ | ✔ | ✔ | – | – | – |
| **Monkey Island 2** | ✔ | ◐ | ✔ | ✔ | ◐ | ✔ | ✔ | – | – | – |
| **Indiana Jones 4** | ✔ | ◐ | ✔ | ✔ | ◐ | ✔ | ✔ | – | – | – |
| **Maniac Mansion** enhanced | ✔ | ◐ | ✔ | ✔ | ✔ | ✔ | ✔ | – | – | – |
| **Maniac Mansion** classic | ✔ | ◐ | ✔ | ✔ | ✔ | ✔ | ✔ | – | – | – |
| **Zak McKracken** enhanced | ✔ | ◐ | ✔ | ✔ | ✔ | ✔ | ✔ | – | – | – |
| **Zak McKracken** classic | ✔ | ◐ | ✔ | ◐ | ✔ | ✔ | ✔ | – | – | – |
<!-- support-matrix:end -->

✔ works (Full), ◐ partly, – not supported. What each column asks for, at each level:

| Column | ◐ Partial | ✔ Full | Checked by |
|---|---|---|---|
| Rooms | – | every room picture decodes and is offered; LOOM also shows the first-visit state (R-1.4) | rooms offered, picture decodes |
| Objects | stored object boxes and names from the room data (no script run) | first-visit state from the entry scripts, and click responses through the game's own scripts (R-1.5) | LOOM only today |
| Walk boxes | – | read for the room and drawn in Advanced | – |
| Costumes | drawn, but in an estimated palette (Zak McKracken classic: 10 of 35 actor colours depend on what ran and stay labelled estimates; Maniac Mansion classic is derived from the scripts and counts as full) | drawn in the correct palette | costumes offered |
| Sound | some devices or kinds not heard or not played (v5 script-driven hooks, loops not yet listened to, MT-32 needs the user's ROMs) | every kind plays on every device the game has | sounds offered |
| Texts | – | Game Texts extracts spoken lines, names and labels | extractor finds entries |
| Scripts | – | raw disassembly in the family's dialect, and the Readable view | scripts offered |
| Play | – | live room: actors, camera, clicks run the game's scripts | LOOM only today |
| Cutscenes | list derived from the scripts, not hand-validated | hand-validated catalog (English Roland LOOM only) | – |
| Status | – | Game Status editor | LOOM only today |

Family notes (what is behind a row):

- `LoomEgaReference`: the English Roland edition (`GameSupport.ReferenceEditionLabel`); the replay catalog (328 replays, R-8.18) is enabled only for its fingerprint. `LoomEga`: the other editions (Spanish, plain English, French, German, v1.2, Italian) use the same code with the script-derived cutscene list (R-8.24); their first-visit state per edition is not recorded (I-07).
- `LoomEgaDemo`: the short and long self-running demos; the picker offers only **The preview** (R-3.3). `LoomCd`: CD/VGA with voices and disc music; cutscenes derived from scripts (R-8.21, R-8.25).
- `Indy3Ega`: backgrounds, costumes, sounds, scripts and texts including dialogue choices (R-11.6); no objects with state, no play. The browsed families (`Indy3Vga`, `MonkeyFloppy`, `MonkeyCd`, `Monkey2`, `Indy4`, `ManiacEnhanced`, `ManiacClassic`, `ZakEnhanced`, `ZakClassic`) share the same reading: rooms, costumes, walk boxes, sounds, scripts, texts and stored-object boxes (R-3.10 to R-3.14, R-13.8).
- Not in the matrix: `OtherV3` (any other old-v3 game whose `00.LFL` parses: rooms and texts, LOOM script dialect, may read wrongly) and `OtherBrowsed` (an unrecognised v1/v2/v4/v5 game, read like the browsed families).

Every Rooms ✔ means the same thing: no gaps, objects and actors drawn by the shared engine code. The reference edition differs only in having a **replay catalog**, which is data, not room rendering.

### 3.2 Detection and opening

| ID | Tag | Requirement | Basis |
|---|---|---|---|
| R-3.1 | SCUMM v3 | A folder is identified from the bytes of its LFL files, in this order: (1) the edition whose known SHA-256 hashes (`known-editions.json`, 14 files per edition, made by `.verification/known-editions`) match best (at least 3 files and 60 percent; ties go to the closer file count); (2) the game by six language-free LOOM files or five Indy3 files; (3) the executable name (`LOOM.EXE`, `LOOMDEMO.EXE` → demo, `INDY3.EXE`); (4) last, the index shape (176 costumes and 117 scripts → LOOM EGA, 100 and 130 → Indy3 EGA), labelled `index shape only (inferred)` in the detection note. A folder with none of these is untitled (folder name). Names, sizes and marker files such as `DISK*.ID` are never used. SCUMM v3 is confirmed only when `00.LFL` parses. If it does not parse, the version is `Unknown`, rooms are not claimed, and the note says so. | Code (GameFolderScanner, KnownGameFiles) |
| R-3.9 | LOOM EGA, Indy3 EGA | The edition is named in `GameLibrary.Edition` and the detection note when known hashes match: LOOM English, English with the Roland upgrade, Spanish, Spanish with the Roland upgrade, Spanish v1.2, French, German, Italian, the long and the short demo; Indy3 English, Spanish and the demo. Demos are flagged from their data, so a demo without its executable still opens as a demo (R-3.3). Another release (or one with most of those files changed) is still opened as the game when the fallbacks of R-3.1 recognise it, with no edition name. Regenerate `known-editions.json` when a copy is added (`python .verification/known-editions/make.py`). | Code |
| R-3.2 | SCUMM v4 | `000.LFL` + `DISK01.LEC` is claimed as LOOM CD only for its known fingerprint or its directory sizes (78 rooms, 165 costumes, 637 scripts, 30 sounds); any other game on that format is browsed (R-3.10), never claimed as LOOM. The CD game opens from a `.cue`, `.iso`, `.bin`, a folder holding one, or a folder with `000.LFL` + `DISK01.LEC`. The title is the game's name with the edition mark (for LOOM CD: `LOOM (CD)`), platform `CD (VGA)`. | Code |
| R-3.10 | BROWSED, SCUMM v4 | A v4 game that is not LOOM CD is read from **every** `DISKnn.LEC` (`ScummV4Disks`; each resource says its disk in `ContainerFileNumber`) and offered with its rooms, costumes and sounds; a room is offered only if its picture decodes. The 256-colour Indiana Jones is the same format with one plain `NN.lfl` per room (`00.lfl` starts with a `0R` block). If no room decodes, nothing is offered and the note says so. Implementation: `GameFolderScanner.ScanOtherV4`, `GameLibrary.V4Disks` / `V4ArchiveFor`. | Code |
| R-3.11 | BROWSED, SCUMM v2 | An index whose directories end exactly at the end of the file with **one** byte per global object (the old-v3 index has four) is an enhanced v2 game (`GameLibrary.Version` = `V2`); rooms (those whose picture decodes: room 0 is the index file itself), costumes, sounds, scripts and texts are offered (12 October); objects are shown as stored boxes only. | Code |
| R-3.12 | BROWSED, SCUMM v1 | An index with magic `0x0A31` whose fixed per-game directory sizes add up to the length of the file is a classic v1 game (`Version` = `V1`; `GameLibrary.V1Game` says Maniac Mansion or Zak McKracken and picks the colour table); rooms, costumes, sounds, scripts and texts are offered (costume colours are the actor's, derived from the game's scripts by `ScummV1ActorColors`: the actor that wears the costume from `actorOps` set-costume, then Maniac's engine tables by actor number or the palette Zak's scripts set for that actor; the costumes whose colours depend on the run, 10 of Zak's 35, keep a labelled estimate). | Code |
| R-3.13 | BROWSED | Evidence for these games is the format reading only: a picture that decodes is shown as stored (the palette of the room that holds a costume is used for v4 costumes), and nothing is claimed about scripts, objects or interaction. Format references: [resource formats](engine/SCUMM_RESOURCE_FORMATS.md). | Rule/Doc |
| R-3.14 | BROWSED, SCUMM v5 | A folder or disc image holding `NAME.000` and `NAME.001` is a v5 game (`GameLibrary.Version` = `V5`): `ScummV5Archive` reads the index (room names, the directories of costumes) and the `LECF` resource file; on a disc the candidates may be in language or game folders (`ENGLISH/MONKEY.000`, `ATLANTIS/ATLANTIS.000`, demos beside the game): the one whose index lists the most rooms is opened, an `ENGLISH` folder winning a tie. Rooms (picture and `CLUT` palette) and costumes (16 and 32 colours) are offered, and since 12 October also sounds (iMUSE: Roland MT-32, AdLib, PC speaker, sampled), the talkie speech, Monkey Island 1 CD tracks, scripts and texts; objects are shown as stored boxes only. | Code |
| R-3.3 | LOOM demo | A demo's `00.LFL` is not a subset of the game's: entries pointing at missing containers, at non-resource offsets or at unreadable resources are dropped, and the dropped count is stated in the detection note. Title `LOOM (demo)`. Full-game script/bit/actor/sound names are **not** applied to a demo; variables and rooms are. | Code |
| R-3.4 | APP | The app reopens the last game folder at start (`last-folder` file under `%LOCALAPPDATA%\SCUMMexplorer`). A missing folder yields `Last game folder is unavailable. Choose a folder to open.` and no crash. Only reopening the last game may reuse saved room thumbnails; the **Open Game Folder** button rebuilds them. | Code |
| R-3.5 | APP | Opening another game stops playback, sound and CD voice, replaces the session and rebuilds the catalog; nothing from the previous game leaks. | Code |
| R-3.6 | SCUMM | One engine entry maps game → variant: `ScummProfile.For(game)` gives script dialect (`V3`, `V3Indy3`, `V4`), walking style and archive kind. Code that needs scripts, objects or costumes takes `IScummDataSource` (`ScummDataSources.For(game)`), and never branches on the game. | Rule/Code |
| R-3.7 | SCUMM v3 | Indy3 EGA (non-Mac) uses dialect `V3Indy3`: `0xAE` wait has no subopcode; `0x3B/0xBB` waitForActor takes a byte parameter (LOOM: no-op, no operands); `getActorX/Y` take a byte parameter. | Doc |
| R-3.8 | SCUMM v4 | Dialect `V4` uses the v5 operand forms: roomOps sub-opcode first, line-height print operand, charset cursor command, opcodes 0x30, 0x3B, 0x4C differ. | Code |

### 3.3 What is shared and what is not

**Between SCUMM versions (v3 ↔ v4).** Shared: script reader (`ScummScriptReader`, dialect-parametrised), bounded executor, runtime state, actors/walk boxes/camera/verbs/messages, costume animation, room-object selection and paint order, verb-area rendering, audio devices (PC speaker, AdLib, Roland), script observers and relation trackers, disc image reading, and the session core `ScummSession` (script runner with child-script tracking, cutscene/override/freeze stack, room entry, camera, pointer, click/key delivery to the game's input script, frame loop, boot driver; S4a, 14 October 2026). Version-specific: LFL/index readers, `ScummV3DataSource`, `ScummV4Archive`/`ScummV4DataSource`, charsets, EGA vs VGA costume and message renderers, v3 vs v4 walker (`UsesV4Walking`). The seam is `IScummDataSource`.

**Between LOOM editions (EGA ↔ CD).** Shared (`Core/Loom`): `LoomExplorationSession` (LOOM's boot, catalog visits and arrivals, staff, Game Status, on top of `ScummSession`), game status model, story events, glossary and `LoomScriptPresenter` (Readable), text extractor, speaker names, scene setup/doorways/arrivals/replays, relationships, draft names (`LoomDraftNames`: same verb numbers and slot pairs in both), the single/double click scripts 005–007. Edition-specific: `Cd/` (`LoomCd*`: voice cues, cutscene index derived from scripts, scene setup/guards, CD glossary) and `Ega/` (reviewed names, demo looks, EGA relationships); the EGA replay catalog is data (`loom-replays.json`), the CD list is derived at run time.

**Between LOOM EGA editions.** Behaviour is derived from each edition's own data. Never key shared code on a byte offset or assume a language identifies numbering (R-14.2).

## 4. Window layout and modes

| ID | Tag | Requirement | Basis |
|---|---|---|---|
| R-4.1 | APP | Window title `SCUMMexplorer` followed by the release label (`Preview1`); minimum 900×600. Dark theme; accent `#B78442`. Header row: app name, then (right-aligned wrap) `Play Cutscene` (Advanced), `Game Texts` (Advanced), `Game Status` (LOOM, once a session exists), the CD sound box (CD only), the music sound box (not CD), the **Advanced** switch, and the primary **Open Game Folder** button. The right-hand group (sound, Advanced, Open) never moves when others appear. | Code (MainWindow.xaml) |
| R-4.2 | APP | Three columns: left side panel (250), centre content, right inspection panel (290, **Advanced and a room selected only**). Footer bar shows `<Title> · <Platform>` with the folder path as tooltip. | Code |
| R-4.3 | APP | **Normal mode**: left panel is a visual picker — tabs `Rooms` / `Costumes` with thumbnail cards; no resource tree. **Advanced mode**: the resource tree is shown instead; right panel, hotspots/doorways lists, depth/walk-box toggles, step button, Restart, trace expanders, `Play Cutscene` and `Game Texts` buttons appear. | Code (UpdateAdvancedMode) |
| R-4.4 | APP | Side panel header: `EXPLORE`, then game title (e.g. `LOOM`), then the platform line: `DOS edition` for any platform string that starts with `DOS` (the scanner writes `DOS (inferred)`), otherwise the platform string. Tooltip `SCUMM v3 · <platform>` (or `SCUMM version unconfirmed`) with the full platform string. | Code |
| R-4.5 | APP | Page header: title + subtitle; in a room scene the header is hidden to give the scene the height (`Explore the scene` / `Original artwork from the game` are only the hidden defaults). | Code |
| R-4.6 | APP | Subtitles per game: Indy3 v3 `Room backgrounds and texts from the game.`; LOOM v3 and LOOM CD `Explore rooms, objects, and sounds.` (empty preview: `Pick a room to explore the game from there.`); an unsupported layout says that room/resource inspection is not supported **for this game** — which is never shown for a supported game (fixed 10 October for LOOM CD). | Code |
| R-4.7 | APP | Welcome text before a game is open: title `Welcome to SCUMMexplorer`, `Explore classic adventure games from the inside.`; the empty preview says `Your game will appear here`. After opening LOOM: `Choose a room` / `Pick a room to explore the game from there.` | Code |
| R-4.8 | APP | View options persist between runs in `view-options.txt` (doorway mode, framing, sound device, CD voices on/off, bytecode column, Readable). **Object hotspots always start at `Never`.** Advanced is not persisted. | Code |
| R-4.9 | APP | F11 toggles full screen in a room view; Esc leaves it unless Esc is skipping a cutscene. | Code |

## 5. Catalog: room cards, costume cards and the resource tree

### 5.1 Room and costume cards (Normal mode)

| ID | Tag | Requirement | Basis |
|---|---|---|---|
| R-5.1 | SCUMM | One card per room, 96×42.7 px, wrapped in the order of the index. A card shows the room's **first-visit** thumbnail; until it loads, the placeholder is the room number as `NN` (two digits, grey). Tooltip `Room NN · loading preview`. Automation name `Open room NN`. | Code |
| R-5.2 | SCUMM | The selected/last-viewed room's card has the accent border (`#B78442`). Thumbnails are produced off the UI thread, one at a time, cancellable, and cached per game folder. A failed room shows a failure card with the reason, not a blank. | Code |
| R-5.3 | SCUMM | A room thumbnail is the room's entry script run in a fresh session; if that cannot run, the plain background. | Code |
| R-5.4 | SCUMM | Costume cards: 96×94, thumbnail 80×62 of the best pose, caption `NNN` (three digits), tooltip is the costume name and detail. | Code |
| R-5.5 | BROWSED | The Rooms and Costumes tabs of the normal catalog (as for LOOM) appear for every browsed game whose costumes can be read (v4, v5, enhanced v2 and classic v1). Room cards and costume thumbnails load by themselves when the game opens, through the disk's archive (v4) or the room and costume files. | Code |

### 5.2 Resource tree (Advanced)

Top level order and header format are exact: **label, two spaces, count in parentheses**.

| Node header | Present for | Children (header text) | Tooltip |
|---|---|---|---|
| `Rooms  (N)` | all games | `Room NN` (two digits); LOOM adds ` · <name>` where the glossary names the room (`Room 02 · cliff top where the game starts`; 8 rooms, all hand-made glossary entries: SCUMM v3/v4 data hold no room names). The room cards use the same label in their tooltip. | glossary entry `<name> (<evidence>): <source>` as tooltip |
| `Costumes  (N)` | every game that has costumes | `Costume NN` | resource description |
| `Scripts  (N)` | every game whose scripts are read (LOOM; the others show `Script NN` as stored) | EGA: `Script NN`, or `Script NN · <glossary name>` when the glossary names it; CD: `Script NN` and `Room NN local script N` | `<name> (<evidence>): <source>` if named, else description |
| `Sounds & music  (N)` | every game that has sounds | `Sound NN` — always plain | description |
| `Game files  (N)` | all games | file name (`LOOM.EXE`) | – |
| `CD audio  (N)` | LOOM CD with a disc audio track | `CD audio NNN · <s> s` + ` · <first 40 chars…>` ; music/narration prefixed `♪ ` | `<voice | music / narration>, <s> s at disc second <t>; first asked for by <source>.` |
| `CD audio  (N)` | Disc-image games with red-book tracks (Monkey Island) | `CD track NN · m:ss · room NN "<name>"` (up to two rooms, then `, ...`); `CD track NN · m:ss · not started by a sound` when no sound of the game starts it | `<label>` plus the description: track length and the sounds that start it (sound id, repeats, disc time range). |

| ID | Tag | Requirement | Basis |
|---|---|---|---|
| R-5.5a | APP | A group with zero items is omitted. Selected-item foreground is dark `#15212B` on the selection colour; otherwise `#E7E0D2`. | Code |
| R-5.6 | LOOM EGA | Script names apply only to *global* scripts of a full game (`ScriptRoom is null`); room-local scripts and a demo's scripts are unnamed. | Code |
| R-5.7 | LOOM CD | CD audio cue numbers are the order found by scanning every room for `sayVoice`; those with no line or long duration are music/narration (♪). | Code |
| R-5.8 | SCUMM v3 | Resource labels come from `00.LFL`: `Room NN`, `Costume NN`, `Script NN`, `Sound NN` (two digits; larger ids stay as numbers). When the index is not parsed, physical files appear as `Room file NN` with the note `Physical LFL file; no supported index was parsed.` | Code |
| R-5.10 | APP | CD audio is always its own tree group, never mixed into `Sounds & music`, in every game that has disc audio (LOOM CD cues, Monkey Island tracks). A CD label shows, in order: the track or cue number, the duration, the room (when known) and, for speech, the first words played. Labels that do not fit the sidebar must end in an ellipsis with the full text in the tooltip, never be cut mid-word. | Decided 12 October |
| R-5.11 | APP | Selection colour of tree items is the app theme's, not the system blue, in every game's tree (see R-5.5a). | Code |
| R-5.9 | SCUMM v4 | Names: `Room NN`, `Costume NN`, `Sound NN`, `Script NN` (global), `Room NN local script N` (room-local). Descriptions say what the item is and where it is stored (`LOOM CD costume, stored in room 04 (DISK01.LEC).`). | Code |

## 6. Room scene (the central view)

| ID | Tag | Requirement | Basis |
|---|---|---|---|
| R-6.1 | SCUMM | The room is drawn with nearest-neighbour scaling, enlarged to fit. **Original framing** (default on) shows the game's 320-pixel-wide view; wide rooms are panned with scroll buttons (`Scroll room left/right`, press-and-hold repeats) or the Left/Right keys. The keys work wherever the focus is (since 10 October), except in text boxes, combo boxes, sliders and tree nodes that expand or collapse, and while the game's own keys are active in Interactive mode. | Code |
| R-6.2 | SCUMM | Layers, bottom to top: room picture, actors, navigation overlay (doorways), walk-box overlay, hotspot highlight, message text. Overlays never take clicks except the picture. | Code |
| R-6.3 | SCUMM v3 | Old-v3 rooms are XOR 0xFF encoded; the object directory starts at byte 29; room-local script ids are ≥ the global script count declared in `00.LFL`. | Doc |
| R-6.4 | SCUMM | **Paint order**: a room redraw paints active objects in reverse directory order (first entry ends on top); `drawObject` queues; queued objects paint on top in call order; `setState` marks the area dirty and clears the queue. Close-up faces (LOOM rooms 56, 58, 86 …) have colour-0 cut-outs that eye/mouth images fill. | Doc (ScummVM-checked) |
| R-6.5 | APP | The Options expander (collapsed by default) holds: `Original framing`; `Doorways` `Never / On hover / Always`; `Object hotspots` `Never / On hover / Always`; and, Advanced only, `Show depth areas` and `Show walk boxes`. | Code |
| R-6.6 | SCUMM | Under the room: the **verb strip** (the game's own bottom verb screen, rendered from the game's verbs; hover tooltip is Advanced only) and a full-screen button. Below that, a one-row control bar: group 1 `⏸ Pause` (`▶` when paused), `Step ▸` (Advanced), `⏭ Skip (Esc)`, `Skip line`; group 2 `Play Cutscene` (shown when the room has any), `Interact ▴`, `Sounds ▴` (CD, when the room has cues and voices are on). | Code |
| R-6.7 | SCUMM | Mode switch **Interactive** (default on, right panel): on, clicks and keys go to the game; off (inspection), a click cycles an object's stored images and no script runs. The tooltip lists the keys: LOOM's `q w e r t y u i` and `c d e f g a b C` play the distaff's notes (the staff labels them c d e f g a b c′, R-12.2), `.` skips the text, Esc skips a cutscene, Space pauses. Implementation: LOOM keys and its distaff. | Code |
| R-6.8 | SCUMM | The hint line under the scene (`ObjectsNote`) is shown only in inspection mode (and Advanced); the game's own screen needs none. | Code |
| R-6.9 | SCUMM | Right-click or **Interact ▴** on an object opens a menu of the object's actual verbs, labelled by `LoomVerbNames`; choosing one is an *exploration action* and says so. | Code |
| R-6.10 | APP | Session bar text, Advanced only in inspection mode; busy badge (`BusyBadge`) with wait cursor while the app does blocking work such as opening a scene or preparing music. | Code |
| R-6.11 | APP | The right panel (Advanced) sections, in order: `DOORWAYS`, `HOTSPOTS` (objects that are not doorways, R-6.13a), `ACTIONS`, object preview + description, `Room entry` (summary, event buttons, `Raw disassembly`), `Last action` (summary, timeline, issues, `Raw trace`), `Assumptions` (`No assumptions` or `Assumptions · N`). | Code |
| R-6.12 | INDY3 EGA | Background preview only, with the note `Background preview only. Object art, scripts, and room routes are not decoded for this game yet.` | Code |
| R-6.13 | BROWSED | The same background preview and note as R-6.12 for the browsed games; the room is shown as stored (no first-visit state, actors or depth masks). Since 12 October the room's stored objects are drawn as outline boxes with their names (option `Object hotspots`) and listed in Advanced, with doorways as a separate option: raw stored data, no script run. | Code |
| R-6.13a | APP | **A doorway is not an object hotspot** (decided 11 October). An object whose verbs can load a room (`DoorwayTargets`) appears only as a doorway: in the `DOORWAYS` section and the Doorways overlay, and through room clicks (R-1.6). It has no row in `HOTSPOTS`, no label suffix, no `Object hotspots` box (Always or hover outline) and no entry in the object list. The game's own pointer still sees it, so the cursor, the hover object sent to the game and a click on it work as for any object. Implementation: `IsObjectHotspot`. | Code |
| R-6.14 | LOOM | **Objects at 0, 0 are preview objects, not hotspots** (decided 11 October): LOOM keeps its 48 × 40 draft previews (and a few other off-scene images) at position 0, 0 of a room. Whatever the session position says is 0, 0 gets no row, no box, no hover outline and no interaction menu entry. Implementation: `IsVerbHotspotAvailable`. | Code |
| R-6.15 | SCUMM | **Untouchable objects are not hotspots** (decided 11 October): an object the session marks untouchable (class 24) gets no row, box, hover outline or interaction-menu entry, because the game's own object search skips it. Room 28 keeps one dragon object per state (asleep/awake) and makes the others untouchable; running Awaken on the asleep-state object while the dragon is awake started its scene in a state the game never reaches (the sparkle actor, costume 4, at elevation 38 beside the head). Implementation: `IsVerbHotspotAvailable`. | Code |

## 7. Interaction model (LOOM)

The rows below describe the **LOOM binding** of the generic mechanism (input scripts 005–007, the single and double click, the distaff keys). Their `SCUMM` tag states the intended reach of the mechanism (the script chain, the playback clock, skipping, device handling); the click gestures and keys are LOOM's. **Open requirement:** what Play means for a verb-based game (sentence line, verb pick, inventory, the game's own input scripts) is not specified yet. It is to be written, with its first game, when the Play column is raised for a non-LOOM family (see the review of 14 October, stage 3).

| ID | Tag | Requirement | Basis |
|---|---|---|---|
| R-7.1 | SCUMM | Click input is delivered to the game's input script chain (`VAR_VERB_SCRIPT` = script 005 → 006 → 007): script 006 timers (`var[0x0C]`, double-click window ≤ 0x28 jiffies, ±6 px across/3 down), class 1 objects show close-ups on hover, class 2 are exits. The session has no pointer; the virtual mouse position is stated, and labelled assumed input. | Doc (script map) |
| R-7.2 | SCUMM | Walk, use and look go through source scripts; **source responses and touchability decide navigation**, not static possible destinations. A static doorway guess is never presented as a certain exit. | Rule |
| R-7.3 | SCUMM | Keyboard: notes via the key rows above; `.` skips a line, Esc skips the cutscene only after the cutscene has set its skip point (`beginOverride`); until then the Skip button is disabled and its tooltip explains. Space toggles pause during playback. | Code |
| R-7.4 | LOOM | Physical spellcasting works with keyboard **and** mouse in EGA and VGA (user-confirmed). | Rule |
| R-7.5 | SCUMM | The playback clock runs at the game's frame rate; animations, camera, sound and voice follow it. Pause, step (Advanced) and skip are available during a scene. | Code |
| R-7.6 | SCUMM | Voices/music/effects start with the scene and stop when the scene is left, another game is opened, or the device changes. Changing device stops current audio; the next sound command uses the new device. | Code |

## 8. Cutscenes

### 8.1 Entry points and lists

| ID | Tag | Requirement | Basis |
|---|---|---|---|
| R-8.1 | APP | **Global picker** (header `Play Cutscene`, Advanced only): a dialog with search, a category filter (`Scenes`, `Dialogue and reactions`, `All playable`, and in Advanced `Needs validation`), the list, a selection note and `Play`. Double-click on an entry plays it. Entries without a play action show their status in the note and disable `Play`. | Code |
| R-8.2 | APP | **Room list** (the room's `Play Cutscene` button, Normal mode too): one entry → plays directly; several → popup headed `CUTSCENES IN THIS ROOM`. In the dialog form there is no category filter, and no search when ≤ 15 entries. Advanced adds each entry's note as tooltip. On LOOM CD, while the background derivation of the rooms' scenes still runs, the caption carries a small turning arc (the button is disabled until the first entries are known) and a gold italic line under the controls says what is happening (`Looking for the scenes that happen in each room… The first launch takes a while; the result is kept for the next one.`). Both go when the scan is done. | Code |
| R-8.3 | APP | A scene belongs to the rooms it plays out in: the room list of a room includes scenes that *start* there and scenes that *visit* it, the latter with the suffix ` (played from Room NN)` (NN = the start room, two digits). | Code |
| R-8.4 | APP | Playing a cutscene from the picker, the Game Texts window or a script's `▶ Play as cutscene` button switches the Interactive mode on first. | Code |
| R-8.5 | APP | Single scenes keep the running story/status; only missing source prerequisites run unseen and are disclosed in the notes (to be widened by R-8.26 to R-8.31, which also set missing state). Explicit sequences and the intro start from a fresh session and replace the exploration state (the note says so). | Rule |

### 8.2 Title formats (exact)

All titles are **`Room NN · <description>`** (two-digit room, ` · ` = space, middle dot, space), produced by `LoomCutsceneTitles.Format` (Core, `Loom/Scenes`; the CD list and the script scan use `LoomCutsceneTitles.Title`). A description that already starts with that prefix is not prefixed again. Before prefixing, a leading `Use response · `, `Look response · ` or `Walk response · ` is removed when followed by `Reached Room(s)`. Whole-game entries have no room prefix.

| Pattern | Example | Tag | Origin |
|---|---|---|---|
| `The Intro` | – | LOOM | boot opening (EGA script 044) and CD start (script 001); same title in both |
| `The preview` | – | LOOM demo | the demo's self-running tour; the only entry of a demo |
| `Room NN · Reached Room MM` | `Room 02 · Reached Room 03` | LOOM | a use/look/walk response whose source loads room(s) MM; several → `Reached Rooms 03, 05` (zero-padded, ascending, comma+space) |
| `Room NN · Use response` / `Look response` / `Walk response` | `Room 18 · Use response` | LOOM EGA ref | catalog responses with no destination |
| `Room NN · Look at <object>` | `Room 02 · Look at leaf` | LOOM | verb 0x36 + object's name from the game's data (trailing `@` marks removed). An object with no name reads `Room NN · Use response` / `Look response` / `Walk response` in both editions (the CD used to say `Use object 248`) |
| `Room NN · Use <object>` / `Walk to <object>` | `Room 24 · Use sheep`, `Room 34 · Walk to Rusty` | LOOM | verbs 0x38 / 0x37 |
| `Room NN · <Draft> at <object>` | `Room 02 · Opening at sky`, `Room 04 · Closing at hole` | LOOM | a draft (spell) forward verb; the reverse verb uses the second half of the name |
| `Room NN · <Draft> (reverse) at <object>` | `… Sleep (reverse) at …` | LOOM | reverse verb of a draft whose title has no `A / B` split |
| `Room NN · Other draft response at <object>` | `Room 02 · Other draft response at leaf` | LOOM | verb 0x56, "that spell does nothing here"; also the CD fallback for an unnamed draft verb |
| `Room NN · Entry` | – | LOOM CD, scan | a room's entry script that holds a cutscene span (the EGA catalog's only entry reads `Arrival in Room 47`) |
| `Room NN · Draft response N at <object>` | – | LOOM | fallback shown only if the draft is not in the name table |
| Named sequences | `The Elders' trial`, `The firewood scene`, `Walk to Room 13` | LOOM EGA | hand-reviewed recipes, with room prefix: `Room 08 · The Elders' trial`, `Room 36 · The firewood scene`, `Room 10 · Walk to Room 13` |

| ID | Tag | Requirement | Basis |
|---|---|---|---|
| R-8.6 | SCUMM | In **Advanced**, a title for a verb-triggered entry gets the suffix ` (verb 0xHH)` (two-digit uppercase hex, e.g. `(verb 0x38)`); entries for room entry scripts get none. Normal mode never shows the verb byte. | Code |
| R-8.7 | LOOM | Draft naming (`LoomDraftNames`) is the one source for both editions. Slot order is the game's draft table: 0 `Opening / Closing` (0x3A/0x3B), 1 `Straw into Gold` (0x3C/0x3D), 2 `Dyeing / Bleaching` (0x3E/0x3F), 3 `Night Vision` (0x40), 4 `Twisting` (0x41/0x42), 5 `Sleep` (0x43/0x44), 6 `Emptying` (0x45/0x46), 7 `Invisibility` (0x47/0x48), 8 `Terror` (0x49), 9 `Sharpening / Blunting` (0x4A/0x4B), 10 `Reflection` (0x4C), 11 `Healing` (0x4D), 12 `Silence` (0x4E/0x4F), 13 `Shaping` (0x50/0x51), 14 `Unmaking` (0x52/0x53), 15 `Transcend` (0x54). A draft not matching its slot's verbs is `Draft N` (1-based). Each name's evidence is recorded beside it in code. | Code/Doc |
| R-8.8 | LOOM | Slash names split into forward/reverse; single names get `(reverse)` for the reverse verb. One spelling must be used everywhere (see F-12). | Target |
| R-8.9 | LOOM EGA | Categories (catalog): **Scenes** = the replay visits another room (all 51 multi-room replays) or is a long silent animation; **Dialogue and reactions** = the rest. The scan's rule (R-8.10) agrees with this catalog for 301 of 328 replays. The EGA catalog holds 79 scenes and 249 dialogue/reaction previews. | Code |
| R-8.10 | LOOM CD | Category rule (`LoomCutsceneGroup.IsScene`): **Scenes** = the room entry, a span that loads a room, or a look/walk/use verb whose code leads to another room (doorway finder, `LoomCutscene.Destinations`); a span whose scripts load a room right after it ends, or a silent Use (since 10 October); everything else is **Dialogue and reactions**, however many lines it prints (the earlier "≥ 3 lines is a scene" rule was dropped on 10 October after comparing with the EGA catalog). Entries are *groups*: triggers that reach the same cutscene scripts are one entry (e.g. four owl holes, five wool piles); a draft verb is its own entry even when the same scripts are reached by another verb; scripts reached from > 20 triggers are shared helpers and are dropped. The representative trigger is the first the player meets: entry, then verbs 0x38, 0x37, 0x36, then lowest verb/object. | Code |
| R-8.11 | LOOM CD | Entry ids are `cd-<room>-<object>-<verb>`; the intro is `cd-intro`. | Code |
| R-8.12 | LOOM EGA ref | Entry ids: `Room NN object <id> Verb 0xHH`, `Room NN entry`, or a sequence id. | Code |

### 8.3 Notes (the text behind each entry)

| ID | Tag | Requirement | Basis |
|---|---|---|---|
| R-8.13 | APP | Each entry has a note stating: source (room/object/verb → script → span size and offsets), the **stated start** (which state the scene begins in), what is assumed, and that branches make lines/loads *possible, not certain*. | Code |
| R-8.14 | LOOM CD | The CD note begins `Found in the CD scripts: NN · <the room's entry script | object N's verb label> reaches <scripts> (a cutscene ... endCutscene span of N instructions, N printed line(s), N room load(s)).` followed by `A branch can keep part of it from running, so the lines and loads are possible, not certain.`, other triggers reaching the same scripts, the start-setup sentence (`A fresh start begins its cutscene (checked by a headless run).` or the derived setup, or `Whether a fresh start begins it is still being checked (open the list again in a few seconds).`), and the stated start. Verb labels are `entry script`, `verb 0x36 (look)`, `verb 0x37 (walk to)`, `verb 0x38 (use)`, `verb 0xHH`. | Code |
| R-8.15 | APP | Playing sets status `Playing <title> from a stated start (see the tooltip). Escape skips once the cutscene allows it.` and the tooltip carries note + assumptions. | Code |
| R-8.16 | APP | Directly-run verbs say: an *exploration action*, not the game's click; Bobbin does not walk there; the object may be untouchable in this state; a draft response does not simulate casting. | Code |
| R-8.17 | APP | **Needs validation** (Advanced only): coverage entries (51 on the reference: 42 `Start needs validation`, 7 `Shared input/draft helper; not a standalone story scene`, 2 `Some branches need validation`) and the catalog summary line are visible only in Advanced, by design (R-1.13): Normal mode does not mention hidden scenes. | Code |

### 8.4 Catalog scope

| ID | Tag | Requirement | Basis |
|---|---|---|---|
| R-8.18 | LOOM EGA ref | The replay catalog (`loom-replays.json`) is enabled only when **every** listed source file's SHA-256 matches (78 files). Otherwise the catalog is empty with an explanatory summary and the four established starts remain: The Intro, `Walk to Room 13`, `The Elders' trial`, `The firewood scene`. | Code |
| R-8.19 | LOOM EGA | No rerun of the discovery pipeline for other editions (user decision, 5 Oct); other editions open, show rooms and texts, and keep the four starts. | Rule |
| R-8.20 | LOOM EGA ref | Catalog starts for a late room (Room 46) run the reviewed actual prerequisites (Awakening, Reflection, Firewood, dream) only when ghost/restored state is absent; existing state is preserved; failure cancels the visit with `Could not prepare this scene` / `The earlier story scene did not finish. Choose another room or restart the session.` | Rule/Code |
| R-8.21 | LOOM CD | The CD list is derived from the scripts when the game opens (`LoomCutsceneIndex`, depth 3 through literal `startScript`/`chainScript`), start setups are derived in the background, and per-room lists are saved per build+data for reuse. For the known disc a shipped seed (R-8.25) replaces the per-room derivation. | Code |
| R-8.22 | LOOM CD | The intro starts script 001 from the beginning over the 84 s overture cue; the skill screen waits for a click (assumed answer after 3 s for unattended runs only); the draft question is not simulated; `SCUMM_CD_INTRO_AUTOANSWER=1` lets harnesses run unattended. | Code |
| R-8.23 | LOOM | **Scan vs catalog (checked 10 October, reference English EGA, `.verification/cutscene-scan-ega titles`)**: the script scan finds all 328 catalogued replays as triggers, plus 72 more (42 groups with no catalogued member). With the title and category rules above, 217 of 328 titles+categories are identical (187 before the 10 October scan changes: loads after the span, silent Use, literal load targets in titles of unnamed plain verbs) and only 9 titles differ (a doorway the finder follows but the run did not take, and conditional loads); 15 categories differ besides the 90 helpers (loads in branches the replay did not take, and silent look scenes). **Headless run (10 October, `LoomCutsceneRun`, `.verification/cutscene-scan-ega visit`)**: running each of the 328 triggers in a fresh session without a start setup (every one began a cutscene; 271 s in all, so a background job) and calling it a scene when the run left the room, or it is an entry or a silent Use, agrees with the catalog's category for 320 of 328 (the static rule alone: 225 of the 238 grouped ones). The run never made a catalogued dialogue a scene; the 8 left are catalog scenes that stay in their room and speak (Room 09 distaff and feather, Room 18 Use, Room 35 gate, Room 43 Opening, Room 51 Dead One and feather). 90 catalogued reactions (generic draft/look responses reached from > 20 triggers) are dropped by the scan as shared helpers. The scan is therefore a sound source for editions without a catalog (not yet wired for EGA: see M-01), with those two known differences. | Doc |
| R-8.24 | LOOM EGA | **Editions without a validated catalog** (the replay catalog is empty because a fingerprint does not match: Spanish, plain English, French, German, v1.2, Italian) get the CD's script-derived cutscene machinery (`ScannedCutscenes`): the picker and the room's `Play Cutscene` list come from `LoomCutsceneIndex` plus the four established starts; start setups come from `LoomCdSceneSetupPlanner` (it runs on EGA data; `NewSession` boots the EGA game) and playing uses the same path as the CD (`PlayCdCutscene`). Room lists settle after one fresh headless run per scene (`LoomCutsceneRun`, in parallel on half the cores: about 1 minute on 16 workers, scaling with fewer cores, measured 10 October on every edition; the CD-style turning arc and note show meanwhile); the planner's long trials for the state-gated scenes (18 on the Spanish edition, 17 find a start; 2 to 2.5 minutes more on one thread, the same on every edition since the plain English status schema read its own initial note limit and Italian is detected by data) follow in the background and the scene notes say they are still being checked. Cached per game folder like the CD's lists. The reference edition keeps its catalog; the catalog is not to be removed until the scan agrees with it (R-8.23: 217 of 328 title and category, 320 of 328 category with the run). | Code |
| R-8.25 | LOOM CD | **Shipped seed** (`loom-cd-scenes.json`, `.verification/cd-seed`): the rooms each of the 223 scanned groups plays out in, and the intro's rooms, made offline from the scan, the derived setups and a headless run, and tied to `ScummV4Archive.Fingerprint` (SHA-256 of the decoded `DISK01.LEC` and `000.LFL`; the Kixx release matches too). With a matching fingerprint the room lists are known at once (no arc or note) and only the start setups are derived in the background; otherwise the live scan of R-8.21 runs. Categories are not in the seed: the run calls 19 of the static rule's 98 scenes dialogue, some of which leave the room in the validated EGA game, so the CD keeps the static rule (R-8.10) until the run is checked against the real game. Regenerate the seed after any change to the scan, the planner or the headless run. | Code |

### 8.5 Story readiness of a launched cutscene (Target, set 11 October 2026)

A cutscene can be launched from a freshly opened game. The user's target is that it plays as it would on the full story path. This **reverses** the earlier rule that the explorer sets no story flag (R-8.5, the planner's "keep their first-visit value 0" notes); the reversal is accepted for the cutscene button only. Rooms and Interact keep the first-visit rule (R-1.4).

| ID | Tag | Requirement | Basis |
|---|---|---|---|
| R-8.26 | LOOM | **Implemented 11 October** for the staff, notes and story bits the scene's own code reads (`LoomCutsceneReadiness`, single catalog and scanned scenes, both editions; not explicit sequences or the intro). Playing a cutscene from the picker or the room's `Play Cutscene` **adds the game state the scene needs and the session lacks**: staff held, available notes, learned drafts, story bits (conversations had, places visited), costume variable. Existing state is never lowered, so a scene may move the status forward (an ending scene may leave the game near its end). Everything added is disclosed in the scene note (Advanced) and in the status tooltip. | Target |
| R-8.27 | LOOM | *Already met for direct scene starts* (`ActorSetup`, 9 October). **Actors** the scene uses are put in the room at the positions and dress the game's own code gives them for that scene, if they are not already there. | Target (partly Code: `ActorSetup`) |
| R-8.28 | LOOM | **Room state** the scene reads (portals, objects' states, owners) is brought to what the scene needs before it starts. | Target |
| R-8.29 | LOOM | **Bobbin's costume** is the one the scene's story point gives him (robe, swan, etc.), derived from the game's own costume changes, not guessed. | Target |
| R-8.30 | LOOM | The needed state is **derived from the edition's own scripts** (the branch conditions the scene's code tests and the story point that sets them), not from per-room hacks or a list kept for the English edition only. Where the scripts do not determine it, the choice is stated as an estimate. | Target (rule) |
| R-8.31 | LOOM | Acceptance: for each scene, the lines, actors, camera and room loads of the launched scene equal those of the same scene reached by playing the story, on the reference edition, compared by the harness. | Target |

**Status (11 October).** `LoomCutsceneReadiness` (Core, `Loom/Scenes`) finds the state by headless search: in fresh sessions it raises the staff, eight notes and each story bit the scene's own code reads, one at a time, and keeps a raise when the scene then begins, enters more rooms or prints more lines; the plan is cached per trigger, applied to the running session as a raise only, the room is entered again so its entry script follows the new bits, and the scene note (tooltip) says what was added and that it is an estimate. On the reference EGA (`.verification/cutscene-state-audit readiness`, 4 minutes): 36 of 218 groups gain state (for example Room 09 objects 268/272 go from 2 and 1 lines to 49 and 50 with bit D7A, Room 43 object 651 from 20 to 47 lines with staff and bit DB7, Room 34 walk from nothing to 20 lines), 2 begin only with it, 15 room entries still begin nothing from a fresh state (they need the planner's real-writer setups, which the picker already applies). Not done: R-8.28 (room object state beyond what the entry script derives from bits), R-8.29 (costumes beyond the planner's actor dress and the staff pair) and R-8.31 (no full-story reference run exists to compare against); the score (begin, rooms, lines) picks the fuller branch, which is not always the story's.

**Feasibility (11 October, `.verification/cutscene-state-audit`).** On the reference EGA, 76 of 218 scan groups (35%) print different lines or enter different rooms once the state their own code reads is present; 19 begin no cutscene at all from a fresh game. So the gap is real but bounded. The mechanisms exist: supported status edits (staff, notes, drafts, story bits), the planner's real-writer scene runs, actor placers from the game's code. Missing: (1) choosing *which* branch a scene's interesting variant needs; the scanner lists the bits a scene reads, not the value that gives the full scene; (2) a costume and room-object requirement scan (the audit does not vary these); (3) the acceptance comparison (R-8.31) needs a story-path reference run per scene, which a full-story replay of the reference edition would provide. Proposed order: R-8.26 for story bits via variant search (run each variant, keep the one with the most lines or room loads, label it an estimate), then R-8.27/R-8.29 from the scanner, then R-8.31 on a handful of scenes.

## 9. Scripts

*Implementation note (14 October):* the script page, the Readable view, speaker names and the text extractor are generic in behaviour (they work through the dialect and `IScummDataSource`, and already serve the browsed games) but still live under `Core/Loom/Text` and carry `Loom*` names (`LoomScriptPresenter`, `LoomTextExtractor`, `LoomSpeakerNames`). Move them to `Core/Scumm/` when a second game needs a LOOM-free version of one, as the layering rule asks; the LOOM tables (glossary, speaker names) stay LOOM data.

### 9.1 Script page

| ID | Tag | Requirement | Basis |
|---|---|---|---|
| R-9.1 | APP | Selecting a script hides the right panel and shows: title = tree label (`Script 91 · cage scene`), subtitle `Indexed script resource · DOS LOOM old-v3` (+ ` · name: <evidence>` when named) or `LOOM CD script · SCUMM v4`; tooltip = glossary evidence or description. Controls: **Readable** (default on), **Show bytecode** (only meaningful in Raw, disabled in Readable), **▶ Play as cutscene** (when the script is a known cutscene), event buttons (static candidates only), **Used by** expander, output box (Consolas 13). | Code |
| R-9.2 | APP | The note reads `Control-flow decode: N/M resource bytes, K unresolved paths. Event links are static candidates; branch choices and script order are not executed in this view.` (script body is never executed here). | Code |
| R-9.3 | APP | The status line reads `<name> · N instructions · K decode issues`; an unreadable script says `bytecode unavailable` and shows the reason. | Code |
| R-9.4 | LOOM EGA | If an index entry points inside another resource ("leftover"), the page states that the entry does not establish a standalone script. | Code |

### 9.2 Raw view (the decoded instructions)

| ID | Tag | Requirement | Basis |
|---|---|---|---|
| R-9.5 | SCUMM | Header lines: global script `Script NN  ·  <container>  ·  resource 0xOFFS` then `Resource length N bytes  ·  bytecode N bytes  ·  code starts at file offset 0xOFFS`; local script `Room NN local script NN  ·  <container>  ·  file offset 0xOFFS` then `Embedded bytecode N bytes`. Container is `NN.LFL` (v3) or `DISK01.LEC` (v4). Then `Control-flow decode: N instructions, N/M resource bytes; K unresolved paths. Undecoded bytes may be data or unreachable code.` and `Static instructions are possible paths, not proof of execution. Bounded execution reports unsupported behavior explicitly.` | Code |
| R-9.6 | SCUMM | One line per instruction: `XXXX  <bytes padded 17>  <operation> <operands>` (offset 4 hex digits, uppercase byte pairs); with bytecode off: `XXXX  <operation> <operands>`. Operation names are the ScummVM-style camelCase names (`startScript`, `loadRoomWithEgo`, `isEqual` …). | Code |
| R-9.7 | SCUMM | Trailing sections: `Unresolved path at +0xXXXX: <reason>` for decode issues; `Partial disassembly at byte +0x…` with `Undecoded bytes: …`; `Bytes after stopObjectCode: …`. Nothing is silently dropped. | Code |
| R-9.8 | SCUMM | Variables print as `var[0xXXX]`, bits as `bit[0xXXX]`, and bit/var arithmetic keeps ScummVM numbering; dialect differences (R-3.7, R-3.8) are applied by dialect, never by guessing. | Code |

### 9.3 Readable view (plain language)

The Readable view only re-reads what the disassembler decoded; the raw view stays the evidence. Anything without a phrase keeps its raw line.

| ID | Tag | Requirement | Basis |
|---|---|---|---|
| R-9.9 | SCUMM | Header: `Script NN · <name>  ·  <container>` (global) or `<Context>  ·  <container>` (local), then the fixed paragraph `Readable view: the decoded instructions in plain language. Static paths, not proof of execution. Names carry their evidence (listed at the end); anything unnamed keeps its raw form. Switch to Raw for the instructions as decoded.` | Code |
| R-9.10 | SCUMM | Each line is `XXXX` (4-hex offset of the first instruction of the construct, blank for closing keywords) + two spaces + 4-space indent per nesting level + phrase. | Code |
| R-9.11 | SCUMM | **Structure** is recovered from forward jumps: `if <cond>` … `else` … `end if`; `while <cond>` … `end while`; `repeat` … `until <cond>`; a one-instruction wait loop becomes `wait until <cond>`; `if <cond>, go to XXXX` where a jump cannot be nested. SCUMM conditionals jump when false, so a block is the code that runs when true; conditions are negated accordingly. The jump after `beginOverride` is printed as `begin the skippable part (if the player skips, the game jumps to XXXX)`. | Code |
| R-9.12 | SCUMM | Condition phrases: bit tests as the bare name or `not (<name>)` (bit `== 0/1` collapses to truth); numeric relations `==, !=, <, <=, >, >=` with the operand order that reflects ScummVM's jump; `X is in state N`, `X is in walk box N`, class tests `X is in class N`/`not in class N`. | Code |
| R-9.13 | SCUMM | Action phrases (examples, exact): `end of script`; `pause until the next frame`; `begin cutscene` / `end cutscene`; `begin the skippable part…` / `end of the skippable part`; `start <script>` / `continue in <script> (this script ends)` / `stop this script`; `go to <room>`; `go to <room> through <object>; Bobbin walks to <x,y>`; `place <actor> at (x, y)`; `<actor> walks to (x, y)` / `… to <object>`; `<actor> turns to face <object>`; `the camera pans to x=N`; `wait N jiffies (60 jiffies = 1 second)`; `start <sound> (music)`; `set <object> to state N`; `draw <object> at (x, y)`; `pick up <object>`; `verb N: …`; `<var> += 1`; `<var> = a random number from 0 to N`; `<var> = x of <actor>`; `(no operation in LOOM)` for the LOOM no-op waitForActor. | Code |
| R-9.14 | SCUMM | Names: `<kind> N` becomes `Role N · <glossary name>` where the glossary names it (rooms `Room 05 · name`, scripts, sounds, actors, objects by their stored name). Actor names are scoped by script: actor numbers are reused between scenes, so a scoped name wins over a general one. | Code |
| R-9.15 | SCUMM | The readable view ends with `Names used (evidence: decoded = …, engine = ScummVM, reading = interpretation, play = seen in play):` and one line per used name: reference, name, `[evidence]`, source. Unresolved paths print as `Not decoded: path at +0xXXXX: <reason>`. | Code |
| R-9.16 | SCUMM | Print phrases show text with `\xNN` escape runs cleaned and speaker named through `LoomSpeakerNames` (`Bobbin` for the ego; actor-number names scoped by script/room; `Unknown speaker`). | Code |
| R-9.17 | LOOM CD | Readable views exist for global scripts, room-local scripts, object verb scripts and entry/exit scripts, with source-scoped links. | Doc |

### 9.4 Disassembler requirements

| ID | Tag | Requirement | Basis |
|---|---|---|---|
| R-9.18 | SCUMM | Decoding is conservative: it follows reachable control flow only, reports unresolved paths rather than guessing, and decodes under the right dialect. Coverage is counted (`DecodedBytes/CodeLength`); it measures decoder coverage, not gameplay coverage. | Doc |
| R-9.19 | SCUMM | The bounded executor runs scripts in sessions with a step/frame cap, reports unsupported behaviour explicitly (never silently), and is cancellable. | Doc/Code |
| R-9.20 | APP | Findings from decoding go in `docs/loom/LOOM_SCRIPT_MAP.md` (variables, bits, verbs, rooms, with source and date) for future features. | Rule (memory) |

## 10. Names, glossary and speakers

| ID | Tag | Requirement | Basis |
|---|---|---|---|
| R-10.1 | LOOM | Glossary (`loom-glossary.json`, seeded from the script map) names variables, bits, scripts, actors, rooms and sounds, each with evidence + source text; object names come from the game's own data; `Room` names exist only for a few rooms. Bits from `0xD63` are shifted by one in the English Roland edition (derived at run time from the edition's own scripts: `LoomStatusSchema`); the shifted entry's evidence is downgraded to `reading` and says so. | Code |
| R-10.2 | LOOM EGA | Reviewed names (`loom-ega-reviewed-glossary.json`) are applied only for a non-demo edition and keyed by derived data, not offsets. | Code |
| R-10.3 | LOOM CD | The CD glossary loads only `engine` variable rows from the shared table plus its own `loom-cd-glossary.json`. | Code |
| R-10.4 | LOOM | Speaker display: actor 1/ego `Bobbin`; Elders `Elder (actor 2/3/4)`; 5 `Hetchel`; 6 `Glassmaker`; 254/255 `Game menu`; scoped overrides (`Mandible`, `Cob`, `Rusty`, `The Dead One`, `Mr. Stoke`). Unknown: `Actor N`. | Code |

## 11. Game Texts (Advanced)

| ID | Tag | Requirement | Basis |
|---|---|---|---|
| R-11.1 | SCUMM | Window `Game Texts` for every game whose texts are read (every family with Texts ✔ in §3.1; `GameSupport.Texts`; since 10 October also for LOOM CD: the extractor reads the CD's script blocks and object verbs; 1,196 entries on the English CD; the replay buttons and first-visit room renders of the EGA window are not offered there). Implementation: per-game extraction (script blocks and object verbs by dialect); the window itself is shared. Heading `WORDS FROM THE GAME`, title `Game texts`, coverage line `<N> texts found. Browse spoken lines, object names and other stored words.` Left: search, room filter (`Every room`, `Room NN`), kind filter (`Every kind`, `Spoken lines`, `Conversations and cutscenes`, `Object names`, `Other labels and stored text`), result count (`N matching texts` / `N matching groups`), **Export visible text**, list. | Code |
| R-11.2 | SCUMM | List item heading, all caps: `ROOM NN  /  OBJECT N · <name>  /  <KIND>` or `ELSEWHERE IN GAME  /  <KIND>`; conversations: `ROOM NN  /  <KIND> GROUP  /  N LINES  /  OBJECT N · <name>` or `… /  LINE NN`. Text below, trimmed to 2 lines. | Code |
| R-11.3 | SCUMM | Entry kinds: `Spoken line` (print/printEgo), `Object name`, `Stored line` (stringOps), `Verb label` (verbOps), `Object label`. A **conversation group** is a run of lines in one decoded script; `Dialogue choices` groups are menus offered to the player, each followed by the replies in its branch. Conditions can hide options or change replies, and the detail text says so. | Code |
| R-11.4 | SCUMM | Detail pane: kind, heading, room artwork (first-visit state for LOOM; stored background otherwise), conversation lines with speakers, **Visit room** and, when a known cutscene prints the text, **Play this cutscene**; an expander `Source and extraction details` lists script ranges visited, unresolved paths, skipped ranges, leftover entries and diagnostics (max 8 shown). | Code |
| R-11.5 | LOOM | Cutscene links are offered only where the printing script maps to exactly one known cutscene (shared helpers and multiple-start scripts inherit none); script 052 prints no text so none offers it. Unavailable for a demo. | Code |
| R-11.6 | INDY3 EGA | Multiple-choice labels use verbs 120–125 (`verbOps(n,[on, name "…"])`); the script clears `Var[114]`, waits, branches on `isEqual var[0x072], n`. Choices are grouped as **Dialogue choices**. The standalone conversation map (`tools/Indy3ConversationExport`) expands decoded menus into collapsible, source-linked branches with room backgrounds, plus a `.graph.json`; conditional state is left unevaluated and no outcome is assigned. | Doc |

## 12. Game Status (LOOM)

| ID | Tag | Requirement | Basis |
|---|---|---|---|
| R-12.1 | LOOM | `Game Status` (header button once a session exists) opens a modal window, 900×780: **Staff / distaff** (resolved from the session or `Unresolved`), **Available notes** (1–8), **Spell / draft sequences** grid, **Story** list, `Apply and close` / `Cancel`. | Code |
| R-12.2 | LOOM | Draft grid columns: `Spell / draft` (title from R-8.7), notes `1`–`4` (dropdowns from the eight-note set, labelled as ScummVM names them: c d e f g a b c′; the eighth is the upper-octave c, ScummVM's Mac GUI notes "the note names c through c'"), `Learned`, `Can reverse`; Advanced adds `State source`. A `Mark all as learned` button exists. | Code |
| R-12.3 | LOOM EGA | Story section header `Story: conversations, scenes and objects the game remembers`. Each row is a checkbox with the **catalog scene title** that sets the bit (`A / B ...`), else `<place> ran`; named rows first, then by label; the bit key (`bit[0xD9C]`) at the right. Intro text: the game remembers what happened in story bits; checking marks the scene as done as the game would after it ran; clearing undoes it; changes show when the room is entered again; **these are your choices, not played scenes**. | Code |
| R-12.4 | LOOM CD | Reads CD staff, notes and drafts; the story list uses the CD bit bank (no EGA flag source). | Code |
| R-12.5 | LOOM | Editing is allowed only when the session can be edited (not while a cutscene plays); the staff held/not held and notes edits go through supported session edits (the disclosed off-island staff setup runs the real pickup). The window refreshes every 250 ms while open. | Code |
| R-12.6 | LOOM | The picker note for pitches: the source raises available pitches to five at Room 13's Use response leading to Room 15 (`Use response` in VGA (the scan finds no doorway for it), `Reached Room 15` in EGA). | Doc |

## 13. Audio

| ID | Tag | Requirement | Basis |
|---|---|---|---|
| R-13.1 | SCUMM | Music/effects device box: `No Sound`, `PC Speaker`, `AdLib` (default), `Roland MT-32`, each with an icon. The choice is remembered. | Code |
| R-13.2 | LOOM EGA | Roland needs `82.LFL`–`85.LFL` **and** the user's MT-32 or CM-32L ROM images. ROM folder search order: `SCUMM_MT32_ROM_ROOT`, the remembered folder (`mt32-rom-folder.txt`), a `mt32` folder beside the game folder; otherwise the user is asked once. If unusable for a folder the session plays on AdLib, the choice is kept (not saved over), and the box says so. Roland sets `VAR_SOUNDCARD` to 4 so the game picks Roland sounds itself. | Code/Doc |
| R-13.3 | LOOM CD | Sound box: `No Sound` / `CD Audio` (default `CD Audio`), only when the disc has an audio track. Voices follow `sayVoice` cues scheduled from the disc; the overture timer counts jiffies from the cue start. | Code |
| R-13.4 | SCUMM | Sound resources are playable on demand in a SOUND PLAYER (title, description, ▶ Ⅱ ■, `0:00 / 0:00`); AdLib has a level slider (`+6.0 dB` default, 0–24, peaks kept below clipping). A sound with no data says `No playable audio`. | Code |
| R-13.8 | BROWSED, SCUMM v4 | A v4 sound of a browsed game plays on demand in the same SOUND PLAYER from the `AD` (AdLib) or `WA` (PC speaker) part of its `SO` block (Indy3 256 colours nests the parts in a second `SO`); Roland MT-32 says the game has no MT-32 music here. A sound without speaker notes says so. | Code |
| R-13.5 | SCUMM | Room music/cues: the MUSIC strip (▶ Ⅱ ■, title, time in Advanced, evidence) plays a room's candidate cue; a candidate is "may start", not necessarily played. | Code |
| R-13.6 | SCUMM | AdLib via NukedOPL3Sharp (LGPL), PC speaker per the SCUMM driver's envelopes and pitch modulation, Roland via Munt; third-party notices stay current (`THIRD_PARTY_NOTICES.md`). | Doc |
| R-13.7 | LOOM | The Roland upgrade's overture is rendered on first need (≈10 s); the scene waits, then the music starts with it. | Doc |

## 14. Editions and compatibility

| ID | Tag | Requirement | Basis |
|---|---|---|---|
| R-14.1 | LOOM EGA | The English Roland copy is the reference for tests, harness drivers and `loom-replays.json`. Spanish Roland is comparison only (`SCUMM_LOOM_TEST_ROOT`). | Rule |
| R-14.2 | SCUMM | **Editions differ in data, not only text.** Never key shared code on a byte offset or assume a language identifies numbering. The English Roland upgrade lacks Spanish `bit[0xD62]` and shifts later bits by one; plain en/fr/de/v1.2 use Spanish numbering; object ids differ between English and Spanish from Room 34. Derive bits, ids, resource availability and offsets from each edition's scripts (`LoomStatusSchema`). | Rule |
| R-14.3 | LOOM EGA | Every edition opens, shows rooms without gaps, and extracts texts. v1.2 differs from plain only in data the app reads (e.g. Room 15 depth mask) so needs no special handling. Hebrew is dropped. | Doc |
| R-14.4 | APP | For each feature, check the plausible edition-dependent differences (opening/detection, format/scripts, text/fonts, graphics, audio), reuse `.verification/editions-20261005`, and choose only relevant checks. Italian (`Games/loom ega it`) is detected by its data (R-3.1) and its cutscene scan agrees with Spanish; its room, glossary, text and sound checks pass (`.verification/editions-20261005/italian`); only a look at the real window is open. Isolate genuine differences only where evidence requires. | Rule |
| R-14.5 | LOOM CD | Compared with EGA, CD verb numbers and slot pairs match; object verb tables put each verb on the same rooms' objects; shortened dialogue is expected. | Doc |

## 15. State, session and persistence

| ID | Tag | Requirement | Basis |
|---|---|---|---|
| R-15.1 | SCUMM | One live session object holds the game's state (implementation: `LoomExplorationSession`, which derives from the game-independent `ScummSession`): boot to Room 02 (ego at 828,60 on the English reference), then room visits; the CD session starts past the intro (`completeBoot: false`) and plays the intro on request. | Code |
| R-15.2 | APP | **Restart game state** (`↺ Restart game state`, Advanced) forgets everything explored and starts from the beginning; its tooltip lists story, objects, inventory. | Code |
| R-15.3 | APP | Persisted under `%LOCALAPPDATA%\SCUMMexplorer`: last folder, view options, MT32 ROM folder, room thumbnails per game folder, CD room scene lists. The folder is **never deleted** by harnesses or by the app unless the user asks. Harnesses set `SCUMM_EXPLORER_PROFILE_ROOT` to isolate. | Rule |
| R-15.4 | APP | All file I/O failures on settings/caches are swallowed with a status note, never a crash (`Game opened; last folder could not be saved.`). | Code |

## 16. Architecture and quality constraints

| ID | Tag | Requirement | Basis |
|---|---|---|---|
| R-16.1 | SCUMM | Core layering: `Scumm` cannot see `Loom` (compiler-enforced); `Domain` and `Discovery` may use `Scumm`; new code goes in the lowest layer true for it. | Rule |
| R-16.2 | APP | Build: `dotnet build SCUMMexplorer.slnx` (.NET 10, Windows); output in `Exe/<Project>/…`; all shell commands through RTK. | Rule |
| R-16.3 | APP | Tests are filtered to the change; harnesses are chosen by the change; no routine "run everything". One targeted check per change; the user retests at the end. | Rule (memory) |
| R-16.4 | APP | Verification harnesses include `.verification/HarnessFailurePolicy.cs`; fatal exceptions log to `Exe/HarnessFailures/` (or `SCUMM_HARNESS_FAILURE_LOG`) and exit 1 without Windows crash UI. | Doc |
| R-16.5 | APP | Reproduce app differences through the real window (`WindowCheck`) and compare with the bundled ScummVM; never `git stash`. | Rule (memory) |
| R-16.6 | APP | Git: commit each finished task separately on `master` with explicit paths; `Games/` ignored; merge worktree commits to master with fast-forward after fixes; pushing is allowed. | Rule |
| R-16.7 | APP | Session end: update `docs/CURRENT_WORK.md`, record finished work with validation in `docs/CHANGELOG.md`, evidence under `.verification/<topic>/`; end with a fresh ready-to-copy next prompt (`NEXT_SESSION_PROMPT.md`). | Rule |
| R-16.8 | APP | Raising what the explorer supports for a game follows [GAME_SUPPORT_INSTRUCTIONS.md](GAME_SUPPORT_INSTRUCTIONS.md): one family and one stage at a time, a registry level changes only with its conformance test and evidence record, and a stage that needs a new requirement adds it here as a `Proposed` row (own commit) that the user confirms or rejects at the run's checkpoint; agents decide and log instead of asking. Where the two differ, the requirements win and the instructions are corrected. | Rule (14 Oct) |

## 17. Out of scope (today)

Objects with first-visit state, click responses and live play for every game except LOOM (Indy3 and the browsed games show stored object boxes only); depth masks; the real actor colours of v1 costumes (the scripts choose them); v5 script-driven sound hooks (a song plays its default path); games after v5 (v6 and later: Day of the Tentacle and on); the Hebrew LOOM; an M5 "secrets" guide; replay catalogs for non-reference EGA editions; native-control resume; optional General MIDI. These are parked in `docs/CURRENT_WORK.md`.
