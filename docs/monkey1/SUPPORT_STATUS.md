# Monkey Island 1 CD support status

Family: MonkeyCd | Fixture: `Games/FUTURE/monkey1 vga en` (registry path; the folder now sits at `Games/monkey1 vga en`, `The_Secret_of_Monkey_Island.cue`) | Last updated: 14 October 2026 | Owner: S3-leftovers agent

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
| S9 Editions | not started | – | | the disc also carries ENGLISH, FRENCH, GERMAN, ITALIAN, SPANISH data folders |

Blocked on: S6 for the sound hooks (see the record, section 4).
Next: the user listens to the loops (list in the run report); S4b.
Open uncertainties: disc track 21 (16 s) is started by no sound and stays unnamed; six `startSound` operands are variables (script 101 `var[100]`, script 162 `local[4]`, room 75 local script 205 `local[0]`), which sound they start needs running scripts.

## Decision log

| Date | Stage | Open question | Options | Choice and why | How to undo |
|---|---|---|---|---|---|
| 14 Oct | S3 | The sound's CD track byte against the cue's track numbers | (a) byte = cue number, as written on 12 Oct; (b) byte counts the audio tracks from 1 | (b): the disc has 24 audio tracks (cue 2 to 25) and the sounds name 1 to 24; the data track cannot be played; room names and one-shot lengths fit. Cue-number labels in the list are unchanged, the rooms/sounds shown are now right | revert the `gameTrack` change in `GameFolderScanner` |
| 14 Oct | S3 | Name disc track 21? | name it from a guess / leave unnamed | unnamed: no sound, no direct start (only LOOM CD has one) and no literal script start names it (R-2.1) | none needed |
| 14 Oct | S3 | How long a looping song is rendered | one pass; 3 minutes (the preview limit already used by the other devices); more | 3 minutes, ending at the first jump back at or after it (hard cap 420 s) | `ScummImuseTimeline.MinLoopSeconds` |
