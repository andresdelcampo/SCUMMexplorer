# ZakClassic S6 native play investigation — 17 October 2026

Fixture `D:\Juegos\Lucas\zak ega en`, SCUMM v1. Independent worktree and execution against this edition; no Enhanced results are substituted for Classic evidence. The bundled ScummVM executable was not run. S5 visual acceptance and Classic costume-colour estimates remain open.

## Reproduce

```powershell
rtk dotnet run --project .verification/zakclassic-play-20261017/PlayRoute/ZakClassicPlayRoute.csproj -p:RestoreIgnoreFailedSources=true -p:NuGetAudit=false -- "D:\Juegos\Lucas\zak ega en" .verification/zakclassic-play-20261017/source.txt source
rtk dotnet run --project .verification/zakclassic-play-20261017/PlayRoute/ZakClassicPlayRoute.csproj --no-build -- "D:\Juegos\Lucas\zak ega en" .verification/zakclassic-play-20261017/drawer.txt wait,150 key,119 wait,2 roomclick,50,65 wait,2 click,10,145 wait,180 key,100 wait,2 roomclick,55,62 wait,2 click,10,145 wait,180
```

Only native screen clicks, verb keys and normal frame advancement are used. The source mode only decodes this fixture. No scene visits, direct sentences or state assignments occur.

## Verified opening sequence

The actual boot reaches Room 1, screen top16/bottom144, with native verbs at rows152/160/168. W selects Open. Clicking screen(50,81) selects drawer92 and composes the native sentence; clicking sentence area(10,145) executes it through Script4 and Script2, opening the drawer. D selects Pick up. Clicking screen(55,78) selects exposed phonebill15; clicking sentence area(10,145) executes its verb14, yielding owner15=1. Trace contains Script2 verb1/object92 and verb14/object15. The two-step sentence is the game's own behavior.

Two focused tests pass with the shared old-engine walk matrix and native interface renderer. They pin the visible sentence `Open dresser drawer`, rendered phone-bill inventory pixels, inventory hit target screen(180,177), the bedroom door's state and native load into Room2, and Esc from Script126's reached override followed by restored player control. Script126 begins with a delay600 and can interrupt a longer opening route; skip only when `CutsceneSkippable` is true, then wait for control.

An independently executed longer route initially reached Room3 and Room5 but stopped at (88,116) when walking toward shop door161. Shared `e156b55` fixed native direction remapping and held-object distance/code lookup. The completed route reaches Room7 and selects Buy17 plus nose glasses40 at room(330,16), then the native sentence line. Script24 prints the shopkeeper's CashCard response. Returning to the floor initially stalled in NPC-only box2; shared `3a5a426` corrected old player identity, preserving the original box matrix and story state.

## Completed route and source evidence

`route.steps` drives the headless harness; `route.json` is the equivalent real-window sequence, with sentence, inventory, cutscene and choice captures. Run `PlayRoute` with arguments `"D:\Juegos\Lucas\zak ega en" acceptance.txt "@.verification/zakclassic-play-20261017/route.steps"`. No Enhanced output is reused as a Classic result.

| Native step | Classic source evidence |
|---|---|
| Compose and execute | Script4 +0x01B4 draws the sentence; +0x01DC dispatches it through Script2 |
| Open drawer92 / take bill15 | Room1 object92 verb1 +0x002D sets intrinsic state; object15 verb14 +0x0025 picks it up |
| Bedroom exit | Room1 object84 verb13 +0x0066 loads Room2 through object111 |
| Reached cutscene skip | Room2 entry +0x0049 starts Script126; +0x000D override resumes at jump+0x000E and cleans up from+0x01B4 |
| Exit apartment / cross street | Script29 +0x0186 loads Room3; object141 fallback +0x0020 loads Room5; Room5 object161 verb13 reaches Room7 |
| Native choice | Room7 object183 FD +0x002D/+0x003D creates Buy17/Sell18; nose glasses40 verb17 +0x0029 starts24; Script24 +0x0113 gives the shopkeeper's response |
| Return control | Script24 +0x02D6 walks Zak to the floor; +0x02EE ends the cutscene |

The equivalent original-engine route is described by those data and ScummVM's [`ScummEngine_v2::checkExecVerbs`](https://raw.githubusercontent.com/scummvm/scummvm/master/engines/scumm/verbs.cpp): native verb, inventory and sentence inputs. In ScummVM the same actions open the dresser, take the bill, leave the bedroom, skip the alien scene, walk through the apartment/street to the shop, choose Buy nose glasses, receive the refusal and return control. This is source comparison, not execution of the prohibited ScummVM binary.

## Final verification

Three `ScummZakClassicPlayTests` pass, zero failures (43.7s), including one continuous boot-to-shop route with native choice response and returned control. The final state is Room7, phonebill15 owned by Zak, input enabled and no active cutscene. Earlier failed diagnostics remain local evidence.

The orchestrator's real WPF run also passes: **2911 frames, 32 delivered inputs plus Esc**, control restored63 frames after the NPC response. Captures inspected the native sentence, visible phonebill inventory, reached alien scene, shopkeeper response, restored verbs/inventory and final “How embarrassing!” line. Evidence: `.verification/maniaczak-play-20261017/window-zakclassic-final` in the integration worktree. S5 original-picture/darkness limits and Classic estimated costume colours remain explicit; perceived timing remains the user's check. S7 and S9 were not attempted.
