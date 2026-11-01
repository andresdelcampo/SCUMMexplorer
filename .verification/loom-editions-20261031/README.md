# LOOM's editions on the shared scene engine (31 October 2026)

| | |
|---|---|
| Item | ROADMAP work item 2b, "one scene engine for every game"; REQUIREMENTS R-8.36 |
| Fixtures | `D:\Juegos\Lucas`: `loom ega en`, `es`, `fr`, `de`, `it`, `v1.2 es`, `mt32 en`, `mt32 es`, `loom vga en` (CD) |
| Tool | `Editions/` (`dotnet run -c Release -- <folder> [max]`; `EDITIONS_LOOM_SESSION=1` runs the shared planner on LOOM's own session; `EDITIONS_TRACE=room,object,verb` shows LOOM's derived start and each shared candidate tried alone, with `EDITIONS_BOX=n` it stands Bobbin in box n and prints his box per frame); runs `run1-*.txt` (before), `run2-*.txt` (walk-box step), `run3-*.txt` (on LOOM's session). The answer key: `../loom-answer-key-20261031/Compare` with `COMPARE_LOOM_SESSION=1` |

Every edition but the English reference lists the scenes LOOM's scan finds (`LoomCutsceneVerbs.Scan`) and played them through LOOM's own path: a fresh run, else `LoomCdSceneSetupPlanner`'s derived start (and `LoomCutsceneReadiness` for story state). The harness plays each listed scene both that way and through the shared engine (`ScummCutscenePlanner.PlanSetup`, the path Indy 3, Monkey Island and Indy 4 use). "Shows nothing": started at its cutscene with no line and no other room.

## Results

| Copy | Scenes | LOOM's path begins | Shared: own way | with setup | at cutscene | shows nothing |
|---|---|---|---|---|---|---|
| EGA English, Spanish, French, German, Italian, v1.2, Spanish Roland (shared on `ScummGameSession`, before) | 216 each | 99.5% | 91.7% | 0.9% | 7.4% | 3.2% |
| EGA English Roland (same, before) | 218 | 99.5% | 91.7% | 0.9% | 7.3% | 3.2% |
| EGA English Roland, Spanish (walk-box step) | 218, 216 | 99.5% | 91.7% | 4.6% | 3.7% | 0.5% |
| CD (on `ScummGameSession`, before and with the step) | 223 | 99.6% | 91.9% | 1.8% | 6.3% | 4.5% |
| **EGA English Roland, Spanish (on LOOM's session)** | 218, 216 | 99.5% | 91.7% | **5.0%** | **3.2%** | **0.5%** |
| **CD (on LOOM's session)** | 223 | 99.6% | 91.9% | **5.4%** | **2.7%** | **0.9%** |

The seven EGA copies give the same numbers (their scripts differ in ids and bit numbering, not in what the scenes test). Every scene begins on the shared engine (100%); LOOM's path left one (Room 34's entry) without a start.

Against the English catalog's 238 reviewed replays (`../loom-answer-key-20261031/compare-run-loom-session.txt`), the shared engine on LOOM's session: all 238 begin the game's own way and reach the catalog's rooms (100%), 219 share its category (92.0%), 217 are within 10% of its length (91.2%; 212, 89.1%, on `ScummGameSession`).

## Fix 1: the hero stood in the walk box a script waits for

Most of the gap was room entries whose scene a room-local script starts when Bobbin walks into a box: Room 10's local script 200 notes his box (+0x0001 `getActorWalkBox local[0x000], 0x01`), waits for it to change (+0x000A), and box 3 begins the scene (+0x0032 `isEqual local[0x000], 0x0003`). LOOM's path stood him there (`LoomCdSceneSetupPlanner`, "box" step); the shared engine had no such step and started the scene at its cutscene. Now `ScummSceneSetupSearch` turns `getActorWalkBox` (of a literal actor or `VAR_EGO`) followed by `isEqual`/`isNotEqual` with a constant into a `WalkBox` setup value, and `ScummSession` stands the actor in that box four frames after the scene's room is entered (the waiting script must first see the box he arrived in); a box that is only a line is walked across. Generic: any game whose script watches a walk box gets it; Indy 3, Monkey Island 1 (both) and Monkey Island 2 measured unchanged; Indy 4 one scene more played its own way instead of at its cutscene, Room 76's entry (local script 200 +0x00C7 waits for Indy in walk box 15) (`../scene-setup-20261031/*-run13.txt`).

EGA copies: 8 scenes moved from "at cutscene" to "with setup" (the entries of Rooms 2, 10, 15, 17, 22, 24, 25 and 36; Room 2's waits in global script 45 +0x0016), 6 of them showed nothing before.

## Fix 2: the planner runs on any session, and LOOM's scanned scenes play through it

`ScummGameSession` boots LOOM CD only into its intro (Bobbin in no room), so the CD could not be judged there. The scene run, the boot snapshot and the planner now take any `ScummSession` (`StepFrame`, `EnterRoomAgain` and `VisitRoom` moved to `ScummSession`), so the planner runs on `LoomExplorationSession`, which the app plays LOOM on. In the app (`MainWindow.LoomCutscenes.cs`), LOOM's scanned scenes (every copy but the English reference, and the CD) now take their setup and start from the shared planner: the setup values are raised before and after the room visit, Bobbin is put at the object for an object's verb, a scene whose own code does not begin from here is started at its cutscene; the background derivation works out each plan (and records the rooms its run went through for the room lists). `LoomCdSceneSetupPlanner` keeps the room lists and the CD intro's rooms; its derived starts and `LoomCutsceneReadiness` are no longer used for these scenes (the English reference catalog keeps its own path).

## Fix 3: LOOM's Interact offers each object's answer to any other draft

Verb 0x56 is an object's answer to a draft it has no answer of its own for. 61 of the English catalog's 90 scenes the scan leaves out are these (their cutscene is global script 41, reached by more than twenty triggers), and LOOM's Interact did not offer the verb, so in every copy but the English reference they could not be reached at all. Interact now lists it as "Any other draft" (last, after the named drafts). Headless (`EDITIONS_OTHER_DRAFT=1`, English Roland, each room entered and its entry left to finish first, `other-draft-mt32-en.txt`): 95 objects have it, all 95 begin (100%), 93 print a line (97.9%), mostly "That draft won't work on ..." and longer answers such as Room 49's (17 lines). With the verb in Interact, the room's Play Cutscene list leaves out the catalog's matching "Other draft response" entries (the duplicate rule, R-8.2b): Room 2 keeps only the intro.

Real window: Room 62's wheel, "Any other draft" from Interact, shows "That draft won't work on a spinning wheel." Room 47's swan (`startScript 41`, then `print 0x01 "That draft won't work on her."`): the cutscene begins and ends but no line is printed after the window's room visit, while headless it is. Cause (1 November): LOOM's Interact ran the verb where things stood, and after the visit Bobbin was in no room of his own there, so `print 0x01` showed nothing, as ScummVM's `actorTalk` does for an actor not in the current room (`oldact = 0xFF`, return). Interact now puts Bobbin at the object first (`PutHeroAtObject`, R-8.38, as a picked scene does). Real window: "That draft won't work on her." with Bobbin by the pond ([picture](window-swan/c-1-1.png)); Room 62's wheel unchanged ("That draft won't work on a spinning wheel.").

## Real window (`../scene-setup-20261031/WindowCheck`, private profile)

- Indy 4 Room 76's entry, picked from the list, plays its own way: Indy and Sophia at the jeep, the cutscene begins with "Hold on!" about 12 s after the pick ([picture](window/76-0-0.png)).
- LOOM Spanish (`loom ega es`), Room 10's entry from the list: the room lists are ready after 55 s; the cutscene plays about 7 s on the dock and ends ([picture](window-es/10-0-0.png)). The harness now also finds LOOM's list entries (`cd-room-object-verb`).

## Not done

- Still started at the cutscene on the EGA copies (3.2%): among them Room 13's, Room 23's, Room 28's and Room 37's entries, Room 25 objects 446 and 447, Room 34's entry (which LOOM's path could not start either), Room 43 object 640.
- The English reference keeps its reviewed catalog; retiring it to titles, draft names and named sequences is the next part of item 2b.
