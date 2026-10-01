# Requested UI refinements, 1 October 2026

## Control placement

Game Status is in the top right immediately before global Play Cutscene. Status remains available in normal/Advanced LOOM; the global picker is Advanced-only. Room Play Cutscene follows Skip line in the playback bar, retaining generated zero/one/multiple memberships and existing fresh-session, busy/pause/departure behavior. Bars wrap within the available width; the room button reserves a stable minimum width for its picker marker.

Fresh normal-clock WPF `RefinementCheck`: **11/11** in ignored `Exe/UiRefinementSession-20261001/placement-1/refinement-check.txt`. Normal, Advanced and minimum900x600 screenshots inspected. Build zero warnings/errors. Synthetic handlers/layout, not physical input or independent listening. No Core/session/catalog changes or user processes stopped.

## Shared original scene names

One `LoomReplayTitle.Format` supplies the existing `Room NN · descriptor` convention to original and generated starts. Original labels are Room01 Opening, Room08 The Elders' trial, Room10 Walk to Room13 and Room36 The firewood scene. Global/room pickers, legacy menu and Game Texts use these shared titles. Generated titles, IDs, start actions and all334 memberships are unchanged; no catalog regeneration.

Fresh `naming-1/refinement-check.txt`: **15/15**, including all334 formatted titles and original Game Texts parity. The actual stopped boot is in69 and frame advancement first reaches01: the opening's scene label is01 while its validated setup/membership retains69. This check distinguishes resource preloads in044 from actual scene-room changes. Build zero warnings/errors.

## Ready-to-edit Game Status

Game Status opens ready to edit when idle. Apply Changes writes explicit pending choices and stays open; Close (including the window close action) discards unapplied choices. Clean fields follow live state without replacing grid rows. Pending fields retain their baseline; game changes disable/refuse stale Apply and require reopening. Session replacement discards pending choices. Busy/paused/draft/movement guards remain enforced by both Core and App.

Normal shows state only. Advanced shows source/dependency evidence and the State source column, including explicit-edit provenance and unresolved/estimated state. Long provenance has an ellipsis plus its full tooltip. The source expander and footer share a row to preserve draft browsing at minimum820x520, including expanded sources.

Source IDs1..8 display C,D,E,F,G,A,B,C-prime, including the real four-note combo editors. Numeric storage/input/sound is unchanged. Native staff segments are decoded from Room01 using018's image IDs/positions. Musical notes are rendered from charset0 using018's actual glyph pairs, coordinates and colors: the first seven use underscore/backtick; upper C uses less-than/equals. Empty choices outline the decoded pixels; full choices retain native colors. Staff toggles independently of notes. Nine focusable, accessible note buttons select zero or the contiguous range through the chosen note; pending graphics update immediately.

Learned is an editable checkbox; Can reverse is a separate read-only indicator. The loaded schema additionally verifies023's D69/D6A writes,026 +0x0000 refusal and022 +0x00CC reverse gate. `UpdateDraftLearned` changes only0x2000, preserving low12 notes,0x1000 reverse,0x4000 knowledge,0x8000 and response words. Explicit edit masks accumulate so a combined sequence/Learned edit retains both choices' provenance. No status edit is used by replay setup.

### Verified display names

Small LOOM-only label map, guarded by both slot and response pair. No note patterns, room links or prerequisite values are hardcoded. Thirteen other slots currently retain neutral Draft N labels.

| Slot / response pair | Label | Native association |
| --- | --- | --- |
|0 /3A,3B|Opening / Closing|Room01 local211 +0x030E initializes response3B3A. Room09 egg270 look starts200, which loads023 slot0 +0x0008 and learns025 slot0 +0x003E. Room02 sky156/3B +0x000A/+0x002B explicitly describes closing/opening.|
|2 /3E,3F|Dyeing / Bleaching|Room01 local211 +0x0300/+0x0307 initializes slot2/3F3E. Room06 local201 +0x0016/+0x0045 loads/learns slot2; local200 +0x00E0 describes the color change. Room06/2063E/3F choose opposite color states; Room24/4333F +0x000F describes the white limit.|

