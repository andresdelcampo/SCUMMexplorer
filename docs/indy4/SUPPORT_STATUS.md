# Indiana Jones and the Fate of Atlantis support status

Family: Indy4 | Fixture: `Games/indy4 vga en` (`ATLANTIS.cue`) | Last updated: 18 October 2026 | Owner: S6 Indy4 agent

| Stage | State | Registry cell | Record | Notes |
|---|---|---|---|---|
| S1 Intake | done (before this file) | Rooms | | |
| S2 Reading | done (before this file) | Costumes, Scripts, Texts | | polish: ~3% of v5 script blocks unresolved |
| S3 Sound | in progress, **partial** | Sound | [v5-sound-leftovers-20261014](../../.verification/v5-sound-leftovers-20261014/README.md) | listening check listed in the run report: yes; result: pending |
| S4b Session | done (17 Oct) | – | [indy4-session-20261017](../../.verification/indy4-session-20261017/README.md) | logo (Room 68), Barnett College (Room 4) with the player in control after 721 frames; unsupported 0; no assumed input |
| S5 Objects | done (17 Oct); Full with S6 | Objects (Full) | [indy4-objects-20261017](../../.verification/indy4-objects-20261017/README.md) | 96/96 rooms without a gap; Rooms 76, 77, 87 keep a camera-after-walk item; the ten-room comparison with ScummVM is not done |
| S6 Play | done (18 Oct) | Play (Full) | [indy4-play-20261017](../../.verification/indy4-play-20261017/README.md) | route: prologue click, Esc skip, sentence, take, dialogue line with speech, crates + fire escape cutscene skip to Room 12; spoken lines from MONSTER.SOU in play; current-state images and iMUSE in play come from S6 Monkey2 (not on master yet) |
| S7 Cutscenes | not started | Cutscenes | | |
| S8 State | not started | Status | | requested in a run: no |
| S9 Editions | not started | – | | indy4 talkie vga es and classic vga es are local; the classic (floppy) release may ask the copy-protection question the talkie skips |

Blocked on: S6 Monkey2's shared play additions (current-state object images, iMUSE music in play) for the play view's images and music; the script-driven sound hooks with them.
Next: S7 Cutscenes; merge the Monkey2 play additions and re-run the window check; the user checks of the S6 record.
Open uncertainties: as Monkey Island 2. The CD image has a single data track (`TRACK 1 MODE1/2352` only), so there are no CD audio tracks to list; its music is iMUSE on the devices (456 parts, 226 loop). Session sound lengths are the iMUSE default path (estimates). A magenta line crosses Room 4's picture after the boot (not investigated).

## Decision log

| Date | Stage | Open question | Options | Choice and why | How to undo |
|---|---|---|---|---|---|
| 14 Oct | S3 | "Indy 4 CD has no tracks" (instructions, section 2) | treat as missing | not a gap: the image holds no audio track; nothing to name | none |
| 14 Oct | S3 | Loops and hooks 49 to 53 | see the Monkey Island 2 log | same shared drivers | see the Monkey Island 2 log |
| 2026-10-14 | S3 | Raise Sound to Full? | keep partial until S6 hooks / raise now | raised: the user listened and accepted the v5 sounds (loops, hooks with id 0, MI1 CD track list); the script-driven hooks stay a known limit for S6 | revert the one registry line |
| 17 Oct | S4b | Which copy is the reference? | `indy4 vga en`, `indy4 talkie vga es`, `indy4 classic vga es` | `indy4 vga en`: the fixture `SupportConformanceTests` names | change the folder in the two Indy4 test classes |
| 17 Oct | S4b | Boot parameter (copy protection) | 0 (the original's default) / -7873 (ScummVM's bypass when copy protection is off) | 0: script 1 takes the same branch for both (+0x0696/+0x06A0 to +0x3539); no protection question, no assumed input | pass -7873 in the boot |
| 17 Oct | S4b | Where does the boot end? | Script 1's room load / player control (R-15.8) | player control: Room 4 local 206 turns user input on after Marcus' first line | drop Indy4 from `BootEndsWhenPlayerHasControl` |
| 17 Oct | S4b | How long does an iMUSE sound play for `isSoundRunning`? | unknown / the S3 timeline's default path | the AdLib part's timeline (hooks 0), labelled estimate; looping songs never end | return Unknown in `ScummGameSession.ImuseLifetime` |
| 17 Oct | S4b | `soundKludge` commands | not modelled / all applied / start-stop only | start, stop, stop-all as events (they change which sounds play); others presentation, reported | the `SoundKludgeEvent` helper |
| 17 Oct | S5 | Several images per object (IM01, IM02, …) | first image only / per state | per state, as ScummVM's `getObjectImage(ptr, getState(obj))`; a state without an image draws nothing | `ScummV5DataSource.ReadObjectImages(room, states)` |
| 17 Oct | S5 | `getActorScale` with walk-box scaling not simulated | unsupported / actorOps scale as estimate | estimate, labelled | the executor's actor query |
| 18 Oct | S6 | Where does the route start? | the credits (Room 4, Script 78) / New York | both: one prologue click (Script 78) and the game's own Esc skip, then New York for the verb steps | edit the route test |
| 18 Oct | S6 | "Leave the room" in New York | back door (Biff's dialogue tree) / fire escape | fire escape: three pushes and Use, decided by the crates' scripts; Biff's way in not explored | the route's last block |
| 18 Oct | S6 | Talkie line duration | text delay / sample length | sample length after the boot (ScummVM processSfxQueues); the boot keeps text timing so S4b's pinned frames of Indy4 and MI2 talkie stay | move `SpeechJiffies` before `Boot()` |
| 18 Oct | S6 | Double room entry (Script 21's follow inside endCutscene, then the caller's) | kill the caller as ScummVM / skip a follow into the current room | skip: killing the caller changed MI2's pinned boot (control never came within 5000 frames); logged for the engine owner | `ScummSessionFrames` follow checks |
| 18 Oct | S6 | `loadString` on a seeded-pick path | skip / store | store: the literal is the instruction's own and the verbs that show it are turned on on the same path | one line in the executor |
