# SCUMMexplorer

An audio-visual, interactive explorer for old SCUMM games. It is not meant as a technical SCUMM viewer. Users walk through rooms, trigger cutscenes, change objects and discover the games' secrets. **LOOM EGA (DOS) is the main focus.** Indiana Jones and the Last Crusade EGA is supported only for backgrounds and texts, including dialogue choices.

## Layout

- `src/SCUMMexplorer.Core`: parsing, rendering and bounded script execution (net10.0). It covers LFL/index readers, the old-v3 disassembler (`Formats/LoomScriptReader.cs`), the bounded executor and session, object selection and painting (`Rendering/RoomObjectSelection.cs`), the text extractor, and audio (PC speaker, AdLib via NukedOPL3Sharp).
- `src/SCUMMexplorer.App`: the WPF desktop UI (net10.0-windows). `MainWindow.xaml.cs` holds most of the UI logic; `TextGalleryWindow` is the Game Texts browser.
- `tests/SCUMMexplorer.Tests`: xUnit tests for Core.
- `docs/`: the plan, audits and references. Start with `docs/LOOM_EGA_PLAN.md` (priorities and milestones). `docs/LOOM_SCRIPT_MAP.md` records what the decoded scripts, variables, object verbs and rooms do. `docs/CHANGELOG.md` lists finished work with links to its validation records. `docs/NEXT_SESSION_PROMPT.md` is the hand-off for the next session: its state, ordered tasks, rules and tooling notes. The other docs are historical.
- `.verification/`: UI harnesses (for example `WindowCheck`, which renders rooms from the real `MainWindow`), traces and evidence. Evidence from the Spanish-edition work (27 September to 4 October 2026) is in `.verification/archive/`; `.verification/README.md` says what is live and what is a record. `source-backup-2026-09-27/` holds the sources as they were before the 27 September changes.
- `Games/`: the local game data. Never commit it.
  - `Games/loom ega en`: DOS LOOM EGA, **English**. The reference edition: the tests, the harness drivers and `loom-replays.json` use it (since 4 October 2026).
  - `Games/loom ega es`: DOS LOOM EGA, Spanish. Comparison only (`SCUMM_LOOM_TEST_ROOT` points the tests at it; `.verification/ScriptDump objmap` compares object ids).
  - `Games/indy3 ega en`: Indy3 EGA, **English**. The Indy tests expect this one (`SCUMM_INDY3_EGA_TEST_ROOT`).
  - `Games/indy3 ega es`, `indy3 vga es`, `indy3 demo en`, `monkey1`, `mmansion`: other local data, not used by the tests.
- `scummvm/`: a ScummVM Windows build for comparing behaviour at runtime. It contains no source code.
- Build output goes to `Exe/<Project>/…` (see `Directory.Build.props`).

## Build and test

From the repository root (Windows, .NET 10 SDK):

```powershell
dotnet build SCUMMexplorer.slnx
$env:SCUMM_SOUND_TEST_ROOT = "$PWD\Games\loom ega en"
$env:SCUMM_INDY3_EGA_TEST_ROOT = "$PWD\Games\indy3 ega en"
dotnet test tests\SCUMMexplorer.Tests\SCUMMexplorer.Tests.csproj --no-build
```

`.verification\run-verify.cmd` does the same and writes `.verification\verify.log`. The README uses `rtk proxy dotnet …`; RTK is optional. The LOOM data tests find `Games/loom ega en` (then `loom ega es`) relative to the test binary; without `SCUMM_INDY3_EGA_TEST_ROOT` the Indy tests skip silently.

## Engine facts that matter here (checked against ScummVM `engines/scumm`)

- Old-v3 rooms are XOR 0xFF encoded. The object directory starts at byte 29. Room-local scripts have IDs at or above the declared global script count in `00.LFL`.
- **Paint order:**
  - A room redraw paints active objects in *reverse* directory order, so the first entry ends up on top (`drawRoomObjects`).
  - `drawObject` only queues an object. Queued objects are painted on top in call order (`processDrawQue`).
  - `setState` marks the object's area dirty and clears the queue.
  - Close-up faces (LOOM rooms 56, 58, 86 and others) have colour-0 cut-outs that their eye or mouth images fill. Painting in directory order alone leaves black gaps.
- **Editions differ in data, not only text.** Spanish and English LOOM EGA have other script lengths, so never key code on a byte offset; the English release lacks the Spanish `bit[0xD62]` and numbers bits from the Spanish 0xD63 on one lower; object ids from Room 34 on are one higher (English Room 33 has an extra object). Derive bits, ids and offsets from the scripts (see `LoomStatusSchema`), and compare editions with `.verification/ScriptDump`.
- **Script dialects:** Indy3 EGA (non-Mac) differs from LOOM in a few opcodes:
  - `0xAE` wait has no subopcode.
  - `0x3B`/`0xBB` is waitForActor with a byte parameter. In LOOM it is a no-op with no operands.
  - `getActorX`/`getActorY` take a byte parameter.
  - Use `ScummScriptDialects.For(game.Id)` whenever you construct `LoomScriptReader` or `LoomObjectVerbReader`.
- **Indy3 dialogue choices** use verbs 120–125, set with `verbOps(n, [on, name "…"])`. The script clears `Var[114]`, waits, then branches on `isEqual var[0x072], n`.

## Evidence rules (from the plan)

- Tie every exit, action, state change, cue, scene or secret to the resource, script and instruction that support it.
- A decoded branch or room load is a **possible** outcome. Calling a verb directly is an **exploration action**. A partial session is a **state preview**. Label estimates as estimates.
- Do not add room-specific hacks or invent states. The user's product rules are:
  - Rooms should show no gaps and should show their first-visit state. When in doubt, pick at random and label the pick.
  - Objects respond as in the game, with Bobbin on screen (decided 28 September). A single click walks Bobbin to the object and uses it (scripts 005–007, verb 0x38; exits lead on). A double click also looks at it (verb 0x36: the description). Cycling an object's stored images belongs only to the inspection mode, and the mode's text says so. There is no hidden modifier gesture such as Alt+click. Hiding an object is allowed only if the background behind it has no gap.
  - Room hotspots navigate between rooms.

## Workflow notes

- Verification executables include `.verification/HarnessFailurePolicy.cs` through `Directory.Build.targets`: rebuild old harnesses before running them. Fatal exceptions log under `Exe/HarnessFailures/` (or `SCUMM_HARNESS_FAILURE_LOG`) and exit 1 without Windows crash/debug UI. The shipped app is unaffected.

- The repository is on git (`master`). Commit each finished task separately; `Games/` stays out of git.
- Record verification for each milestone under `.verification/<topic>/`, and update `docs/LOOM_EGA_PLAN.md` with its status and next task, and add a row to `docs/CHANGELOG.md`.

## Current work

See `docs/CURRENT_WORK.md` (rules, route priority, scene start setups and open decisions) and the top of `docs/NEXT_SESSION_PROMPT.md`. Keep that file current instead of growing this one.
