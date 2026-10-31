# Changelog

The running log of finished work, newest first, each entry linking its record under `.verification/`. The current state is in [ROADMAP](../ROADMAP.md); what the app must do in [REQUIREMENTS](../REQUIREMENTS.md). Earlier parts: `CHANGELOG_THROUGH_2026-10-17.md`, `CHANGELOG_THROUGH_2026-10-20.md` in this folder.








## 1 November: README readiness table with percentages; scene setup reads call arguments and actors held in variables

The README's support matrix is now the roadmap's ten features with one number where one is measured: scenes played the game's own way (LOOM 97% to 100%, Indy 3 84%, Monkey Island 1 86% and 87%, Monkey Island 2 84%, Indy 4 83%, Maniac/Zak 24% beginning) and readable-script names (37% to 81%); the generated matrix stays in REQUIREMENTS §3.1. Scene setup: a called script's locals are its caller's literal arguments, an actor may be held in a variable, a comparison with VAR_ROOM is one with the scene's room; own way 84.1% → 84.4% ([record](../../.verification/scene-setup-20261031/README.md)).
## 31 October: an object whose code asks who owns it is given to the hero; class tests of "this object" read right

Indy 3 Room 17's books show their text only when held ("It looks like a first edition"): the setup now gives the picked object to the hero when its code tests the owner of "this object". A class test of "this object" now reads its class list (it read the `[0x007]` of the variable as class 7). Indy 3 own way 80.4% → 84.3%, Indy 4 81.2% → 82.4%; all five copies 83.1% → 84.1%, started at the cutscene 16.9% → 15.9% ([record](../../.verification/scene-setup-20261031/README.md)).
## 31 October: scenes that test where the hero came from are entered from that room

A setup now visits the room a scene's code expects the hero to come from (the variable each game's exit hook fills with the room left), sets story variables to the value their test compares with, and the app re-enters a room after an entry's setup. Scenes played their own way: 81.1% → 83.1% of 1,093 (Indy 4 77.9% → 81.2%, Monkey Island 2 81.6% → 83.7%, Monkey Island 1 +1.2% and +1.3%, Indy 3 unchanged); started at the cutscene 18.9% → 16.9%; LOOM unchanged ([record](../../.verification/scene-setup-20261031/README.md)).
## 31 October: readable scripts of every game name their verbs

The verb names each game's own `verbOps` give (Indy 3: "Open" … "Pick up", "Talk"; dialogue choices stay unnamed) now show in the other games' readable scripts (`startObject`, `doSentence`): verb references named 0% → 56.8% (Indy 3 57%, MI1 44% and 29%, MI2 61%, Indy 4 80%) ([record](../../.verification/readable-names-20261031/README.md)).
## 31 October: readable scripts of every game name their objects and actors

The readable view of Indy 3, Monkey Island 1 (both), Monkey Island 2 and Indy 4 now names objects (each room's stored name) and actors (`actorOps … actorName`, per script, with a game-wide reading where one name dominates). Object references named 0% → 76.1%, actor references 0% → 62.7% across 13,131 scripts (Indy 3 51%/75%, MI1 65%/39% and 70%/39%, MI2 79%/50%, Indy 4 85%/79%) ([record](../../.verification/readable-names-20261031/README.md)).
## 31 October: LOOM's Interact offers each object's answer to any other draft

Verb 0x56 ("Any other draft") is now in LOOM's Interact: the 61 catalog scenes of that kind were unreachable in every copy but the English reference. 95 objects in the English game have it; all begin (100%), 93 print a line (97.9%). The English reference's room lists drop the matching "Other draft response" entries (now in Interact). Room 47's swan prints nothing in the window (open) ([record](../../.verification/loom-editions-20261031/README.md)).
## 31 October: LOOM's other editions and the CD play their scenes through the shared scene engine

LOOM's scanned scenes (every copy but the English reference, and the CD) now take their setup and start from the shared planner (the one Indy 3, Monkey Island and Indy 4 use), run on LOOM's own session; LOOM keeps its titles, lists, voices and playback. The shared setup also stands the hero in the walk box a script waits for (`getActorWalkBox` then compared; Room 10's entry). Every scene begins (LOOM's own path 99.5%); started at their cutscene: EGA copies 7.4% → 3.2%, CD 6.3% → 2.7%; showing nothing: EGA 3.2% → 0.5%, CD 4.5% → 0.9%. Against the English catalog's 238 replays: 100% begin and reach its rooms, 92.0% share its category, 91.2% within 10% of its length. Indy 4 one more own-way scene (Room 76's entry), the other games unchanged ([record](../../.verification/loom-editions-20261031/README.md)).
## 31 October: scene lists ready in a minute, and in seconds on later launches

