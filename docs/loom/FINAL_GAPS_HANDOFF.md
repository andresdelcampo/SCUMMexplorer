# LOOM EGA / VGA final gaps — next-session dossier

Prepared 8 October 2026, after local correctness-audit commit `62e1f43`. This session prepares the evidence and next work; questions and investigation of the gaps belong to the next session. The [audit](../../.verification/correctness-20261008/README.md) remains the authoritative validation record. This document distinguishes observed results, earlier research, hypotheses and work still needed.

## Established baseline

| Area | Checked result | What it does not establish |
| --- | --- | --- |
| English EGA replay catalog | 328/328 recipes complete normally and with Escape; source-file hashes match | Reachability of uncatalogued source sites, every native route, or Spanish replay parity |
| VGA derived groups | 222/223 complete normally and with Escape | The Room 34 crossing; original-game fidelity of estimated starts |
| Script coverage | English EGA: 1,427 formatted / 1,424 complete; Spanish EGA: 1,419 / 1,417 plus four invalid resources; VGA: 1,470 / 1,470 | Meanings of unnamed story roles; validity of EGA unresolved resources |
| Core and WPF | 833 tests passed, five explicit diagnostic skips, zero failures; 201 WPF assertions passed; clean build | Physical input, independent listening, or frame-for-frame ScummVM agreement |
| Isolated object verbs | 658 EGA + 662 VGA callbacks, anomalies extended to 6,000 frames | The prerequisites and player route a direct callback bypasses |

The audit corrected three issues that must stay corrected: native look/use answers retain their conditional outcome instead of being overridden by static doorway possibilities; widely shared VGA reactions are counted across draft verbs before grouping; a completing VGA setup needs balanced cutscene boundaries and settled motion/dialogue, not merely `InCutscene == false`. VGA Room 20's coordinate setup uses its object's own walk point and remains explicitly labelled as an estimate.

Annotation work is also established: all 122 VGA globals and 116 valid English / 113 valid Spanish EGA globals have individual titles backed by their own edition's instruction fingerprints. EGA globals added in the reverse review use [their own manifest and review](../../.verification/advanced-scripts/README-EGA.md); [VGA review](../../.verification/advanced-scripts/README.md) did not license copying numeric meanings into EGA. Reused actor slots stay scoped. Local script IDs stay scoped by room. Untitled/invalid entries are evidence gaps, not permission to invent names.

Counts describe different populations: 328 EGA recipes, 223 current VGA groups, and the historical 235 EGA source sites / 204 VGA groups are not interchangeable denominators. Earlier counts describe earlier catalogs and grouping rules.

## 1. Room 34 crossing: reachable scene or unused source branch?

**Current result.** VGA's `room 34 object 0 verb 00` is the one remaining group without a completing setup. The latest audit records `no-scene`, zero starts/ends, after 300 frames both normally and with Escape. The planner records five unsuccessful trials. English EGA's catalog still has a separate historical Room 34 local 200 `+0035` reachability question; completing its 328 recipes does not resolve that extra source site.

**Evidence from the VGA code and previous probes.** Room 34 local 200 samples Bobbin's walk box each frame. Its tests at `+0020` and `+0027` require current box 8 and previous box 6, with the camera below 464. Prior [step 19/20 research](../../.verification/loom-vga/README.md) found:

- Box 6 is a vertical line at x=246, y=64..82, initially invisible until the entry clears its flag. Box 7 lies between it and box 8. Box 7 spans x=246..298; box 8 spans x=298..333.
- The bounded walking probe visits boxes 5, 7, 8, 9. Placing Bobbin on box 6 snaps him to box 7 in that probe. This model changes walk box through neighbouring boxes, so its normal walk cannot yield the monitor's immediate 6-to-8 change.
- The previous search found one relevant script placement: global 97 (`0x61`), called by Room 46 object 610 look (`0x36`). It loads Room 34, puts Bobbin at (246,64), walks him to (246,80), sets box 6's flag to `0x80`, and puts him at (246,80) again. The recorded bounded run ends at (246,82), box 5, camera 216, without the monitor scene.

**Not established.** That the original ScummVM walk-box update behaves identically at line boundaries, that every possible placement/caller was covered, or that the branch is dead in both editions. A leftover from earlier box geometry is a hypothesis. The old EGA `+0035` label is not a verified cross-edition offset: English/Spanish resource lengths and object IDs differ, and the current VGA tests have their own offsets.

