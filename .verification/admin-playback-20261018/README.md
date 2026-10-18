# Normal sound selection and independent playback controls

Date: 18 October 2026. Shared App review requested with the Maniac/Zak admin-status clarification. No registry level changes: this corrects shared controls for already supported playback, without claiming additional game fidelity.

## What was done

The existing header sound selector is visible in Normal mode when the support registry offers live Play, cutscenes and audio. Browse-only sound selection remains in Advanced. LOOM CD keeps its existing disc-audio selector. Existing device choices, unavailable-device filtering and private-profile persistence are reused.

The generic Play branch previously returned before LOOM's device-lock updates, refreshed Skip only every twelve frames, and let Space pause the frame clock without refreshing the Pause caption. It now uses the same scene lock in both the controls and selection handler, updates transport state every frame and after keys/clicks, and resumes a paused scene when Skip sends its native override, as LOOM does. Room launch lists still refresh periodically; entering or leaving a cutscene refreshes their enabled state immediately.

Pause, Skip, Skip line and Play Cutscene have separate purposes. Ordinary live Play may be paused without a native cutscene. A cutscene is skippable only after its native override exists. A room launch button requires offered derived entries; an entry/global/action-triggered native scene can be skippable even when no such launch entry is available. No scene entry or override is manufactured to make the buttons appear together.

V1/v2 audio-device selection changes the renderer only: their ordinary game word at the later engine's `VAR_SOUNDCARD` address must remain untouched. V3+ uses the source-defined device values already documented by `ScummGameSession` (`vars.cpp`: Speaker 0, Tandy 1, AdLib 3, Roland 4). No Core files were changed.

## Commands run (through RTK)

Working directory: `.worktrees/admin-playback`. Game fixtures below are read-only external folders; every harness output has its own `SCUMM_EXPLORER_PROFILE_ROOT` and `SCUMM_ROOM_CACHE_DIR`.

```powershell
rtk proxy dotnet build .verification/admin-playback-20261018/WindowCheck/WindowCheck.csproj --nologo
rtk proxy dotnet Exe/WindowCheck/Debug/net10.0-windows/WindowCheck.dll "D:\Juegos\Lucas\mmansion ega en" .verification/admin-playback-20261018/output-maniac-classic-confirmed
rtk proxy dotnet Exe/WindowCheck/Debug/net10.0-windows/WindowCheck.dll "D:\Juegos\Lucas\mmansionE ega es" .verification/admin-playback-20261018/output-maniac-enhanced-confirmed
rtk proxy dotnet Exe/WindowCheck/Debug/net10.0-windows/WindowCheck.dll "D:\Juegos\Lucas\zak ega en" .verification/admin-playback-20261018/output-zak-classic-confirmed
rtk proxy dotnet Exe/WindowCheck/Debug/net10.0-windows/WindowCheck.dll "D:\Juegos\Lucas\zakE ega en" .verification/admin-playback-20261018/output-zak-enhanced-confirmed
rtk proxy dotnet Exe/WindowCheck/Debug/net10.0-windows/WindowCheck.dll "D:\Juegos\Lucas\loom ega mt32 en" .verification/admin-playback-20261018/output-loom-ega-final
rtk proxy dotnet Exe/WindowCheck/Debug/net10.0-windows/WindowCheck.dll "D:\Juegos\Lucas\loom vga en" .verification/admin-playback-20261018/output-loom-cd-autoanswer
rtk proxy dotnet Exe/WindowCheck/Debug/net10.0-windows/WindowCheck.dll "D:\Juegos\Lucas\indy3 ega en" .verification/admin-playback-20261018/output-v3-device device-only
rtk proxy dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --filter "FullyQualifiedName~LoomKeyInputTests|FullyQualifiedName~LoomCdCutsceneTests|FullyQualifiedName~LoomBootTests|FullyQualifiedName~ScummManiacClassicCutsceneTests|FullyQualifiedName~ScummManiacEnhancedCutsceneTests|FullyQualifiedName~ScummZakClassicCutsceneTests|FullyQualifiedName~ScummZakEnhancedCutsceneTests" --nologo
```

## Results

- App and harness build: zero warnings, zero errors.
- All four Maniac/Zak actual-window runs pass: sound visible before selecting a room; ordinary Pause without Skip; zero-frame complete known-global preservation on renderer switch; immediate Pause/Space captions; native override and device lock; selection-handler lock; paused Skip resumes; immediate post-Skip state; native Skip independent of an absent room launch list; global picker remains Advanced only.
- Both LOOM actual-window runs pass: Normal sound selector, native opening override, scene lock, immediate paused caption and Skip resumes the paused scene.
- V3 actual-window regression passes: AdLib sets 3, Speaker sets 0, all unrelated words remain equal.
- Captures inspected for both Maniac editions, both Zak editions and LOOM EGA/CD. The games' room artwork and controls are rendered by the actual MainWindow. Output folders and screenshots are uncommitted.
- Focused native cutscene and LOOM Core regression: **44 passed, zero failed, zero skipped**, 1 minute 28 seconds. The parent owns final full-suite and matrix verification.

## Source comparison, assumptions and failed evidence

The four native scene cases use existing production mechanisms: Maniac's disclosed native Syd/Bernard startup preparation followed by Room 17 television object 190 / verb 4; Zak's Room 2 stereo entry. Room visits and direct verbs are exploration actions, not an earned story route. LOOM reference cases use the existing intro actions. CD uses the already supported explicit `SCUMM_CD_INTRO_AUTOANSWER=1` harness assumption for its skill question; ordinary Play continues to wait for the player. The harness holds background scene census work: this run checks controls, not the completeness of the scene catalog.

The separate launch-list contract is tested deliberately by clearing the harness's planner reference during a real paused skippable native scene. Skip remains enabled and Play Cutscene hides. This is a controlled test of independence, not a claim that a particular source scene lacks a catalog entry.

Failed first runs remain in their output folders. The initial harness incorrectly expected Speaker value 1; existing source documentation says Speaker 0. Review also caught and removed an unsafe draft assignment of the v3 sound-card address in v1/v2 before integration; final tests compare every known word across the switch. The first CD run omitted the skill-answer assumption, remained at the genuine question and failed the bounded override check. The corrected run uses the explicit existing harness mechanism. Neither failure is evidence against the native game or a reason to bypass its choices in Play.

No ScummVM executable was run, and no game data, user profile, ROM, save or junction was changed. These checks establish integration and source-driven control state, not timing, sound quality or complete visual fidelity.

## User checks

Open either Maniac or Zak edition in Normal mode: the sound selector is in the existing header. Between scenes, choose PC Speaker or PCjr/Tandy and listen. During a native scene the selector is disabled until the scene ends. Press Space and check that Pause immediately becomes Play, then press Space again. Pause a skippable scene and click Skip: it continues from the game's skip point. An unavailable Skip alongside Pause, or an active Skip without a room launch entry, is valid under the clarified requirements when the corresponding native state/list differs.

Parent requirements/checkpoint record: `docs/checkpoints/20261018-AdminStatus-ManiacZak.md` (parent owns its final filename and integration result).
