# Next session

State and evidence for the next session, prepared 8 October 2026. The user's next task is the remaining LOOM EGA/VGA correctness gaps, beginning with an explanation of everything established about them and the specific help needed from the user. Questions belong to that next session; none are requested during handoff preparation. The actual task prompt is supplied to the next chat, not stored in this file.

## Current entry points

- [Final-gap investigation](../.verification/final-gaps-20261008/README.md): Room 70 operand correction, complete EGA helper decodes, interior global entries, successful Firewood/straw prerequisite route, and the distinct native VGA actor walker. Generated packet/traces remain ignored locally. An optional request for an existing ScummVM save near Room 46's return to Rusty's outdoor area is pending; no replay/new save was requested. Initial handoff counts below are historical.

- [FINAL_GAPS_HANDOFF.md](loom/FINAL_GAPS_HANDOFF.md): detailed gap dossier, facts versus hypotheses, investigation order, completion criteria and conditional user needs.
- [8 October correctness audit](../.verification/correctness-20261008/README.md): exact results, product fixes, reproduction and ignored local evidence.
- [CURRENT_WORK.md](CURRENT_WORK.md): standing product/workflow rules.
- [LOOM_EGA_PLAN.md](LOOM_EGA_PLAN.md): open items and parked work.
- [CHANGELOG.md](CHANGELOG.md): completed milestones and validation.

The superseded handoff is preserved as [HANDOFF_2026-10-08_BEFORE_FINAL_GAPS.md](archive/HANDOFF_2026-10-08_BEFORE_FINAL_GAPS.md). Older handoffs and research remain records; statements such as "VGA step 3", "no full suite" and the old step-24 missing-feature list do not describe the current implementation.

## Verified state

Local master commits, not pushed: 38b5ce3 (individual VGA Advanced Scripts review), 88feef8 (VGA findings ported back into EGA), 62e1f43 (correctness audit and fixes). This preparation changes documentation only.

All 328 English EGA recipes and 222/223 current VGA groups complete normally and with Escape. The full Core suite has 833 passes, five explicit diagnostic skips and zero failures; 201 WPF assertions pass; the solution builds with zero warnings/errors. All script kinds are audited: English EGA 1,427 formatted / 1,424 complete; Spanish 1,419 / 1,417 with four invalid global resources; VGA 1,470 / 1,470. English replay source hashes match; the English recipes were not applied to Spanish data.

Native look/use answers now retain their conditional outcome (the Room 04 grave stays there without Reading). Doorway fallback shares the executor's hit selection. VGA shared-helper grouping counts across draft verbs. Completing setup trials require matching cutscene boundaries and settled output. VGA Room 20 has an explicitly labelled placement estimate derived from the triggering object's own walk point; no story bit or cutscene ending is forced.

All 122 VGA globals and 116 valid English / 113 valid Spanish EGA globals have individually reviewed titles with their own instruction evidence. CD Used by / Explore links and room-local namespaces work. Every supplied VGA block uses the shared Readable presenter. EGA global 0 and Spanish 51/55/116 remain unresolved; actor slots 2–6 are scoped, not globally named. EGA sound/save helpers and English/Spanish Script 7 thresholds stay distinct. Detailed records: [VGA](../.verification/advanced-scripts/README.md), [EGA reverse review](../.verification/advanced-scripts/README-EGA.md), [shared presentation/status/sounds](../.verification/loom-parity/README.md).

The 7 October feedback is implemented: synchronized staff/notes and three-note browsing floor; Room 69 suppression; shared Advanced sound rows; Room 68 shadow palette; per-room scene membership/titles; The Intro; scoped script links; Apply and close / Cancel. Remaining original-game viewing/listening checks are evidence limits, not a reset of completed features.

## Remaining work, in priority order

