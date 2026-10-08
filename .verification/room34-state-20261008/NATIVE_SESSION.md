# Existing-save native session — 8 October 2026

**Current status:** this chronology preserves the completed save/console/close assists and ordinary eastward route. The standalone Room 34 fortress reveal is parked. The final recorded user report was a return to Room 34, followed by physical Escape stopping Computer Use before a refreshed capture. Current native screen/state is unverified; do not resume control without a direct human request. Earlier descriptions of an open debugger or proposed next walk are superseded. The separate Explorer catalog-return correction is in [its own checkpoint](../catalog-return-20261008/README.md).

The user supplied an existing save and left ScummVM in Rusty's outdoor room, authorizing a short native investigation. Their first attempt did not show the fortress. This supersedes the earlier optional request for a save; do not request another save or a whole-game replay.

## Identity established

- Running window: `Loom (EGA/DOS/English)`.
- Executable: `scummvm/scummvm.exe`; ScummVM 2.2.0, build 14 September 2020 10:38:39. The process had no command-line game overrides.
- Target `[loom]` in the existing ScummVM configuration: game path `Games/loom ega mt32 en`, save path `Games/loom ega en`, AdLib driver. Configuration was read, not changed.
- Running target's encoded `34.LFL` SHA-256: `726AB2C92507FF64CA653394469BA73303EC9C435FDC2453105BB5FA33472AC6`, identical to the English Roland EGA source packet. The plain-English folder's room hash differs; the save directory alone must not be used as an edition identifier.
- New slot: `Games/loom ega en/loom.s13`, 14,512 bytes, description `Rusty`, modified 8 October 2026 18:40:33 local. Compressed SHA-256 `F80D6831D15FE16D17033B416D345B98920392A5A974483CB3A2E60A1B8006F6`.
- Its read-only gzip header inspection gives `SCVM`, serializer version 99, and 98,441 decompressed bytes. Only header metadata was interpreted; no saved variables, actors or script slots were decoded or inferred from arbitrary byte patterns.