**Next investigation.** Produce an edition-separated packet for English EGA, Spanish EGA and VGA: resource hashes; entry and local 200 instructions; box geometry, flags and adjacency; all callers/placements that can enter the room; camera and previous/current box assignments. Compare the corresponding ScummVM implementation and the original game's actual crossing before blaming source geometry or the explorer. Trace global 97's original-game arrival, including when `putActor`, walking, flags and monitor sampling run. A small targeted per-frame trace should include coordinates, box IDs, camera, active monitor, input and cutscene boundaries.

**Completion criterion.** Either demonstrate a source-supported reachable route and retain it as a reproducible scene start, or document enough original-game/source evidence to classify the branch as unused/unreachable for a particular edition. If neither is proved, keep the unresolved picker note. Artificial box assignments or a forced cutscene would not answer this question.

**Possible help from the user, next session only.** A ScummVM save immediately before the relevant Room 34 arrival/crossing would allow an original-game trace. Edition/language, ScummVM version and the steps that lead to the save matter. First check the bundled ScummVM and existing local saves; do not ask for another game copy by default. If there is no save, explain the relevant game location and a short route before asking the user to reach it.

## 2. Incomplete EGA resources: boundaries, unused data or damaged data?

The audit preserved the exact failures below. Both language copies report identical Room 70 local lengths and final truncation offsets.

| Edition/resource | Observed failure |
| --- | --- |
| English global 0 | Code length 12,655; 77 bytes decoded; control-flow targets -31,832, 12,675 and 29,133 lie outside the resource |
| English and Spanish Room 70 local 203 | Code length 21; 18 bytes decoded; instruction or operands truncated at decimal 18 (`+0012`) |
| English and Spanish Room 70 local 204 | Code length 72; 69 bytes decoded; instruction or operands truncated at decimal 69 (`+0045`) |
| Spanish global 0 | Declares 33,139 bytes at file offset `0x9E07`, beyond `01.LFL` |
| Spanish global 51 | Declares 17,151 bytes at `0x4775`, beyond `11.LFL` |
| Spanish global 55 | Declares 25,292 bytes at `0x41FB`, beyond `18.LFL` |
| Spanish global 116 | Declares 65,280 bytes at `0x6F67`, beyond `02.LFL` |

These are parser observations, not a finding that the user's copies are corrupt. Competing explanations include unused/stale index entries, a resource-boundary or dialect error, non-executable stored data being interpreted as code, and edition-specific or damaged bytes. Identical Room 70 truncations in two copies are useful comparative evidence but do not choose among those explanations. Room 70 stored-text coverage remains incomplete; unreachable bytecode is not exhaustively scanned for strings.

**Next investigation.** Compare the original encoded and XOR-decoded bytes, index entries, room-local boundaries, headers and trailing bytes in the already available Roland/plain English and Spanish copies, then Spanish v1.2 where relevant. Record file hashes and byte ranges. Check the actual ScummVM resource loader and runtime references to each entry. Establish whether anything executes global 0 or the invalid Spanish globals; do not give global 0 a title based on the first few accidentally decoded bytes. For Room 70, determine whether the final bytes are genuine truncated instructions, a decoder extent error, or stored material that has a different purpose. Extract only independently established text and retain provenance.

Useful source entry points: [LflIndexReader](../../src/SCUMMexplorer.Core/Formats/LflIndexReader.cs), [LflResourceChain](../../src/SCUMMexplorer.Core/Formats/LflResourceChain.cs), [LoomEgaDataSource](../../src/SCUMMexplorer.Core/Formats/LoomEgaDataSource.cs), [LoomScriptReader](../../src/SCUMMexplorer.Core/Formats/LoomScriptReader.cs), and [the EGA reviewed glossary](../../src/SCUMMexplorer.Core/Formats/loom-ega-reviewed-glossary.json). Resource bytes must stay unchanged.

**Completion criterion.** A justified decode/boundary correction with a targeted regression, or a documented classification supported by byte comparison and loader/reachability evidence. If a source copy is genuinely incomplete, the limitation remains visible; deleting diagnostics or supplying invented instructions is not completion.

**Possible help from the user, next session only.** None is needed for the initial comparison: the local repository already contains several releases. Only if those comparisons cannot settle a suspected copy-specific defect should the next session request an independently known-good copy of the *same* edition, or ask how that edition behaves in ScummVM. Clarify the exact files/edition and reason before requesting anything.

## 3. Residual isolated callbacks: prerequisites versus executor defects

The longer probes start independent seed-0 sessions, select a room and invoke a verb directly. They do not reproduce walking, casting a draft, prior story state or every native-input guard. Classification must precede any fix.

