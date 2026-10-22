# MI2 play start: "play starts 104.5 s after opening", 20 October 2026

| | |
|---|---|
| Item | Regression report from the globals-tracking agent: `monkey2-cutscenes-20261018/CutsceneWindowCheck` on master showed "play started … 104.5 s after opening" and "frames played by the window: 0" |
| Fixtures | `D:\Juegos\Lucas\mi2 talkie en` (talkie), `mi2 mt32 en` (floppy); checks on `indy4 vga en`, `monkey1 vga en` (MonkeyCd), `indy3 ega en` |
| Base | master `c0b8ef9f` (globals tracking included) |
| ScummVM | not run (not a behaviour comparison: the question is the app's own catalog policy) |

## Finding

Play does not start late: **in Room 4 it never starts**. Since `273cc399` (19 October, "Separate catalog browsing from native scenes and explicit intros", the App side of R-8.5b/R-8.5c on top of `b6231d7e`'s `ScummSession.PrepareManualRoom`), choosing a room in the catalog runs that room's native entry offscreen on a copy and adopts it only when it settles quietly with player input; otherwise the window keeps a **static preview** without a play clock (`_catalogPreviewOnly`). MI2's play boot ends in Room 4, the campfire; its entry tells the story and leaves the room (Room 4 → Room 1 "Part One" after 214 frames → Room 7 Woodtick after 309 more), so the preparation returns "Native entry left the selected room; no live preview was adopted." after 215 frames (0.1–0.5 s). The old check waited `900 × 100 ms` for `_playTimer`, then read `_play` (the booted session, set but never clocked): 90 s plus the open and boot gives the reported 104.5 s, and 0 frames.

Not the cause: today's sound polish (`49718df6`), talkie speech/mouth sync (`7417ce43`), the Indy 4 credits/inventory fix (`a6c0082c`) and the globals tracking (`c0b8ef9f`). Headless `BootForPlay` takes 0.5–2.2 s for MI2 (2841 boot frames, unchanged), Room 7's preparation 0.5 s.

## Bisect (scratch worktrees in the scratchpad, harness copied in, no junctions, removed after)

| Commit | MI2 talkie, select Room 4 in the real window |
|---|---|
| `b6231d7e~1` (before the manual-room policy) | play Room 4, clock on at 6.7 s; first frame **6.8 s** after opening; 46 frames in the next 5 s |
| `273cc399` (catalog browsing separated) | play session Room 4 at 6.7 s, static preview at 7.0 s; **no frame within 120 s** |
| master `c0b8ef9f` | same as `273cc399` (static preview at 13.4 s; no frame in 180 s; machine shared) |

## Decision (GAME_SUPPORT_INSTRUCTIONS §4.4/4.5)

Making Room 4 play again would autoplay the rest of MI2's opening on a catalog selection, which R-8.5b ("if bounded preparation cannot produce a quiet usable state, keep a static preview without autoplay") and R-8.5c (opening a game or its first room must not autoplay its intro) forbid: hard stop 4.4.1 for that path, so the app is unchanged. The intro stays available through the header's **Play Game Intro** (`ScummGameSession.StartIntro`). Fixed instead: the checks that assumed the old behaviour. Undo: revert this branch's commits.

- `monkey2-cutscenes-20261018/CutsceneWindowCheck` opens **Room 7** (Woodtick, live) and fails at once with a clear message when the selection falls back to a static preview, or when play is not clocked within 30 s of opening (no more silent 90 s waits).
- New `PlayStartWindowCheck/` (this folder): headless boot and preparation timings, then the real `MainWindow` (private `SCUMM_EXPLORER_PROFILE_ROOT` and `SCUMM_ROOM_CACHE_DIR`) opens the boot's room, or the first room in index order whose preparation is live, and logs the session, clock, static-preview flag, frames and background derivation every 100 ms. Exit 1 if play has no first frame within 30 s of opening. `probe [n]` lists the first n rooms' preparation results headless.
- New `tests/SCUMMexplorer.Tests/ScummPlayStartTests.cs`: for MI2 talkie and floppy, Indy 4, MonkeyCd and Indy3 EGA, boot plus catalog preparation reach a live room with the input script, and 30 frames, in under 20 s (measured 0.3–7 s); MI2's Room 4 is a static preview ("left the selected room") and Room 7 is live.

## Timings in the real window (first frame after `OpenGameFolder`, private profile, cold cache)

| Game | Room selected | Before (`b6231d7e~1`) | Master before this branch | This branch |
|---|---|---|---|---|
| MI2 talkie (`mi2 talkie en`) | 4 (boot room) | 6.8 s | never (static preview) | never (static preview, by R-8.5b) |
| MI2 talkie | 7 (Woodtick) | – | 6.4 s | 6.4 s; cutscene check: play at 6.9 s |
| MI2 floppy (`mi2 mt32 en`) | 1 (Room 4 static) | – | 1.7 s | 1.7 s |
| Indy 4 (`indy4 vga en`) | 4 (boot room, live) | – | 5.9 s | 5.9 s |
| MonkeyCd (`monkey1 vga en`) | 38 (boot room, live) | – | 0.8 s | 0.8 s |
| Indy3 EGA (`indy3 ega en`) | 73 (boot room, live) | – | 1.8 s | 1.8 s |

The ~5 s for the talkies is `OpenGameFolder` itself (4.9 s at both commits), not play. No code changed in the app or Core, so the "This branch" column is the same build as master plus the checks.

The updated cutscene check on this branch (`Room 74 · Pick up voodoo key to jail`): play in Room 7 at 6.9 s; background derivation done 21 s after play start (260 of 260; planner 536 / 260 / 76 / 165 begun); Room 74's scene began with sound 176 and LeChuck's lines.

MI2 talkie headless probe (first 30 rooms from the boot state): 20 live, 10 static (Rooms 2, 4, 5, 12, 13, 14, 20, 21, 26, 30: left the room, pending scene initialization, or not quiet within 2400 frames).

## Commands (through RTK)

```
rtk proxy dotnet build .verification/mi2-play-start-20261020/PlayStartWindowCheck/PlayStartWindowCheck.csproj
rtk proxy dotnet Exe/PlayStartWindowCheck/Debug/net10.0-windows/PlayStartWindowCheck.dll "D:\Juegos\Lucas\<game>" <scratch>\<out> [room|probe n]
rtk proxy dotnet build .verification/monkey2-cutscenes-20261018/CutsceneWindowCheck/Monkey2CutsceneWindowCheck.csproj
rtk proxy dotnet <Monkey2CutsceneWindowCheck.dll> "D:\Juegos\Lucas\mi2 talkie en" <scratch>\mi2-cutscene-check "Room 74 · Pick up voodoo key to jail"
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-build --filter "FullyQualifiedName~ScummPlayStartTests|FullyQualifiedName~ScummManualRoomPolicyTests"   (18 passed)
```

## Uncertainties and open points

- R-5.3a still says the generic session's first playable room is the boot's own state, while R-8.5b now makes the boot's room a static preview when its entry moves on (MI2). A user opening MI2 and choosing Room 4 sees no play and, in Normal mode, no reason (the reason is an Advanced tooltip). Whether Room 4 should show the boot's settled state as play, or a Normal-mode hint toward Play Game Intro, is a user decision (checkpoint question), not changed here.
- The 20 s bound in the test is generous on purpose (parallel tests, shared machine); the window guard is 30 s.
