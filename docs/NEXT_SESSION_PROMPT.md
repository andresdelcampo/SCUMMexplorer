# Next session

State and evidence for the next session, updated 8 October 2026 after the Room 34 state investigation and the user's supplied native save. Room 34 remains unresolved. Its state/camera source comparison is completed; native state readings are pending on console submission. The actual task prompt is supplied to the next chat, not stored in this file.

## Current entry points

- [Room 34 state comparison](../.verification/room34-state-20261008/README.md): three-edition guards, flag/box/camera writers, source scheduling semantics, 24 passing prepared-state assertions, limitations and reproduction. Probe outputs are ignored under `out*/`.
- [Supplied native session](../.verification/room34-state-20261008/NATIVE_SESSION.md): English Roland EGA, ScummVM 2.2.0, matching Room 34 resource; untouched existing slot 13 `Rusty`, exact save hash. One ordinary ground walk responded. Console is open with `help` entered but unsubmitted: automated letters work, automated Enter does not. User corrected the shortcut to Ctrl+Alt+D. A user console assist is pending (`scripts`, `camera`, `actors`, `box 6 7 8`, and read-only `scumm_vars[214/215/220]`, each separately). No native flags/script slots/box values have been obtained. Preserve the game and save, and do not repeat the save request or infer its phase from the Explorer trace.