The background scene derivation runs every scene first (the room lists and Play Cutscene buttons are then ready) and works out the setup plans afterwards; everything derived is kept between launches of the same build and game data. Indy 4 in the real window: room lists ready after 58 s on a first launch (282 s before), 9 s on the next ([record](../../.verification/derivation-time-20261031/README.md)).
## 31 October: four in five picked scenes now play the game's own way

The setup search reads the trigger's own code first (the scan sometimes listed only the scripts it reaches), tries object classes (`ifClassOfIs`), and gives inventory-only items. Across Indy 3, Monkey Island 1 (both), Monkey Island 2 and Indy 4: scenes played the game's own way 68.3% → 80.9%, scenes showing nothing 4.8% → 3.0% ([record](../../.verification/scene-setup-20261031/README.md)).
## 31 October: a picked scene runs to its end when it is a chain of cutscenes

Found against LOOM English's catalog (Room 18's bell: five cutscenes one frame apart): a scene run now ends only after 32 quiet frames (no cutscene, no line) instead of at the first one; versions 1 and 2 unchanged. LOOM: same length as the catalog 84.5% → 89.1% of 238. Scenes showing nothing: Monkey Island 2 7.9% → 6.3%, Indy 4 6.8% → 5.6%; all five copies 5.5% → 4.8% ([record](../../.verification/loom-answer-key-20261031/README.md)).
## 31 October: LOOM English's catalog fixes the shared scene engine

