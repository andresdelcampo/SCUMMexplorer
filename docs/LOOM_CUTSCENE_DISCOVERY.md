# Cutscene discovery

The session executes reached script/object/room instructions independently of the manual menu. The menu currently uses `KnownCutscenes` (046, 052, 086) plus the opening (044), with explicit room membership and start actions. `GalleryCutscenes` uses the same entries. A naturally triggered scene is therefore playable by game input even when it has no manual catalog entry. Today, adding a manual entry requires an individual validated descriptor.

## Source-derived candidate inventory

The reproducible [discovery tool](../.verification/cutscene-discovery-20260930/discover.py) reads decoded `code.tsv`, indexes `cutscene` sites, literal `startScript`/`chainScript` callers, owner room commands and state reads. It keeps dynamic targets and unresolved local scope explicit. Run from the repository root:

```powershell
python .verification/cutscene-discovery-20260930/discover.py .verification/fallback-evidence/code.tsv .verification/cutscene-discovery-20260930/candidates.json
```

This snapshot has 235 sites across 216 owners. The [compact inventory](../.verification/cutscene-discovery-20260930/candidates.json.md) records its input hash. It is a discovery index, not reachability analysis: owner-wide room commands are possibilities, state reads are requirements to investigate, and transitive call chains do not establish a playable start. Dynamic script numbers, script arguments, object dispatch and caller room scope can require further resolution.

| Candidate | Decoded trigger | Membership / required state |
|---|---|---|
| 044, opening | Script 001 starts 044 after the intro answer | Existing validated boot stop; difficulty/draft answer assumed. Rooms 01, 02, 53. |
| 046, trial | Room 08 object 255 verb 0x38 starts 046 | Existing validated fresh-boot catalog/direct-verb start; scene rooms recorded in KnownCutscenes. |
| 052, transition | Room 10 object 295 verb 0x38 starts 052 | Existing validated start; Rooms 10, 11, 13. |
| 086, firewood | Room 37 local 200 +0x001D chains 086 after a walk-box change into box 8 | Existing reached arrival through Room 36 object 599, then assumed game click; catalog arrival into 37 alone remains estimated. Rooms 37, 58, 38. |
| 049, swans/draft reaction | Room 02 object 156 verb 0x3A starts 049 | Draft dispatch, object target, distaff and literal draft state matter. A direct invocation must not imply the draft was played. Existing draft evidence establishes one route; no new standalone descriptor added. |
| Room 02 local watcher / 045 | Frame watcher and actor walk-box conditions | Multiple transitions requiring reached position and input; not a fresh standalone scene per opcode. |
| 011 and similar helpers | Many callers, including note/draft scripts | Brief cutscene scopes around reactions/input suppression; not automatically independent menu scenes. |

## Scalable promotion

Generate candidates from current decoded resources, resolve literal/global/local call scope and argument bindings, then trace each candidate backwards to an object verb, room entry/watcher or boot instruction. Record the exact trigger, required state, assumptions, and reached room membership. Validate the start through a real session and focused playback before assigning a plain title and enabling replay. Runtime traces can record newly reached candidates for review, but cannot infer a safe reset/replay recipe alone.

Store promoted descriptors together with their evidence and validated setup, rather than adding a button for every opcode. Both the room menu and Game Texts should consume those same descriptors. Keep uncertain candidates out of Play cutscene until their starts are established. No catalog expansion was made during this investigation.
