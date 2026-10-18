# ZakEnhanced S6 native play investigation — 17 October 2026

Fixture: `D:\Juegos\Lucas\zakE ega en`, SCUMM v2. This record distinguishes decoded game instructions and ScummVM source semantics from an original-game runtime comparison: the bundled ScummVM executable was not run. S5 original-picture, darkness and remaining object-gap uncertainties remain open.

## Reproduce

From this worktree:

```powershell
rtk dotnet run --project .verification/zakenhanced-play-20261017/PlayRoute/ZakEnhancedPlayRoute.csproj -p:RestoreIgnoreFailedSources=true -p:NuGetAudit=false -- "D:\Juegos\Lucas\zakE ega en" .verification/zakenhanced-play-20261017/baseline.txt wait,150 click,100,150 wait,2
rtk dotnet run --project .verification/zakenhanced-play-20261017/PlayRoute/ZakEnhancedPlayRoute.csproj --no-build -- "D:\Juegos\Lucas\zakE ega en" .verification/zakenhanced-play-20261017/source.txt source
```

The harness boots the game and sends only screen clicks or keys. `roomclick,x,y` converts room coordinates through the current camera and refuses points outside the view. `wait,n` advances normal frames. The source mode reads data only. Generated trace files are local evidence, not tracked assets.

## Initial baseline

At `f5d2e5c`, boot reaches Room 1 with input enabled and screen rows 16–143. The fourteen verbs are Push, Open, Walk to, Put on, Turn on, Pull, Close, Pick up, Take off, Turn off, Give, Read, What is and Use. They have native keys and positions, but the existing play layer supplies no old-engine font and refuses inputs because no later-engine `VAR_VERB_SCRIPT` is set. Shared engine owner is implementing native fixed script 4 input. This baseline does not pass S6.

## Source-derived actions

| Action | Decoded instruction |
|---|---|
| Open dresser drawer 92 | Room 1 verb 1 +0x002D sets intrinsic state; drawer rectangle (40,56,32,24), room coordinates |
| Pick up phone bill 15 | Room 1 verb 14 +0x0025 `pickupObject var[0x009]`; visible inside the opened drawer at (48,56,16,16) |
| Read held phone bill | Room 1 object 15 verb 12 +0x0028 starts Script 41 |
| Open bedroom door 84 | Room 1 verb 1 +0x0056 starts Script 10; +0x0058 sets the destination door's state |
| Walk through bedroom door | Room 1 object 84 verb 13 +0x0066 loads Room 2 via object 111 |
| Take butter knife 26 | Room 2 verb 14 +0x0026 picks it up; rectangle (400,48,24,16), requiring a genuine walk to bring it into view |
| Recover CashCard | Use plastic card 106 with a suitable held object invokes hidden object 93 at +0x006A; its +0x0013/+0x001E checks class 1 or 2, then +0x0029 picks up CashCard 18 |
| Leave apartment | Room 2 object 112 verb 13 +0x002B starts Script 29 for Zak; its alternative +0x003A loads Room 3 via stairs 134 |
| Native shop choice | Room 7 object 183 entry FD +0x002D/+0x003D creates Buy 17 and Sell 18. Merchandise Buy entries call Script 24 with their own item/price |
| Purchase response and skip | Script 24 +0x0023 rejects a purchase without CashCard; funded route +0x007B says the selected merchandise name, +0x0094 opens an override targeting +0x014E |

These candidates are inferred from this fixture, not forced story state. No Talk verb or later-engine dialogue menu is fabricated.

## Verified native input and first exit

With shared input `f0f29f2`, movement `275b72e`, built-in charset `efe8ed5`, edition identification `720e439`, and interface `01c8ad8`, two focused tests pass. The drawer sequence now verifies the visible sentence **Open dresser drawer**, opening state, phone-bill ownership, drawn inventory pixels and the real inventory hit target. W/Open, screen(50,81), sentence(10,145), wait180; D/Pick up, screen(55,78), sentence(10,145), wait180. Two normal frames separate individual inputs.

