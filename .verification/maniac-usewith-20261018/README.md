# Maniac battery/flashlight Use-with: integrated window checks

| | |
|---|---|
| Families / stage | ManiacClassic and ManiacEnhanced / requested bounded fidelity follow-up |
| Fixtures | `D:/Juegos/Lucas/mmansion ega en` (English v1); `D:/Juegos/Lucas/mmansionE ega es` (Spanish v2) |
| Date / author | 18 October 2026, parent Maniac/Zak orchestrator |
| Engine prerequisite | `3572a57`, integrated as `01eb536`; independent review `a4e4bad` / `5768d56` |
| Registry | Unchanged: Play/Objects/Status already Full; no complete-game claim |

## What was done

Two private real `MainWindow` processes started Play from each edition's party screen. The harness delivered native Syd/Bernard/Start choices and Escape at the opening override. It checked the real Play timer advanced, then drove the window's actual frame callback deterministically. Each process had its own profile and room cache. Inventory coordinates were recomputed from native owner/slot order, including the changed slots after consumption. No production App code changed.

Preparation is deliberately disclosed exploration: preview Room 2, bounded native settle, direct radio 142 Pick up/Open, preview Room 7 and direct flashlight 84 Pick up. These are actual source verbs, not owner/variable assignments. They establish held objects without claiming an earned journey. The checked action itself clicks the displayed Use verb, held batteries 167, held flashlight 84, then the selected flashlight again to execute the native sentence. The source uses preposition 1: `Use batteries in flashlight` / `Usar pilas en linterna`, irrespective of this issue's colloquial “Use-with” name.

The harness waits for native battery mode 2 and consumed owner 0, captures the inventory, then clicks displayed Switch on and the held flashlight twice. After an explicit Room 5 preview it checks the reached native beam, moves the pointer and checks the actual displayed bitmap changes. Every displayed bitmap must equal the composed Play frame. Rendering must preserve the serialized snapshot and frame counter. The source background is partly black and partly revealed; room-light bit 2 is off and beam bit 4 is on. Normal has no technical Play tooltip or Objects note.

## Commands and output

Working directory: `D:/Documents/Personal/Code Projects/SCUMMexplorer/.worktrees/maniac-usewith`.

```powershell
rtk proxy dotnet build .verification/maniac-usewith-20261018/WindowCheck/WindowCheck.csproj --verbosity quiet -p:NuGetAudit=false
rtk proxy Exe/WindowCheck/Debug/net10.0-windows/WindowCheck.exe "D:\Juegos\Lucas\mmansion ega en" ".verification/maniac-usewith-20261018/window-classic" ".verification/maniac-usewith-20261018/classic.json"
rtk proxy Exe/WindowCheck/Debug/net10.0-windows/WindowCheck.exe "D:\Juegos\Lucas\mmansionE ega es" ".verification/maniac-usewith-20261018/window-enhanced" ".verification/maniac-usewith-20261018/enhanced.json"
```

Harness build: **zero warnings/errors**. Both processes exit 0 with `PASS actual-window Use-with route`. Classic finishes at window frame 1254; enhanced at 1389. These are callback counters including preparation, not original-game timing measurements. The source `settle` preparation also runs native session work outside that counter.

Eight local captures were inspected: each edition's `use-with.png`, `battery-inserted.png`, `beam-first.png`, `beam-moved.png`. Both native sentences are visible; batteries disappear and flashlight remains; the dark-room beam changes position. Classic's actor is natively gray, while enhanced retains its own colors. Local outputs are under the two `window-*` directories beside this record and are ignored, with private profile/cache and `result.txt`. No game assets or profiles are committed. The JSON recipes and harness are retained for reproduction.

## Source, regressions and boundaries

[Independent baseline/review](../maniac-usewith-review-20261018/README.md), [Classic own-source regressions](../maniacclassic-usewith-20261018/README.md), [enhanced engine/family record](../maniacenhanced-usewith-20261018/README.md). The original failed attempts remain in their earlier records; no failed history was rewritten. The fresh observations establish missing header semantics rather than a guessed input-mode or story-state workaround. Source get/set/copy behavior applies at the version layer, without object/room exceptions. No ScummVM executable ran.

This is native insertion/refusal/beam acceptance after explicit preparation, not a complete played puzzle route, other parties/releases, or original-game picture/timing/listening acceptance. No new requirement proposal, authored catalog, seed, junction or worktree removal. Parent final build/full-suite/matrix and integration results are in the [clean checkpoint](../../docs/checkpoints/20261018-UseWith-ManiacZak.md).

Facts also recorded in the [game map](../../docs/maniac/MANIAC_SCRIPT_MAP.md), [engine notes](../../docs/engine/SCUMM_SCRIPT_MAP.md), [tracker](../../docs/maniac/SUPPORT_STATUS.md), README/REQUIREMENTS notes and dedicated handoff. User checks: the commands above reproduce the bounded result; no subjective recheck requested.
