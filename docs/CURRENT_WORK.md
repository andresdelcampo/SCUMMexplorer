# Current work (moved out of CLAUDE.md on 3 October 2026)

Keep this file current; CLAUDE.md only points here. The hand-off for the next session is `docs/NEXT_SESSION_PROMPT.md`; priorities and milestones are in `docs/LOOM_EGA_PLAN.md`.

The user's 2 October route-priority decision still holds: target one blocked route, use existing diagnostics first, add instrumentation only to answer a named question that blocks that route, and do not start another general observer-hardening or access-coverage session by default. Follow the current priority and workflow at the top of `docs/NEXT_SESSION_PROMPT.md` and in `docs/LOOM_EGA_PLAN.md`. Preserve every source/evidence/estimate barrier. Keep old captures immutable; a genuine execution fix must explain its expected differences in fresh captures while unaffected evidence remains exact. Never erase or renumber historical evidence to make comparisons pass. Documentation-only updates need document/protection checks, not a replay of the full runtime suite; execution changes retain the required regressions and final main Release build. No App launch or push unless the handoff says otherwise.

The Room 41 native090 raw4 recipe is no longer blocked: the conditional-load guard was relaxed on 2 October (0318792) and the unchanged recipe reaches Room 43 (15/15 scopes). Audited coverage is 234/235; the one remaining site is Room 34 local200 +0035, which looks unreachable in play (see the handoff).

Costumes and story bits (user's rules, 3 October): the ego keeps whatever costume a scene gave him when he moves to another room, unless that room's own code needs a different one. A catalog start has no previous scene, so it takes the costume the room's entry gives (the audit found nothing more to derive; see `.verification/setup-placer-20261003/audit-costume-entries.md`).

Cutscene starts: the picker derives a per-scene start setup from the scene's own code (`LoomSceneStateScanner`, `LoomSceneSetupPlanner`). Scenes whose code clears or reads bit D61 start with the distaff held through the game's own pickup (Room 09 object 271), run unseen, when the scene is outside the LOOM island (Room 11 on, since nobody boards the boat without it; the user's decision of 3 October); island scenes keep the fresh state. Every assumption is shown in the picker note. No one-off teleports and no invented story flags.

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

## Room state rule (user, 3 October)
Rooms start in the earliest game state. A room moves to a late state only after the user triggers the late cutscene that causes it (the session then carries that state on). So no scene is chained after other scenes to show a later branch (the Room 44/652, Room 45/655, Room 41/633 question is closed: fresh branch). The staff rule for scenes outside the island (decided 3 October) stays as its own explicit exception.

## State control (user, 3 October)
The user controls the game state (Game Status: staff, notes, drafts, story events) and can change it freely to explore. The app only helps by raising the state to the minimum the chosen room or cutscene needs, and keeps a later state if the session already has one (menu picks should not reset the session; a "Start over" resets). Done 3 October: a single cutscene picked from the menu keeps the running session (`PlayKnownCutscene`) and `LoomSceneSetupPlanner.Adapt` drops the staff pickup when the session already holds it; stated sequences still start fresh (they carry their own steps). Restart returns to the boot. Story-event checkboxes need no extra presetting: the real pickups and scenes that run set their bits, and they show as checked. The LOOM-specific flag list is `LoomEgaStateFlags` (App), the window is generic.
