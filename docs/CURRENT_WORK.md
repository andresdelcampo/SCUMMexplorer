# Current work

Updated 9 October 2026. This is the single plan and session handoff. Completed work and validation belong in [CHANGELOG](CHANGELOG.md); source knowledge belongs in the game references.

## Status

Current priority: finish bounded actor-state checks, then source-earned learning/progression. Nothing has been pushed.

LOOM EGA and CD/VGA have live interaction, actors, scripts, scene playback, Game Status and audio. English Roland EGA (`Games/loom ega mt32 en`) is the replay reference; derive comparison editions from their own data. Indy EGA supports backgrounds/text and conversation export; further gameplay is deferred.

Room 43's checked arrivals and placement are accepted: the other two arrive after a couple of seconds, and the swordsman and Bobbin are correctly placed. Do not repeat this investigation. The English Rusty catalog-return correction and whole-draft WPF input checks are finished; their results are in the changelog, not new tasks.

## Next work

Take the easiest bounded checks first. Do not change behavior without a demonstrated mismatch and source evidence.

1. **Cutscene actor state — final integration in progress.** The original inventory is reviewed and preserved uncommitted. The own-edition story range correction and 168 staff/Rusty/close-up assertions are complete; see [state checks](../.verification/actor-progression-20261009/STATE_CHECKS.md). Finish the separate CD single-scene preservation correction and its WPF checkpoint. Prepared state choices remain distinct from earned history and native actor bounds.
2. **Source-earned learning/progression.** Check whether ordinary scripts earn drafts and notes while playing in Explorer. Review established routes first; prepared Game Status input is not proof of earned progression. Fix only demonstrated omissions.
3. **Audio/timing, only if straightforward.** Use ScummVM as the fidelity reference; otherwise leave this for manual comparison. No broad audiovisual audit is requested.

[Exact original-game comparison recipes](loom/FINAL_GAPS_HANDOFF.md) retain the editions, source phases and measurements still missing: Room 09/17 actor bounds, earned casting with physical keyboard/mouse input and focus, and representative CD picture/voice/music timing. Roland A/B is conditional on a specific audible discrepancy. Native control needs its own explicit resume.

Shaping's canonical label, exact reverse draft names and unidentified story roles remain uncertain. Silence and the dynamic-call classification are established. Extend names or engine behavior only when new own-source evidence justifies a visible correction.

## Product rules

- Rooms begin in their earliest state; the running session carries changes caused by real scenes. Game Status is under user control. A scene adds only its minimum prerequisites and preserves later state. Single picks keep the session; explicit sequences start fresh; Restart returns to boot.
- The approved late-room exception runs reviewed actual source prerequisites when absent. The fingerprinted English Room 46 recipe implements it; do not generalize it by guessing story bits. The disclosed off-island staff setup runs the real pickup.
- Costumes persist until source changes them. Derive actor look, placement and scene starts from own-edition scripts; no forced costumes/flags/boxes, one-off teleports or room-specific engine hacks. Label estimates and picker assumptions.
- Single click walks/uses through source scripts; double click also looks. Stored-art cycling is inspection only. Source responses and touchability govern navigation, not static possible destinations.
- Normal mode shows artwork, game text and concise controls. Technical details belong in Advanced. Preserve edition differences, palettes and verified source glitches; keep window subtitles short.
- Source execution, prepared Explorer/Core/WPF checks and original-game observation are distinct evidence. A scene completing does not prove correct actors, earned prerequisites, physical event delivery or timing.

## Preservation and workflow

Read [CLAUDE.md](../CLAUDE.md) for layout, engine facts and build instructions, and `C:\Users\adelc\.codex\RTK.md` for the mandatory shell wrapper. All shell/child commands use RTK.

Preserve game data, ROMs, saves, running applications, the native session and `%LOCALAPPDATA%\SCUMMexplorer`. Use focused checks and isolated output/profile directories; do not repeat broad audits without a relevant failure. Keep failed evidence and uncertainty. [Verification index](../.verification/README.md).

Commit finished tasks separately with explicit paths; preserve unrelated/unfinished work and do not push. Update this file for current state and CHANGELOG for completed results. Create or message another task only with direct human authorization; do not generate idle continuation chains. An existing waiting chat is `01a11cdb-3f40-78a2-a3a7-4ab351cefc34`; it must read the current handoff before continuing.

## Separately parked

- **Room 34 standalone fortress reveal**, local 200 +0035: unresolved, separate from local 206's ghost pan. Save/console/close assists are fulfilled; current native screen/state is unverified. [Preserved native evidence](../.verification/room34-state-20261008/NATIVE_SESSION.md).
- M5/secrets, Indy expansion/later games, demo carrier rooms, broad performance and optional General MIDI/card archaeology.
- Other cleanup/deletion/worktree proposals remain outside current work; see the [historical review](archive/CLEANUP_REVIEW.md).
