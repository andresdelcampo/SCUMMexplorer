Room2 object142: preposition0
Room 02 / Object 142 / Verb 0x0E  ·  02.LFL  ·  file offset 0x0800
Embedded bytecode 113 bytes
Control-flow decode: 6 instructions, 16/113 resource bytes; 0 unresolved paths. Undecoded bytes may be data or unreachable code.
Static instructions are possible paths, not proof of execution. Bounded execution reports unsupported behavior explicitly.

001E  D0 09             pickupObject var[0x009]
0020  50 A7 00          pickupObject 0x00A7
0023  29 A7 00 0E       setOwnerOf 0x00A7, 0x0E
0027  07 8C 00          setState 0x008C, IntrinsicOn
002A  57 8C 00          setState 0x008C, Untouchable
002D  00                stopObjectCode

Room2 object142: preposition0
Room 02 / Object 142 / Verb 0x01  ·  02.LFL  ·  file offset 0x0800
Embedded bytecode 113 bytes
Control-flow decode: 10 instructions, 51/113 resource bytes; 0 unresolved paths. Undecoded bytes may be data or unreachable code.
Static instructions are possible paths, not proof of execution. Bounded execution reports unsupported behavior explicitly.

002E  0F 8E 00 11 00    ifState 0x008E, IntrinsicOn, +17  ->  0x0044
0033  19 01 8E 00 00 00 00 doSentence 0x01, 0x008E, 0x0000, sub=0x00
003A  19 0E 8E 00 00 00 00 doSentence 0x0E, 0x008E, 0x0000, sub=0x00
0041  18 1C 00          jumpRelative +28  ->  0x0060
0044  10 01 A7 00       getObjectOwner var[0x001], 0x00A7
0048  48 01 0E 00 12 00 isEqual var[0x001], 0x000E, +18  ->  0x0060
004E  D8 42 61 74 74 65 72 69 65 73 21 00 printEgo ego, [text "Batteries!"]
005A  69 A7 00 00       setOwnerOf 0x00A7, var[0x000]
005E  3C 14             stopSound 0x14
0060  00                stopObjectCode

Room2 object167: preposition1
Room 02 / Object 167 / Verb 0x0B  ·  02.LFL  ·  file offset 0x0871
Embedded bytecode 43 bytes
Control-flow decode: 4 instructions, 15/43 resource bytes; 0 unresolved paths. Undecoded bytes may be data or unreachable code.
Static instructions are possible paths, not proof of execution. Bounded execution reports unsupported behavior explicitly.

001C  48 0A 54 00 08 00 isEqual var[0x00A], 0x0054, +8  ->  0x002A
0022  1A 32 02 00       move var[0x032], 0x0002
0026  29 A7 00 00       setOwnerOf 0x00A7, 0x00
002A  00                stopObjectCode

Room7 object84: preposition2
Room 07 / Object 84 / Verb 0x04  ·  07.LFL  ·  file offset 0x0BCC
Embedded bytecode 121 bytes
Control-flow decode: 8 instructions, 43/121 resource bytes; 0 unresolved paths. Undecoded bytes may be data or unreachable code.
Static instructions are possible paths, not proof of execution. Bounded execution reports unsupported behavior explicitly.

002C  38 32 01 00 11 00 isLessEqual var[0x032], 0x0001, +17  ->  0x0043
0032  1A 9C 01 00       move var[0x09C], 0x0001
0036  42 52             startScript 0x52
0038  48 32 01 00 02 00 isEqual var[0x032], 0x0001, +2  ->  0x0040
003E  42 96             startScript 0x96
0040  18 13 00          jumpRelative +19  ->  0x0056
0043  D8 49 F4 6E 65 65 64 F3 62 61 74 74 65 72 69 65 73 2E 00 printEgo ego, [text "It needs batteries."]
0056  00                stopObjectCode

