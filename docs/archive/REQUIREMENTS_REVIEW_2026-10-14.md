# Requirements review and scale-out proposal

**Status 14 October: stage 0 done** (decisions: stage 0 approved, demos stay a separate family, ID suffixes instead of renumbering). Done: D1–D8 fixed in REQUIREMENTS.md; `GameSupport` is the registry (families `LoomEgaReference`, `LoomEga`, `LoomEgaDemo`, `LoomCd`, `Indy3Ega`, `Indy3Vga`, `MonkeyFloppy`, `MonkeyCd`, `Monkey2`, `Indy4`, `ManiacEnhanced`, `ManiacClassic`, `ZakEnhanced`, `ZakClassic`, plus the hidden `OtherV3` and `OtherBrowsed`); the README and §3.1 tables are generated from it and pinned by `SupportMatrixTests`; `SupportConformanceTests` checks, on the local `Games/` copies, that each family is recognised and its claimed rooms, costumes, sounds, scripts and texts exist (all 13 present families pass). The UI gate that uses the registry is the Game Texts button and the page texts; the other levels are not wired to buttons yet (they would change behaviour). The text below is the original review.

14 October 2026. Static review of [REQUIREMENTS.md](REQUIREMENTS.md) against the README support matrix, `CURRENT_WORK.md`, the CHANGELOG and the source. Nothing was built or run; nothing in REQUIREMENTS.md was edited. Purpose: decide what to fix before the requirements are applied to the other games.

## 1. Verdict

**The LOOM half is accurate; the cross-game half is stale and the document has no mechanism for the second game.**

Spot-checked against the source and found correct: window and Game Status sizes, minimum window, 250 ms status refresh, accent colour, and about twenty literal strings (Game Texts headings, `CUTSCENES IN THIS ROOM`, `Restart game state`, welcome and empty-preview texts, `Background preview only`, `Could not prepare this scene`, Readable-view header and `Names used (evidence`). The LOOM replay catalog and the sound-device, Game Status and cutscene sections match what `CURRENT_WORK` describes.

## 2. Defects in REQUIREMENTS.md

| # | Where | Problem |
|---|---|---|
| D1 | §3.1 matrix | Contradicts the README matrix and the CHANGELOG. It says Scripts `–`, Game Texts `–`, sounds `– (not read)` for Monkey Island, v5, v1/v2 and Indy3 256; says Indy3 EGA scripts are "not shown in tree". Since 11–12 October all of these read scripts and texts, and sounds play (AdLib, PC speaker, PCjr/Tandy, Roland, sampled, speech, CD tracks). |
| D2 | R-3.11, R-3.12, R-3.14, §17 | Still say scripts, sounds and objects are "not offered" / out of scope for v1/v2/v5 games, and §17 still lists v5 sounds and Monkey Island 1 CD tracks as out of scope. Stored-object boxes and the doorways overlay for browsed games (12 October) appear nowhere. |
| D3 | R-5.2 tree table, R-5.5 | "Costumes / Scripts / Sounds present for LOOM EGA, LOOM CD" and "Indy3 shows only Rooms and Game files" are wrong. `MainWindow.xaml.cs:467` builds the groups from the resource kinds for any game. |
| D4 | R-11.1 | "Game Texts today LOOM EGA, LOOM CD and Indy3 EGA" is stale. `GameSupport.Texts` is true for every browsed family that reads scripts. |
| D5 | R-1.3, R-4.1 | R-1.3 says the README omits Indy3, but the README now lists every browse-only game. R-4.1 says the title is `SCUMMexplorer`; the main window now shows the release label (`Preview1`, commit a6ef241). |
| D6 | IDs | `R-5.5` and `R-6.13` are each used twice; `R-5.9`–`R-5.11` and `R-3.1`–`R-3.14` are out of order. A requirement cannot be cited unambiguously. |
| D7 | Tags | §7 is titled "Interaction model (LOOM)" and §9 "Scripts" is tagged `SCUMM`, but the text is the LOOM binding: input scripts 005–007, single/double click, `LoomSpeakerNames`, `LoomScriptPresenter`, `LoomCutsceneIndex`. R-0's "Implementation:" convention is applied to some rows (R-1.5) and not to these. A second game cannot tell which half of each row is the generic part. |
| D8 | Dating | "Drafted 10 October… read, not run" is still the header. Five days of work (v5 audio, scripts for 9 games, stored-object hotspots, CD tracks) post-date it. |

## 3. Gaps (missing requirements, not errors)

1. **No generic interaction requirement.** R-1.5 (one click walks and uses, double click looks) is LOOM-specific: LOOM has no sentence line. Every other game needs verb pick, sentence line, inventory, and the object-click rules of its own input scripts. Nothing says what "Play" means outside LOOM.
2. **No per-feature definition of "done" for another game.** §3.1 lists ✔/– per game, but not what ✔ requires (for Objects: first-visit state through the entry script? stored state only?). The README uses ◐ with notes the requirements do not define.
3. **No requirement for Costumes, Walk boxes or Objects as features.** They appear only inside tree rows and R-5.5. They are README columns, so they need rows.
4. **No claim-to-evidence link.** Nothing ties a ✔ to a test or harness that would fail if the claim became false.