| Edition and target | Latest raw outcome | Known context / next discriminator |
| --- | --- | --- |
| English EGA Room 20, objects 390/391, verb `48` | Unbalanced 1 start / 0 ends after 136 frames | Local 206 reads actor 1's unknown x at `+002D`; the catalog arrival is not established. Compare own object walk points and native-input placement. |
| VGA Room 20, objects 315/316, verb `48` | Unbalanced 1/0 after 143 frames | Same coordinate issue in raw starts. The picker representative is already fixed with the object's (153,102) walk-point estimate and a balanced trial. Keep raw and prepared outcomes separate. |
| English EGA Room 38, object 611, look `36` | Timeout at 6,000 frames, 1/0 | Script 87 runs; the recorded path loads Rooms 34 (`+00F8`), 68 (`+0192`), 38 (`+02C7`), then 37 (`+0400`). Capture its exact remaining wait and pending scripts; compare the proper preceding route and state. The first missing prerequisite or model operation is not yet identified. |
| VGA Room 41, object 559, verbs `41`/`4B` | Timeout at 6,000 frames, 2/1 | Already explained and handled in picker setup: foreman object 557 look `36` runs local 207, which sets bit `077`; local 204 clears it at `+004E`. The fresh hammer loop local 206 repeatedly cues the CD timer. Compare the native foreman-to-sword route before claiming full route fidelity. |
| VGA Room 64, object 757, look/use `36`/`38` | Unbalanced after 124 frames, 1/0; **zero target starts** | Callback overlaps an open entry scene. Establish when input becomes legal and whether the object adds a scene after entry settles. |
| VGA Room 93, object 899, look/use `36`/`38` | Unbalanced after 98 frames, 1/0; **zero target starts** | Same overlapping-entry issue; inspect owner, entry continuation and native-input gating. |
| VGA Room 93, object 900, verbs `36`/`39` | Entry-only after 317/313 frames, 1/1; **zero target starts** | Only the room entry completed. Do not count it as an independent object scene. Establish the verb's intended non-scene response or prerequisite. |

The earlier timeouts that completed at the longer bound are not remaining failures: EGA Room 08/255/38 (2,845 frames), 09/270/3A (1,995), 41/633/36 (1,425), 41/634/41 and 4B (1,755 each), 47/698/36 (1,821); VGA 08/180/38 (1,440), 09/195/3A (1,364), 44/579/36 (1,300), 47/623/36 (1,475). Raising the limit to make a probe finish is diagnostic evidence, not proof of correct timing.

**Next investigation.** Start with EGA Room 38 because its cause remains unidentified. Add targeted traces of pending script/offset, local values, messages, actor and camera motion, sound timer, scope owner and native input eligibility. Compare raw activation, settled room entry, prepared picker start and the actual native route as separate cases. Then classify Room 20 and the VGA entry overlaps. Cross-port a generic correction only where the other edition's own instruction evidence supports it. Preserve the established fresh-room, running-session and labelled-prerequisite rules in [CURRENT_WORK](../CURRENT_WORK.md).

**Completion criterion.** Each row becomes one of: valid prepared preview with disclosed prerequisites; ordinary non-scene response; callback impossible under native input; demonstrated executor defect with correction/regression; or explicitly unresolved. All advertised successful previews still require matching starts/ends and settled output. Entry completion and killed owners must not masquerade as target completion.

**Possible help from the user, next session only.** A ScummVM save or short reproduction for the Rusty/firewood transition, foreman/sword interaction, or grave/close-up room is useful only after the next session identifies the exact missing state. Do the available code and trace work first; avoid a broad request to replay the whole game.

## 4. Original-game visual, input and audio comparison

Automated coverage verifies bounded execution and presentation paths; it does not settle the remaining perceptual checks. The existing plan records these user-viewing items:

- Room 43 staff scenes; El Muerto and Room 17 actor sizes; actor 4 placement. These are prior open checks, not newly reproduced regressions.
- Draft responses: the original text, animation and actual casting path, distinct from selecting a direct draft verb in the picker. Draft words remain seeded estimates until learnt through a route.
- VGA staff note-head alignment: step 26 aligns the artwork's staff lines to eight-row strips, but the record does not establish original-game agreement. EGA must retain its own layout.
- Representative timing/audio: The Intro and skill screen, the Elders scene, foreman/sword, and the feather/sheep timer-dependent scenes. The timer-dependent stalls were fixed earlier; the remaining issue is independent agreement of picture, voice and music timing. The user already reported CD music in Advanced working; that feedback is retained, not reset to an untested feature.
- EGA Roland A/B with the same ROMs, if a specific discrepancy remains. MT-32 is implemented and the user confirmed the device and overture behavior. Possible IMuse volume/part-allocation differences remain a comparison question, not evidence that playback is broken.

