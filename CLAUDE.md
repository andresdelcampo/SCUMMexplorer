# SCUMMexplorer

An audio-visual, interactive explorer for old SCUMM games. It is not meant as a technical SCUMM viewer. Users walk through rooms, trigger cutscenes, change objects and discover the games' secrets. **LOOM EGA (DOS) is the reference; LOOM CD/VGA also has live interaction, actors, scripts, scene playback, Game Status and CD audio.** Indiana Jones and the Last Crusade EGA is supported only for backgrounds and texts, including dialogue choices.

## Layout

- `src/SCUMMexplorer.Core`: parsing, rendering and bounded script execution (net10.0). It covers LFL/index readers, the old-v3 disassembler (`Formats/LoomScriptReader.cs`), the bounded executor and session, object selection and painting (`Rendering/RoomObjectSelection.cs`), the text extractor, and audio (PC speaker, AdLib via NukedOPL3Sharp, Roland via Munt and CD voice/music cue timing).
- `src/SCUMMexplorer.App`: the WPF desktop UI (net10.0-windows). `MainWindow.xaml.cs` holds most of the UI logic; `TextGalleryWindow` is the Game Texts browser.
- `tests/SCUMMexplorer.Tests`: xUnit tests for Core.
- `docs/`: `docs/README.md` is the index. `docs/CURRENT_WORK.md` is the single current plan, rules and session handoff; `docs/CHANGELOG.md` holds finished work with validation links. `docs/loom/` and `docs/indy3/` hold game references, `docs/engine/` holds generic SCUMM notes, and `docs/archive/` preserves dated plans/audits. `docs/loom/LOOM_SCRIPT_MAP.md` records decoded scripts, variables, verbs and rooms.
- `.verification/`: UI harnesses (for example `WindowCheck`, which renders rooms from the real `MainWindow`), traces and evidence. Evidence from the Spanish-edition work (27 September to 4 October 2026) is in `.verification/archive/`; `.verification/README.md` says what is live and what is a record. `source-backup-2026-09-27/` holds the sources as they were before the 27 September changes.
- `Games/`: the local game data. Never commit it.
  - `Games/loom ega mt32 en`: DOS LOOM EGA, **English**, with the Roland (MT-32) upgrade installed. The reference edition: the tests, the harness drivers and `loom-replays.json` use it (since 4 October 2026).
  - `Games/ALTERNATIVE LOOMS/loom ega mt32 es`: DOS LOOM EGA, Spanish, Roland upgrade installed. Comparison only (`SCUMM_LOOM_TEST_ROOT` points the tests at it; `.verification/ScriptDump objmap` compares object ids).
  - Plain English/Italian EGA folders are under Games; other comparison editions and the short/long demos are under `Games/ALTERNATIVE LOOMS/`. English/Spanish/French/German and v1.2 comparison records are dated; current available folders may differ from that inventory. Hebrew support is dropped. Do not move/delete game data during documentation cleanup.
  - `Games/loom vga en`: LOOM CD (VGA, SCUMM v4) as `Loom.cue` + `Loom.bin` (ISO 9660 data track plus one CD audio track). Read directly by the app, including live v4 session, object/actor rendering, Readable scripts, source-derived scenes and CD voice/music. See CURRENT_WORK for comparison limitations.
  - `Games/mt32`: the user's MT-32/CM-32L ROM images for the Roland sound device. Never commit.
  - `Games/FUTURE/indy3 ega en`: Indy3 EGA, **English**. Set `SCUMM_INDY3_EGA_TEST_ROOT` to this absolute folder for Indy checks.
  - Other Indy editions/demo, Monkey Island and enhanced Maniac Mansion folders are under `Games/FUTURE/`; they are deferred comparison data.
- `scummvm/`: a ScummVM Windows build for comparing behaviour at runtime. It contains no source code.
- Build output goes to `Exe/<Project>/…` (see `Directory.Build.props`).

## Build and test

From the repository root (Windows, .NET 10 SDK):

```powershell
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet build SCUMMexplorer.slnx
& 'C:\Users\adelc\AppData\Local\Microsoft\WinGet\Links\rtk.exe' proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build
```

All shell and child commands must use RTK, including PowerShell; `C:\Users\adelc\.codex\RTK.md` is mandatory. `.verification/run-verify.cmd` is a full-audit driver, not a routine check. LOOM tests prefer `Games/loom ega mt32 en`; legacy Spanish/`loom` fallback names remain in TestGames. For the moved Spanish comparison use `SCUMM_LOOM_TEST_ROOT` with its absolute path. Set `SCUMM_SOUND_TEST_ROOT` and `SCUMM_INDY3_EGA_TEST_ROOT` when those checks are needed; Indy checks skip without their data setting.

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

## Evidence rules

- Tie every exit, action, state change, cue, scene or secret to the resource, script and instruction that support it.
- A decoded branch or room load is a **possible** outcome. Calling a verb directly is an **exploration action**. A partial session is a **state preview**. Label estimates as estimates.
- Do not add room-specific engine/renderer hacks or invent states. On 8 October the user approved direct late-room catalog starts running reviewed actual source prerequisites when missing, while preserving later state. The fingerprinted English Room 46 recipe implements this exception; operands remain in source-reviewed recipe data. The user's product rules are:
  - Rooms should show no gaps and should show their first-visit state. When in doubt, pick at random and label the pick.
  - Objects respond as in the game, with Bobbin on screen (decided 28 September). A single click walks Bobbin to the object and uses it (scripts 005–007, verb 0x38; exits lead on). A double click also looks at it (verb 0x36: the description). Cycling an object's stored images belongs only to the inspection mode, and the mode's text says so. There is no hidden modifier gesture such as Alt+click. Hiding an object is allowed only if the background behind it has no gap.
  - Room hotspots navigate between rooms.

## Workflow notes

- At session end, update `docs/CURRENT_WORK.md` and record finished results in `docs/CHANGELOG.md`. Create a new task or message another chat only with direct human authorization. Do not create idle continuation chains.
- Verification executables include `.verification/HarnessFailurePolicy.cs` through `Directory.Build.targets`: rebuild old harnesses before running them. Fatal exceptions log under `Exe/HarnessFailures/` (or `SCUMM_HARNESS_FAILURE_LOG`) and exit 1 without Windows crash/debug UI. The shipped app is unaffected. WPF harnesses can set `SCUMM_EXPLORER_PROFILE_ROOT` to a private output/profile directory before constructing MainWindow; otherwise the normal user profile is used and must be preserved.

- The repository is on git (`master`). Commit each finished task separately; `Games/` stays out of git.
- Record verification under `.verification/<topic>/`, update `docs/CURRENT_WORK.md` with current status/next work, and add finished results to `docs/CHANGELOG.md`.

## Current work

See `docs/CURRENT_WORK.md` for status, ordered work, product rules and the session handoff. Keep current state there instead of growing this file.
