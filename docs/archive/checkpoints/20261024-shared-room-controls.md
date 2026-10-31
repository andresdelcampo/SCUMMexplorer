# Checkpoint: shared room controls, static previews, LOOM parity (20 to 24 October 2026)

User requests: room controls in every game (playback bar, Play Cutscene, Interact, Sounds, static previews with ambient scripts only), then "UI controls ... should NOT be game specific ... only how those controls are filled may vary" (R-16.9), "hotspots and doorways visibility should not be game specific", "use LOOM as reference", judgement calls where a game's mechanics would be misled.

## Done (all merged into master; last full suite 1781 passed, 0 failed, 5 skipped; master builds with 0 warnings)

- Static previews keep all controls and run only ambient scripts (`ScummAmbientScripts.Classify`); LOOM previews gained the controls too.
- LOOM parity: Interact button and menu, Sounds rows, Step, Hotspots/Doorways default `Never` (available in Normal and Advanced), R-6.21 to R-6.25.
- Shared layer: `RoomControlContract.cs` (`IRoomControlProvider`), `RoomHotspots.cs` (one hotspot/doorway rule, drawing and rows), `RoomOverlayRules.cs`, `MainWindow.RoomControls.cs`, providers for LOOM and the generic games; cutscene picker, Game Status and Advanced inspection from providers.
- Double-click look in MI1 (floppy, CD), MI2, Indy 4 (`ScummProfile.DoubleClickLookVerb`); off in Indy3 (second click on the same target is used) and inconclusive/off in Maniac/Zak.
- REQUIREMENTS: new section 6.1 (R-6.30 to R-6.46) with the contract and each button's behaviour; older rows point to it (commit 57de42d6).

## Not done, found by the requirements pass (marked "Not yet met" in R-6.30 to R-6.46)

1. Full-screen Esc: in play-through games Esc leaves full screen even while a skippable cutscene plays; LOOM skips the cutscene (R-6.34, R-4.9).
2. Key handling: LOOM and the play family use separate key handlers (R-6.45).
3. Play Game Intro: still chosen by `GameId` (`UpdateGameIntroButton`, `GameIntro_Click`); needs a provider method (R-6.38).
4. LOOM CD Sounds rows and Stop differ from R-6.24 (R-6.40).
5. LOOM keeps its own Room entry / Last action panel (judgement call, R-6.25 item 6).
6. No visible hint for the double-click look gesture; the gesture is tested headless only.

## Questions for the user

- LOOM Pause is disabled when no scene or animation runs and in static previews; the play family enables it whenever the session runs. Change LOOM, or accept?
- Right button ownership: the code uses `!RightClickReachesGame` (also Indy3 EGA); is Indy3 256 colours v3 or v4 for this rule (R-3.x versus R-7.16)?
- Static-preview Skip line and Interact enablement are stated from the code; confirm.
- A picked room that can only be a still picture (R-5.3a versus R-8.5b): Normal-mode hint, or play the boot state (raised 20 October)?

## Next run

Items 1 to 4 above as one migration agent, then S9 and polish items in NEXT_SESSION_PROMPT.md.

## Answered 24 Oct (user; recorded in REQUIREMENTS R-6.32, R-6.35, R-6.36, R-6.39, R-6.25 item 7, R-7.16, R-8.5b)

- Pause in LOOM vs the play family: LOOM's rule wins. Pause is for cutscenes and scenes; ambient animations (campfire) just run. Code: the play family's Pause is enabled only on activity or while paused; the Space key follows the button.
- Right button: the game's where its interface uses it, otherwise the app's LOOM-style shortcut. Indiana Jones 3 on 256 colours is engine version 3 (app shortcut), as the EGA one.
- Skip line: only while a line is shown (also in cutscenes), live or static. Static-preview Skip line and Interact enablement confirmed as stated.
- Campfire still picture: the animation runs permanently in a loop; the controls stay (R-8.5b).
- New from the same message, done: Normal `Play Cutscene` lists what Advanced lists (plain titles); Interact is an upfront, grouped overview of the room, not "select an object first". Open: none from these answers; items 1 to 6 above stay open.

## Hover freeze: fixed (24 October, evening)

User report: Maniac Mansion enhanced, Play Game Intro, two kids, skip, Hotspots On hover, hover over any object: sluggish, then the whole Windows desktop stalled for minutes (no Event Viewer entries). Cause found by the user's own read of the code ("each hover calls RefreshPlayOverlays"): only On hover refreshed the overlays on every pointer move, and each refresh re-captured the scene (two full session snapshots, the hotspot table, the action lists, the overlay canvas, option-control property writes). Fix `2bbe9888` (merge `df77ecb3`): `UpdatePlayOverlayHover` redraws only when `RoomHotspotRules.HoverKey` changes; `ScummPlayScreen.Scene()` takes one snapshot; requirement R-6.48; guard `HoverCostGuardTests`. Confirmed by the user in the real app: "works like a charm". Diagnostic switch kept: `SCUMM_EXPLORER_RENDER=software`.

Still open from this checkpoint: the five "Not done" items above (full-screen Esc, one key handler, Play Game Intro as a provider method, LOOM CD Sounds rows, visible double-click hint); Maniac static-preview Interact (accepted); the still-picture question is answered (permanent animation, controls present).