**Next investigation.** Build a short comparison sheet from the highest-value unresolved cases, using the same edition, state, sound device and route in ScummVM and the explorer. Record the trigger, expected original behavior, observed explorer behavior and uncertainty. Use the bundled ScummVM where possible. Save exact source/build identity with any capture. Fix only demonstrated discrepancies; do not silently change source-selected palettes or original glitches.

**Completion criterion.** Representative checks have an explicit original-game comparison, or remain stated limitations with a precise reason. The final result should distinguish a source-proven implementation from a labelled preview rather than claim that every scene was visually and aurally verified.

**Possible help from the user, next session only.** A small viewing/listening batch with exact app actions and comparison expectations. Ask for the first useful one or two checks, not the whole list at once. A save, brief clip or description can be chosen according to the evidence required; nothing is requested in this preparation session.

## 5. Smaller evidence gaps and historical items to triage

These remain in the plan/script map, but must be checked against current code before turning them into implementation tasks. The old VGA step-24 list is historical: later commits delivered Scripts, Readable, Used by/Explore links, status and shared presentation. It is not a current missing-feature checklist.

| Item | Established limit and next-session treatment |
| --- | --- |
| Silence (`4E`/`4F`), Shaping (`50`/`51`), Sharpening direction (`4A`/`4B`) | The [script map](LOOM_SCRIPT_MAP.md) records manual-order or text-based readings and unresolved direction. Compare each edition's own text/actions and authentic supporting material before strengthening labels. |
| Numeric story bits and actor roles | Unknown meanings remain numeric; actor identities stay scene-scoped. Extend the glossary only where own code/text establishes a visible improvement, with fingerprint guards. |
| Used by / Explore links | Literal references are candidates. Dynamic script/current-room targets remain unresolved; a title does not make a static link proof of execution. |
| 17 EGA costume gallery entries without data | Previously recorded as unused by scripts. Recheck references if relevant; do not synthesize missing artwork or call empty index entries decoding failures without evidence. |
| `stopObjectScript`, inventory object code, room-owned resource/script cleanup and unsupported expression forms | Historical engine-gap list; inspect current implementation and a route that needs the operation before making a generic correction. Passing catalogs do not establish every opcode path. |
| Room 69 difficulty and draft answer | Intro automation and assumptions are documented; actual draft-answer gameplay is not fully simulated. Keep the skill-click behavior and uncertainty explicit. |
| Ending without `cutscene` (`Room 66`, global 106) | Existing Interact action, not necessarily a missing picker scene. A future coverage inventory should include non-cutscene narrative sequences before claiming all story scenes are indexed. |
| Room 03 town/cemetery naming | Earlier unidentified labels; derive from the edition's own data, never a room-number exception. |

Demos' unused carrier rooms, broad performance work, historical worktree cleanup, M5/secrets, Indy3 and later games remain parked. They are not prerequisites to closing the specific correctness gaps above. The user previously kept the replay catalog on English Roland EGA; no full Spanish/other-release replay migration is authorized by this handoff.

## Recommended first-session order and information needed

1. Explain the baseline and gap inventory to the user, separating facts from hypotheses. Check local ScummVM/save availability and the local comparison releases.
2. Compare EGA resource bytes/boundaries and assemble the cross-edition Room 34 source packet. These can progress without user input.
3. Trace EGA Room 38's exact wait and classify the remaining raw callbacks using native-input eligibility and source prerequisites.
4. Request only the original-game save or viewing/listening check that the independent work shows is necessary. The strongest likely request is a Room 34 pre-transition save; a second request should be selected from actual findings rather than guessed now.
5. Make source-supported fixes individually, run their focused regressions and one relevant WPF/original-game check, then broaden validation only when the changes justify it. Update the audit/dossier with the classification and evidence for every resolved or still-open row.

Nothing needs to be supplied in this preparation session. Existing game files cover the first comparisons. A source branch may legitimately remain unused, and an estimate may legitimately remain labelled; neither should be hidden to obtain a 100% count.

## Local evidence, commands and working rules

Repository / command cwd: `D:\Documents\Personal\Code Projects\SCUMMexplorer`. Required RTK executable: `C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe`. All shell commands run through RTK, including nested PowerShell. If sandbox policy blocks RTK execution, use the normal escalation mechanism; do not bypass the wrapper.

Current game folders (verified during preparation):