## 4. Why scaling out is hard today: three sources of truth that disagree

| Source | What it says about e.g. Monkey Island 1 floppy |
|---|---|
| README matrix | Scripts ✔, Texts ✔ |
| REQUIREMENTS §3.1 | Scripts `–`, Texts `–` |
| `GameSupport.For` (Core) | `Scripts: false`, `Texts: true` (but `Scripts: true` for v1/v2/v5) |

`GameSupport` carries seven capability flags, but the app reads only `Texts`, `Subtitle` and `EmptyPreview` (`MainWindow.xaml.cs:521`, `:1025`); `Rooms`, `LiveSession`, `Scripts`, `Cutscenes`, `Status`, `Audio` are unused, so nothing stops them drifting, and they have. Its last switch arm (`Unknown` + V5/V4/V2/V1, all false) is reachable only by V4 without archives. It has no columns for Objects, Walk boxes, Costumes or Sound, which are half of the README. The UI also branches on game or version in about 84 places in the App project.

## 5. Proposal: make "upgrade feature X for game G" a small, repeatable step

### 5.1 One capability registry (single source of truth)

Turn `GameSupport` into the registry the README columns come from:

- A **family key** (not the raw `GameId`): `LoomEga`, `LoomEgaDemo`, `LoomCd`, `Indy3Ega`, `Indy3Vga`, `MonkeyFloppy`, `MonkeyCd`, `Monkey2`, `Indy4`, `ManiacV1`, `ManiacV2`, `ZakV1`, `ZakV2`. One place maps a `GameLibrary` to a family (next to `ScummProfile.For`).
- Per family, one **level** per feature: `None / Partial / Full` plus a one-line note, for the ten README columns (Rooms, Objects, Walk boxes, Costumes, Sound, Texts, Scripts, Play, Cutscenes, Status).
- The UI reads it for its gates (buttons, tabs, subtitles), so a flag that is false really switches the feature off.
- A small tool or test **renders the README table and REQUIREMENTS §3.1 from the registry** and fails when they differ. The matrix is then edited in one place and cannot drift.

### 5.2 A feature ladder instead of per-game rows

For each feature, requirements define levels once, with the seam and the acceptance check, for example:

| Feature | L1 | L2 | L3 |
|---|---|---|---|
| Objects | stored boxes and names (`IScummDataSource` object list) | first-visit state: entry script run by the bounded executor in the family's dialect | click responses: verb scripts run, game's hero on screen |
| Scripts | disassembler in the family's dialect, coverage counted | Readable view, names with evidence | bounded execution, unsupported behaviour reported |
| Play | none | Interactive room: walk, camera, verbs from the game's own input scripts | scenes/cutscenes |

Then "upgrade Objects for Monkey Island 2" is: supply the family's dialect to the executor, tick L2 in the registry, pass the L2 acceptance check. The LOOM rows become *the worked example of each level*, with their LOOM-only parts (input scripts 005–007, draft verbs) labelled as the LOOM binding (fixes D7 and gap 1).

### 5.3 Conformance tests per claimed level

One parameterised test per level over every family whose data folder is present (the `SCUMM_*_TEST_ROOT` convention already exists; skip when data is missing): rooms decode without gaps, scripts reach a coverage floor, texts extract, costumes render, the registry claim is tested. The check set for a level is written once and each new family is one fixture row. This also enforces the "choose only relevant checks" policy without repeating harnesses by hand.

### 5.4 Move generic code out of the `Loom` namespace as a second game needs it

`LoomCutsceneIndex`, `LoomScriptPresenter`, `LoomTextExtractor`, `LoomSpeakerNames` and `LoomDraftNames` are named and placed for LOOM but work through `IScummDataSource` (the findings file already says so: K-06). Move each to `Scumm/` when the first non-LOOM game uses it, leaving the LOOM tables (names, drafts, verbs) as data the shared code takes. Do this per feature, not as a big-bang rename.

### 5.5 Suggested stages

0. **Consolidate** (small, no behaviour change): fix D1–D8, introduce the registry (5.1) with the current truth, generate the README and §3.1 from it, wire the UI gates to it.
1. **Objects, level 2** for the v4/v5/v2 families (executor dialects exist for scripts; the 12 October probe says Indy3 and Monkey rooms need the live session for real starting states, so this is the first real design question).
2. **Costumes and Walk boxes**: close the `◐` cells (palette of the storing room, classic v1/v2 actor colours from scripts).
3. **Play, level 2** for one verb-based family first (Monkey Island 1 floppy, or Indy3 EGA, whose dialect and dialogue verbs 120–125 are already decoded), which settles the generic interaction requirement from gap 1.
4. Cutscenes and Status per family, only where the game has the matching mechanism.

## 6. Decisions needed

1. Approve stage 0 (registry plus generated README/§3.1) as the first step. It changes no behaviour, only removes the three-way drift.
2. Is the family list in 5.1 the right granularity? (e.g. is `LoomEgaDemo` its own family, or an edition of `LoomEga`?)
3. Should I renumber the requirement IDs now (D6)? It touches every cross-reference in the CHANGELOG, code comments and harness READMEs, so I would rather add suffixes (`R-5.5a`) than renumber.