Opening and Dyeing terminology checked against the original Lucasfilm [manual](https://mocagh.org/lucasfilm/loom-manual.pdf) (printed p6, egg/pot) and [Book of Patterns](https://mocagh.org/lucasfilm/loom-book.pdf) (Opening/Dyeing entries). Source instructions remain the authority for slot/action association; the manuals supply display terminology only.

### Fresh verification

Final ignored `Exe/UiRefinementSession-20261001/status-11/refinement-check.txt`: **42/42** visible WPF assertions, normal clock. Actual combo selection maps upper C to ID8; graphical zero/eight/staff independence, live rows, Apply/Close, Advanced provenance, stale/session/busy/paused guards and minimum expanded-source layout checked. Subsequent native input on this same edited session reaches026's refusal with Learned clear; after settled playback, restoring Learned through Apply reaches156/3A and049. The positive check establishes target dispatch/start, not full scene completion or original-game prerequisites. Screenshots inspected.

Core **265/265**, both game roots and unrelated untracked audit excluded. Nine status tests include preservation and native refusal/forward/reverse matching with capable slot0 and incapable slot3. Fresh shared regressions: global21/21, room20/20, doorways16/16 (2/3/4/8/10/37), controls19/19, input18/18, audio10/10; Blockers zero blocked random sites. Synthetic input/layout/media checks, no physical input or independent listening.

Fresh `audits-1/comparison.txt`: all363 literal statuses/frame counts/reached rooms/scope boundaries unchanged (330 complete,4 incomplete,19 no-start,10 brief); four established memberships and both sequence packets identical to the scope checkpoint. RoomAudit76 zero gaps, all76 historical pixel hashes unchanged; no PNG refresh. Catalog remains334 starts,84 scenes/250 reactions,168/235 sites,50 outstanding owners,123 unresolved edges. No source-route promotion.

Intermediate packets are retained, not presented as complete passes: status1 exposed a real two-way binding on read-only capability (fixed to OneWay); status2/3 exposed upper C's distinct glyph pair; status4 passed the initial36 checks before native casting additions. Status5 reached the positive cast but could not continue unrelated status checks after paused draft departure; status6/7 tried Apply during later source animation phases and hit the existing guard. Status8 passed the native phases after awaiting complete playback settlement. Status9's broad visual-tree search found display combos as well as the edited combo; the harness now targets the actual cell. Status10 passed42 checks; status11 repeats them after the denser Advanced footer. No user app or harness process was killed; harnesses closed themselves with failure reports/nonzero exit.

```powershell
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/ui-refinements-20261001/run-ui.ps1 -Output Exe/UiRefinementSession-20261001/status-fresh
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/ui-refinements-20261001/run-audits.ps1 -Output Exe/UiRefinementSession-20261001/audits-fresh
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/ui-refinements-20261001/summarize-audits.ps1 -Output Exe/UiRefinementSession-20261001/audits-fresh
```

Exact build cwd: `D:\Documents\Personal\Code Projects\SCUMMexplorer`. Main Release command: `rtk proxy dotnet build SCUMMexplorer.slnx -c Release`; executable `Exe/SCUMMexplorer.App/Release/net10.0-windows/SCUMMexplorer.App.exe`. Session token-usage tool unavailable; account limits are not session usage. Stop at the completed committed UI checkpoint; remaining actual Room41 input/096 actor certainty/fresh bounded failures remain handed off.

Reproduce from the main checkout:

```powershell
rtk proxy powershell -NoProfile -ExecutionPolicy Bypass -File .verification/ui-refinements-20261001/run-ui.ps1 -Output Exe/UiRefinementSession-20261001/placement-fresh
```