- `Games/loom ega mt32 en`: reference English EGA.
- `Games/loom vga en`: VGA disc image.
- `Games/ALTERNATIVE LOOMS/loom ega mt32 es`: Spanish EGA comparison.
- `Games/ALTERNATIVE LOOMS/loom ega en`, `loom ega es`, `loom ega fr`, `loom ega de`, `loom ega v1.2 es`: available comparison copies.
- `Games/ALTERNATIVE LOOMS/loom vga en kixx`: alternative CD image; establish its identity before treating it as the same disc.
- `Games/mt32`: user's ROMs; `scummvm/`: bundled executable, not a source checkout. Game data and ROMs never enter Git.

The old game-folder and VGA-start paragraphs in CLAUDE.md are historical; current paths/state above and in the audit are verified. No CLAUDE.md edit is part of this preparation.

Local evidence is Git-ignored under `.verification/correctness-20261008/`; it exists in this checkout but will not appear in a clean clone:

| Evidence | Use |
| --- | --- |
| `out/ega/scripts.json`, `out/ega-es/scripts.json` | Exact incomplete blocks and resource errors |
| `out/ega/catalog.json`, `out/ega/scenes.json` | English source fingerprint and 328 normal/Escape recipes |
| `out/vga-fixed/setups.json`, `out/vga-fixed/scenes.json` | Current 223 groups, Room 34 note, successful prepared starts |
| `out/ega/interactions.json`, `out/vga-final/interactions.json` | Full raw callback sweep, corrected target/entry scope accounting |
| `out/ega-anomalies/anomalies.json`, `out/vga-anomalies/anomalies.json` | Longer callbacks, boundaries and recorded uncertainties |
| `out/dump-vga/room20.txt`, `out/dump-vga/room93.txt` | Existing targeted code dumps |
| `results/correctness-fixed.trx` and WPF report folders listed in the audit | Passing suite and real-window assertions/captures |

Focused source dumps, after rebuilding the existing audit harness:

```powershell
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet build .verification/correctness-20261008/Audit/Audit.csproj --verbosity quiet
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet run --project .verification/correctness-20261008/Audit/Audit.csproj --no-build -- 'Games/loom ega mt32 en' '.verification/correctness-20261008/out/next-ega-dump' - dump 34 38 70
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet run --project .verification/correctness-20261008/Audit/Audit.csproj --no-build -- 'Games/loom vga en' '.verification/correctness-20261008/out/next-vga-dump' - dump 34 41 64 93
```

The dump mode includes locals/object verbs, not room entry/exit or global scripts. [CutsceneScanCompare](../../.verification/cutscene-scan-ega/Program.cs) has edition-aware `boxes`, `script`, `grep` and `verbs` modes; its argument pattern is `<game-folder> - <mode> <ids-or-pattern>`. `grep` covers globals/entry/locals but not every object verb, so it alone is not an exhaustive caller search. The [Audit source](../../.verification/correctness-20261008/Audit/Program.cs) supports room-filtered `scenes` and `interactions`, full `scripts`, and `anomalies` supplied with a previous interactions JSON. Use fresh output folders and keep raw, native and prepared starts distinct.

Relevant regressions: `LoomCdCutsceneTests.EveryCdSceneEitherPlaysFromADerivedSetupOrSaysWhyNot`, `TheSwordSceneStallsFromAFreshStartAndEndsAfterTheForemanScene`, `ASceneReadingUnknownEgoCoordinatesUsesItsObjectsOwnWalkPointAndClosesItsScope`, and `LoomCutsceneGroupingTests`. The [UiAudit](../../.verification/correctness-20261008/UiAudit/Program.cs) covers native look/use outcomes and playback guards. Rebuild old harnesses so they receive the current failure policy; inspect actual assertions, not just process exit codes.

Full reproduction remains [run.ps1](../../.verification/correctness-20261008/run.ps1); the EGA sweep takes roughly 20 minutes, so it is not the first next-session action. Build command: `rtk proxy dotnet build SCUMMexplorer.slnx --no-restore --verbosity quiet`. Application artifact: `D:\Documents\Personal\Code Projects\SCUMMexplorer\Exe\SCUMMexplorer.App\Debug\net10.0-windows\SCUMMexplorer.App.exe`.

Preserve the user's saved state, source artwork, edition differences and normal/Advanced separation. No forced story bits, hidden prerequisites or room-number exceptions. Stage explicit task files, commit finished work separately, do not stash unrelated work or push. Read [CURRENT_WORK](../CURRENT_WORK.md) for the full standing rules. M5 and other games stay parked.
