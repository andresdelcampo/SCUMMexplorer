# Existing-save native session — 8 October 2026

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

The user corrected the debugger shortcut to **Ctrl+Alt+D** and opened the console. Automated letter keys can enter `help`; automated `type_text`, Return/Enter/keypad Enter did not submit it. Function-key menu attempts also produced no visible menu. No debugger command has executed, no flag/box/camera/actor assignment was made, and no save was overwritten. The console is left open with `help` entered but unsubmitted.

A concise console assist is pending: submit `help`, then the following **read-only** commands, one at a time. Leave the console open for screenshots/scrolling.

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

Native monitor, box flags, camera range and flag values remain **unread**. The save's suitability and the reason its first attempt showed no fortress remain unresolved. Do not promote the Explorer preparation trace to this native save's state. After obtaining the readings, a short ordinary walk or an existing-slot reload may be useful; retain the save hash and record the exact route. Never force flags, actor placement, boxes or a cutscene to manufacture eligibility.
