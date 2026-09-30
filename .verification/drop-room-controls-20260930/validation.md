# Discard obsolete room-controls work, 30 September 2026

The user reviewed the description of commit 8878cd9 and instructed: "sounds like too old to be relevant. Drop it."

Removed the detached worktree at `C:\Users\adelc\.codex\worktrees\room-controls\SCUMMexplorer` using `git worktree remove`, through the absolute RTK executable with explicit main-checkout working directory and approved escalation.

Before removal, verified the resolved absolute path matched the requested worktree, `git status --porcelain` was empty, and no reparse points/junctions existed anywhere in the checkout. Therefore no junction-removal commands were needed. Ignored contents were build outputs and prior UI harness captures. No tracked or ordinary untracked changes were discarded. The worktree was not a managed attachment in this chat, so the archive tool did not apply.

After removal, `git worktree list` no longer lists room-controls. Its old commit was not merged or cherry-picked. Other worktrees and the main checkout's pre-existing untracked files were left untouched. Updated the handoff, plan and changelog to record the user's decision. No application code changed, so builds and UI tests were not rerun.