The shared engine (used by Indy 3, Monkey Island, Indy 4) was run on LOOM English and compared with its 328 hand-reviewed replays. All 238 replays it lists begin; the other 90 are shared draft reactions (Interact actions by design). Two generic fixes: the hero is put at the object on every path of a pick (LOOM picks left Bobbin in the boot's room, so its scenes loaded Room 2 at the end), and a scene's category comes from its run (another room reached, or 100 frames without a line). Rooms reached as the catalog: 185 → 238 of 238; category: 212 → 219 ([record](../../.verification/loom-answer-key-20261031/README.md)).
## 31 October: a Cutscenes branch in the resource tree

Like the sounds: every playable scene of the game, the ones Interact offers included, grouped by the room it starts in; selecting one plays it. Seen in the window: Indy 3 153 scenes, LOOM English 332 (REQUIREMENTS R-8.2c).
## 31 October: no duplicates between Interact and Play Cutscene

User: keep Interact complete, remove the duplicates from the cutscene lists. A room's Play Cutscene list now leaves out the scenes that are actions of the room's Interact; room entries, the intro, named sequences, actions Interact does not offer and scenes started elsewhere stay; the Advanced whole-game picker lists everything. Interact offers every verb whose code starts decoding (Indy 3's package Open was missing). REQUIREMENTS R-8.2b, R-8.36 (one scene engine for every game), R-8.38 ([record](../../.verification/scene-setup-20261031/README.md)).
## 31 October: fewer scenes have to be started at their cutscene

The setup search now tries a picked object's own states when its code tests "this object" through a variable (Indy 3 `ifState var[0x007]`, Monkey Island 2 `getObjectState … var[0x007]`), offers inventory-only second objects, and applies the setup again after the room's entry (which may reset it). Scenes that showed nothing when started at their cutscene: Indy 3 10 → 4, Monkey Island 1 10 → 6 (floppy and CD), Monkey Island 2 24 → 15, Indy 4 35 → 29 (with a last try of all candidates together); every scene still begins ([record](../../.verification/scene-setup-20261031/README.md)).
## 31 October: Indy 4's actions show their verb names; documents tidied

Interact's actions in Indy 4 read "Action" for every verb: its catalog rooms play before the credits build the verb area, so the session had no verb names; the window now takes the names the cutscene planner read once the game showed its verbs. When the game's sentence script and the verb's own code answer equally, the verb's own code is kept (the sentence's answer was the game's refusal). Seen in the window: Monkey Island 2 "Look at sign" ("Help wanted^"), Indy 4 Room 10's crate with Push, Pull, Look at, Walk to.

Documents (user): CURRENT_WORK and USER_CHECKS were merged into ROADMAP, the one current-state document; it and NEXT_SESSION_PROMPT hold only readiness and open work, finished work lives here and in its records, the earlier versions are in `docs/archive/`. The `docs/` root keeps five files. GAME_SUPPORT_INSTRUCTIONS was rewritten for the explorer scope (its ground rules no longer forbid a picked scene's setup; units of work are the ROADMAP's ten features); its S1 to S9 stage playbooks, still used by the Maniac/Zak track, are in `docs/archive/GAME_SUPPORT_INSTRUCTIONS_STAGES_2026-10-14.md`. The rule is in ROADMAP and CLAUDE.md. The roadmap artifact is retired.

## 31 October: LOOM's Interact lists every object of the room

Objects with a LOOM verb that are in the room only at other points of the story are listed after those present, and a pick brings them in: in Room 18 "Look at sphere" plays the crystal-ball vision (Room 19). Lists of rooms without such objects are unchanged ([record](../../.verification/scene-setup-20261031/README.md)).

## 31 October: picked objects that are not in the room yet are brought in, and the hero with them

An Interact pick on an object that is not in the room now makes it present (owner, touchable, its parent's state, a first image), and the hero is put at it with the camera following, even in a room the hero never enters at that point of the story (Indy 3 Room 13's trellis: "I think it could hold my weight if I could get to it."). A present object is never changed. ([record](../../.verification/scene-setup-20261031/README.md))

## 31 October: Interact offers every object of the room and plays it like a picked scene

Indy 3, Monkey Island 1 and 2, Indy 4: the Interact button lists every object of the room's data with each verb that has code (on screen first), and a choice goes the way of a picked scene (the room opened again if a scene plays, the state its code tests set, the hero put at the object, the game's sentence script). A pick also ends a scene still playing. Seen in the window: Indy 3's junk mail and package (the Grail Diary scene, after its setup bit), Monkey Island 1's SCUMM Bar exit ("Meanwhile"). ([record](../../.verification/scene-setup-20261031/README.md))

## 31 October: Play Game Intro answers the copy protection

User: "No value on forcing the user to have the wheel." Monkey Island 2's recipe screen (Room 108) and Monkey Island 1 floppy's history quiz (Room 90) are answered by the explorer with the answer the game's own script derives; the Monkey 2 / Lite choice stays the player's. Seen in the real window with no key pressed: prompt, recipe, answer 1412, choice, opening ([record](../../.verification/mi2-intro-protection-20261030/README.md), addendum 31 Oct).

## 31 October: every Indy 3 cutscene plays when picked (scene setup in the engine)

The user's scope (REQUIREMENTS R-1.14, §8.6): explore everything, the game's rules never forbid a pick. `ScummSceneSetupSearch` (engine layer) finds the state a scene's own code tests (bits, story variables, object states, items held, the second object of a two-object verb, an actor's costume or room) by trial runs, as LOOM's readiness search does; a pick goes through the game's sentence script with the hero put at the object, then the verb run directly, then the cutscene started directly. Indy 3 EGA: all 153 groups begin (86 the game's way, 6 direct, 12 with a setup, 49 at their cutscene; none before began 58). The app applies the plan when a scene is picked. `ScummSceneSetupTests`; full suite 1840 passed ([record](../../.verification/scene-setup-20261031/README.md)). Not yet looked at in the real window.

## 31 October: a held object's verb in another room

"Look at" on an inventory slot in Indy 3 said nothing outside the room the object was taken in: Script 2 measures the ego's distance to the object, and the executor gave a held object its owner's position only in versions 1 and 2. As ScummVM's `getObjectOrActorXY` does in every version, a held object now stands where its owner stands when the owner is in the room (else -1 / 0xFF) for `getDist` and `getActorX`/`getActorY`. `ScummIndy3PlayTests.AHeldObjectsVerbRunsInAnotherRoom` takes the junk mail in Room 2 and looks at it in Room 73 ([record](../../.verification/indy3-rooms-20261030/README.md), addendum 31 Oct).

## 31 October: talkie speech listing read ahead; first-launch numbers

A profile (dotnet-trace) showed the talkie games' scan opening `MONSTER.SOU` once per small read of the speech walk; `ScummSpeechBank.ListSamples` now reads through a 256 KB window. With the earlier inspection-copy change, Indy 4 shows play at 4.5 s and MI2 at 4.2 s after startup (87 s and 18 s before), with about 2 s of UI blocking in total ([record](../../.verification/indy4-rooms-20261030/README.md)).

## 31 October: the first launch no longer freezes the window

Indy 4's first launch held the UI thread for 85 of 87 seconds: the room actions, the doorway overlay and the room sounds each rescanned the whole game (`GameFolderScanner.Reopen`) on every room change, two on the UI thread, up to 56 s under the cutscene derivation's load. One inspection copy per opened game is now made once in the background (`MainWindow.Inspection.cs`), the room actions and doorway targets are read on workers, and the last folder reopens after the window's first render. Play shows at 18.5 s instead of 87 s; the UI is blocked 15 s instead of 85 ([record](../../.verification/indy4-rooms-20261030/README.md)). Open: a 5.5 s scan and a 10 s first render on MI2 and Indy 4.

## 30 October: indexed script arguments

A started script's indexed argument (`local[0x003+0x000]`, `var[0x064+local[0x000]]`) is read at its resolved slot when the script starts, as ScummVM's `getWordVararg` does. Indy 3 Rooms 3 and 13 were the last rooms with an unfollowed item; all 88 EGA rooms now run with nothing unfollowed ([record](../../.verification/indy3-rooms-20261030/README.md)).

## 30 October: Indy 3 getActorWidth and actorFollowCamera into another room

`getActorWidth` is answered (actor width 24 from `initActor`, changed by `actorOps width`), and `actorFollowCamera` on an actor in another room starts that room's scene in version 3 too, as ScummVM's `setCameraFollows` does in every version. Indy 3 Rooms 6, 18, 78, 80 now leave as their scenes do; the rest of the 88 rooms are unchanged by it ([record](../../.verification/indy3-rooms-20261030/README.md)); `ScummIndy3FirstVisitTests`.

## 30 October: Indy 4 room items; Indy 3 castle rooms explained

Indy 4: a walk taken to end at once with no box route now ends where it started (ScummVM `walkActor`, as the stepped walk already did), so Room 76's catalog visit keeps the ego and camera and follows the parrot's local script 214; Rooms 77 and 87 were already clean; the first-state images and Room 4's magenta line are no longer seen ([record](../../.verification/indy4-rooms-20261030/README.md)). Indy 3 castle Rooms 20, 28, 32: black by the game's cut-away design and still pictures because guards can start a confrontation (R-8.5b); decision 7 in ROADMAP (Your checks) ([record](../../.verification/indy3-castle-20261030/README.md)).

## 30 October: Monkey Island CD Rooms 88, 89, 99 and the lookout frame

Room 88's `ENCD`/`EXCD` are empty blocks: the v5 data source now reads them as no script (ScummVM would run the next block's header bytes). Room 89 runs clean (closed by earlier reader work). Room 99 holds inventory icons 955–1045 past the 1000-object index, so the game never enters it (ScummVM `getState`/`drawObject` assert): its refusal now says so. The one-frame lookout at start no longer occurs (real-window `StartFrames`). [Record](../../.verification/monkeycd-rooms-20261030/README.md); `ScummMonkeyCdFirstVisitTests`.

