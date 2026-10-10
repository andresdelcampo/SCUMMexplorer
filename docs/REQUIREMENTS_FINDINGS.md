# Findings from drafting the requirements

10 October 2026. Found while reading the docs and source for [REQUIREMENTS.md](REQUIREMENTS.md). **Static reading only**: nothing was built, run or tested, so every item says how sure it is. Nothing here has been changed in the code. Requirement ids point back to the requirements.

Severity: **High** = wrong visible behaviour, **Med** = user-visible inconsistency or real risk, **Low** = cosmetic or hygiene. Confidence: *Read* = the code plainly does this; *Likely* = follows from the code but not run; *Question* = needs your intent.

**Status 10 October:** F-01..F-07 and I-01..I-06 applied or answered, see the CHANGELOG entry "requirements findings applied". Open from here: wiring the script scan into EGA editions without a catalog (M-01), and whether to show the 90 helper reactions the scan hides.

## A. Faults

| ID | Sev | Conf | Finding | Where | Req |
|---|---|---|---|---|---|
| F-01 | Med | Read | **Opening LOOM CD shows the "unsupported game" texts.** `ShowGame` picks the page subtitle and the empty-preview text with a switch/ternary that knows only Indy3 v3 and LOOM v3. For `GameId.LoomVga` (v4) it falls to the default: subtitle `The folder index was read, but room and resource inspection is not supported for this game yet.` and preview text `This game layout does not yet have a supported room background decoder.` Nothing later overwrites them before the first room is chosen. Fix: add a `LoomVga` case (e.g. `Explore rooms, objects, and sounds.` / `Pick a room to explore the game from there.`). | `MainWindow.xaml.cs:427-440` | R-4.6, R-4.7 |
| F-02 | Low | Read | The platform line has a dead branch: `GameInfo.Text = game.Platform == "DOS" ? "DOS edition" : game.Platform`, but the scanner always sets `"DOS (inferred)"` for folder games, so the user sees `DOS (inferred)`, never `DOS edition`. Either the scanner string or the check is stale; as written, the "(inferred)" wording leaks into a Normal-mode label for a game the app identified with confidence. | `MainWindow.xaml.cs:373`, `GameFolderScanner.cs:128` | R-4.4 |
| F-03 | Low | Read | Misplaced doc comment: `LoomResourceNames` has two stacked `<summary>` blocks; the first ("Adds the glossary name after each `script 028`…") describes `NameScripts`, which is declared below `NameBits`. Tooling attaches the wrong summary to `NameBits`. | `LoomResourceNames.cs:25-32` | – |
| F-04 | Low | Read | Fragile coupling: `CutscenePickerWindow` removes the "Needs validation" filter with `CategoryFilter.Items.RemoveAt(3)`. It silently removes the wrong item if the XAML list order ever changes. Use a named item or tag. | `CutscenePickerWindow.xaml.cs:18`, `.xaml:15` | R-8.1 |
| F-05 | Low | Read | Title spelling differs between two code paths for the same draft: `LoomDraftNames.ForVerb` yields `Name (reversed)` while `ReplayTitle` yields `Name (reverse)`. The CD title code patches it with `.Replace("(reversed)", "(reverse)")`. Works today, but two spellings for one concept is a trap; the Game Status draft list also uses `Title`. | `LoomDraftNames.cs`, `MainWindow.xaml.cs:4139` | R-8.8 |
| F-06 | Low | Likely | `GameTexts_Click` tells a user with no game open `Open a LOOM game folder first.`, but the window is also valid for Indy3. Wording only. | `MainWindow.xaml.cs:187-193` | R-11.1 |
| F-07 | Low | Read | Stale build output inside the source tree: `src/SCUMMexplorer.Core/Exe/` holds 11 old harness build folders (`click-fix`, `room28-build`, `step2-build` …). Ignored by git (`Exe/`), so harmless to history, but it sits where `Core` sources are and clutters searches. Deleting is yours to approve. | `src/SCUMMexplorer.Core/Exe/*` | – |

