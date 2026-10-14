# Monkey Island 2 support status

Family: Monkey2 | Fixture: `Games/FUTURE/mi2 talkie en` (registry path; the folder now sits at `Games/mi2 talkie en`; `mi2 mt32 en` and `mi2 sb16 en` for the Roland and Sound Blaster data) | Last updated: 14 October 2026 | Owner: S3-leftovers agent

| Stage | State | Registry cell | Record | Notes |
|---|---|---|---|---|
| S1 Intake | done (before this file) | Rooms | | |
| S2 Reading | done (before this file) | Costumes, Scripts, Texts | | polish: ~3% of v5 script blocks unresolved |
| S3 Sound | in progress, **partial** | Sound | [v5-sound-leftovers-20261014](../../.verification/v5-sound-leftovers-20261014/README.md) | listening check listed in the run report: yes; result: pending |
| S4b Session | not started | – | | |
| S5 Objects | not started | Objects (partial) | | |
| S6 Play | not started | Play | | the script-driven sound hooks wait for this |
| S7 Cutscenes | not started | Cutscenes | | |
| S8 State | not started | Status | | requested in a run: no |
| S9 Editions | not started | – | | mi2 classic es, mi2 talkie es are local |

Blocked on: S6 for the hooks that wait for a script id (jump hooks with id other than 0, part on/off 50, volume 51, program 52, transposes 49/53), script loop control, markers, `isSoundRunning` and music-timer waits, concurrent-sound special cases (`startSound_internal`: sounds 100, 107, 113, 115, 118).
Next: the user listens to the loops; S4b.
Open uncertainties: loops are taken at the from-point to the tick (ScummVM: first 250 Hz timer step at or after it); where a looping song stops in the wave (3 minutes) is ours.

## Decision log

| Date | Stage | Open question | Options | Choice and why | How to undo |
|---|---|---|---|---|---|
| 14 Oct | S3 | Loop sysex 0x7D 80 | keep "end here" / port `setLoop` | port: ScummVM sets a loop (count, to, from) and jumps at the from-point; "end here" rendered some songs (sound 120) as a tenth of a second | `ScummImuseTimeline` case 80 |
| 14 Oct | S3 | Hooks 49 to 53 with id 0 were ignored | keep / act at once | act at once as `maybe_*` do; changes what is heard (parts switched off by a hook stay off in the default path) | remove the cases 49 to 53 in the three drivers |
| 14 Oct | S3 | Loop length | one pass / 3 minutes | 3 minutes at the first jump back at or after it, like the preview limit of the other devices | `MinLoopSeconds` |