1. **Room 34 reachability.** VGA local 200 requires an immediate box 6-to-8 change while camera <464, but box 7 intervenes. Earlier global 97 placement probes did not trigger it. The analogous EGA +0035 source-site question remains open; each edition needs its own current code/geometry packet. Unused branch is a hypothesis, not a proved conclusion.
2. **EGA incomplete/invalid resources.** English global 0 has out-of-resource targets; both languages' Room 70 locals 203/204 end in truncated instructions. Spanish global 0/51/55/116 declares lengths beyond file bounds. Existing alternative copies allow initial byte/index/loader comparisons without asking the user for files.
3. **Residual raw callbacks.** EGA Room 38 object 611 look still times out in isolated state; capture Script 87's exact wait. Raw Room 20 callbacks lack ego coordinates; the VGA picker representative is fixed. VGA foreman/sword prerequisites are already supplied by the picker. VGA Room 64/93 callbacks overlap entry scenes, and Room 93 object 900 produces entry-only outcomes. These need classification against native input and prior state, not automatic product-bug labels.
4. **Original-game comparison.** Targeted source/state-matched ScummVM checks for Room 34, identified callback prerequisites, actor/staff sizing, draft effects, note-head alignment and representative voice/music timing. Existing user confirmations remain valid. Likely user needs are a particular pre-transition save and a small viewing/listening batch; the next session selects precise requests after independent investigation.
5. **Smaller evidence questions.** Draft-name directions, unnamed story roles, literal-only links, unused costume entries, non-cutscene narrative coverage and historical engine-gap claims need triage against current code. Details and limits are in the dossier. Broad polish is not required to fix a concrete correctness defect.

## Local environment

Repository: D:\Documents\Personal\Code Projects\SCUMMexplorer. Shell commands must be wrapped by C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe as instructed in C:\Users\adelc\.codex\RTK.md.

Verified folders: Games/loom ega mt32 en (reference), Games/loom vga en (CD), Games/ALTERNATIVE LOOMS/loom ega mt32 es (Spanish comparison), with plain/language/v1.2 and Kixx copies under Games/ALTERNATIVE LOOMS/. Games/mt32 holds the user's ROMs. scummvm/ is the bundled executable, not source. CLAUDE.md's older folder/VGA-start paragraphs are stale; the audit/dossier records current paths. Game data and ROMs stay out of Git.

Solution: SCUMMexplorer.slnx. Build from the repository with rtk proxy dotnet build SCUMMexplorer.slnx --no-restore --verbosity quiet. App: Exe/SCUMMexplorer.App/Debug/net10.0-windows/SCUMMexplorer.App.exe.

The existing .verification/correctness-20261008/out/ and results/ evidence is Git-ignored, available in this checkout. The dossier maps its files to each gap and records focused dump/harness commands. The full audit driver takes about 20 minutes for EGA; it is not a necessary first next-session action.

## Standing decisions

Scene starts preserve the running session for individual selections; explicit sequences start fresh. Rooms begin in their earliest state, later state follows the user's triggered scene, and setup assumptions stay visible. Outside-island staff prerequisites use the game's pickup; costume changes use the room/scene's own assignments. No forced story bits, room-specific exceptions or one-off teleports to manufacture reachability. Original artwork and edition-specific differences remain intact.

Normal mode prioritizes rooms, actors, original text and simple controls; source details and estimates belong in Advanced. Single click walks/uses (38); double click also looks (36); inspection-image cycling is explicit. Static possibilities do not override executed native answers.

M5/secrets, Indy3/other games, broad performance work, historical worktree cleanup and demo carrier-room archaeology remain parked. The replay catalog stays on English Roland EGA; no other-release catalog migration is part of this handoff.

Focused verification follows demonstrated changes; existing full audit remains the baseline. Preserve %LOCALAPPDATA%\SCUMMexplorer saved state. Stage explicit files, commit finished tasks separately, do not stash unrelated work or push. Ask before editing CLAUDE.md. The session-ending workflow records state here and starts the requested next chat with a self-contained task prompt.