## 30 October: MI2 copy protection readable in Play Game Intro; strings inside strings; talk-line wrapping (v4+)

User report (28 October): the MI2 opening stopped at the copy protection. The explicit intro answers nothing for the player, and the floppy recipe screen could not be answered: `ScummTextDecoder` dropped a string named inside a string (code 7 in string 31 names string 43, the recipe's name; string 45 names the ingredients and the `_` fields), and talk lines were never wrapped, so the centred prompt ran off the screen. Code 7 now converts the stored string with string support (depth-bounded, as ScummVM's `convertStringMessage`), and `ScummMessageTable.AddLineBreaks` ports `displayDialog`/`addLinebreaks` for engine version 4 and later. Real-window check through the protection to the opening ([record](../../.verification/mi2-intro-protection-20261030/README.md)); `ScummMonkey2IntroTests`. Wrapping now applies to every v4/v5 game's talk lines.

## 30 October: roadmap, crash log, honest matrix

[ROADMAP](../ROADMAP.md) (seven gates per game), `CrashLog` in the shipped app (`%LOCALAPPDATA%\SCUMMexplorer\crash-logs`, last actions, unclean-exit log), Sound ◐ for the ten non-LOOM rows until the user's listening check, and [ROADMAP (Your checks)](../ROADMAP.md).

