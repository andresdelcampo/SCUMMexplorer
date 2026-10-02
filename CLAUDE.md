# SCUMMexplorer

An audio-visual, interactive explorer for old SCUMM games. It is not meant as a technical SCUMM viewer. Users walk through rooms, trigger cutscenes, change objects and discover the games' secrets. **LOOM EGA (DOS) is the main focus.** Indiana Jones and the Last Crusade EGA is supported only for backgrounds and texts, including dialogue choices.

## Layout

- `src/SCUMMexplorer.Core`: parsing, rendering and bounded script execution (net10.0). It covers LFL/index readers, the old-v3 disassembler (`Formats/LoomScriptReader.cs`), the bounded executor and session, object selection and painting (`Rendering/RoomObjectSelection.cs`), the text extractor, and audio (PC speaker, AdLib via NukedOPL3Sharp).
- `src/SCUMMexplorer.App`: the WPF desktop UI (net10.0-windows). `MainWindow.xaml.cs` holds most of the UI logic; `TextGalleryWindow` is the Game Texts browser.
- `tests/SCUMMexplorer.Tests`: xUnit tests for Core.
- `docs/`: the plan, audits and references. Start with `docs/LOOM_EGA_PLAN.md` (priorities and milestones). `docs/LOOM_SCRIPT_MAP.md` records what the decoded scripts, variables, object verbs and rooms do. `docs/CHANGELOG.md` lists finished work with links to its validation records. `docs/NEXT_SESSION_PROMPT.md` is the hand-off for the next session: its state, ordered tasks, rules and tooling notes. The other docs are historical.
- `.verification/`: UI harnesses (for example `WindowCheck`, which renders rooms from the real `MainWindow`), traces and evidence. `source-backup-2026-09-27/` holds the sources as they were before the 27 September changes.
- `Games/`: the local game data. Never commit it.
  - `Games/loom`: DOS LOOM EGA, Spanish.
  - `Games/IndyCru/EGA`: Indy3 EGA, **English**. The Indy tests expect this one.
  - `Games/indy3/EGA`: Indy3 EGA, Spanish.
- `scummvm/`: a ScummVM Windows build for comparing behaviour at runtime. It contains no source code.
- Build output goes to `Exe/<Project>/…` (see `Directory.Build.props`).

## Build and test

From the repository root (Windows, .NET 10 SDK):

```powershell
dotnet build SCUMMexplorer.slnx
$env:SCUMM_SOUND_TEST_ROOT = "$PWD\Games\loom"
$env:SCUMM_INDY3_EGA_TEST_ROOT = "$PWD\Games\IndyCru\EGA"
dotnet test tests\SCUMMexplorer.Tests\SCUMMexplorer.Tests.csproj --no-build
```

`.verification\run-verify.cmd` does the same and writes `.verification\verify.log`. The README uses `rtk proxy dotnet …`; RTK is optional. The LOOM data tests find `Games/loom` relative to the test binary.

## Engine facts that matter here (checked against ScummVM `engines/scumm`)

- Old-v3 rooms are XOR 0xFF encoded. The object directory starts at byte 29. Room-local scripts have IDs at or above the declared global script count in `00.LFL`.
- **Paint order:**
  - A room redraw paints active objects in *reverse* directory order, so the first entry ends up on top (`drawRoomObjects`).
  - `drawObject` only queues an object. Queued objects are painted on top in call order (`processDrawQue`).
  - `setState` marks the object's area dirty and clears the queue.
  - Close-up faces (LOOM rooms 56, 58, 86 and others) have colour-0 cut-outs that their eye or mouth images fill. Painting in directory order alone leaves black gaps.
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

The user's 2 October route-priority decision supersedes older instructions to expand observer coverage as the next task. Follow the current priority and bounded workflow at the top of `docs/NEXT_SESSION_PROMPT.md` and in `docs/LOOM_EGA_PLAN.md`. Target one blocked route; use existing diagnostics first. Add instrumentation only to answer a named question that prevents that route's fix, then immediately return to the same route. Do not begin another general observer-hardening or access-coverage session by default. More tests, observations or unchanged captures are supporting evidence, not additional working routes.

Start with the unchanged Room41 native090 raw4 recipe. After at most two focused diagnostic cycles without a route-relevant implementation decision, record the precise remaining blocker and next bounded experiment, then stop that investigation for the session instead of broadening it. Preserve every source/evidence/estimate barrier. Keep old captures immutable; a genuine execution fix must explain its expected differences in fresh captures while unaffected evidence remains exact. Never erase or renumber historical evidence to make comparisons pass. Documentation-only updates need document/protection checks, not a replay of the full runtime suite; execution changes retain the required regressions and final main Release build. No App launch or push under the current handoff.

Replanned on 28 September around what the user sees. The priorities are: (M1) object clicks run the game's interaction and its text appears on screen in the game's font, (M2) actors are drawn with costume animation, and (M3) cutscenes and animations play with the game's timing. The session already runs most of this logic, but the app does not draw it. Engine gaps that do not change the screen wait until a milestone route needs them. See the plan's "Ordered work".