## B. Inconsistencies (code ↔ docs, edition ↔ edition)

| ID | Sev | Conf | Finding | Req |
|---|---|---|---|---|
| I-01 | Med | Read | **EGA and CD cutscene titles are built by different rules.** EGA catalog titles (generated, then rewritten at load) read `Use response`, `Look response`, `Walk response`, and `Use sheep`, `Use gate` for the same kind of trigger. The CD path always builds `Use <object>` / `Look at <object>` / `Walk to <object>` unless the source loads rooms. The same game action can therefore have two different titles across editions, and the EGA list mixes both styles. A single title function in Core would remove this. | R-8.2 |
| I-02 | Med | Read | Room names: `LoomResourceNames.Entry` supports `ResourceKind.Room` and the glossary has 8 room names, but the tree builds room nodes from `room.Name` (plain `Room NN`) and never calls it. Room names appear only in Readable scripts. Intended? (Question.) | R-5.2 |
| I-03 | Low | Read | `README.md` "Verified editions" has no Italian line, though `CLAUDE.md` says Italian exists under `Games/loom ega it` and is unreviewed; and the `editions-20261005` record does not list Italian either. A short "not yet reviewed" bullet would keep the list honest. | R-14.4 |
| I-04 | Low | Read | `CLAUDE.md` tells the reader to use `C:\Users\adelc\.codex\RTK.md` as mandatory, while the global instructions load `C:\Users\adelc\.claude\RTK.md`. Two RTK files may drift. | R-16.2 |
| I-05 | Low | Question | `LoomSpeakerNames` comments quote Spanish lines (`Basta, Lachesis.`) as evidence for actor names. The English reference may differ in which actor numbers appear in which scripts (script ids 91/93/98/105). Is the scoped table verified against English? Not checked. | R-10.4 |
| I-06 | Low | Question | `GameTexts_Click` runs the LOOM text extractor for any open game; for LOOM CD (v4) it is not stated anywhere that Game Texts is supported. The README lists Game Texts under EGA/Indy3 only, and the support matrix in the requirements marks it "not stated". Confirm whether CD should offer it, hide it, or show a one-line "not available". | R-11.1 |

## C. Risks

| ID | Sev | Conf | Finding | Req |
|---|---|---|---|---|
| K-01 | Med | Read | **Catalog fingerprinting hashes 78 files synchronously on the UI thread** every time a LOOM EGA folder is opened (`LoomReplayCatalog.Load` is called from `OpenGameFolder`). It stops at the first mismatch, so other editions are cheap, but the reference edition reads every `.LFL`. Probably tens of ms on an SSD, but it is avoidable blocking on a hot path. Move to a background task or cache by folder timestamp. | R-8.18 |
| K-02 | Med | Read | **Replay catalog is all-or-nothing.** One changed byte in any of 78 files (for example a v1.2 patch to a depth mask, which the editions record shows is harmless to the app) disables all 328 replays for that edition. The decision not to rerun discovery per edition is yours (5 Oct); the narrower point here is that the fingerprint could cover only the files a recipe actually reads, so an unrelated file does not disable unrelated scenes. | R-8.18, R-8.19 |
| K-03 | Med | Read | **Titles and picker logic cannot be unit-tested.** Cutscene title formatting (`LoomReplayTitle`, `LoomDraftNames`, `CdCutsceneTitle`, `PickerVerbTitle`) lives in the App project; the test project covers Core only. The title formats in R-8.6… are therefore guarded by no automated test. Moving the pure string logic to Core (`Loom/Scenes` or `Loom/Text`) lets tests pin the formats. | R-8.x |
| K-04 | Med | Read | **Very large files.** `MainWindow.xaml.cs` ≈ 5 990 lines, `ScummBoundedScriptExecutor` ≈ 2 835, `LoomExplorationSession` ≈ 2 760. Review, merge and test cost grow with that; the cutscene section (≈ 400 lines, 3900–4300) and the tree/catalog code are natural partial-class or Core extractions (`MainWindow.CostumeCards.cs` already shows the pattern). | R-16.1 |
| K-05 | Low | Read | `LoomDraftNames`, `LoomVerbNames`, `LoomResourceNames`, `LoomStatusPresentation` and `LoomEgaStateFlags` are `LOOM`-generic or `LOOM EGA` but live in the App project, against the rule "put code in the lowest layer that is true for it". `LoomDraftNames` even says CD uses it. Naming and layering tidy-up. | R-3.6, R-16.1 |
| K-06 | Low | Read | `LoomCdCutsceneIndex` works only through `IScummDataSource` and generic opcodes (`cutscene`, `print`, `loadRoom`), but sits in `Loom/Cd` and its `VerbLabel` hard-codes LOOM's 0x36/0x37/0x38. The scan itself is `SCUMM` generic and could serve the EGA path too (see M-01). | R-8.21 |
| K-07 | Low | Likely | `view-options.txt`, `last-folder` and cache files are unversioned plain text/`key=value`. An older build reading a newer file ignores unknown keys (good) but a future change of meaning of an existing key would not be detected. Fine for now. | R-4.8 |