## 28 October: format rules replace the last game-id capability lists

The Advanced tree's Costumes/Sounds/Scripts groups, the Costumes tab (`HasCostumeCards`) and the old-v3 costume lookup tested `GameId.Loom`/`LoomVga`/`IndianaJones3` lists. They now share one format rule, `HasCostumeCards`: version 1 to 3 room files, or a v4/v5 archive (`V4Disks`/`V4Archive`). The browsed object names on the stored-image rows were keyed on `GameId.Unknown`; they now follow `_roomNamesAreStored`, set where a browsed room's names are read. For the nine checked games nothing changes (tree groups, cards and the 27 preview rooms are equal before and after); a version 3 game with an unrecognised id now gets the groups too. Left as LOOM-specific on purpose: the EGA live-path splits in `DecodeBrowsedOldV3Room` and the session toggle, the live sound/clock code, and the text gallery's speaker names.

## 26 October: Indy 3 judgment call (reusability against specificity)

Indy 3's special cases are of two kinds. Facts about the game (its script dialect, AdLib constants, extra timers, its 256-colour room-file layout) are already in `ScummProfile`, the one place the engine maps a game to variants: they stay. The `GameId.IndianaJones3`/`Unknown` lists in the window were an early allow-list of "the games the old-v3 browsing path serves", not Indy behaviour. Two of them are now format rules: the v3 LFL room source reads any version 3 room it can decode (`game.Version == V3`), and the preview's browsed branch is chosen by `RoomLoad.Live` (the source says whether the room belongs to a live session) instead of by game id. Not changed, on purpose: the Advanced tree's group filter, the Costumes tab test and the old-v3 object lookup (`MainWindow.xaml.cs`, `MainWindow.CostumeCards.cs`) still list game ids; they are capability tests whose right form is a per-game or per-format flag in `GameSupport`, to be done with the next game that needs it. The trial Indy module (hero name "Indy") was not kept: the name would be hardcoded, and a reusable version would take the hero from the game's data. Verification: same room-preview check as above, equal before and after.

## 26 October: room sources and module registry (non-LOOM trial)

