# ManiacClassic S8: current-party inventory choices

| | |
|---|---|
| Family / stage | ManiacClassic / S8 |
| Fixture and edition | `D:/Juegos/Lucas/mmansion ega en`, English classic v1 |
| Date and author | 2026-10-18, Codex ManiacClassic worker |
| Commits | Family evidence 099722d; shared API 30b2ccd with provenance e25aca2 and running-action guard 2d74a55; proposed requirements R-12.7–R-12.10 in d3103ed; App integrated by parent |
| Registry change | Parent owns the final bounded Status claim and generated matrix |

## What was done

Derived the selected party from the edition's native actor-switch input, rather than using a hardcoded character list or treating every named actor as playable. The existing native Play route genuinely acquires the key before the new explicit reassignment: Dave → Syd → Undo back to Dave. These are user choices, not played scenes or a simulated Give action. No route, item or flag is invented to satisfy the check.

The one family harness has source-extraction and route/round-trip modes. The test route is shared with it, preventing separate versions of the acquisition sequence. Source facts live in the [script map](../../docs/maniac/MANIAC_SCRIPT_MAP.md); [source.txt](source.txt) contains the extracted instructions, and [route-pre-api.txt](route-pre-api.txt) retains the initial native acquisition check.

## Commands run (through RTK)

From `.worktrees/maniac-classic-s8`:

```powershell
rtk proxy dotnet run --project .verification/maniacclassic-state-20261018/Check/StateCheck.csproj
rtk proxy dotnet run --project .verification/maniacclassic-state-20261018/Check/StateCheck.csproj -- "D:\Juegos\Lucas\mmansion ega en" route
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --filter "FullyQualifiedName~ScummManiacClassicStateTests"
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --filter "FullyQualifiedName~SupportConformanceTests&DisplayName~ManiacClassic"
```

## Results (numbers)

**Three state tests pass, zero failed/skipped** (about four seconds), plus **one classic conformance row passes**. [round-trip.txt](round-trip.txt) records the successful native acquisition, eligible names/source bindings, held item, Apply, stale-view rejection and Undo. Native acquisition reaches Room 1, key 55 owned by Dave (3), player control true; party variables hold Dave (3), Syd (1), Bernard (5). Apply moves only key ownership to Syd; Undo restores Dave, retaining inventory slot 0 and source room 1.

Regressions compare all variables, object states/classes, other owners and structurally serialized actors, plus unchanged room and scene count. Cancel-by-discard changes nothing. Room item 54, named NPC 9, unselected kid 2, a mixed valid/invalid batch, duplicate-item batch, stale view, reused undo, boot menu and pending input are refused. Missing names are excluded from eligible recipients by the shared API; this is a code guard, not an additional tested classic release.

Initial test result was two passed/one failed: record equality compared nested actor array references rather than content. The invariant was corrected to structural serialization; the complete three-test set then passed, including added class/slot/atomic-batch assertions. No corresponding engine behavior was changed. The Core owner checks shared LOOM regressions; parent owns final full-suite and real-window evidence.

All three classic tests passed again after each shared follow-up: explicit edit attribution and refusal while native player-action scripts still run. The native acquisition endpoint needed no change. Apply/Undo provenance does not increment the scene counter or claim script execution.

## Comparison with ScummVM (source files and date; runtime comparison if any)

The game-specific facts come from classic Script 1 actor names/initial party, Script 114 native selection and Script 4 actor switching, extracted 18 October 2026. Earlier S6 verified the mat/key route against source `script_v2.cpp` and `verbs.cpp` (17 October); S8 does not claim that explicit reassignment executes those scripts. ScummVM was not launched. Parent owns the actual app-window check and shared source/API references.

## Differences and uncertainties kept

Only this reference copy is available for ManiacClassic; enhanced is a separate family. S9 alternate-release comparisons remain blocked. S5 original-image, darkness/flashlight, walking/timing/audio limits and S7 post-doorbell Script 174 limitation remain unchanged. Inventory state editing neither completes a story prerequisite nor validates downstream responses to an arbitrary assignment. No item creation, flags, actor placement, party change, save-file write or game-data mutation is included.

## User checks (exact steps and where; none if none)

Open classic Maniac, choose Syd and Bernard and Start, use the existing native route to lift the mat and pick up the key. Open Game Status, select Syd as the key's recipient, Cancel and verify Dave retains it. Reopen, Apply the reassignment, then Undo **in the same paused modal window**; verify the key returns to Dave without moving characters or changing rooms. Undo expires on any intervening playback frame, so this check does not require undo after resuming Play. Parent records the same real-window actions. Original-game perceived timing and listening checks remain separate.

## Where the facts went (links to the script map / engine notes / tracker entries)

[Script map](../../docs/maniac/MANIAC_SCRIPT_MAP.md), [support requirements](../../docs/REQUIREMENTS.md#maniac-and-zak-support-boundaries), [family index](../../docs/maniac/README.md). Shared requirement and API facts belong to the parent and sole Core owner.