## D. Possible improvements

| ID | Idea | Why | Req |
|---|---|---|---|
| M-01 | **Derive the EGA cutscene list from scripts like CD does**, for editions other than the reference. CD already finds cutscene triggers, groups them, derives start setups and per-room lists with no hand-made catalog. Reusing that on EGA would give every edition a picker without the discovery pipeline you decided not to rerun, with the reference catalog still preferred where its fingerprint matches. | Closes the biggest edition gap (R-8.19) using the same code path that already works for CD. Needs your OK because it changes the 5 Oct decision's practical effect, not the decision itself. | R-8.18–8.21 |
| M-02 | One shared title builder (Core) for both editions, with a golden-file test of all titles of the reference catalog. | Removes I-01, F-05 and K-03 together. | R-8.2 |
| M-03 | A single "support level" description per game in Core (opens / rooms / live session / scripts / cutscenes / texts / status / audio), used for the subtitle texts, the empty-preview text and the README table. | F-01 and I-06 are both "what does the UI say this game supports?" answered in scattered `if`s. | R-4.6, §3.1 |
| M-04 | Show **where a draft name comes from** (its evidence level: `decoded`/`reading`) in the Game Status grid tooltip. Shaping and the reverse of Silence are known `reading`s; today the grid shows them as plain names. | Matches R-2.3, "never present a reading as a fact". | R-12.2 |
| M-05 | ~~A "N scenes are not yet playable" note in the Normal picker.~~ **Rejected (10 Oct):** Normal mode is strictly exploratory/playful and never shows validation status; hidden `Needs validation` entries are intended. Recorded as R-1.13. | Intent of the two modes. | R-1.13, R-8.17 |
| M-06 | A golden test that every EGA catalog title round-trips through `LoomReplayTitle.Format` to exactly one `Room NN · …` prefix (guards against double prefix, F-05-class regressions). | Cheap, pins §8.2. | R-8.2 |
| M-07 | Document, in one place, the **tag map** of the requirements (this file's §0) in `CLAUDE.md` so new work chooses `Scumm`/`Loom`/edition folders from the same vocabulary. | Keeps R-16.1 practical. | §0 |

## E. Things I could not confirm without running the app

Listed so nobody mistakes them for verified requirements:

1. That the room's **Play Cutscene** button is visible in Normal mode (the XAML has it outside the Advanced-only group and `UpdateAdvancedMode` does not hide it, but its visibility is also set elsewhere).
2. Keyboard pan of the room viewport (`RoomViewport_PreviewKeyDown`) — documented in requirements only as "or keys".
3. The exact wording of CD draft/skill-screen messages after the intro.
4. Whether Game Texts for LOOM CD opens, and what it shows (I-06).
5. That every `Dialogue and reactions` EGA entry is reachable from the room popup (`Membership` is generated data).