The matching [save header source](https://github.com/scummvm/scummvm/blob/v2.2.0/engines/scumm/saveload.cpp) defines the 44-byte header (tag, size, version, 32-byte description). Original game/save bytes and configuration stay out of Git.

## Actions and limits

The computer-use skill was applied through its documented `sky` API. A single ground click near the middle of the clearing was accepted: the blue-cloaked staff-holding actor walked from the left toward that point. No room change or fortress scene was observed during that short movement. This was not the required 6-to-8 crossing, and does not establish the actor's numeric box or the current story phase. An earlier commentary description of the blue actor as Rusty's ghost was premature; the visible moving actor holds Bobbin's staff, and numeric identity/state remain to be read.

During the initial automation attempt, the user corrected the debugger shortcut to **Ctrl+Alt+D** and opened the console. Automated letter keys could enter `help`; automated `type_text`, Return/Enter/keypad Enter did not submit it. Function-key menu attempts also produced no visible menu. At that point no debugger command had executed and the console was left with `help` unsubmitted. The user's later screenshots below supply the readings. No flag/box/camera/actor assignment was made, and no save was overwritten.

The user fulfilled the console assist by supplying two screenshots of the following **read-only** queries. The submission problem was with automation; the native console itself accepts these commands.

```text
scripts
camera
actors
box 6 7 8
scumm_vars[214]
scumm_vars[215]
scumm_vars[220]
```

Matching [ScummVM debugger source](https://github.com/scummvm/scummvm/blob/v2.2.0/engines/scumm/debugger.cpp) registers these queries; [array query syntax](https://github.com/scummvm/scummvm/blob/v2.2.0/gui/debugger.cpp) permits `scumm_vars[index]` with no second argument. **Do not add a value**: a second argument is a write. For this EGA resource, backing word 214 contains D6D (input), 215 contains D71/D7A (entry gate/late state), and 220 contains DC6/DC7/DCF (ghost/restoration/completion). Decode those bits by their own masks; raw word values are not themselves boolean flags. `scripts` must distinguish active local 200 from stale/inactive slot numbers.

## Native readings supplied by the user

The screenshots are retained locally as ignored `out/native-console-scripts-camera-actors.png` and `out/native-console-boxes-vars.png`. They establish the live state at the user's query time, after the short ground walk; they do not independently deserialize slot 13 or establish its earlier history.

| Query | Native result |
| --- | --- |
| `scripts` | Slot 1: global 6, offset `001bf`, status 2, type 2; slot 4: local 200, offset `0a127`, status 2, type 3. Both have freeze/recursive/freeze-count/cutscene columns zero. |
| `camera` | Current/destination/last `(160,100)`, acceleration `(0,0)`. This does not read the minimum/maximum camera range. |
| `actors`, actor 1 | `(148,90)`, width 24, height 45, elevation 0, costume 1, box 2, moving 0, z-plane 0, frame 3, scale 255, direction 90, class `0x00400000`. |
| `actors`, actor 3 | `(72,106)`, width 24, height 10, elevation 0, costume 112, box 255, moving 0, frame 3, scale 255, direction 180, class `0x00280000`. |
| `box 6` | `(246,64)` to `(246,80)` vertical line; flags `80`, mask 1, scale 255. Hidden. |
| `box 7` | `(248,82)` to `(296,82)` horizontal line; flags `00`, mask 1, scale 255. |
| `box 8` | `(298,82)` to `(332,79)` line; flags `00`, mask 1, scale 255. |
| `scumm_vars[214]` | 1923 = `0783` hex. |
| `scumm_vars[215]` | 34848 = `8820` hex. |
| `scumm_vars[220]` | 25089 = `6201` hex. |

Matching [script status enum](https://github.com/scummvm/scummvm/blob/v2.2.0/engines/scumm/script.h#L80-L85) gives status 2 = running; [script location enum](https://github.com/scummvm/scummvm/blob/v2.2.0/engines/scumm/object.h#L315-L316) gives type 2 = global, 3 = local. Local 200 is therefore active, not merely a stale numbered slot. Its displayed offset is a native resource-relative instruction pointer, not the diagnostic local-code offset `+0020`.

| English EGA role | Backing word / bit | Native value |
| --- | --- | --- |
| Entry gate D71 | 215 / 1 | 0 |
| Late state D7A | 215 / 10 | 0 |
| Crossing complete DCF | 220 / 15 | 0 |
| Input/cutscene guard D6D | 214 / 13 | 0 |
| Ghost conversation DC6 | 220 / 6 | 0 |
| Restoration DC7 | 220 / 7 | 0 |

This is a native example of the **early eligible monitor with box 6 hidden**, matching the prepared fresh-room/source result. It rules out the late-ghost camera branch as the explanation for this particular sample. It does not reveal the monitor's preceding observed box, demonstrate a 6-to-8 transition, establish the camera range, explain the user's earlier attempt, or prove the fortress scene unused. All three monitor guards are clear, but the actor is presently settled in box 2, not at the crossing.

## Ordinary native eastward route

After the user closed the debugger and authorized the walk, the agent made two ordinary mouse clicks in the 1124×896 window. Rusty was not woken and no draft was cast.

1. From the established `(148,90)`/box-2 sample, click window `(890,417)`. This selected a **grave**, as shown by the native item icon/caption, and Bobbin walked toward it while the camera followed. It was an object-selection walk, not the intended bare-ground click. No numerical endpoint/box reading was obtained.
2. From the scrolled view, click window `(1010,412)` on the eastward path. Bobbin moved right and the game entered the next screen, showing the fortress/guild gate. The user independently confirmed the screen transition. Its numerical room ID was not queried.
3. The user reopened the debugger and queried `scumm_vars[220]`: **25089 again**, unchanged `6201` hex, so completion bit DCF remained clear. The agent also observed the fresh query in the native console. Local 200 writes DCF immediately after its cutscene instruction; this ordinary route did not run its normal fortress-reveal start sequence. No per-frame native box trace was obtained, and the result does not prove every legitimate route fails.

An automated Ctrl+Alt+D attempt after the first click also produced no console; native keyboard submission/shortcuts remain unavailable through this automation. The user performed the final read-only query. At the end of this earlier route the game was left in that debugger on the next screen; the later close/return chronology below supersedes that screen description. Original saves/configuration remain untouched.

## Separate late ghost-conversation camera move

The user suggested that the missing walking-triggered scene might occur on the later return with Rusty's ghost. Source inspection found **another** pan inside that conversation: global 98 starts local 206 at `+03FA` English EGA, `+040A` Spanish EGA, `+0572` VGA. In all three local 206 opens the range at `+0023`, pans to x=464 at `+0029`, and walks Bobbin to `(388,88)` at `+003C`.

The existing English prepared Explorer trace corroborates that separate callback: global 98 starts 206 at frame 4743; local 206 executes the scroll-range instruction at frame 4773; the camera reaches 464 at frame 4811 during the `ghost` phase with UserPut=0. This reuses the existing ignored `out-transitions-checked/ega-en-transitions.json`; it is not a new native late-state run.

**The user clarified that this is a different scene.** The conversation's own pan must not be treated as resolving the missing standalone local-200 fortress reveal. That walking scene remains a separate reachability question. Entry source skips local 200 in the late state, and the ghost conversation does not restart it; the later reported native return supplied no refreshed monitor/guard readings before Computer Use stopped. Keep these events distinct and do not manufacture the monitor/box state. The early native route above is completed evidence, not grounds to request a whole-game replay.

## Follow-up console inspection

The continuation chat read both user-supplied screenshots, then inspected the current console showing their submitted results. After the recording chat finished, it tried one documented `Escape` key to close the console before a short ordinary walk. The key was ignored and the console remained open. No walk, room change, slot reload, debugger query or state assignment followed; this is an automation-input limit, not a negative crossing result. Both the save and read-only console assist remain fulfilled. [Independent staff/Room 17 source work](../staff-layout-20261008/README.md) preserves the existing working staff alignment and records the complete native opcode adjustments.

The next continuation inspected the same native window once through the documented `sky` API. The debugger was still open, displaying the submitted box/word readings above. It issued no input and did not retry Escape, Enter, text submission or menu keys. Slot 13's compressed SHA-256 was reverified as `F80D6831D15FE16D17033B416D345B98920392A5A974483CB3A2E60A1B8006F6`. At that checkpoint the UI prerequisite was a manual debugger close through working human input; the user was asked only to close that overlay and report when the game view was visible. That request was fulfilled below. No ordinary crossing walk, reload or new query occurred. At that checkpoint a short source-geometry-derived ground walk was proposed after closing the console; later instructions parked further investigation. A non-triggering walk would be bounded evidence, not unreachability proof.

The user then replied **“closed.”** A refreshed view confirmed that the console was closed. The first capture was partly occluded by the Explorer window; activating the existing ScummVM window and refreshing exposed a fortress interior with a standing staff-holding actor. This picture differs from the decoded English Roland Room 34 outdoor clearing. The existing `DemoProbe` rendered Room 34 and three nearby candidate backgrounds (41/43/46) into ignored `out/current-room*-reference.bmp`; none matched the current interior. These are resource-layout references with prepared Explorer state, not native room-variable readings or independent renderer validation. The current numeric room, story phase and cause of the changed view remain unknown. The user was asked whether they moved the game after the readings or this view appeared on closing the console. No ground click, debugger query, reload or state assignment was issued. The manual-close request is fulfilled; it must not be repeated. An ordinary Room 34 route cannot be inferred from the earlier coordinates while a different layout is visible.

The user subsequently stated **“I moved him back to 34.”** The attempted follow-up activation/capture was stopped by the user's physical Escape key, so no new Room 34 screenshot was returned and no walking click was issued. Computer Use stopped immediately; do not resume it without a direct human request. A read-only `CutsceneScanCompare ... boxes 34` resource export confirmed the recorded English EGA coordinates for boxes 6/7/8. Resource defaults are not current native flags (the earlier native sample hid box 6). The reported return to Room 34 is user context, not fresh monitor/guard/camera evidence. The running native game is preserved; this chat observed neither the crossing nor a negative crossing result. This continuation changes records only and does not justify a product correction or a broad test rerun.
