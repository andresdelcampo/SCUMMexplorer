# VisitProbe: verb-area click, key and script-start steps (28 September 2026)

`VisitProbe` (`.verification/opcodes/VisitProbe`) has three new steps:

| Step | What it does | Label |
| --- | --- | --- |
| `v:x,y` | `ClickVerbArea(x, y)`: a click at a screen point in the verb area (rows 144–199) | assumed input, as in the app |
| `k:0x65`, `k:e` | `PressKey(key)`: a key by code (hex, for example `k:0x1B` Escape, `k:0x13F` F5) or by character | assumed input, as in the app |
| `s:12:2` | Starts global script 0x12 with arguments [2] (decimal, comma-separated) through the `RunTopLevelScript` test seam, as a synthetic `startScript` | not game input: it stands for a start the route did not reach, such as script 003 → 018 in play |

Refused inputs print a `refused:` line (for example F8, the restart key).

## Sample: the distaff in Room 02

```
dotnet Exe/VisitProbe/Debug/net10.0/VisitProbe.dll Games\loom 02 s:12:2 !660,20 v:76,150 ?056,062 k:c ?056,057,062 k:0x1B +40 ?056,057,062,05A
```

Output: [probe-distaff.txt](probe-distaff.txt). What it shows:

1. `s:12:2`: script 018 [2] turns the distaff on; `verbs` would list 0x01–0x08 on at y 144.
2. `!660,20`: a room click on object 156 (class 1) runs scripts 005 → 006 → 007 and aims at it (`var[0x067]`).
3. `v:76,150`: verb 0x03 (slot 3) → script 005 [1, 3, 0] → script 010 → 027; `var[0x056]` = 3, `var[0x062]` = 1.
4. `k:c`: script 005 [4, 0x63, 1] → script 010 → 027; `var[0x057]` = 1, `var[0x062]` = 2.
5. `k:0x1B` then `+40`: Escape opens script 005's cutscene (+0x01C0, scripts 002/003 as hooks); after the waits, script 021 stops script 020 and script 028 clears the buffer: `var[0x062]` = 0, `var[0x05A]` = 0. The recorded notes in `var[0x056..]` stay, but the index is back at 0. This agrees with the decoded area-4 table in [key input](../key-input/validation.md).

No `src` change, so the tests, audit and WPF checks are as in [dynamic scripts](../dynamic-scripts/validation.md).