- [Native walking comparison](../.verification/native-walk-20261008/README.md): corrected VGA/v4 walker, independent C++ kinematic reference, actual return and live monitor samples, validation and evidence limits. Its checked-in Export/Reference harnesses reproduce ignored `out-v4-final/`, `out-arrival2/`, `out-monitor/`, `out-scenes/`, `out-tests/` and `out-ui/`.
- [User's Room 34 observation](../.verification/final-gaps-20261008/USER_OBSERVATIONS.md): EGA debugger room change, Rusty conversation before rightward camera access, no observed cliff scene. Unknown language/version/prior state; explicitly not native-route or unreachability proof. Preserved by separate local commit `1f6d939`.
- [Final-gap investigation](../.verification/final-gaps-20261008/README.md): Room 70 operand correction, guarded interior-global annotations, successful Firewood/straw prerequisite route, Room 20 placement and VGA callback classifications, and the distinct native VGA actor walker. Generated packet/traces remain ignored locally. Its optional existing-save request is now fulfilled; no replay/new save is needed.

- [FINAL_GAPS_HANDOFF.md](loom/FINAL_GAPS_HANDOFF.md): detailed gap dossier, facts versus hypotheses, investigation order, completion criteria and conditional user needs.
- [8 October correctness audit](../.verification/correctness-20261008/README.md): exact results, product fixes, reproduction and ignored local evidence.
- [CURRENT_WORK.md](CURRENT_WORK.md): standing product/workflow rules.
- [LOOM_EGA_PLAN.md](LOOM_EGA_PLAN.md): open items and parked work.
- [CHANGELOG.md](CHANGELOG.md): completed milestones and validation.

The superseded handoff is preserved as [HANDOFF_2026-10-08_BEFORE_FINAL_GAPS.md](archive/HANDOFF_2026-10-08_BEFORE_FINAL_GAPS.md). Older handoffs and research remain records; statements such as "VGA step 3", "no full suite" and the old step-24 missing-feature list do not describe the current implementation.

## Verified state

Room 34 source comparison: after its initial entry gate, local 200 starts only when late/completion/input are all zero: English EGA D7A/DCF/D6D, Spanish D7B/DD0/D6E, VGA 01B/070/00E. That early branch hides box 6. Script 85 +000A stops 200 before +000C marks completion. Late Room 46 entry sets the late flag; global 97 return hides 6 and locks the EGA camera to 160 while the ghost flag is clear. Global 98's ghost conversation opens the camera and boxes 10/11, reloads 34 enabling 6, but never restarts 200 or clears completion. Bones/local 209 restoration enables 6 after the monitor is absent. Literal-reference scans do not exhaust aliases or dynamic callers; no dead-code proof.

Focused validation: 24 diagnostic assertions (14 English EGA, five Spanish EGA, five VGA); zero-warning/error solution build. Prepared English return/ghost/restoration and eastward walk settle with balanced boundaries where expected. Spanish/VGA late camera behavior is source-compared, not dynamically rerun. No production code changes or broad suite rerun. Native save/header inspection establishes identity only; saved variables were not decoded.

Local master commits, not pushed: 38b5ce3 (VGA review), 88feef8 (EGA annotations), 62e1f43 (correctness audit), d873d31 (Room 70 operands), 2d1598e (interior-entry titles), 1f6d939 (user's caveated EGA observation), a16deea (native v4 walking). A documentation handoff commit follows; identify it from local history rather than relying on a circular in-commit hash.

New validation: 11 bounded kinematic walks match unchanged native C++ methods frame for frame; 51 focused Core tests, 30 WPF assertions, clean final solution build. The VGA scene rerun completes 222/223 normally and with Escape; Room 34 remains the only `no-scene` group. No full EGA replay or full Core rerun. The native reference excludes animation/facing/rendering/audio, and shares Core's resource inputs/adjusted placements; it is not a full original-game run. Separate source-based turning regressions pass.

The actual fresh Room 46/object 610 look returns through global 97: frame 1 `(246,65)`, frame 9 `(246,80)` / box 6; hide box 6 and re-place at `+003C` gives `(246,82)` / box 5. UserPut becomes 1 at frame 11. Entry `+0375` reads `01B = 1` and skips local 200. Fresh Room 34 instead reads `01B/070/00E = 0`, starts the monitor and hides box 6. Public walking to object's `(246,64)` walk point adjusts to box 7; the second walk toward box 8 yields current 8 / previous 7 at frame 246, camera 272. No crossing scene starts. Native geometry with box 6 enabled also samples 7 before 8, but those isolated geometry defaults are not claimed as one reachable story state. The picker retains its unresolved note, with the blanket one-neighbour-per-frame claim removed.

The earlier full audit completes all 328 English EGA recipes and 222/223 current VGA groups normally and with Escape: 833 Core passes, five explicit diagnostic skips, zero failures, 201 WPF assertions. It was not rerun during this focused investigation. Refreshed script coverage is English EGA 1,427 formatted / 1,426 complete; Spanish 1,419 / 1,419 with four interior-entry read errors; VGA 1,470 / 1,470. Focused validation: 67 operand-fix Core checks, 25 boundary-review Core checks, 13 WPF assertions; clean solution build. English replay source hashes matched in the full audit; English recipes were not applied to Spanish data.

Native look/use answers now retain their conditional outcome (the Room 04 grave stays there without Reading). Doorway fallback shares the executor's hit selection. VGA shared-helper grouping counts across draft verbs. Completing setup trials require matching cutscene boundaries and settled output. VGA Room 20 has an explicitly labelled placement estimate derived from the triggering object's own walk point; no story bit or cutscene ending is forced.

All 122 VGA globals and 115 standalone English Roland / 113 Spanish EGA globals have reviewed titles with their own instruction evidence. The EGA reader now also rejects reviewed labels for entries inside other resources: English 0/116, Spanish 0/51/55/116. English 116's complete decode did not establish a standalone script. Historical review rows remain intact, and Advanced retains raw diagnostics plus a containing-resource note. Script 0 is refused by ScummVM; absence of literal callers does not exhaust dynamic targets. CD Used by / Explore links and room-local namespaces work. Actor slots 2–6 stay scoped. Detailed records: [VGA](../.verification/advanced-scripts/README.md), [EGA review](../.verification/advanced-scripts/README-EGA.md), [shared presentation/status/sounds](../.verification/loom-parity/README.md).

The 7 October feedback is implemented: synchronized staff/notes and three-note browsing floor; Room 69 suppression; shared Advanced sound rows; Room 68 shadow palette; per-room scene membership/titles; The Intro; scoped script links; Apply and close / Cancel. Remaining original-game viewing/listening checks are evidence limits, not a reset of completed features.

## Remaining work, in priority order

1. **Room 34 native state/reachability.** The source writers/guards/camera comparison is complete in the new packet. Read the supplied native session's console results when available; the small console-submission assist is already pending, so do not duplicate it. EGA backing word 214 contains D6D at bit 13; word 215 contains D71 at bit 1 and D7A at bit 10; word 220 contains DC6/DC7/DCF at bits 6/7/15. Query without a second argument. Distinguish active local 200 from stale script slot numbers. Record camera, actor/box and flags before a short ordinary route or existing-slot reload. No forced placement/bits/boxes. All editions retain +0020 current 8, +0027 previous 6, +002E camera <464, +0035 cutscene. No eligible 6-to-8 sample has been demonstrated; retain the unresolved note and do not declare unused code. The save request is fulfilled; no new save or broad replay.
2. **Original-game comparison.** Targeted source/state-matched checks for actor/staff sizing, actual draft casting, note-head alignment and representative voice/music timing. Existing user CD music/MT-32 confirmations remain valid. Select one or two exact checks after a demonstrated discrepancy; do not ask for a whole-game replay.
3. **Residual evidence limits.** EGA Room 38 straw look waits for actor 5's box 2 while fresh entry hides it. Existing FirewoodSteps sets D99 naturally; retry completes Script 87 1/1. All four EGA/VGA Room 20 callbacks complete 4/4 after entry settlement and the object's (153,102) walk-point placement, which remains an estimate. VGA Room 64/93 raw calls occur with UserPut=0; settled calls are navigation or ordinary non-scene answers. These are classified diagnostics, not still-unexplained executor defects. Native route/timing comparison remains open.
4. **Smaller evidence questions.** Draft-name directions, unnamed story roles, literal-only links, unused costume entries, non-cutscene narrative coverage and historical engine-gap claims need triage against current code. Room 70 decode is fixed; exhaustive unreachable-text extraction is not claimed. Broad polish is not required to fix a concrete correctness defect.

## Local environment

Repository: D:\Documents\Personal\Code Projects\SCUMMexplorer. Shell commands must be wrapped by C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe as instructed in C:\Users\adelc\.codex\RTK.md.

Verified folders: Games/loom ega mt32 en (reference), Games/loom vga en (CD), Games/ALTERNATIVE LOOMS/loom ega mt32 es (Spanish comparison), with plain/language/v1.2 and Kixx copies under Games/ALTERNATIVE LOOMS/. Games/mt32 holds the user's ROMs. scummvm/ is the bundled executable, not source. CLAUDE.md's older folder/VGA-start paragraphs are stale; the audit/dossier records current paths. Game data and ROMs stay out of Git.

Solution: SCUMMexplorer.slnx. Build from the repository with rtk proxy dotnet build SCUMMexplorer.slnx --no-restore --verbosity quiet. App: Exe/SCUMMexplorer.App/Debug/net10.0-windows/SCUMMexplorer.App.exe.

The existing .verification/correctness-20261008/out/ and results/ evidence is Git-ignored, available in this checkout. New .verification/final-gaps-20261008/out-boundaries/, rusty-prepared/, entries/, room20/, coverage-*/ and ui-out/ hold focused evidence. Matching ScummVM 2.2.0 source downloads are in that folder's ignored scummvm-v2.2.0/. The checked-in Probe/ and Ui/ harnesses plus README reproduce them. The full audit driver takes about 20 minutes for EGA; do not rerun it without a change that warrants it.

## Standing decisions

Scene starts preserve the running session for individual selections; explicit sequences start fresh. Rooms begin in their earliest state, later state follows the user's triggered scene, and setup assumptions stay visible. Outside-island staff prerequisites use the game's pickup; costume changes use the room/scene's own assignments. No forced story bits, room-specific exceptions or one-off teleports to manufacture reachability. Original artwork and edition-specific differences remain intact.

Normal mode prioritizes rooms, actors, original text and simple controls; source details and estimates belong in Advanced. Single click walks/uses (38); double click also looks (36); inspection-image cycling is explicit. Static possibilities do not override executed native answers.

M5/secrets, Indy3/other games, broad performance work, historical worktree cleanup and demo carrier-room archaeology remain parked. The replay catalog stays on English Roland EGA; no other-release catalog migration is part of this handoff.

Focused verification follows demonstrated changes; existing full audit remains the baseline. Preserve %LOCALAPPDATA%\SCUMMexplorer saved state. Stage explicit files, commit finished tasks separately, do not stash unrelated work or push. Ask before editing CLAUDE.md. The session-ending workflow records state here and starts the requested next chat with a self-contained task prompt.