Room preview: `RefreshRoomPreview`'s four ways of reading a room are now `RoomSource` classes (`MainWindow.RoomSources.cs`: browsed v4 disk, older v1/v2 file, v3 LFL file; `Loom/MainWindow.LoomRoomSource.cs`: LOOM CD), tried in order from `RoomSourceList`. Each returns a `RoomLoad` (kind, background, bytes, walk boxes, CD scene, first-visit scene, stored objects, CD room data) and the preview no longer tests the game id to read a room; the unsupported-format message is "no source reads this room". Modules: `IGameModule` gained `Handles(game)`, `GameModuleBase` holds the defaults (a game's module overrides only what differs; `NoGameModule` is just the base) and `GameModules` is the one list the window consults (`CurrentModule` asks each), replacing the `IsLoomGame` pick and the LOOM-only instance field. Trial: a temporary Indy 3 module of six lines (`Handles` plus `CreateNames` with hero "Indy") built and, in the real-window check, changed only Indy's readable script text (8240 to 8212 characters, "the hero" became "Indy") and left Zak's identical; the trial was then removed (adding the hero's name is a visible change nobody asked for). Verification: the real-window check now selects the first three rooms of every game and reports size, pixel hash and object, record and verb counts. Before and after agree for all 27 rooms. The object-note and status text of the browsed games (Zak, MI2) changes between two runs of the same build (a race with the background first-visit work, seen on the commit before this change too), so the comparison leaves those two fields out.

## 26 October: playback and verb traits of IGameModule

Smaller LOOM checks became traits: `EstimatesRoomArtwork` and `RoomScreenRows` on the module, the CD game's real-clock playback (catch-up frames, the first-frame refresh, the scene-sound attach) keyed on `Voice.ReplacesSceneSound`, and the object-verb decode on whether the game has its own data source (`_cdData`). Checked by the real-window run (rows per game added): identical before and after on nine games. Not moved: `RefreshRoomPreview` / the room preview's `isCd` branches. It is one 150-line method that builds a room from four different sources (CD archive, browsed v4 disk, older v1/v2 file, v3 EGA file); moving the CD branch alone would copy the other three. It wants a room-source abstraction designed together with a non-LOOM module trial.

## 26 October: tree and room-card seams of IGameModule

`MainWindow.RoomCardActors.cs` moved to `Loom/MainWindow.LoomRoomCards.cs`, with LOOM's card rendering behind one static list in the shared window (`GameRoomCardRenderers`: each entry returns a game's own card or null; LOOM's `TryRenderLoomRoomCard` covers the CD scene and the EGA initial state). `UsesInitializedRoomCards` moved there too. The Advanced tree's CD audio cue group is built by `IGameModule.ExtraResourceGroup` (`CdCueTreeGroup` in the LOOM file). The real-window check now renders the first four room cards of each game (pixel hashes) and lists the tree groups: identical before and after on nine games. Still LOOM-keyed in the shared window: the tree's group filter by game id, the browsed old-v3 room (`DecodeBrowsedOldV3Room`, `game.Id != Loom`), the room preview's CD/EGA branch and the object-verb decoding.

## 26 October: script page, session-kind and open-time seams of IGameModule

`IGameModule` also gives the script page its loading note, its leftover-bytes note and the CD decode source (`ScriptLoadingNote`, `ScriptBoundaryNote`, `DecodeScript`), says whether the game runs in a live session of its own (`HasDedicatedSession`, which replaces the window's `IsLoomGame` for the click path, keys, scroll arrows, one-screen layout and the generic-play exclusions; `IsLoomGame` now only picks LOOM's module and room controls) and starts that session at open time (`ReadObjectSnapshot`, `StartSession`: object snapshot, EGA/CD session, scene sound or voice, charset). Verified by the real-window check (now also charset, CD data, object states, snapshot and the script loading note): identical before and after on nine games. Remaining LOOM checks are the tree and room-card building, the room preview for the CD and EGA data, and the object-verb decoding.

## 26 October: status seam of IGameModule

Game Status needed little: the button and the dialog already went through `IRoomControlProvider.GameStatus()`. What moved: `BuildGameStatusWindow`, `CanEditGameStatus` and the speaker placement of a direct verb (`PlaceSpeakersForDirectVerb`) into `Loom/MainWindow.LoomStatus.cs`, with `IGameModule.SpeakersForDirectVerb` for the one shared caller (`ActivateObjectVerb`; empty for other games). The Costumes tab (`HasCostumeCards`) stays shared: it is a format capability, not a game's. Verified by the real-window check extended with the Game Status button state and tooltip: identical before and after on nine games (LOOM games show the exploration-session button, the others hide it or show the inventory one). Opening the Game Status dialog itself was not exercised. Remaining: the script page's LOOM loading note and leftover lookup, and the live-session flow, room cards and script view checks.

## 26 October: sound/voice seam of IGameModule

`IGameModule.Voice` (`IGameVoice`: stop, pause, resume, detach, update, skip pending, follows, attach, `ReplacesSceneSound`; `NoVoice` for games without one), `VoicesEnabled` and `UpdateRoomSounds(room)` take the LOOM CD checks out of the shared window: session attach, the per-frame voice update, pause and resume, the room-visit cue skip, the voices option and the CD Sounds list. LOOM's side is `Loom/MainWindow.LoomVoice.cs` (`_cdVoice`, `_cdRoomCues`, `LoomCdVoice`, `UpdateCdRoomSounds`); `SceneSoundPlayer` stays shared because LOOM EGA and the generic session both use it. Verified with the real-window check extended with the session's sound owner (scene sound or voice), the Sounds button and the voices option: identical before and after on nine games (LOOM CD follows the voice, LOOM EGA the scene sound). Not exercised: playing a CD voice line. Remaining: Game Status and costume cards, the script page's loading note.

## 26 October: names seam of IGameModule

`IGameModule` also gives the window the names of the open game: `CreateNames(game)` returns a `GameNames` (glossary, verb names, hero name) that replaces the window's `_glossary` and all its `LoomResourceNames.*(_glossary, ...)` calls (`_names.Label/Evidence/NameScripts/NameBits`); `ScriptKind(resource)` holds LOOM's two fixed script-page subtitles (R-9.1) and `ScriptPlayback(scriptId)` the script page's Play button. LOOM's module supplies the glossary, `LoomVerbNames` and "Bobbin"; every other game gets the engine variable names and "the hero". One deliberate change: the "Used by" lines of a non-LOOM game no longer label verbs with LOOM's verb names (they did, wrongly); they show the verb number. Verified by `.verification/game-module-20261026/Check` (extended with the first room's and first script's page title, subtitle and readable-text hash) on nine games: identical before and after. Remaining LOOM checks in the window: sound/voice, Game Status and costume cards, the script page's loading note and leftover lookup.

## 26 October: LOOM-only App code separated; IGameModule (cutscene seam)

`MainWindow.Cutscenes.cs` and the LOOM room-control provider moved to `App/Loom/` (`MainWindow.LoomCutscenes.cs`, `MainWindow.LoomRoomControls.cs`); the generic provider has its own file (`MainWindow.PlayRoomControls.cs`), so `MainWindow.RoomControls.cs` holds only the shared consumer. New `IGameModule` (`MainWindow.GameModule.cs`, picked by `CurrentModule` like `CurrentRoomControls`) takes what the window kept for LOOM at open time: reset of the CD scan state, loading of the replay catalog, the background scan start and the Game Texts cutscenes; `LoomGameModule` lives in the LOOM file. Verified: build, and `.verification/game-module-20261026` (nine games through the real `OpenGameFolder`, picker/gallery/catalog output identical before and after). Not done: the other ~100 LOOM checks in `MainWindow.xaml.cs` and the shared files (names/glossary, script presentation, CD voice, costume cards, Game Status); each is a seam of its own, in the order cutscene (this), names, sound/voice, status.

## 26 October: one game identification for every format (Discovery refactor)

`GameFolderScanner` is now only a dispatcher: `ArchiveLayout` lists v4/v5 games (disc images, DISKnn.LEC, NAME.000) and `LflFolderLayout` lists one-file-per-room games (v1 to v3); both identify the game with the single `GameIdentifier` from a `GameFingerprint` (SHA-256 of the data files and the sizes of the resource directories): known edition (`known-editions.json`, now every game and format, one row per disc language folder), then files shared by an edition's game, then the format's own index layout (classic v1), then directory sizes, labelled as inferred. Executable and folder names are no longer used (`KnownGameFiles`, `KnownGameTitles`, `IsLoomCd` and the index-shape table removed); `KnownGame` names the game, `GameId` stays the LOOM/Indy3 code path, and `GameSupport.FamilyOf` maps (`Game`, `Version`). The catalog is made by `.verification/known-editions/Make` (replaces `make.py`); LOOM CD's Kixx dump and MI2's MT-32/SB16 copies hold identical game data and are one edition each. Checked: full test suite after the change, and a test that identifies each format from its data alone. Not verified in the running app.

Follow-up the same day: the fallback for an edition the catalog lacks (files shared by a game's editions, then directory sizes) is no longer a hand table for LOOM and Indy 3 only: `Make` derives it for every game from the catalog (`games` section of `known-editions.json`), keeping only what matches no other game. LOOM CD is listed by the same `ListBrowsed` as every other v4/v5 game (`ListLoomCd` removed); `GameIds.For(game, version)` is the one place that picks the game-specific code path (`GameId`), and `GameLibrary.V4Archive` is set for any single-disk v4 archive game. The LOOM CD session and the window's CD paths (`LoomExplorationSession`, `_cdVoice`, CD cutscenes) are still LOOM-only code: retiring them in favour of the generic session is a separate, larger task. Full test suite passes (1824); not tried in the running app.

## 24 October: Maniac enhanced hover freeze, live route: still not reproduced (guard added, R-6.48)

The user's steps (Play Game Intro, two kids, skip, Hotspots On hover, hover the sign, then in and out of it) were run live: headless on `ScummGameSession` (flat 0.09 ms frame, 0.15 ms scene, 2.4 ms compose over 3,600 frames), in a real `MainWindow` placed off screen with 6,000 and 8,000 synthetic moves (body 0.2 ms, heartbeat gap at most 31 ms, no growth in overlay children, rows or trace) and with the real pointer for 20 s each in Hover and Always. No cost growth, so no fix. The user then spotted that every On hover pointer move called `RefreshPlayOverlays` (scene capture with two snapshots, hotspot derivation, canvas rebuild): a move now redraws only when `RoomHotspotRules.HoverKey` changes and `Scene()` takes one snapshot (per move 0.20 ms to 0.015 ms off screen). Added `HoverCostGuardTests` and R-6.48; record: [hover-freeze-live-20261024](../../.verification/hover-freeze-live-20261024/README.md).

## 24 October: Maniac Mansion and Zak doorways in a static preview; hover freeze not reproduced

The browse path found doorways only for engine versions 3 and 4, so Maniac Mansion and Zak (v1/v2, classic and enhanced) showed none until Play Game Intro ran the native screen; v1 to v4 now derive them from the room's own verb scripts, and the browsed Advanced rows are no longer collapsed while no native screen is live (R-6.47, ScummStaticDoorwayTests, window check in [record](../../.verification/hover-freeze-20261024/README.md)). The reported hover freeze on Maniac Room 44 did not reproduce (heartbeat gaps under 40 ms); the HoverStorm harness stays.

## 24 October: REQUIREMENTS based on the shared room controls (R-6.30 to R-6.46)

New section 6.1 states every room control once (bar, both Play Cutscene buttons, Play Game Intro, Interact, Sounds, Options, Advanced panel, Game Status entry, keys, live/static/cutscene states) as fixed UI plus what IRoomControlProvider supplies; the scattered rows point to it (R-6.9, R-6.17 and R-6.22 are stubs merged into R-6.39; R-6.5, R-6.6, R-6.23, R-7.3 and others rewritten). Documentation only; the rows record the places where code does not yet meet them.

## 24 October: LOOM parity of the room controls (R-6.21 to R-6.25)

Interact (bar button and object menu, right click), Step ▸, Sounds rows and the Hotspots/Doorways default radios of every verb-based game follow LOOM; the radios were unselected in LOOM too. [Record](../../.verification/loom-parity-20261024/README.md).

## 24 October: static previews keep the room's controls and run only ambient scripts (all generic games, LOOM)

- A room that cannot be prepared live (MI2 Room 4, Indy 3 Room 1, LOOM static rooms) keeps the playback bar (Pause works), Play Cutscene, Interact, Sounds and Play Game Intro, and its permanent animation runs: only the scripts its entry starts that loop on pictures/sounds and never cut, print, load a room, move the camera or touch input (`ScummAmbientScripts`, decided from the game's code, no room list); none of the other entry code runs and no click reaches the game. Maniac Mansion keeps its own startup rules. Requirements R-8.5b, R-6.17, R-8.2a, R-13.11, R-13.12 extended. Room Sounds by number for MI2 and Indy 4. Record: [static-preview-controls-20261020](../../.verification/static-preview-controls-20261020/README.md).

## 24 October: room overlays survey of the verb-based games

Surveyed Indy3 EGA/256 colours, Monkey Island 1 floppy/CD, Monkey 2 and Indy 4 in the real window on master 71d55e98: Hotspots, Doorways (own list and markers), depth areas, walk boxes and the Advanced room scripts/interactions already work through the shared generic path; none of them has a Game Status editor, so the button is correctly absent. No product change. [Survey](../../.verification/room-overlays-20261020/README.md).

## 23 October: room controls and audio lifecycle (Maniac/Zak, shared UI)

- Requirements now make Normal/Advanced room cutscene and direct Interact access, native room-selector following, effective overlays and Advanced room source inspection explicit for the supported games. Plain native names replace verb bytes in Normal mode.
- Live hotspots and doorways use their own display modes and native eligibility; depth areas and walkboxes align with the actual game screen. Native doorway travel reveals the selected card and preserves its entry cutscene.
- Game Status validates and applies choices, then closes in one click; rejected batches stay open and session-bound Undo remains available after reopening. Source-associated Sounds menus work outside LOOM, and changing folders retires every old audio source and pending render.
- All four editions pass final action, overlay and audio/status windows. Required full suite: **1,739 passed, zero failed, five expected skips**; final build zero warnings/errors; three regenerated-matrix checks pass. Current bounded availability: [checkpoint](checkpoints/20261019-RoomControls-ManiacZak.md). No support levels are promoted; current classic costume estimates and derived cutscene limits remain in REQUIREMENTS.
