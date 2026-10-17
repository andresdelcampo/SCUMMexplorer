# Decoded first-visit source comparisons

Ten heaviest rooms by entry/exit/object-verb decoded instruction count, plus opening Room44 and Room1. This is static source comparison, not original-game execution.

## Room 18: 317 decoded instructions across room blocks
```text
Room 18 / Entry script  ·  18.LFL  ·  file offset 0x2BF2
Embedded bytecode 3 bytes
Control-flow decode: 2 instructions, 3/3 resource bytes; 0 unresolved paths. Undecoded bytes may be data or unreachable code.
Static instructions are possible paths, not proof of execution. Bounded execution reports unsupported behavior explicitly.

0000  42 37             startScript 0x37
0002  00                stopObjectCode

```
Entry +0x0000 calls Script55, 17 decoded instructions. Its literal object-state operations (all branches):
## Room 01: 224 decoded instructions across room blocks
```text
Room 01 / Entry script  ·  01.LFL  ·  file offset 0x4A42
Embedded bytecode 7 bytes
Control-flow decode: 3 instructions, 7/7 resource bytes; 0 unresolved paths. Undecoded bytes may be data or unreachable code.
Static instructions are possible paths, not proof of execution. Bounded execution reports unsupported behavior explicitly.

0000  70 0B 00 00       lights 0x0B, 0x00, 0x00
0004  3C 20             stopSound 0x20
0006  00                stopObjectCode

```
## Room 07: 200 decoded instructions across room blocks
```text
Room 07 / Entry script  ·  07.LFL  ·  file offset 0x22F7
Embedded bytecode 12 bytes
Control-flow decode: 5 instructions, 12/12 resource bytes; 0 unresolved paths. Undecoded bytes may be data or unreachable code.
Static instructions are possible paths, not proof of execution. Bounded execution reports unsupported behavior explicitly.

0000  42 37             startScript 0x37
0002  0F 58 00 02 00    ifState 0x0058, IntrinsicOn, +2  ->  0x0009
0007  1C 11             startSound 0x11
0009  42 51             startScript 0x51
000B  00                stopObjectCode

```
Entry +0x0000 calls Script55, 17 decoded instructions. Its literal object-state operations (all branches):
Entry +0x0009 calls Script81, 11 decoded instructions. Its literal object-state operations (all branches):
## Room 16: 143 decoded instructions across room blocks
```text
Room 16 / Entry script  ·  16.LFL  ·  file offset 0x2DEB
Embedded bytecode 33 bytes
Control-flow decode: 11 instructions, 33/33 resource bytes; 0 unresolved paths. Undecoded bytes may be data or unreachable code.
Static instructions are possible paths, not proof of execution. Bounded execution reports unsupported behavior explicitly.

0000  70 0B 00 00       lights 0x0B, 0x00, 0x00
0004  3C 20             stopSound 0x20
0006  4F 95 00 0C 00    ifState 0x0095, IntrinsicOff, +12  ->  0x0017
000B  30 02 80          setBoxFlags 0x02, 0x80
000E  30 03 80          setBoxFlags 0x03, 0x80
0011  30 04 80          setBoxFlags 0x04, 0x80
0014  18 09 00          jumpRelative +9  ->  0x0020
0017  30 02 00          setBoxFlags 0x02, 0x00
001A  30 03 00          setBoxFlags 0x03, 0x00
001D  30 04 00          setBoxFlags 0x04, 0x00
0020  00                stopObjectCode

```
## Room 30: 128 decoded instructions across room blocks
```text
Room 30 / Entry script  ·  30.LFL  ·  file offset 0x2FD1
Embedded bytecode 86 bytes
Control-flow decode: 25 instructions, 86/86 resource bytes; 0 unresolved paths. Undecoded bytes may be data or unreachable code.
Static instructions are possible paths, not proof of execution. Bounded execution reports unsupported behavior explicitly.

0000  70 0B 00 00       lights 0x0B, 0x00, 0x00
0004  30 05 00          setBoxFlags 0x05, 0x00
0007  2D 17 1E          putActorInRoom 0x17, 0x1E
000A  01 17 24 44       putActor 0x17, 0x24, 0x44
000E  3D 17 04          setActorElevation 0x17, 0x04
0011  30 05 80          setBoxFlags 0x05, 0x80
0014  11 17 FA          animateActor 0x17, 0xFA
0017  11 17 0C          animateActor 0x17, 0x0C
001A  2D 09 1E          putActorInRoom 0x09, 0x1E
001D  01 09 26 42       putActor 0x09, 0x26, 0x42
0021  0F B0 00 02 00    ifState 0x00B0, IntrinsicOn, +2  ->  0x0028
0026  1C 3B             startSound 0x3B
0028  48 6C 01 00 02 00 isEqual var[0x06C], 0x0001, +2  ->  0x0030
002E  42 7F             startScript 0x7F
0030  48 6C 02 00 02 00 isEqual var[0x06C], 0x0002, +2  ->  0x0038
0036  42 7F             startScript 0x7F
0038  68 01 2B          isScriptRunning var[0x001], 0x2B
003B  48 01 00 00 11 00 isEqual var[0x001], 0x0000, +17  ->  0x0052
0041  42 81             startScript 0x81
0043  48 6C 00 00 09 00 isEqual var[0x06C], 0x0000, +9  ->  0x0052
0049  1A 6C 01 00       move var[0x06C], 0x0001
004D  0C 7B 51          resourceRoutines 0x7B, 0x51
0050  42 7D             startScript 0x7D
0052  0C 36 61          resourceRoutines 0x36, 0x61
0055  00                stopObjectCode

```
Entry +0x002E calls Script127, 7 decoded instructions. Its literal object-state operations (all branches):
Entry +0x0036 calls Script127, 7 decoded instructions. Its literal object-state operations (all branches):
Entry +0x0041 calls Script129, 6 decoded instructions. Its literal object-state operations (all branches):
Entry +0x0050 calls Script125, 35 decoded instructions. Its literal object-state operations (all branches):
- Script125 +0x0004: setState 0x00A9, IntrinsicOff
## Room 17: 125 decoded instructions across room blocks
```text
Room 17 / Entry script  ·  17.LFL  ·  file offset 0x1DF1
Embedded bytecode 20 bytes
Control-flow decode: 6 instructions, 20/20 resource bytes; 0 unresolved paths. Undecoded bytes may be data or unreachable code.
Static instructions are possible paths, not proof of execution. Bounded execution reports unsupported behavior explicitly.

0000  42 37             startScript 0x37
0002  48 A7 01 00 0B 00 isEqual var[0x0A7], 0x0001, +11  ->  0x0013
0008  48 5E 19 00 05 00 isEqual var[0x05E], 0x0019, +5  ->  0x0013
000E  1C 19             startSound 0x19
0010  18 00 00          jumpRelative 0  ->  0x0013
0013  00                stopObjectCode

```
Entry +0x0000 calls Script55, 17 decoded instructions. Its literal object-state operations (all branches):
## Room 03: 124 decoded instructions across room blocks
```text
Room 03 / Entry script  ·  03.LFL  ·  file offset 0x2BB1
Embedded bytecode 33 bytes
Control-flow decode: 9 instructions, 33/33 resource bytes; 0 unresolved paths. Undecoded bytes may be data or unreachable code.
Static instructions are possible paths, not proof of execution. Bounded execution reports unsupported behavior explicitly.

0000  42 37             startScript 0x37
0002  68 01 15          isScriptRunning var[0x001], 0x15
0005  48 01 01 00 02 00 isEqual var[0x001], 0x0001, +2  ->  0x000D
000B  1C 19             startSound 0x19
000D  4F E4 00 06 00    ifState 0x00E4, IntrinsicOff, +6  ->  0x0018
0012  48 9D 2C 00 00 00 isEqual var[0x09D], 0x002C, 0  ->  0x0018
0018  48 AD 01 00 02 00 isEqual var[0x0AD], 0x0001, +2  ->  0x0020
001E  1C 14             startSound 0x14
0020  00                stopObjectCode

```
Entry +0x0000 calls Script55, 17 decoded instructions. Its literal object-state operations (all branches):
## Room 24: 113 decoded instructions across room blocks
```text
Room 24 / Entry script  ·  24.LFL  ·  file offset 0x2261
Embedded bytecode 46 bytes
Control-flow decode: 15 instructions, 46/46 resource bytes; 0 unresolved paths. Undecoded bytes may be data or unreachable code.
Static instructions are possible paths, not proof of execution. Bounded execution reports unsupported behavior explicitly.

0000  42 37             startScript 0x37
0002  4F E9 00 12 00    ifState 0x00E9, IntrinsicOff, +18  ->  0x0019
0007  1C 12             startSound 0x12
0009  30 05 00          setBoxFlags 0x05, 0x00
000C  2D 0C 18          putActorInRoom 0x0C, 0x18
000F  01 0C 18 30       putActor 0x0C, 0x18, 0x30
0013  30 05 80          setBoxFlags 0x05, 0x80
0016  18 0D 00          jumpRelative +13  ->  0x0026
0019  30 05 00          setBoxFlags 0x05, 0x00
001C  2D 0C 18          putActorInRoom 0x0C, 0x18
001F  01 0C 1E 30       putActor 0x0C, 0x1E, 0x30
0023  30 05 80          setBoxFlags 0x05, 0x80
0026  0F FD 00 02 00    ifState 0x00FD, IntrinsicOn, +2  ->  0x002D
002B  1C 11             startSound 0x11
002D  00                stopObjectCode

```
Entry +0x0000 calls Script55, 17 decoded instructions. Its literal object-state operations (all branches):
## Room 04: 105 decoded instructions across room blocks
```text
Room 04 / Entry script  ·  04.LFL  ·  file offset 0x59E2
Embedded bytecode 5 bytes
Control-flow decode: 2 instructions, 5/5 resource bytes; 0 unresolved paths. Undecoded bytes may be data or unreachable code.
Static instructions are possible paths, not proof of execution. Bounded execution reports unsupported behavior explicitly.

0000  70 0B 00 00       lights 0x0B, 0x00, 0x00
0004  00                stopObjectCode

```
## Room 43: 102 decoded instructions across room blocks
```text
Room 43 / Entry script  ·  43.LFL  ·  file offset 0x0AB5
Embedded bytecode 12 bytes
Control-flow decode: 4 instructions, 12/12 resource bytes; 0 unresolved paths. Undecoded bytes may be data or unreachable code.
Static instructions are possible paths, not proof of execution. Bounded execution reports unsupported behavior explicitly.

0000  70 0B 00 00       lights 0x0B, 0x00, 0x00
0004  0C 13 61          resourceRoutines 0x13, 0x61
0007  13 00 0F 05       actorOps 0x00, 0x0F, sub=0x05
000B  00                stopObjectCode

```
## Room 44: 15 decoded instructions across room blocks
```text
Room 44 / Entry script  ·  44.LFL  ·  file offset 0x18F8
Embedded bytecode 7 bytes
Control-flow decode: 3 instructions, 7/7 resource bytes; 0 unresolved paths. Undecoded bytes may be data or unreachable code.
Static instructions are possible paths, not proof of execution. Bounded execution reports unsupported behavior explicitly.

0000  70 0B 00 00       lights 0x0B, 0x00, 0x00
0004  3C 20             stopSound 0x20
0006  00                stopObjectCode

```