<!-- rtk-instructions v2 -->
# RTK (Rust Token Killer) - Token-Optimized Commands

## Golden Rule

**Always prefix commands with `rtk`**. If RTK has a dedicated filter, it uses it. If not, it passes through unchanged. This means RTK is always safe to use.

**Important**: Even in command chains with `&&`, use `rtk`:
```bash
# ❌ Wrong
git add . && git commit -m "msg" && git push

# ✅ Correct
rtk git add . && rtk git commit -m "msg" && rtk git push
```

## RTK Commands by Workflow

### Build & Compile (80-90% savings)
```bash
rtk cargo build         # Cargo build output
rtk cargo check         # Cargo check output
rtk cargo clippy        # Clippy warnings grouped by file (80%)
rtk tsc                 # TypeScript errors grouped by file/code (83%)
rtk lint                # ESLint/Biome violations grouped (84%)
rtk prettier --check    # Files needing format only (70%)
rtk next build          # Next.js build with route metrics (87%)
```

### Test (60-99% savings)
```bash
rtk cargo test          # Cargo test failures only (90%)
rtk go test             # Go test failures only (90%)
rtk jest                # Jest failures only (99.5%)
rtk vitest              # Vitest failures only (99.5%)
rtk playwright test     # Playwright failures only (94%)
rtk pytest              # Python test failures only (90%)
rtk rake test           # Ruby test failures only (90%)
rtk rspec               # RSpec test failures only (60%)
rtk test <cmd>          # Generic test wrapper - failures only
```

### Git (59-80% savings)
```bash
rtk git status          # Compact status
rtk git log             # Compact log (works with all git flags)
rtk git diff            # Compact diff (80%)
rtk git show            # Compact show (80%)
rtk git add             # Ultra-compact confirmations (59%)
rtk git commit          # Ultra-compact confirmations (59%)
rtk git push            # Ultra-compact confirmations
rtk git pull            # Ultra-compact confirmations
rtk git branch          # Compact branch list
rtk git fetch           # Compact fetch
rtk git stash           # Compact stash
rtk git worktree        # Compact worktree
```

Note: Git passthrough works for ALL subcommands, even those not explicitly listed.

### GitHub (26-87% savings)
```bash
rtk gh pr view <num>    # Compact PR view (87%)
rtk gh pr checks        # Compact PR checks (79%)
rtk gh run list         # Compact workflow runs (82%)
rtk gh issue list       # Compact issue list (80%)
rtk gh api              # Compact API responses (26%)
```

### JavaScript/TypeScript Tooling (70-90% savings)
```bash
rtk pnpm list           # Compact dependency tree (70%)
rtk pnpm outdated       # Compact outdated packages (80%)
rtk pnpm install        # Compact install output (90%)
rtk npm run <script>    # Compact npm script output
rtk npx <cmd>           # Compact npx command output
rtk prisma              # Prisma without ASCII art (88%)
rtk uv run <cmd>        # Compact uv project command output
```

### Files & Search (60-75% savings)
```bash
rtk ls <path>           # Tree format, compact (65%)
rtk read <file>         # Code reading with filtering (60%)
rtk grep <pattern>      # Search grouped by file (75%). Format flags (-c, -l, -L, -o, -Z) run raw.
rtk find <pattern>      # Find grouped by directory (70%)
```

### Analysis & Debug (70-90% savings)
```bash
rtk err <cmd>           # Filter errors only from any command
rtk log <file>          # Deduplicated logs with counts
rtk json <file>         # JSON structure without values
rtk deps                # Dependency overview
rtk env                 # Environment variables compact
rtk summary <cmd>       # Smart summary of command output
rtk diff                # Ultra-compact diffs
```

### Infrastructure (85% savings)
```bash
rtk docker ps           # Compact container list
rtk docker images       # Compact image list
rtk docker logs <c>     # Deduplicated logs
rtk kubectl get         # Compact resource list
rtk kubectl logs        # Deduplicated pod logs
```

### Network (65-70% savings)
```bash
rtk curl <url>          # Compact HTTP responses (70%)
rtk wget <url>          # Compact download output (65%)
```

### Meta Commands
```bash
rtk gain                # View token savings statistics
rtk gain --history      # View command history with savings
rtk discover            # Analyze Claude Code sessions for missed RTK usage
rtk proxy <cmd>         # Run command without filtering (for debugging)
rtk init                # Add RTK instructions to CLAUDE.md
rtk init --global       # Add RTK to ~/.claude/CLAUDE.md
```

## Token Savings Overview

| Category | Commands | Typical Savings |
|----------|----------|-----------------|
| Tests | vitest, playwright, cargo test | 90-99% |
| Build | next, tsc, lint, prettier | 70-87% |
| Git | status, log, diff, add, commit | 59-80% |
| GitHub | gh pr, gh run, gh issue | 26-87% |
| Package Managers | pnpm, npm, npx | 70-90% |
| Files | ls, read, grep, find | 60-75% |
| Infrastructure | docker, kubectl | 85% |
| Network | curl, wget | 65-70% |

Overall average: **60-90% token reduction** on common development operations.
<!-- /rtk-instructions -->