The bedroom door sequence also passes: W/Open, screen(307,81), sentence(10,145), wait180; E/Walk to, screen(307,81), wait240. Zak reaches Room2. Its entry +0x0049 starts Script126, guarded by bit060A, with initial delay600 in native timer units. Esc at its actual override resumes the skip jump, then wait120 restores Room2 and player input. Script126 +0x000D is `beginOverride`, whose jump targets +0x01B4; the trace describes resumption at the jump instruction +0x000E. The combined route waits for Room16 and `CutsceneSkippable` after leaving the bedroom, skips there, then waits for control before continuing. An earlier hypothesis that its timer started in the bedroom was rejected by the entry source and a retained run that correctly remained in Room1.

The same route's engine interpretation is supported by ScummVM [`verbs.cpp`](https://raw.githubusercontent.com/scummvm/scummvm/master/engines/scumm/verbs.cpp), `ScummEngine_v2::checkExecVerbs`: native verb shortcuts, inventory keys i/o/k/l, and sentence-area dispatch. This is a source comparison, not a ScummVM runtime check.

Walking across Room2 and opening its right door reaches Room3. Two further screen floor clicks and the 14th Avenue exit reach Room5. Both editions initially stopped at (88,116) when walking toward the shop door; this was reported to the shared engine owner. Separately, selecting the picked-up bill with native inventory key O dispatches Read15, but the initial held-object distance lookup stopped Script2. These failed runs are retained and superseded by the final verification below.

## Continuous route and retained diagnostics

`route.steps` is the headless command sequence; `route.json` contains the same screen actions plus WPF captures/assertions for the parent's real-window harness. Run the headless route with `PlayRoute` arguments `"D:\Juegos\Lucas\zakE ega en" acceptance.txt "@.verification/zakenhanced-play-20261017/route.steps"`.

Shared correction `e156b55` resolves the above doorway movement and held-object limitations. The continuous run picks up the phone bill, opens and leaves the bedroom, skips the reached alien scene, crosses Rooms2/3/5 and enters Room7. Buy17 is at screen(105,161). Clicking nose glasses40 at room(330,16), then sentence(10,145), runs object40 verb17 +0x0029 and Script24. After180 frames the shopkeeper's response contains **CashCards only!** (Script24 +0x0113). The remote-control display188 is naturally untouchable state10; it was rejected as a sale target without changing its state. Read+native inventory keyO+sentence reads the held bill and produces the reminder from Script41.

The first full run then exposed a distinct return-walk blocker: Script24 +0x02D6 requests ego(192,120), but +0x02DA kept waiting at(184,94). Read-only diagnostics found ego in NPC-only box2, flags0xA0, whose native matrix intentionally loops to itself. Shared correction `3a5a426` uses the old engine's player identity from variables42–43 for Zak, rather than the later class23 test. No room matrix, story flag or position is patched.

## Final verification

Three `ScummZakEnhancedPlayTests` pass, zero failures (44.2s): native sentence/pickup/inventory rendering; genuine bedroom exit and reached override skip; one continuous opening-to-shop route that observes the CashCard response and returns control in Room7 with phonebill15 still owned by Zak. The native inventory reset correction is included. The final route uses real clicks and keys only, with seeded random effects remaining labelled estimates.

The orchestrator ran `route.json` through the real WPF window: **2911 frames, 32 delivered inputs plus Esc**, with control returning63 frames after the recorded NPC response. Captures were inspected for the native sentence, inventory, alien cutscene, shopkeeper response, restored verbs/phonebill, and final “How embarrassing!” line. Integration evidence: `.verification/maniaczak-play-20261017/window-zakenhanced-final`. Perceived timing and original-game image comparison remain user checks; the existing S5 darkness/gap and original-picture limitations are not erased by this bounded route.

**Same route in ScummVM, described from source and not run:** open the bedroom drawer, take the phone bill, open and walk through the bedroom door; skip the delayed alien scene after its override; cross the apartment and exit to the street; walk along 14th Avenue into the pawnshop, choose Buy and the nose glasses. Without the CashCard, the shopkeeper refuses payment; Zak returns to the floor and the native controls return. This is not a full-game completion claim. S7 and S9 were not attempted.

