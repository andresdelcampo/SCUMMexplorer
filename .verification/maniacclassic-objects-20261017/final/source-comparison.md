# Static source comparison

Entry/local instructions ranked by count; this is not original-game observation. Named operations may be on branches not taken.

| Room | Instructions | Outcome/frames | Active images | Decoded state operations and child starts |
|---|---|---|---|---|
| 30 | 23 | FrameLimit/2400 | 178 | +0x0004 putActorInRoom 0x17, 0x1E; +0x0014 putActorInRoom 0x09, 0x1E; +0x001B ifState 0x00B0, IntrinsicOn, +2  ->  0x0022; +0x0028 startScript 0x7F; +0x0030 startScript 0x7F; +0x003B startScript 0x81; +0x004A startScript 0x7D |
| 51 | 16 | Settled/137 |  | +0x000D putActorInRoom 0x10, 0x33; +0x0026 startScript 0x80; +0x0028 ifState 0x00B0, IntrinsicOn, +5  ->  0x0032 |
| 03 | 10 | Settled/90 | 75,76,78,79 | +0x0000 startScript 0x37; +0x000D ifState 0x00E4, IntrinsicOff, +8  ->  0x001A |
| 12 | 9 | Settled/174 |  | +0x0000 startScript 0x37; +0x000E putActorInRoom 0x0E, 0x0C; +0x001E startScript 0x33 |
| 19 | 8 | Settled/90 |  | +0x000E startScript 0x37; +0x0019 startScript 0x1F |
| 06 | 7 | Settled/90 |  | +0x0006 ifState 0x008B, IntrinsicOn, +6  ->  0x0011 |
| 17 | 7 | Settled/90 | 30,192 | +0x0000 startScript 0x37 |
| 27 | 7 | Settled/90 |  | +0x0000 putActorInRoom 0x13, 0x1B; +0x000A startScript 0x37; +0x000C startScript 0x34; +0x000E startScript 0x36 |
| 01 | 6 | Settled/90 | 56,57,62,63,64 | +0x000F startScript 0x39 |
| 07 | 5 | Settled/90 | 88 | +0x0000 startScript 0x37; +0x0002 ifState 0x0058, IntrinsicOn, +2  ->  0x0009; +0x0009 startScript 0x51 |
| 02 | 4 | Settled/91 |  | No room-entry/local state writes or child starts; index and global scripts still apply. |
| 10 | 4 | Settled/217 | 455 | +0x0000 startScript 0x37; +0x0002 startScript 0x18 |

