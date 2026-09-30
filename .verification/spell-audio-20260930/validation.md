# Spell and result audio, 30 September 2026

The user reported silence while casting notes and in spell effects/cutscenes afterwards.

## Diagnosis and change

The real-window [before probe](before/spell-audio-check.txt) and [media diagnostic](diagnostic/spell-audio-check.txt) showed that a reached note 3 could already open a 1.023-second PC-speaker track (HasAudio true, advancing position). The bug was not universal failure to render note WAVs. Other paths had real gaps:

- New/reset sessions were attached to sound only after their first input, skipping sound commands already executed by that action. Attach after construction and before actions now, excluding historical boot sounds.
- Advanced Continue/direct object verbs did not update the player. Interactive actions now process their sound commands as well as playback frames.
- The ordinary 32-frame idle limit stopped draft playback before script 020 completed all notes and dispatched script 022's result. The clock now follows bit[0xD66] until script 021 clears it, with the existing bounded scene limit. No script waits are skipped.
- Conditional events represented both commands executed along an estimated preview route and unexecuted possibilities collected from unknown branches. The executor now marks visited instructions with Executed, retaining their original certainty. Audio follows executed preview commands, explicitly logged as "preview start" and labelled in the evidence tooltip. Unexecuted possibilities remain silent; no conditional state is promoted to reached gameplay.
- Media-open and failure records make actual playback distinguishable from a start request.

## Verified behavior

[SpellAudioCheck after-02](after-02/spell-audio-check.txt): 8 PASS, zero failures. Script 018 [2] enables the distaff through the existing test seam; clicks/keys are synthetic assumed input. A note opens actual WPF audio. The single-note timeout finishes rather than stopping at the old cap. The boot's literal draft 0 (3,1,3,2, loaded by real script 023) is cast at Room 02 object 156: all four tracks open, script 022 dispatches object verb 0x3A and script 049, and its actual result sound 19 opens as an estimated preview. The draft completes. Advanced Continue also plays the four executed estimated notes from Room 13 object 325's look, labelled separately from reached audio. No physical input or independent listening proof.

The first after-01 run had one wrong harness expectation (sound 13 from generic script 041); the actual decoded route was script 049/sound 19. Corrected to its traced result, not by changing the game route. Preserved that failed report. The later VerbAreaCheck needed its old instant-playback assertion updated: var[0x062] now correctly clears to zero after script 021 completes, instead of staying at 1 in a prematurely stopped draft.

- Committed test suite: 246/246, including new regressions for executed conditional audio retaining its certainty and unexecuted branch audio retaining Executed=false. The unrelated untracked LoomObjectLookAuditTests was excluded.
- Final solution build: zero warnings/errors, after resolving two analyzer warnings in the new tests.
- [SceneSoundCheck](scene-run-01/scene-sound-check.txt): zero failures; opening music has audio and advances; trial's 28 reached start/stop events match its reference session. Music/effect rendition checks pass; new stated starts stop previous voices.
- [VerbAreaCheck final](../advanced-controls-20260930/run-03/verb-area-check.txt): final post-audio regression, including note clicking and complete-draft semantics.
- [DoorwayClickCheck](../advanced-controls-20260930/doorways-01.txt): all 16 doorway boxes across Rooms 02, 03, 04, 08, 10 and 37 pass.
- Main Release rebuilt with zero warnings/errors at `D:\Documents\Personal\Code Projects\SCUMMexplorer\Exe\SCUMMexplorer.App\Release\net10.0-windows\SCUMMexplorer.App.exe`.

No Core rendering or session transition rules changed: Executed is event provenance metadata; the App now keeps the existing session clock running long enough and follows its audio. RoomAudit, PNG refresh and Blockers were not rerun. No broad WPF suite. Fresh folders for every run; absolute RTK, explicit cwd, approved escalation. Unrelated working files were preserved.
