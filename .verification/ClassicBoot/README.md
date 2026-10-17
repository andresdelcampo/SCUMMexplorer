# Classic bounded boot diagnostics — 17 October 2026

## Final ManiacClassic S4b endpoint

After b1999c4 and the v1 room-width correction, genuine assumed clicks `(76,104)`, `(196,104)`, `(292,60)` select Syd/Bernard/Start through Script4, fixed sentence Script2 and object verbs260/263/403. Each click is followed by100frames, then bounded frame advances until control. The route reaches rooms45,0,0,33,49,0,44; after1567post-boot frames Room44 has player control, no active cutscene, and named enabled verbs. Another120frames preserve room/control/cutscene state. No direct story-variable writes or synthetic room data were used. `ScummManiacClassicSessionTests` verifies this route and selected party variables48=1,49=5.

Room49 was previously omitted because the v1 decoder capped width at160tiles. The fixture stores200tiles (1600pixels) and17tile rows (136pixels). [ScummVM room.cpp653–654](https://github.com/scummvm/scummvm/blob/master/engines/scumm/room.cpp#L653) uses the complete unsigned width byte. The decoder now accepts that byte; with existing24-row limit, its maximum pixel allocation is391680bytes and integer products remain bounded. The actual room decodes217600pixels and its indexed entry script executes; no scanner fallback was added.

Validation:11passed,0failed for `ScummV1DataSourceTests`, `ScummManiacClassicSessionTests`, `ScummClassicCutsceneRestoreTests`. Trace: `maniac-trace.txt`, generated with `.verification/maniac-session-20261017/BootTrace` against `mmansion ega en select`.

S4b is complete for this bounded input route. Remaining explicit limitations: the constructor's5000-frame menu wait is expected pending input; sound54 is not recognized by the current Player_V2 lifetime reader; seeded random paths and movement/camera estimates remain labelled. This does not establish S5/S6/S7/S9, other party choices, or runtime costume-colour comparison. The historical endpoints below are superseded for ManiacClassic; ZakClassic's endpoint awaits a fresh run.

## Historical groundwork endpoint

Foundation: a568445, f9eee73 and 2797a4d (including Zak's MachineSpeed reset), plus ScummV1DataSource and factory dispatch. This record is groundwork only: neither classic S4b stage is complete. No support-registry cells changed; Maniac's accepted costume status and Zak's partial status remain.

Command from `.worktrees/maniac-classic-s4b`:

`dotnet run --project .verification/ClassicBoot/ClassicBoot.csproj --no-restore -- "D:/Juegos/Lucas/mmansion ega en" "D:/Juegos/Lucas/zak ega en"`

No game variables are injected. Each session runs its bounded constructor boot, followed by 500 frames without player input.

* ManiacClassic: Script 1 +0x0199 loads Room 45; continuation +0x019B. Constructor reaches its 5000-frame cap in the party-selection cutscene. After another 500 frames Script 1 is at +0x01F8 (delay 72), Script 168 +0x000A (delay 410000). No unsupported instruction reported on this route; party selection and later classic semantics remain unvalidated.
* ZakClassic: initial actor palette blocker +0x0015 is fixed by f9eee73 (v1 palette index is the actor number, ScummVM script_v2.cpp o2_actorOps). The next bounded stop is Script 1 +0x0038: `verbOps 0x00, [delete var[0x042]]`. No room loaded, no pending scripts. Shared executor owner was notified; this is not a v1 index failure.

Colour extraction is static evidence, not runtime comparison. Fresh ZakClassic extraction now yields 35 costumes: 24 derived and 11 estimates (4, 5, 6, 7, 8, 9, 12, 13, 14, 15, 32). The older 25/10 summary predates corrected v1 decoding and object-code bounds and is superseded.

## Classic cutscene restoration follow-up

After integrating b9a5a5f, the session now saves the room and camera mode alongside v1/v2 cutscene user state. The profile selects classic Maniac's restoration path: restore the saved camera mode; follow the current ego when that mode was follow, otherwise request the saved room only when it differs. All other v1/v2 families follow ego, including a room change for classic Zak. Room loads use the existing bounded dispatch. No saved camera coordinate or destination is invented: those fields are absent from ScummVM's `cutSceneData`.

Source: [ScummVM script_v2.cpp o2_cutscene/o2_endCutscene](https://github.com/scummvm/scummvm/blob/master/engines/scumm/script_v2.cpp#L1464), especially saved room/mode at1467–1468 and the classic Maniac branch at1494–1502. Profile also exposes `V2VerbYOffset` (classic Maniac8, others0), from o2_verbOps; runtime wiring belongs to the shared executor integration.

Validation: `dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-restore --filter "FullyQualifiedName~ScummClassicCutsceneRestoreTests|FullyQualifiedName~ScummV2SessionVariableTests" -v minimal`: 12 passed,0 failed. Seven focused restore cases cover normal/panning saved room, same-room destination preservation, classic saved follow mode, enhanced and Zak follow regression, and null-room return. These are native-state tests, not completion of the fixture's boot or S4b.

Adapter/factory validation covers both classic fixtures' index owners/states, all discovered room dimensions, verb entries and tiled images, plus enhanced adapter regression and engine-variable reset tests. Command: `dotnet test tests/SCUMMexplorer.Tests/SCUMMexplorer.Tests.csproj --no-restore --filter "FullyQualifiedName~ScummV1DataSourceTests|FullyQualifiedName~ScummManiacEnhancedSessionTests|FullyQualifiedName~ScummV2SessionVariableTests" -v minimal`: 8 passed, 0 failed. Maniac's fresh colour scan also remains 23 of 23 derived.
